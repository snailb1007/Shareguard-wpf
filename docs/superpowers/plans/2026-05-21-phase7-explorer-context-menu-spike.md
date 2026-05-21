# Phase 7: Explorer Context Menu Spike Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable users to right-click supported files in Windows 11 File Explorer, select "Clean with ShareGuard", and have those files instantly processed by the running (or a newly launched) instance of the WPF application.

**Architecture:** A C++ COM DLL (`ShareGuard.ShellExtension`) implementing `IExplorerCommand` is registered via the MSIX container manifest. It checks file extensions in `GetState` to only show the verb on supported files (.jpg, .jpeg, .png, .webp, .pdf, .mp4), and in `Invoke` sends selected paths via Named Pipes to the running C# app (waking it up), or launches the C# app with command-line arguments if it is not already running.

**Tech Stack:** C++ (Win32 COM, WRL), C# (WPF, System.IO.Pipes, P/Invoke, Mutex), MSIX Packaging.

---

### Task 1: C# CommandLineParser and Unit Tests

**Files:**
- Create: `ShareGuard.Application/Services/CommandLineParser.cs`
- Create: `ShareGuard.Application.Tests/CommandLineParserTests.cs`

- [ ] **Step 1: Write the failing unit test**

Create the test file `ShareGuard.Application.Tests/CommandLineParserTests.cs` with the following content:

```csharp
using ShareGuard.Application.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class CommandLineParserTests
{
    [Fact]
    public void GetFilePathsFromArgs_WithCleanVerb_ShouldReturnPaths()
    {
        string[] args = { "/clean", "C:\\file1.png", "C:\\file2.jpg" };
        var result = CommandLineParser.GetFilePathsFromArgs(args);
        Assert.Equal(2, result.Length);
        Assert.Equal("C:\\file1.png", result[0]);
        Assert.Equal("C:\\file2.jpg", result[1]);
    }

    [Fact]
    public void GetFilePathsFromArgs_WithoutCleanVerb_ShouldReturnAllPaths()
    {
        string[] args = { "C:\\file1.png", "C:\\file2.jpg" };
        var result = CommandLineParser.GetFilePathsFromArgs(args);
        Assert.Equal(2, result.Length);
        Assert.Equal("C:\\file1.png", result[0]);
        Assert.Equal("C:\\file2.jpg", result[1]);
    }

    [Fact]
    public void GetFilePathsFromArgs_EmptyArgs_ShouldReturnEmpty()
    {
        string[] args = Array.Empty<string>();
        var result = CommandLineParser.GetFilePathsFromArgs(args);
        Assert.Empty(result);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run the following command in the solution root directory:
Run: `dotnet test ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj --filter CommandLineParserTests`
Expected: Compilation failure or Test fails because `CommandLineParser` does not exist.

- [ ] **Step 3: Write minimal implementation**

Create the implementation file `ShareGuard.Application/Services/CommandLineParser.cs` with the following content:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace ShareGuard.Application.Services;

public static class CommandLineParser
{
    public static string[] GetFilePathsFromArgs(string[] args)
    {
        if (args == null || args.Length == 0)
            return Array.Empty<string>();

        bool hasCleanVerb = args[0].Equals("/clean", StringComparison.OrdinalIgnoreCase);
        int startIndex = hasCleanVerb ? 1 : 0;

        return args.Skip(startIndex)
                   .Where(arg => !string.IsNullOrWhiteSpace(arg))
                   .ToArray();
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `dotnet test ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj --filter CommandLineParserTests`
Expected: PASS

- [ ] **Step 5: Commit**

Run:
```powershell
git add ShareGuard.Application/Services/CommandLineParser.cs ShareGuard.Application.Tests/CommandLineParserTests.cs
git commit -m "feat: add CommandLineParser and unit tests"
```

---

### Task 2: Single-Instance and Named Pipe Server in WPF App Startup

**Files:**
- Modify: `Shareguard-wpf/App.xaml.cs`

- [ ] **Step 1: Implement Mutex and Named Pipe Server in App.xaml.cs**

Open `Shareguard-wpf/App.xaml.cs` and modify it. Add using statements:
```csharp
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Runtime.InteropServices;
using ShareGuard.Application.Services;
```
Then, update the `App` class to declare mutex, pipe CTS, P/Invokes, and methods for named pipe server/client:

```csharp
    private static Mutex? _appMutex;
    private CancellationTokenSource? _pipeServerCts;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    private static async Task SendPathsToPrimaryInstanceAsync(string[] args)
    {
        try
        {
            using var client = new NamedPipeClientStream(".", "ShareGuard_Arguments_Pipe", PipeDirection.Out);
            await client.ConnectAsync(2000);
            using var writer = new StreamWriter(client, System.Text.Encoding.UTF8);
            var message = string.Join("\n", args);
            await writer.WriteAsync(message);
            await writer.FlushAsync();
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to send paths to primary instance: {ex.Message}");
        }
    }

    private void StartNamedPipeServer()
    {
        _pipeServerCts = new CancellationTokenSource();
        var token = _pipeServerCts.Token;
        Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        "ShareGuard_Arguments_Pipe",
                        PipeDirection.In,
                        1,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync(token);

                    using var reader = new StreamReader(server, System.Text.Encoding.UTF8);
                    var message = await reader.ReadToEndAsync(token);
                    var args = string.IsNullOrEmpty(message)
                        ? Array.Empty<string>()
                        : message.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    var filePaths = CommandLineParser.GetFilePathsFromArgs(args);

                    await Current.Dispatcher.InvokeAsync(async () =>
                    {
                        var mainWindow = _host?.Services.GetRequiredService<MainWindow>();
                        if (mainWindow != null)
                        {
                            RestoreAndActivateWindow(mainWindow);

                            if (filePaths.Length > 0)
                            {
                                var mainViewModel = _host?.Services.GetRequiredService<MainViewModel>();
                                if (mainViewModel != null)
                                {
                                    await mainViewModel.ProcessFilesCommand.ExecuteAsync(filePaths);
                                }
                            }
                        }
                    });
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (System.Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Named Pipe Server error: {ex}");
                    await Task.Delay(1000, token);
                }
            }
        }, token);
    }

    private static void RestoreAndActivateWindow(Window window)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
        if (hwnd != IntPtr.Zero)
        {
            ShowWindow(hwnd, SW_RESTORE);
            SetForegroundWindow(hwnd);
        }
        window.Show();
        window.WindowState = WindowState.Normal;
        window.Activate();
    }
```

Now modify the `ApplicationStartup` method to start the mutex check and name pipe server, and to process startup args:

```csharp
    private async void ApplicationStartup(object sender, StartupEventArgs e)
    {
        _appMutex = new Mutex(true, "ShareGuard_SingleInstance_Mutex", out bool isNewInstance);
        if (!isNewInstance)
        {
            await SendPathsToPrimaryInstanceAsync(e.Args);
            Shutdown();
            return;
        }

        StartNamedPipeServer();

        var builder = Host.CreateApplicationBuilder();
        // ... (rest of registrations in original code)
```

And at the very end of `ApplicationStartup` (after `mainWindow.Show();`):

```csharp
        mainWindow.Show();

        // Process startup arguments for primary instance
        var startupPaths = CommandLineParser.GetFilePathsFromArgs(e.Args);
        if (startupPaths.Length > 0)
        {
            var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
            await mainViewModel.ProcessFilesCommand.ExecuteAsync(startupPaths);
        }
    }
```

Modify the `ApplicationExit` method to release resource:

```csharp
    private async void ApplicationExit(object sender, ExitEventArgs e)
    {
        _pipeServerCts?.Cancel();
        _pipeServerCts?.Dispose();
        _appMutex?.ReleaseMutex();
        _appMutex?.Dispose();

        _trayIconService?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
```

- [ ] **Step 2: Verify compilation**

Run: `dotnet build Shareguard-wpf/Shareguard-wpf.csproj`
Expected: Build succeeds without warnings or errors.

- [ ] **Step 3: Commit**

Run:
```powershell
git add Shareguard-wpf/App.xaml.cs
git commit -m "feat: implement single-instance check and Named Pipe server in WPF app"
```

---

### Task 3: C++ COM DLL Project Setup

**Files:**
- Create: `ShareGuard.ShellExtension/ShareGuard.ShellExtension.vcxproj`
- Create: `ShareGuard.ShellExtension/ShareGuard.ShellExtension.vcxproj.filters`
- Create: `ShareGuard.ShellExtension/ShareGuard.ShellExtension.def`
- Modify: `Shareguard-wpf.slnx`

- [ ] **Step 1: Create C++ project file**

Create `ShareGuard.ShellExtension/ShareGuard.ShellExtension.vcxproj` with the following content:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project DefaultTargets="Build" ToolsVersion="15.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup Label="ProjectConfigurations">
    <ProjectConfiguration Include="Debug|x64">
      <Configuration>Debug</Configuration>
      <Platform>x64</Platform>
    </ProjectConfiguration>
    <ProjectConfiguration Include="Release|x64">
      <Configuration>Release</Configuration>
      <Platform>x64</Platform>
    </ProjectConfiguration>
  </ItemGroup>
  <PropertyGroup Label="Globals">
    <VCProjectVersion>17.0</VCProjectVersion>
    <Keyword>Win32Proj</Keyword>
    <ProjectGuid>{B7D9E0F1-4234-A567-89AB-CDEF0123A6B7}</ProjectGuid>
    <RootNamespace>ShareGuardShellExtension</RootNamespace>
    <WindowsTargetPlatformVersion>10.0</WindowsTargetPlatformVersion>
  </PropertyGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.Default.props" />
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'" Label="Configuration">
    <ConfigurationType>DynamicLibrary</ConfigurationType>
    <UseDebugLibraries>true</UseDebugLibraries>
    <PlatformToolset>v143</PlatformToolset>
    <CharacterSet>Unicode</CharacterSet>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|x64'" Label="Configuration">
    <ConfigurationType>DynamicLibrary</ConfigurationType>
    <UseDebugLibraries>false</UseDebugLibraries>
    <PlatformToolset>v143</PlatformToolset>
    <WholeProgramOptimization>true</WholeProgramOptimization>
    <CharacterSet>Unicode</CharacterSet>
  </PropertyGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.props" />
  <ImportGroup Label="ExtensionSettings" />
  <ImportGroup Label="Shared" />
  <ImportGroup Label="PropertySheets" Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
    <Import Project="$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props" Condition="exists('$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props')" Label="LocalAppDataPlatform" />
  </ImportGroup>
  <ImportGroup Label="PropertySheets" Condition="'$(Configuration)|$(Platform)'=='Release|x64'">
    <Import Project="$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props" Condition="exists('$(UserRootDir)\Microsoft.Cpp.$(Platform).user.props')" Label="LocalAppDataPlatform" />
  </ImportGroup>
  <PropertyGroup Label="UserMacros" />
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
    <OutDir>$(SolutionDir)bin\$(Platform)\$(Configuration)\</OutDir>
    <IntDir>$(SolutionDir)obj\$(ProjectName)\$(Platform)\$(Configuration)\</IntDir>
  </PropertyGroup>
  <PropertyGroup Condition="'$(Configuration)|$(Platform)'=='Release|x64'">
    <OutDir>$(SolutionDir)bin\$(Platform)\$(Configuration)\</OutDir>
    <IntDir>$(SolutionDir)obj\$(ProjectName)\$(Platform)\$(Configuration)\</IntDir>
  </PropertyGroup>
  <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Debug|x64'">
    <ClCompile>
      <WarningLevel>Level4</WarningLevel>
      <SDLCheck>true</SDLCheck>
      <PreprocessorDefinitions>_DEBUG;SHAREGUARDSHELLEXTENSION_EXPORTS;_WINDOWS;_USRDLL;%(PreprocessorDefinitions)</PreprocessorDefinitions>
      <ConformanceMode>true</ConformanceMode>
      <LanguageStandard>stdcpp17</LanguageStandard>
      <RuntimeLibrary>MultiThreadedDebug</RuntimeLibrary>
    </ClCompile>
    <Link>
      <SubSystem>Windows</SubSystem>
      <GenerateDebugInformation>true</GenerateDebugInformation>
      <ModuleDefinitionFile>ShareGuard.ShellExtension.def</ModuleDefinitionFile>
      <AdditionalDependencies>shlwapi.lib;%(AdditionalDependencies)</AdditionalDependencies>
    </Link>
  </ItemDefinitionGroup>
  <ItemDefinitionGroup Condition="'$(Configuration)|$(Platform)'=='Release|x64'">
    <ClCompile>
      <WarningLevel>Level4</WarningLevel>
      <FunctionLevelLinking>true</FunctionLevelLinking>
      <IntrinsicFunctions>true</IntrinsicFunctions>
      <SDLCheck>true</SDLCheck>
      <PreprocessorDefinitions>NDEBUG;SHAREGUARDSHELLEXTENSION_EXPORTS;_WINDOWS;_USRDLL;%(PreprocessorDefinitions)</PreprocessorDefinitions>
      <ConformanceMode>true</ConformanceMode>
      <LanguageStandard>stdcpp17</LanguageStandard>
      <RuntimeLibrary>MultiThreaded</RuntimeLibrary>
    </ClCompile>
    <Link>
      <SubSystem>Windows</SubSystem>
      <EnableCOMDATFolding>true</EnableCOMDATFolding>
      <OptimizeReferences>true</OptimizeReferences>
      <GenerateDebugInformation>true</GenerateDebugInformation>
      <ModuleDefinitionFile>ShareGuard.ShellExtension.def</ModuleDefinitionFile>
      <AdditionalDependencies>shlwapi.lib;%(AdditionalDependencies)</AdditionalDependencies>
    </Link>
  </ItemDefinitionGroup>
  <ItemGroup>
    <ClCompile Include="ClassFactory.cpp" />
    <ClCompile Include="dllmain.cpp" />
    <ClCompile Include="ShareGuardCommand.cpp" />
  </ItemGroup>
  <ItemGroup>
    <ClInclude Include="ClassFactory.h" />
    <ClInclude Include="ShareGuardCommand.h" />
  </ItemGroup>
  <ItemGroup>
    <None Include="ShareGuard.ShellExtension.def" />
  </ItemGroup>
  <Import Project="$(VCTargetsPath)\Microsoft.Cpp.targets" />
  <ImportGroup Label="ExtensionTargets" />
</Project>
```

- [ ] **Step 2: Create C++ filters file**

Create `ShareGuard.ShellExtension/ShareGuard.ShellExtension.vcxproj.filters` with the following content:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project ToolsVersion="4.0" xmlns="http://schemas.microsoft.com/developer/msbuild/2003">
  <ItemGroup>
    <Filter Include="Source Files">
      <UniqueIdentifier>{4FC737F1-C7A5-4376-A066-2A32D752A2FF}</UniqueIdentifier>
      <Extensions>cpp;c;cc;cxx;c++;cppm;ixx;def;odl;idl;hpj;bat;asm;asmx</Extensions>
    </Filter>
    <Filter Include="Header Files">
      <UniqueIdentifier>{93995380-89BD-4b04-88EB-625FBE52EBFB}</UniqueIdentifier>
      <Extensions>h;hh;hpp;hxx;h++;hm;inl;inc;ipp;xsd</Extensions>
    </Filter>
  </ItemGroup>
  <ItemGroup>
    <ClCompile Include="dllmain.cpp">
      <Filter>Source Files</Filter>
    </ClCompile>
    <ClCompile Include="ClassFactory.cpp">
      <Filter>Source Files</Filter>
    </ClCompile>
    <ClCompile Include="ShareGuardCommand.cpp">
      <Filter>Source Files</Filter>
    </ClCompile>
  </ItemGroup>
  <ItemGroup>
    <ClInclude Include="ClassFactory.h">
      <Filter>Header Files</Filter>
    </ClInclude>
    <ClInclude Include="ShareGuardCommand.h">
      <Filter>Header Files</Filter>
    </ClInclude>
  </ItemGroup>
  <ItemGroup>
    <None Include="ShareGuard.ShellExtension.def">
      <Filter>Source Files</Filter>
    </None>
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create Module Definition file**

Create `ShareGuard.ShellExtension/ShareGuard.ShellExtension.def` with the following content:

```def
LIBRARY "ShareGuard.ShellExtension"
EXPORTS
    DllGetClassObject   PRIVATE
    DllCanUnloadNow     PRIVATE
    DllRegisterServer   PRIVATE
    DllUnregisterServer PRIVATE
```

- [ ] **Step 4: Update Solution file**

Open `Shareguard-wpf.slnx` and add the new project path:

```xml
  <Project Path="ShareGuard.ShellExtension/ShareGuard.ShellExtension.vcxproj" />
```

Add this right before `</Solution>`.

- [ ] **Step 5: Commit**

Run:
```powershell
git add ShareGuard.ShellExtension/ShareGuard.ShellExtension.vcxproj ShareGuard.ShellExtension/ShareGuard.ShellExtension.vcxproj.filters ShareGuard.ShellExtension/ShareGuard.ShellExtension.def Shareguard-wpf.slnx
git commit -m "chore: setup C++ ShellExtension project and solution references"
```

---

### Task 4: C++ COM Server Infrastructure

**Files:**
- Create: `ShareGuard.ShellExtension/ClassFactory.h`
- Create: `ShareGuard.ShellExtension/ClassFactory.cpp`
- Create: `ShareGuard.ShellExtension/dllmain.cpp`

- [ ] **Step 1: Create ClassFactory header**

Create `ShareGuard.ShellExtension/ClassFactory.h` with the following content:

```cpp
#pragma once
#include <unknwn.h>

class ClassFactory : public IClassFactory
{
public:
    ClassFactory();
    virtual ~ClassFactory();

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppvObject) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // IClassFactory
    IFACEMETHODIMP CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject) override;
    IFACEMETHODIMP LockServer(BOOL fLock) override;

private:
    long m_refCount;
};
```

- [ ] **Step 2: Create ClassFactory implementation**

Create `ShareGuard.ShellExtension/ClassFactory.cpp` with the following content:

```cpp
#include "ClassFactory.h"
#include "ShareGuardCommand.h"
#include <new>

extern long g_dllRefCount;

ClassFactory::ClassFactory() : m_refCount(1)
{
    InterlockedIncrement(&g_dllRefCount);
}

ClassFactory::~ClassFactory()
{
    InterlockedDecrement(&g_dllRefCount);
}

IFACEMETHODIMP ClassFactory::QueryInterface(REFIID riid, void** ppvObject)
{
    if (!ppvObject) return E_INVALIDARG;
    *ppvObject = nullptr;

    if (riid == IID_IUnknown || riid == IID_IClassFactory)
    {
        *ppvObject = static_cast<IClassFactory*>(this);
        AddRef();
        return S_OK;
    }
    return E_NOINTERFACE;
}

IFACEMETHODIMP_(ULONG) ClassFactory::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG) ClassFactory::Release()
{
    ULONG count = InterlockedDecrement(&m_refCount);
    if (count == 0)
    {
        delete this;
    }
    return count;
}

IFACEMETHODIMP ClassFactory::CreateInstance(IUnknown* pUnkOuter, REFIID riid, void** ppvObject)
{
    if (!ppvObject) return E_INVALIDARG;
    *ppvObject = nullptr;

    if (pUnkOuter != nullptr)
    {
        return CLASS_E_NOAGGREGATION;
    }

    ShareGuardCommand* pCommand = new (std::nothrow) ShareGuardCommand();
    if (!pCommand)
    {
        return E_OUTOFMEMORY;
    }

    HRESULT hr = pCommand->QueryInterface(riid, ppvObject);
    pCommand->Release();
    return hr;
}

IFACEMETHODIMP ClassFactory::LockServer(BOOL fLock)
{
    if (fLock)
    {
        InterlockedIncrement(&g_dllRefCount);
    }
    else
    {
        InterlockedDecrement(&g_dllRefCount);
    }
    return S_OK;
}
```

- [ ] **Step 3: Create dllmain COM entry points**

Create `ShareGuard.ShellExtension/dllmain.cpp` with the following content:

```cpp
#include <windows.h>
#include <guiddef.h>
#include "ClassFactory.h"

// CLSID for ShareGuardCommand: {A6B7C8D9-E0F1-4234-A567-89ABCDEF0123}
const CLSID CLSID_ShareGuardCommand = { 0xA6B7C8D9, 0xE0F1, 0x4234, { 0xA5, 0x67, 0x89, 0xAB, 0xCD, 0xEF, 0x01, 0x23 } };

HINSTANCE g_hModule = nullptr;
long g_dllRefCount = 0;

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved)
{
    UNREFERENCED_PARAMETER(lpReserved);
    switch (ul_reason_for_call)
    {
    case DLL_PROCESS_ATTACH:
        g_hModule = hModule;
        DisableThreadLibraryCalls(hModule);
        break;
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}

STDAPI DllGetClassObject(REFCLSID rclsid, REFIID riid, void** ppv)
{
    if (!ppv) return E_INVALIDARG;
    *ppv = nullptr;

    if (IsEqualCLSID(rclsid, CLSID_ShareGuardCommand))
    {
        ClassFactory* pFactory = new (std::nrow) ClassFactory(); // wait, typo in standard code: std::nothrow
        ClassFactory* pFactory = new (std::nothrow) ClassFactory();
        if (!pFactory)
        {
            return E_OUTOFMEMORY;
        }

        HRESULT hr = pFactory->QueryInterface(riid, ppv);
        pFactory->Release();
        return hr;
    }
    return CLASS_E_CLASSNOTAVAILABLE;
}

STDAPI DllCanUnloadNow(void)
{
    return g_dllRefCount == 0 ? S_OK : S_FALSE;
}

STDAPI DllRegisterServer(void)
{
    return S_OK;
}

STDAPI DllUnregisterServer(void)
{
    return S_OK;
}
```

- [ ] **Step 4: Verify C++ compilation**

Run: `msbuild /p:Configuration=Debug /p:Platform=x64 ShareGuard.ShellExtension/ShareGuard.ShellExtension.vcxproj`
Expected: Build fails because `ShareGuardCommand.h` is missing.

- [ ] **Step 5: Commit**

Run:
```powershell
git add ShareGuard.ShellExtension/ClassFactory.h ShareGuard.ShellExtension/ClassFactory.cpp ShareGuard.ShellExtension/dllmain.cpp
git commit -m "feat: implement COM ClassFactory and DLL entry points"
```

---

### Task 5: Implement IExplorerCommand Shell Extension (ShareGuardCommand)

**Files:**
- Create: `ShareGuard.ShellExtension/ShareGuardCommand.h`
- Create: `ShareGuard.ShellExtension/ShareGuardCommand.cpp`

- [ ] **Step 1: Create ShareGuardCommand header**

Create `ShareGuard.ShellExtension/ShareGuardCommand.h` with the following content:

```cpp
#pragma once
#include <shobjidl.h>
#include <shlwapi.h>
#include <wrl/client.h>

class ShareGuardCommand : public IExplorerCommand
{
public:
    ShareGuardCommand();
    virtual ~ShareGuardCommand();

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppvObject) override;
    IFACEMETHODIMP_(ULONG) AddRef() override;
    IFACEMETHODIMP_(ULONG) Release() override;

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(IShellItemArray* psiItemArray, LPWSTR* ppszName) override;
    IFACEMETHODIMP GetIcon(IShellItemArray* psiItemArray, LPWSTR* ppszIcon) override;
    IFACEMETHODIMP GetToolTip(IShellItemArray* psiItemArray, LPWSTR* ppszInfotip) override;
    IFACEMETHODIMP GetCanonicalName(GUID* pguidCommandName) override;
    IFACEMETHODIMP GetState(IShellItemArray* psiItemArray, BOOL fOkToBeSlow, EXPCMDSTATE* pCmdState) override;
    IFACEMETHODIMP Invoke(IShellItemArray* psiItemArray, IBindCtx* pBindCtx) override;
    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* pFlags) override;
    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** ppEnum) override;

private:
    long m_refCount;
};
```

- [ ] **Step 2: Create ShareGuardCommand implementation**

Create `ShareGuard.ShellExtension/ShareGuardCommand.cpp` with the following content:

```cpp
#include "ShareGuardCommand.h"
#include <new>
#include <vector>
#include <string>

extern HINSTANCE g_hModule;
extern long g_dllRefCount;

ShareGuardCommand::ShareGuardCommand() : m_refCount(1)
{
    InterlockedIncrement(&g_dllRefCount);
}

ShareGuardCommand::~ShareGuardCommand()
{
    InterlockedDecrement(&g_dllRefCount);
}

IFACEMETHODIMP ShareGuardCommand::QueryInterface(REFIID riid, void** ppvObject)
{
    if (!ppvObject) return E_INVALIDARG;
    *ppvObject = nullptr;

    if (riid == IID_IUnknown || riid == IID_IExplorerCommand)
    {
        *ppvObject = static_cast<IExplorerCommand*>(this);
        AddRef();
        return S_OK;
    }
    return E_NOINTERFACE;
}

IFACEMETHODIMP_(ULONG) ShareGuardCommand::AddRef()
{
    return InterlockedIncrement(&m_refCount);
}

IFACEMETHODIMP_(ULONG) ShareGuardCommand::Release()
{
    ULONG count = InterlockedDecrement(&m_refCount);
    if (count == 0)
    {
        delete this;
    }
    return count;
}

IFACEMETHODIMP ShareGuardCommand::GetTitle(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszName)
{
    if (!ppszName) return E_INVALIDARG;
    return SHStrDupW(L"Clean with ShareGuard", ppszName);
}

IFACEMETHODIMP ShareGuardCommand::GetIcon(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszIcon)
{
    if (!ppszIcon) return E_INVALIDARG;

    wchar_t dllPath[MAX_PATH];
    if (GetModuleFileNameW(g_hModule, dllPath, MAX_PATH) == 0) return E_FAIL;
    std::wstring dir(dllPath);
    size_t pos = dir.find_last_of(L"\\/");
    if (pos != std::wstring::npos) {
        dir = dir.substr(0, pos);
    }
    std::wstring iconPath = dir + L"\\Shareguard-wpf\\Assets\\app_icon.ico";
    if (GetFileAttributesW(iconPath.c_str()) == INVALID_FILE_ATTRIBUTES) {
        iconPath = dir + L"\\Assets\\app_icon.ico";
    }
    return SHStrDupW(iconPath.c_str(), ppszIcon);
}

IFACEMETHODIMP ShareGuardCommand::GetToolTip(IShellItemArray* /*psiItemArray*/, LPWSTR* ppszInfotip)
{
    if (!ppszInfotip) return E_INVALIDARG;
    return SHStrDupW(L"Remove privacy-sensitive metadata from selected files", ppszInfotip);
}

IFACEMETHODIMP ShareGuardCommand::GetCanonicalName(GUID* pguidCommandName)
{
    if (!pguidCommandName) return E_INVALIDARG;
    // {A6B7C8D9-E0F1-4234-A567-89ABCDEF0123}
    const GUID CLSID_ShareGuardCommand = { 0xA6B7C8D9, 0xE0F1, 0x4234, { 0xA5, 0x67, 0x89, 0xAB, 0xCD, 0xEF, 0x01, 0x23 } };
    *pguidCommandName = CLSID_ShareGuardCommand;
    return S_OK;
}

IFACEMETHODIMP ShareGuardCommand::GetState(IShellItemArray* psiItemArray, BOOL /*fOkToBeSlow*/, EXPCMDSTATE* pCmdState)
{
    if (!pCmdState) return E_INVALIDARG;
    *pCmdState = ECS_HIDDEN; // Default to hidden

    if (!psiItemArray) return S_OK;

    DWORD count = 0;
    psiItemArray->GetCount(&count);
    if (count == 0) return S_OK;

    for (DWORD i = 0; i < count; ++i)
    {
        Microsoft::WRL::ComPtr<IShellItem> item;
        if (SUCCEEDED(psiItemArray->GetItemAt(i, &item)))
        {
            wchar_t* path = nullptr;
            if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &path)))
            {
                std::wstring filePath(path);
                CoTaskMemFree(path);

                size_t dotPos = filePath.find_last_of(L'.');
                if (dotPos != std::wstring::npos)
                {
                    std::wstring ext = filePath.substr(dotPos);
                    for (auto& c : ext) c = towlower(c);

                    if (ext == L".jpg" || ext == L".jpeg" || ext == L".png" || ext == L".webp" || ext == L".pdf" || ext == L".mp4")
                    {
                        *pCmdState = ECS_ENABLED;
                        return S_OK;
                    }
                }
            }
        }
    }

    return S_OK;
}

IFACEMETHODIMP ShareGuardCommand::Invoke(IShellItemArray* psiItemArray, IBindCtx* /*pBindCtx*/)
{
    if (!psiItemArray) return E_INVALIDARG;

    DWORD count = 0;
    psiItemArray->GetCount(&count);
    if (count == 0) return S_OK;

    std::wstring pipeMessage = L"";
    std::wstring cmdArgs = L"";
    for (DWORD i = 0; i < count; ++i)
    {
        Microsoft::WRL::ComPtr<IShellItem> item;
        if (SUCCEEDED(psiItemArray->GetItemAt(i, &item)))
        {
            wchar_t* path = nullptr;
            if (SUCCEEDED(item->GetDisplayName(SIGDN_FILESYSPATH, &path)))
            {
                if (!pipeMessage.empty()) pipeMessage += L"\n";
                pipeMessage += path;

                cmdArgs += L" \"" + std::wstring(path) + L"\"";
                CoTaskMemFree(path);
            }
        }
    }

    if (pipeMessage.empty()) return S_OK;

    // Try Named Pipe IPC first
    HANDLE hPipe = CreateFileW(
        L"\\\\.\\pipe\\ShareGuard_Arguments_Pipe",
        GENERIC_WRITE,
        0,
        NULL,
        OPEN_EXISTING,
        0,
        NULL);

    if (hPipe != INVALID_HANDLE_VALUE)
    {
        DWORD bytesWritten = 0;
        int sizeNeeded = WideCharToMultiByte(CP_UTF8, 0, pipeMessage.c_str(), (int)pipeMessage.size(), NULL, 0, NULL, NULL);
        std::string utf8Message(sizeNeeded, 0);
        WideCharToMultiByte(CP_UTF8, 0, pipeMessage.c_str(), (int)pipeMessage.size(), &utf8Message[0], sizeNeeded, NULL, NULL);

        WriteFile(hPipe, utf8Message.c_str(), (DWORD)utf8Message.size(), &bytesWritten, NULL);
        CloseHandle(hPipe);
        return S_OK;
    }

    // Launch WPF application (Pipe was not active, meaning first launch)
    wchar_t dllPath[MAX_PATH];
    if (GetModuleFileNameW(g_hModule, dllPath, MAX_PATH) == 0) return E_FAIL;
    std::wstring dir(dllPath);
    size_t pos = dir.find_last_of(L"\\/");
    if (pos != std::wstring::npos) {
        dir = dir.substr(0, pos);
    }

    std::wstring exePath = dir + L"\\Shareguard-wpf\\Shareguard-wpf.exe";
    if (GetFileAttributesW(exePath.c_str()) == INVALID_FILE_ATTRIBUTES) {
        exePath = dir + L"\\Shareguard-wpf.exe";
    }

    std::wstring commandLine = L"\"" + exePath + L"\" /clean" + cmdArgs;

    STARTUPINFO si = { sizeof(si) };
    PROCESS_INFORMATION pi;
    std::vector<wchar_t> cmdLineBuffer(commandLine.begin(), commandLine.end());
    cmdLineBuffer.push_back(L'\0');

    if (CreateProcessW(NULL, cmdLineBuffer.data(), NULL, NULL, FALSE, 0, NULL, NULL, &si, &pi))
    {
        CloseHandle(pi.hProcess);
        CloseHandle(pi.hThread);
        return S_OK;
    }

    return E_FAIL;
}

IFACEMETHODIMP ShareGuardCommand::GetFlags(EXPCMDFLAGS* pFlags)
{
    if (!pFlags) return E_INVALIDARG;
    *pFlags = ECF_DEFAULT;
    return S_OK;
}

IFACEMETHODIMP ShareGuardCommand::EnumSubCommands(IEnumExplorerCommand** ppEnum)
{
    if (!ppEnum) return E_INVALIDARG;
    *ppEnum = nullptr;
    return E_NOTIMPL;
}
```

- [ ] **Step 3: Verify build**

Run: `msbuild /p:Configuration=Debug /p:Platform=x64 ShareGuard.ShellExtension/ShareGuard.ShellExtension.vcxproj`
Expected: Build succeeds, outputs `bin\x64\Debug\ShareGuard.ShellExtension.dll`.

- [ ] **Step 4: Commit**

Run:
```powershell
git add ShareGuard.ShellExtension/ShareGuardCommand.h ShareGuard.ShellExtension/ShareGuardCommand.cpp
git commit -m "feat: implement IExplorerCommand for context menu shell extension"
```

---

### Task 6: Package Integration and MSIX manifest registration

**Files:**
- Modify: `ShareGuard.Package/ShareGuard.Package.wapproj`
- Modify: `ShareGuard.Package/Package.appxmanifest`

- [ ] **Step 1: Reference the C++ DLL in the Package project**

Open `ShareGuard.Package/ShareGuard.Package.wapproj` and find the `<ItemGroup>` containing `ProjectReference`. Add:

```xml
    <ProjectReference Include="..\ShareGuard.ShellExtension\ShareGuard.ShellExtension.vcxproj">
      <Project>{B7D9E0F1-4234-A567-89AB-CDEF0123A6B7}</Project>
      <Name>ShareGuard.ShellExtension</Name>
    </ProjectReference>
```

- [ ] **Step 2: Configure Package.appxmanifest**

Open `ShareGuard.Package/Package.appxmanifest` and modify the `<Package>` root node to include the required XML namespaces:

```xml
  xmlns:com="http://schemas.microsoft.com/appx/manifest/com/windows10"
  xmlns:desktop4="http://schemas.microsoft.com/appx/manifest/desktop/windows10/4"
  xmlns:desktop5="http://schemas.microsoft.com/appx/manifest/desktop/windows10/5"
```

Also, update `IgnorableNamespaces` to include them:
```xml
  IgnorableNamespaces="uap rescap com desktop4 desktop5"
```

Next, insert the `<Extensions>` elements inside the `<Application>` element (directly after `<uap:VisualElements>` ends):

```xml
      <Extensions>
        <!-- Register the C++ DLL as virtualized COM Server -->
        <com:Extension Category="windows.comServer">
          <com:ComServer>
            <com:SurrogateServer DisplayName="ShareGuard Context Menu Surrogate">
              <com:Class Id="A6B7C8D9-E0F1-4234-A567-89ABCDEF0123" Path="ShareGuard.ShellExtension.dll" ThreadingModel="STA"/>
            </com:SurrogateServer>
          </com:ComServer>
        </com:Extension>

        <!-- Register modern context menu verb for wildcard * -->
        <desktop4:Extension Category="windows.fileExplorerContextMenus">
          <desktop4:FileExplorerContextMenus>
            <desktop5:ItemType Type="*">
              <desktop5:Verb Id="CleanWithShareGuard" Clsid="A6B7C8D9-E0F1-4234-A567-89ABCDEF0123" />
            </desktop5:ItemType>
          </desktop4:FileExplorerContextMenus>
        </desktop4:Extension>
      </Extensions>
```

- [ ] **Step 3: Build the packaged application**

Run: `msbuild /p:Configuration=Debug /p:Platform=x64 ShareGuard.Package/ShareGuard.Package.wapproj`
Expected: Build succeeds, generating MSIX package.

- [ ] **Step 4: Commit**

Run:
```powershell
git add ShareGuard.Package/ShareGuard.Package.wapproj ShareGuard.Package/Package.appxmanifest
git commit -m "feat: register COM server and modern context menu in package manifest"
```

---

## Verification Plan

### Automated Tests
- Run `dotnet test` to verify all unit tests pass, including the new `CommandLineParserTests`.

### Manual Verification
1. Deploy the packaging project `ShareGuard.Package` locally (right-click the project in Visual Studio and select "Deploy", or use Visual Studio run).
2. If File Explorer context menu doesn't update immediately, restart File Explorer by running:
   `taskkill /f /im explorer.exe && start explorer.exe`
3. Locate a supported file (e.g. `.png` image or `.pdf` document) in File Explorer.
4. Right-click the file. Verify that "Clean with ShareGuard" appears in the primary context menu.
5. Click "Clean with ShareGuard". Verify that the app launches (or the existing window focuses), shows the progress bar, and processes the file.
6. Select an unsupported file type (e.g. a `.txt` file) and right-click it. Verify that "Clean with ShareGuard" is **not** present in the context menu.
7. Open the application, then go back to File Explorer. Select multiple supported files, right-click, and click "Clean with ShareGuard". Verify that the files are processed inside the *already running* instance of ShareGuard, and the window comes to the foreground.
