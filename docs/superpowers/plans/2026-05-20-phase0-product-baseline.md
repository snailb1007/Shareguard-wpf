# Phase 0: Product Baseline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Transform the bare WPF template into a buildable Clean Architecture solution with MVVM, Dependency Injection, Generic Host, and xUnit test infrastructure — no feature logic yet.

**Architecture:** Clean Architecture with 4 projects (Domain → Application → Infrastructure → WPF App) plus 2 test projects. ViewModels use CommunityToolkit.Mvvm source generators. WPF App hosts the .NET Generic Host for DI/logging/configuration lifecycle.

**Tech Stack:** .NET 10, C# 14, WPF, CommunityToolkit.Mvvm 8.4, Microsoft.Extensions.Hosting, xUnit v3

---

## File Structure

| File | Responsibility |
|------|---------------|
| `Directory.Build.props` | [NEW] Centralized build settings for all projects |
| `Directory.Packages.props` | [NEW] Central Package Management — all NuGet versions |
| `Shareguard-wpf.slnx` | [MODIFY] Add all new projects to solution |
| `ShareGuard.Domain/ShareGuard.Domain.csproj` | [NEW] Domain class library — zero dependencies |
| `ShareGuard.Domain/_readme.md` | [NEW] Layer documentation |
| `ShareGuard.Application/ShareGuard.Application.csproj` | [NEW] Application class library — depends on Domain |
| `ShareGuard.Application/DependencyInjection.cs` | [NEW] DI extension method for Application layer |
| `ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj` | [NEW] Infrastructure class library — depends on Domain + Application |
| `ShareGuard.Infrastructure/DependencyInjection.cs` | [NEW] DI extension method for Infrastructure layer |
| `Shareguard-wpf/Shareguard-wpf.csproj` | [MODIFY] Add project refs + package refs, change namespace |
| `Shareguard-wpf/App.xaml` | [MODIFY] Remove StartupUri, add lifecycle events |
| `Shareguard-wpf/App.xaml.cs` | [MODIFY] Generic Host + DI wiring |
| `Shareguard-wpf/MainWindow.xaml` | [MODIFY] Bind to ViewModel properties |
| `Shareguard-wpf/MainWindow.xaml.cs` | [MODIFY] Constructor-injected ViewModel |
| `Shareguard-wpf/ViewModels/MainViewModel.cs` | [NEW] First ViewModel with source generators |
| `Shareguard-wpf/AssemblyInfo.cs` | [MODIFY] Clean up old namespace |
| `ShareGuard.Domain.Tests/ShareGuard.Domain.Tests.csproj` | [NEW] xUnit v3 test project |
| `ShareGuard.Domain.Tests/ArchitectureTests.cs` | [NEW] Verify Domain has no external deps |
| `ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj` | [NEW] xUnit v3 test project |
| `ShareGuard.Application.Tests/DependencyInjectionTests.cs` | [NEW] Verify DI extension method works |

---

### Task 1: Create Directory.Build.props

**Files:**
- Create: `Directory.Build.props`

- [ ] **Step 1: Create the file**

Create `Directory.Build.props` at the solution root (`c:\Users\ADMIN\source\repos\Shareguard-wpf\Directory.Build.props`):

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>preview</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

Notes:
- `LangVersion=preview` because .NET 10 is preview and `14.0` may not be recognized yet.
- `TargetFramework=net10.0` is the default; the WPF project overrides to `net10.0-windows`.
- `ManagePackageVersionsCentrally=true` enables Central Package Management.

- [ ] **Step 2: Commit**

```powershell
git add Directory.Build.props
git commit -m "build: add Directory.Build.props with centralized settings"
```

---

### Task 2: Create Directory.Packages.props

**Files:**
- Create: `Directory.Packages.props`

- [ ] **Step 1: Create the file**

Create `Directory.Packages.props` at the solution root:

```xml
<Project>
  <ItemGroup>
    <!-- MVVM -->
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <!-- Hosting & DI -->
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.0-preview.5.25277.114" />
    <!-- Logging -->
    <PackageVersion Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0-preview.5.25277.114" />
    <!-- DI Abstractions (needed by Application layer for IServiceCollection) -->
    <PackageVersion Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.0-preview.5.25277.114" />
    <!-- Testing -->
    <PackageVersion Include="xunit.v3" Version="1.1.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
  </ItemGroup>
</Project>
```

Note: If preview versions fail to restore, fall back to the latest stable versions. Run `dotnet restore` after creation to validate.

- [ ] **Step 2: Commit**

```powershell
git add Directory.Packages.props
git commit -m "build: add Directory.Packages.props for central package management"
```

---

### Task 3: Create ShareGuard.Domain class library

**Files:**
- Create: `ShareGuard.Domain/ShareGuard.Domain.csproj`
- Create: `ShareGuard.Domain/_readme.md`

- [ ] **Step 1: Scaffold the project**

Run from the solution root:

```powershell
dotnet new classlib -n ShareGuard.Domain -o ShareGuard.Domain --no-restore
```

Expected: Creates `ShareGuard.Domain/` with `ShareGuard.Domain.csproj` and `Class1.cs`.

- [ ] **Step 2: Replace the csproj content**

Replace the entire content of `ShareGuard.Domain/ShareGuard.Domain.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- TargetFramework, LangVersion, Nullable, ImplicitUsings inherited from Directory.Build.props -->
</Project>
```

- [ ] **Step 3: Delete Class1.cs**

```powershell
Remove-Item ShareGuard.Domain/Class1.cs
```

- [ ] **Step 4: Create _readme.md**

Create `ShareGuard.Domain/_readme.md`:

```markdown
# ShareGuard.Domain

Platform-agnostic domain layer. Contains:
- Core entities (ShareItem, StripResult, Finding)
- Domain interfaces (IStripper, etc.)
- Domain exceptions

MUST NOT reference WPF, Win32, or any UI components.
```

- [ ] **Step 5: Commit**

```powershell
git add ShareGuard.Domain/
git commit -m "feat: add ShareGuard.Domain class library (zero dependencies)"
```

---

### Task 4: Create ShareGuard.Application class library

**Files:**
- Create: `ShareGuard.Application/ShareGuard.Application.csproj`
- Create: `ShareGuard.Application/DependencyInjection.cs`

- [ ] **Step 1: Scaffold the project**

```powershell
dotnet new classlib -n ShareGuard.Application -o ShareGuard.Application --no-restore
```

- [ ] **Step 2: Replace the csproj content**

Replace `ShareGuard.Application/ShareGuard.Application.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\ShareGuard.Domain\ShareGuard.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
  </ItemGroup>
</Project>
```

Note: No `Version` attributes on `PackageReference` — CPM manages versions. `DependencyInjection.Abstractions` is needed for `IServiceCollection` in the DI extension method.

- [ ] **Step 3: Delete Class1.cs and create DependencyInjection.cs**

```powershell
Remove-Item ShareGuard.Application/Class1.cs
```

Create `ShareGuard.Application/DependencyInjection.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace ShareGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Application layer service registrations will be added here
        return services;
    }
}
```

- [ ] **Step 4: Commit**

```powershell
git add ShareGuard.Application/
git commit -m "feat: add ShareGuard.Application class library (depends on Domain)"
```

---

### Task 5: Create ShareGuard.Infrastructure class library

**Files:**
- Create: `ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj`
- Create: `ShareGuard.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Scaffold the project**

```powershell
dotnet new classlib -n ShareGuard.Infrastructure -o ShareGuard.Infrastructure --no-restore
```

- [ ] **Step 2: Replace the csproj content**

Replace `ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\ShareGuard.Domain\ShareGuard.Domain.csproj" />
    <ProjectReference Include="..\ShareGuard.Application\ShareGuard.Application.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Delete Class1.cs and create DependencyInjection.cs**

```powershell
Remove-Item ShareGuard.Infrastructure/Class1.cs
```

Create `ShareGuard.Infrastructure/DependencyInjection.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace ShareGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Infrastructure layer service registrations will be added here
        return services;
    }
}
```

- [ ] **Step 4: Commit**

```powershell
git add ShareGuard.Infrastructure/
git commit -m "feat: add ShareGuard.Infrastructure class library (depends on Domain + Application)"
```

---

### Task 6: Update Shareguard-wpf.csproj for Clean Architecture

**Files:**
- Modify: `Shareguard-wpf/Shareguard-wpf.csproj`

- [ ] **Step 1: Replace csproj content**

Replace the entire content of `Shareguard-wpf/Shareguard-wpf.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows</TargetFramework>
    <RootNamespace>ShareGuard.App</RootNamespace>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\ShareGuard.Application\ShareGuard.Application.csproj" />
    <ProjectReference Include="..\ShareGuard.Infrastructure\ShareGuard.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" />
    <PackageReference Include="Microsoft.Extensions.Hosting" />
  </ItemGroup>

</Project>
```

Key changes from the current state:
- `RootNamespace` changed from `Shareguard_wpf` to `ShareGuard.App`
- Added `ProjectReference` to Application and Infrastructure
- Added `PackageReference` for CommunityToolkit.Mvvm and Microsoft.Extensions.Hosting (no Version — CPM)
- Removed redundant `Nullable` and `ImplicitUsings` (inherited from Directory.Build.props)
- Override `TargetFramework` to `net10.0-windows` (WPF requires Windows TFM)

- [ ] **Step 2: Commit**

```powershell
git add Shareguard-wpf/Shareguard-wpf.csproj
git commit -m "build: update WPF csproj with CA references and new namespace"
```

---

### Task 7: Update solution file

**Files:**
- Modify: `Shareguard-wpf.slnx`

- [ ] **Step 1: Replace solution file content**

Replace the entire content of `Shareguard-wpf.slnx` with:

```xml
<Solution>
  <Project Path="ShareGuard.Domain/ShareGuard.Domain.csproj" />
  <Project Path="ShareGuard.Application/ShareGuard.Application.csproj" />
  <Project Path="ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj" />
  <Project Path="Shareguard-wpf/Shareguard-wpf.csproj" />
</Solution>
```

Test projects will be added in Task 11.

- [ ] **Step 2: Restore and build**

```powershell
dotnet restore Shareguard-wpf.slnx
dotnet build Shareguard-wpf.slnx
```

Expected: Restore succeeds. Build succeeds with zero errors. (There may be warnings about the old namespace in existing files — those get fixed in the next tasks.)

- [ ] **Step 3: Commit**

```powershell
git add Shareguard-wpf.slnx
git commit -m "build: add all CA projects to solution file"
```

---

### Task 8: Create MainViewModel

**Files:**
- Create: `Shareguard-wpf/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Create the ViewModel file**

Create `Shareguard-wpf/ViewModels/MainViewModel.cs`:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ShareGuard.App.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = "ShareGuard";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [RelayCommand]
    private void Loaded()
    {
        StatusMessage = "ShareGuard is ready to protect your privacy.";
    }
}
```

Key points:
- Class must be `partial` for CommunityToolkit.Mvvm source generators
- `[ObservableProperty]` on `_title` generates public `Title` property with `INotifyPropertyChanged`
- `[ObservableProperty]` on `_statusMessage` generates public `StatusMessage` property
- `[RelayCommand]` on `Loaded()` generates `LoadedCommand` (IRelayCommand)

- [ ] **Step 2: Commit**

```powershell
git add Shareguard-wpf/ViewModels/
git commit -m "feat: add MainViewModel with CommunityToolkit.Mvvm source generators"
```

---

### Task 9: Update App.xaml and App.xaml.cs for Generic Host

**Files:**
- Modify: `Shareguard-wpf/App.xaml`
- Modify: `Shareguard-wpf/App.xaml.cs`

- [ ] **Step 1: Replace App.xaml**

Replace the entire content of `Shareguard-wpf/App.xaml` with:

```xml
<Application x:Class="ShareGuard.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Startup="Application_Startup"
             Exit="Application_Exit">
    <Application.Resources>
    </Application.Resources>
</Application>
```

Changes from current:
- `x:Class` changed from `Shareguard_wpf.App` to `ShareGuard.App.App`
- Removed `StartupUri="MainWindow.xaml"` — MainWindow is now resolved from DI
- Removed `xmlns:local` (not needed)
- Added `Startup="Application_Startup"` and `Exit="Application_Exit"` event handlers

- [ ] **Step 2: Replace App.xaml.cs**

Replace the entire content of `Shareguard-wpf/App.xaml.cs` with:

```csharp
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShareGuard.App.ViewModels;
using ShareGuard.Application;
using ShareGuard.Infrastructure;

namespace ShareGuard.App;

/// <summary>
/// Application entry point. Hosts the .NET Generic Host for DI, logging, and configuration.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();

        // Register layer services via extension methods
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices();

        // Presentation layer registrations
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private async void Application_Exit(object sender, ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
```

Key points:
- `Host.CreateApplicationBuilder()` is the modern .NET 10 pattern (not legacy `Host.CreateDefaultBuilder()`)
- Layer DI extension methods called: `AddApplicationServices()`, `AddInfrastructureServices()`
- MainViewModel and MainWindow registered as singletons
- MainWindow resolved from DI and shown manually (no StartupUri)
- Graceful shutdown: `StopAsync()` + `Dispose()` on Exit

- [ ] **Step 3: Commit**

```powershell
git add Shareguard-wpf/App.xaml Shareguard-wpf/App.xaml.cs
git commit -m "feat: wire Generic Host with DI in App.xaml.cs"
```

---

### Task 10: Update MainWindow and AssemblyInfo

**Files:**
- Modify: `Shareguard-wpf/MainWindow.xaml`
- Modify: `Shareguard-wpf/MainWindow.xaml.cs`
- Modify: `Shareguard-wpf/AssemblyInfo.cs`

- [ ] **Step 1: Replace MainWindow.xaml**

Replace the entire content of `Shareguard-wpf/MainWindow.xaml` with:

```xml
<Window x:Class="ShareGuard.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="{Binding Title}"
        Height="600" Width="900">
    <Grid>
        <TextBlock Text="{Binding StatusMessage}"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"
                   FontSize="18" />
    </Grid>
</Window>
```

Changes: `x:Class` → `ShareGuard.App.MainWindow`, `Title` → bound to ViewModel, added `TextBlock` bound to `StatusMessage`.

- [ ] **Step 2: Replace MainWindow.xaml.cs**

Replace the entire content of `Shareguard-wpf/MainWindow.xaml.cs` with:

```csharp
using System.Windows;
using ShareGuard.App.ViewModels;

namespace ShareGuard.App;

/// <summary>
/// Main application window. Receives its ViewModel via constructor injection.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
```

- [ ] **Step 3: Replace AssemblyInfo.cs**

Replace the entire content of `Shareguard-wpf/AssemblyInfo.cs` with:

```csharp
using System.Windows;

[assembly: ThemeInfo(
    ResourceDictionaryLocation.None,
    ResourceDictionaryLocation.SourceAssembly
)]
```

- [ ] **Step 4: Build the solution**

```powershell
dotnet build Shareguard-wpf.slnx
```

Expected: Build succeeds with zero errors.

- [ ] **Step 5: Commit**

```powershell
git add Shareguard-wpf/MainWindow.xaml Shareguard-wpf/MainWindow.xaml.cs Shareguard-wpf/AssemblyInfo.cs
git commit -m "feat: wire MainWindow with constructor-injected ViewModel"
```

---

### Task 11: Create test projects

**Files:**
- Create: `ShareGuard.Domain.Tests/ShareGuard.Domain.Tests.csproj`
- Create: `ShareGuard.Domain.Tests/ArchitectureTests.cs`
- Create: `ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj`
- Create: `ShareGuard.Application.Tests/DependencyInjectionTests.cs`
- Modify: `Shareguard-wpf.slnx`

- [ ] **Step 1: Create ShareGuard.Domain.Tests project**

```powershell
dotnet new classlib -n ShareGuard.Domain.Tests -o ShareGuard.Domain.Tests --no-restore
Remove-Item ShareGuard.Domain.Tests/Class1.cs
```

Replace `ShareGuard.Domain.Tests/ShareGuard.Domain.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ShareGuard.Domain\ShareGuard.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Create ArchitectureTests.cs**

Create `ShareGuard.Domain.Tests/ArchitectureTests.cs`:

```csharp
using System.Reflection;
using Xunit;

namespace ShareGuard.Domain.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_ShouldNotReference_ApplicationOrInfrastructure()
    {
        var domainAssembly = typeof(ArchitectureTests).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies()
            .Select(a => a.Name)
            .ToList();

        Assert.DoesNotContain("ShareGuard.Application", referencedAssemblies);
        Assert.DoesNotContain("ShareGuard.Infrastructure", referencedAssemblies);
        Assert.DoesNotContain("ShareGuard.App", referencedAssemblies);
    }
}
```

Note: This test verifies the Domain test project itself doesn't drag in wrong dependencies. It's a structural guard — later phases will add domain-specific tests.

- [ ] **Step 3: Create ShareGuard.Application.Tests project**

```powershell
dotnet new classlib -n ShareGuard.Application.Tests -o ShareGuard.Application.Tests --no-restore
Remove-Item ShareGuard.Application.Tests/Class1.cs
```

Replace `ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ShareGuard.Application\ShareGuard.Application.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create DependencyInjectionTests.cs**

Create `ShareGuard.Application.Tests/DependencyInjectionTests.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ShareGuard.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationServices_ShouldReturnServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddApplicationServices();

        Assert.Same(services, result);
    }
}
```

- [ ] **Step 5: Update solution file**

Replace `Shareguard-wpf.slnx` with:

```xml
<Solution>
  <Project Path="ShareGuard.Domain/ShareGuard.Domain.csproj" />
  <Project Path="ShareGuard.Application/ShareGuard.Application.csproj" />
  <Project Path="ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj" />
  <Project Path="Shareguard-wpf/Shareguard-wpf.csproj" />
  <Project Path="ShareGuard.Domain.Tests/ShareGuard.Domain.Tests.csproj" />
  <Project Path="ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj" />
</Solution>
```

- [ ] **Step 6: Restore, build, and run tests**

```powershell
dotnet restore Shareguard-wpf.slnx
dotnet build Shareguard-wpf.slnx
dotnet test Shareguard-wpf.slnx --verbosity normal
```

Expected: Build succeeds. 2 tests discovered, 2 tests passed.

- [ ] **Step 7: Commit**

```powershell
git add ShareGuard.Domain.Tests/ ShareGuard.Application.Tests/ Shareguard-wpf.slnx
git commit -m "test: add Domain and Application test projects with xUnit v3"
```

---

## Final Verification

After all tasks are complete, run these checks:

```powershell
# 1. Clean build
dotnet build Shareguard-wpf.slnx

# 2. Run all tests
dotnet test Shareguard-wpf.slnx --verbosity normal

# 3. Verify dependency direction (manual check):
#    - Domain.csproj: 0 ProjectReferences
#    - Application.csproj: 1 ProjectReference (Domain)
#    - Infrastructure.csproj: 2 ProjectReferences (Domain, Application)
#    - Shareguard-wpf.csproj: 2 ProjectReferences (Application, Infrastructure)

# 4. Run the WPF app
dotnet run --project Shareguard-wpf/Shareguard-wpf.csproj
```

Expected:
- Build: zero errors, zero warnings
- Tests: 2 passed, 0 failed
- App launch: Window opens with title "ShareGuard" and displays "Ready" in center
