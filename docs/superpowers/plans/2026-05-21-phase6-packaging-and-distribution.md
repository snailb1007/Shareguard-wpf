# Phase 6: Packaging and Distribution — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Package the ShareGuard WPF application into a signed MSIX installer for side-load distribution on Windows 10/11.

**Architecture:** Add a Windows Application Packaging Project (`.wapproj`) alongside the existing WPF project. The packaging project references the WPF executable, bundles DPI-scaled visual assets, and produces a signed `.msix` file. A lightweight `PackageDetector` helper is added to the Infrastructure layer so the app can detect whether it is running inside the MSIX container and adjust data paths accordingly. A CI build script automates the packaging pipeline.

**Tech Stack:** MSBuild, Windows Application Packaging Project (WAP), MSIX, PowerShell (for certificate creation and CI), `SignTool.exe`

---

## Task 1: Generate DPI-Scaled Visual Assets

Before creating the packaging project, you need the raw PNG assets that the MSIX manifest and Windows shell will use (tiles, taskbar icons, splash screens). These must exist at standard MSIX scaling tiers: 100%, 125%, 150%, 200%, and 400%.

**Files:**
- Create: `ShareGuard.Package/Assets/Square44x44Logo.scale-100.png` (44×44)
- Create: `ShareGuard.Package/Assets/Square44x44Logo.scale-125.png` (55×55)
- Create: `ShareGuard.Package/Assets/Square44x44Logo.scale-150.png` (66×66)
- Create: `ShareGuard.Package/Assets/Square44x44Logo.scale-200.png` (88×88)
- Create: `ShareGuard.Package/Assets/Square44x44Logo.scale-400.png` (176×176)
- Create: `ShareGuard.Package/Assets/Square150x150Logo.scale-100.png` (150×150)
- Create: `ShareGuard.Package/Assets/Square150x150Logo.scale-125.png` (188×188)
- Create: `ShareGuard.Package/Assets/Square150x150Logo.scale-150.png` (225×225)
- Create: `ShareGuard.Package/Assets/Square150x150Logo.scale-200.png` (300×300)
- Create: `ShareGuard.Package/Assets/Square150x150Logo.scale-400.png` (600×600)
- Create: `ShareGuard.Package/Assets/Wide310x150Logo.scale-100.png` (310×150)
- Create: `ShareGuard.Package/Assets/Wide310x150Logo.scale-125.png` (388×188)
- Create: `ShareGuard.Package/Assets/Wide310x150Logo.scale-150.png` (465×225)
- Create: `ShareGuard.Package/Assets/Wide310x150Logo.scale-200.png` (620×300)
- Create: `ShareGuard.Package/Assets/Wide310x150Logo.scale-400.png` (1240×600)
- Create: `ShareGuard.Package/Assets/LargeTile.scale-100.png` (310×310)
- Create: `ShareGuard.Package/Assets/LargeTile.scale-200.png` (620×620)
- Create: `ShareGuard.Package/Assets/StoreLogo.scale-100.png` (50×50)
- Create: `ShareGuard.Package/Assets/StoreLogo.scale-200.png` (100×100)
- Create: `ShareGuard.Package/Assets/StoreLogo.scale-400.png` (200×200)
- Create: `ShareGuard.Package/Assets/SplashScreen.scale-100.png` (620×300)
- Create: `ShareGuard.Package/Assets/SplashScreen.scale-200.png` (1240×600)

- [ ] **Step 1: Create source logo PNG at highest resolution**

  Use the existing `Shareguard-wpf/Assets/app_icon.ico` as the design source. Create a 1240×600 wide banner PNG, and square PNGs at 600×600 and 200×200 with the ShareGuard shield logo centered on a transparent background.

  If you have an image editor available, export from the ICO. Otherwise, create placeholder PNGs using a PowerShell script:

  ```powershell
  # Generate placeholder PNGs using System.Drawing
  # Run from the repo root
  Add-Type -AssemblyName System.Drawing

  $assetDir = "ShareGuard.Package\Assets"
  New-Item -ItemType Directory -Path $assetDir -Force | Out-Null

  $sizes = @{
      "Square44x44Logo"   = @(@(44,44), @(55,55), @(66,66), @(88,88), @(176,176))
      "Square150x150Logo" = @(@(150,150), @(188,188), @(225,225), @(300,300), @(600,600))
      "Wide310x150Logo"   = @(@(310,150), @(388,188), @(465,225), @(620,300), @(1240,600))
      "LargeTile"         = @(@(310,310), @(620,620))
      "StoreLogo"         = @(@(50,50), @(100,100), @(200,200))
      "SplashScreen"      = @(@(620,300), @(1240,600))
  }
  $scales = @{
      "Square44x44Logo"   = @(100, 125, 150, 200, 400)
      "Square150x150Logo" = @(100, 125, 150, 200, 400)
      "Wide310x150Logo"   = @(100, 125, 150, 200, 400)
      "LargeTile"         = @(100, 200)
      "StoreLogo"         = @(100, 200, 400)
      "SplashScreen"      = @(100, 200)
  }

  foreach ($name in $sizes.Keys) {
      $sizeList = $sizes[$name]
      $scaleList = $scales[$name]
      for ($i = 0; $i -lt $sizeList.Count; $i++) {
          $w = $sizeList[$i][0]
          $h = $sizeList[$i][1]
          $scale = $scaleList[$i]
          $bmp = New-Object System.Drawing.Bitmap($w, $h)
          $g = [System.Drawing.Graphics]::FromImage($bmp)
          $g.Clear([System.Drawing.Color]::FromArgb(34, 40, 49))
          # Draw centered "SG" text
          $font = New-Object System.Drawing.Font("Segoe UI", [Math]::Max(8, $h / 4), [System.Drawing.FontStyle]::Bold)
          $brush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(0, 173, 181))
          $sf = New-Object System.Drawing.StringFormat
          $sf.Alignment = [System.Drawing.StringAlignment]::Center
          $sf.LineAlignment = [System.Drawing.StringAlignment]::Center
          $rect = New-Object System.Drawing.RectangleF(0, 0, $w, $h)
          $g.DrawString("SG", $font, $brush, $rect, $sf)
          $g.Dispose()
          $path = Join-Path $assetDir "$name.scale-$scale.png"
          $bmp.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
          $bmp.Dispose()
          Write-Host "Created $path ($w x $h)"
      }
  }
  ```

  Expected: All PNG files created under `ShareGuard.Package/Assets/`.

- [ ] **Step 2: Verify all asset files exist**

  ```powershell
  Get-ChildItem .\ShareGuard.Package\Assets\*.png | Format-Table Name, Length
  ```

  Expected: 22 PNG files listed with non-zero sizes.

- [ ] **Step 3: Commit**

  ```bash
  git add ShareGuard.Package/Assets/
  git commit -m "feat(packaging): add DPI-scaled visual assets for MSIX packaging"
  ```

---

## Task 2: Create the Windows Application Packaging Project

This is the core of the MSIX packaging setup. You will create a `.wapproj` project that references the WPF executable, configure the AppxManifest, and wire it into the solution.

**Files:**
- Create: `ShareGuard.Package/ShareGuard.Package.wapproj`
- Create: `ShareGuard.Package/Package.appxmanifest`
- Modify: `Shareguard-wpf.slnx`

- [ ] **Step 1: Create the `.wapproj` project file**

  Create `ShareGuard.Package/ShareGuard.Package.wapproj`:

  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <Project ToolsVersion="15.0" DefaultTargets="Build"
           xmlns="http://schemas.microsoft.com/developer/msbuild/2003">

    <PropertyGroup Condition="'$(VisualStudioVersion)' == '' or '$(VisualStudioVersion)' &lt; '15.0'">
      <VisualStudioVersion>15.0</VisualStudioVersion>
    </PropertyGroup>

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

    <PropertyGroup>
      <WapProjPath Condition="'$(WapProjPath)' == ''">$(MSBuildExtensionsPath)\Microsoft\DesktopBridge\</WapProjPath>
    </PropertyGroup>

    <Import Project="$(WapProjPath)\Microsoft.DesktopBridge.props" />

    <PropertyGroup>
      <ProjectGuid>{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}</ProjectGuid>
      <TargetPlatformVersion>10.0.22621.0</TargetPlatformVersion>
      <TargetPlatformMinVersion>10.0.19041.0</TargetPlatformMinVersion>
      <DefaultLanguage>en-US</DefaultLanguage>
      <AppxPackageSigningEnabled>false</AppxPackageSigningEnabled>
      <EntryPointProjectUniqueName>..\Shareguard-wpf\Shareguard-wpf.csproj</EntryPointProjectUniqueName>
      <GenerateAppInstallerFile>false</GenerateAppInstallerFile>
      <AppxAutoIncrementPackageRevision>true</AppxAutoIncrementPackageRevision>
      <AppxBundlePlatforms>x64</AppxBundlePlatforms>
      <AppxBundle>Never</AppxBundle>
      <UapAppxPackageBuildMode>SideloadOnly</UapAppxPackageBuildMode>
    </PropertyGroup>

    <ItemGroup>
      <AppxManifest Include="Package.appxmanifest">
        <SubType>Designer</SubType>
      </AppxManifest>
    </ItemGroup>

    <ItemGroup>
      <!-- DPI-scaled visual assets -->
      <Content Include="Assets\**\*.png" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="..\Shareguard-wpf\Shareguard-wpf.csproj" />
    </ItemGroup>

    <Import Project="$(WapProjPath)\Microsoft.DesktopBridge.targets" />
  </Project>
  ```

  > **Key design choice:** `AppxPackageSigningEnabled` defaults to `false` in the project file. Signing is enabled via MSBuild command-line properties so that dev builds don't require a PFX file, while CI and release builds pass the certificate explicitly.

- [ ] **Step 2: Create the AppxManifest**

  Create `ShareGuard.Package/Package.appxmanifest`:

  ```xml
  <?xml version="1.0" encoding="utf-8"?>
  <Package
    xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
    xmlns:mp="http://schemas.microsoft.com/appx/2014/phone/manifest"
    xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
    xmlns:rescap="http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities"
    IgnorableNamespaces="uap rescap">

    <Identity
      Name="ShareGuard"
      Publisher="CN=ShareGuard"
      Version="1.0.0.0" />

    <mp:PhoneIdentity PhoneProductId="00000000-0000-0000-0000-000000000000"
                       PhonePublisherId="00000000-0000-0000-0000-000000000000"/>

    <Properties>
      <DisplayName>ShareGuard</DisplayName>
      <PublisherDisplayName>ShareGuard</PublisherDisplayName>
      <Logo>Assets\StoreLogo.scale-200.png</Logo>
    </Properties>

    <Dependencies>
      <TargetDeviceFamily Name="Windows.Desktop"
                          MinVersion="10.0.19041.0"
                          MaxVersionTested="10.0.22621.0" />
    </Dependencies>

    <Resources>
      <Resource Language="en-us"/>
    </Resources>

    <Applications>
      <Application Id="ShareGuard"
        Executable="Shareguard-wpf\Shareguard-wpf.exe"
        EntryPoint="Windows.FullTrustApplication">

        <uap:VisualElements
          DisplayName="ShareGuard"
          Description="Privacy-first metadata stripping for files and URLs"
          BackgroundColor="#222831"
          Square150x150Logo="Assets\Square150x150Logo.scale-200.png"
          Square44x44Logo="Assets\Square44x44Logo.scale-200.png">

          <uap:DefaultTile
            Wide310x150Logo="Assets\Wide310x150Logo.scale-200.png"
            Square310x310Logo="Assets\LargeTile.scale-200.png" />

          <uap:SplashScreen Image="Assets\SplashScreen.scale-200.png"
                            BackgroundColor="#222831" />
        </uap:VisualElements>

      </Application>
    </Applications>

    <Capabilities>
      <Capability Name="internetClient" />
      <rescap:Capability Name="runFullTrust" />
    </Capabilities>
  </Package>
  ```

  > **Notes:**
  > - `Publisher` must be `CN=ShareGuard` — this must exactly match the self-signed certificate Subject created in Task 4.
  > - `Executable` is `Shareguard-wpf\Shareguard-wpf.exe` because the WAP project copies the WPF build output into a subfolder matching the project name.
  > - `BackgroundColor` `#222831` matches the app's dark theme.

- [ ] **Step 3: Add the packaging project to the solution**

  Modify `Shareguard-wpf.slnx` to include the new project:

  ```xml
  <Solution>
    <Project Path="ShareGuard.Domain/ShareGuard.Domain.csproj" />
    <Project Path="ShareGuard.Application/ShareGuard.Application.csproj" />
    <Project Path="ShareGuard.Infrastructure/ShareGuard.Infrastructure.csproj" />
    <Project Path="Shareguard-wpf/Shareguard-wpf.csproj" />
    <Project Path="ShareGuard.Domain.Tests/ShareGuard.Domain.Tests.csproj" />
    <Project Path="ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj" />
    <Project Path="ShareGuard.Package/ShareGuard.Package.wapproj" />
  </Solution>
  ```

- [ ] **Step 4: Verify the solution loads**

  ```powershell
  dotnet restore Shareguard-wpf.slnx
  ```

  Expected: Restore succeeds. The `.wapproj` project should be recognized (it may warn about needing Visual Studio for full build — that's expected since WAP builds use MSBuild from VS).

- [ ] **Step 5: Commit**

  ```bash
  git add ShareGuard.Package/ShareGuard.Package.wapproj
  git add ShareGuard.Package/Package.appxmanifest
  git add Shareguard-wpf.slnx
  git commit -m "feat(packaging): add Windows Application Packaging Project with AppxManifest"
  ```

---

## Task 3: Add Packaged-Context Detection Helper

The app needs to know at runtime whether it's running inside an MSIX container or as an unpackaged executable. This affects file paths (MSIX virtualizes AppData), registry access, and auto-update behavior. This helper uses the Win32 `GetCurrentPackageFullName` API to detect the packaging state — no new NuGet dependencies required.

**Files:**
- Create: `ShareGuard.Infrastructure/Services/PackageDetector.cs`
- Create: `ShareGuard.Domain/Interfaces/IPackageDetector.cs`
- Create: `ShareGuard.Domain.Tests/PackageDetectorTests.cs`
- Modify: `ShareGuard.Infrastructure/DependencyInjection.cs:14`

- [ ] **Step 1: Write the test for the package detector**

  Create `ShareGuard.Domain.Tests/PackageDetectorTests.cs`:

  ```csharp
  using ShareGuard.Infrastructure.Services;

  namespace ShareGuard.Domain.Tests;

  public class PackageDetectorTests
  {
      [Fact]
      public void IsPackaged_WhenRunningFromTestHost_ReturnsFalse()
      {
          // The test host process is not an MSIX packaged app,
          // so IsPackaged should always return false in unit tests.
          var detector = new PackageDetector();

          bool result = detector.IsPackaged;

          Assert.False(result);
      }

      [Fact]
      public void AppDataPath_WhenUnpackaged_ReturnsLocalApplicationData()
      {
          var detector = new PackageDetector();
          string expected = Path.Combine(
              Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
              "ShareGuard");

          string result = detector.AppDataPath;

          Assert.Equal(expected, result);
      }
  }
  ```

- [ ] **Step 2: Run the test to verify it fails**

  ```powershell
  dotnet test ShareGuard.Domain.Tests --filter "FullyQualifiedName~PackageDetectorTests" -v normal
  ```

  Expected: FAIL — `PackageDetector` class does not exist yet.

- [ ] **Step 3: Create the interface**

  Create `ShareGuard.Domain/Interfaces/IPackageDetector.cs`:

  ```csharp
  namespace ShareGuard.Domain.Interfaces;

  /// <summary>
  /// Detects whether the application is running inside an MSIX package container.
  /// </summary>
  public interface IPackageDetector
  {
      /// <summary>
      /// True if the app is running inside an MSIX container.
      /// </summary>
      bool IsPackaged { get; }

      /// <summary>
      /// Returns the appropriate AppData path. When packaged, this is the
      /// MSIX-redirected LocalApplicationData folder. When unpackaged,
      /// it falls back to LocalApplicationData\ShareGuard.
      /// </summary>
      string AppDataPath { get; }
  }
  ```

- [ ] **Step 4: Implement the package detector**

  Create `ShareGuard.Infrastructure/Services/PackageDetector.cs`:

  ```csharp
  using System.Runtime.InteropServices;
  using System.Text;
  using ShareGuard.Domain.Interfaces;

  namespace ShareGuard.Infrastructure.Services;

  /// <summary>
  /// Detects MSIX packaging context using the Win32 GetCurrentPackageFullName API.
  /// This avoids taking a dependency on the Windows App SDK.
  /// </summary>
  public sealed class PackageDetector : IPackageDetector
  {
      // ERROR_INSUFFICIENT_BUFFER means we ARE packaged (just need a bigger buffer)
      private const int ErrorInsufficientBuffer = 122;

      // APPMODEL_ERROR_NO_PACKAGE means we are NOT packaged
      private const long AppmodelErrorNoPackage = 15700;

      [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
      private static extern int GetCurrentPackageFullName(
          ref int packageFullNameLength,
          StringBuilder? packageFullName);

      private readonly Lazy<bool> _isPackaged = new(DetectPackaged);
      private readonly Lazy<string> _appDataPath;

      public PackageDetector()
      {
          _appDataPath = new Lazy<string>(() =>
          {
              if (IsPackaged)
              {
                  // MSIX apps get a virtualized LocalApplicationData automatically.
                  // No need to append "ShareGuard" — the container isolates it.
                  return Environment.GetFolderPath(
                      Environment.SpecialFolder.LocalApplicationData);
              }

              return Path.Combine(
                  Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                  "ShareGuard");
          });
      }

      public bool IsPackaged => _isPackaged.Value;

      public string AppDataPath => _appDataPath.Value;

      private static bool DetectPackaged()
      {
          if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
          {
              return false;
          }

          try
          {
              int length = 0;
              int result = GetCurrentPackageFullName(ref length, null);

              // If we get ERROR_INSUFFICIENT_BUFFER, we are in a package
              // If we get APPMODEL_ERROR_NO_PACKAGE, we are unpackaged
              return result == ErrorInsufficientBuffer;
          }
          catch
          {
              // P/Invoke failure — assume unpackaged
              return false;
          }
      }
  }
  ```

- [ ] **Step 5: Register in DI**

  Modify `ShareGuard.Infrastructure/DependencyInjection.cs`. Add at the top of the `AddInfrastructureServices` method body, before the existing `IImageCleaner` registration:

  ```csharp
  services.AddSingleton<IPackageDetector, PackageDetector>();
  ```

  Add the using directive at the top of the file:

  ```csharp
  using ShareGuard.Domain.Interfaces;
  ```

  The full `AddInfrastructureServices` method should read:

  ```csharp
  public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
  {
      services.AddSingleton<IPackageDetector, PackageDetector>();
      services.AddSingleton<IImageCleaner, ImageSharpCleaner>();

      // Phase 4 IFileStripper registrations
      services.AddSingleton<IFileStripper, ImageSharpStripper>();
      services.AddSingleton<IFileStripper, OfficeOpenXmlStripper>();
      services.AddSingleton<IFileStripper, PdfMetadataStripper>();

      // Place SQLite database in AppData folder
      string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
      string dbFolder = Path.Combine(appData, "ShareGuard");
      string dbPath = Path.Combine(dbFolder, "history.db");

      // Ensure directory exists
      Directory.CreateDirectory(dbFolder);

      // Add context factory to guarantee thread safety during parallel operations
      services.AddDbContextFactory<ShareGuardDbContext>(options =>
          options.UseSqlite($"Data Source={dbPath};Default Timeout=5"));

      // Register repository
      services.AddSingleton<IHistoryRepository, HistoryRepository>();

      return services;
  }
  ```

- [ ] **Step 6: Run the tests to verify they pass**

  ```powershell
  dotnet test ShareGuard.Domain.Tests --filter "FullyQualifiedName~PackageDetectorTests" -v normal
  ```

  Expected: 2 tests PASS.

- [ ] **Step 7: Commit**

  ```bash
  git add ShareGuard.Domain/Interfaces/IPackageDetector.cs
  git add ShareGuard.Infrastructure/Services/PackageDetector.cs
  git add ShareGuard.Infrastructure/DependencyInjection.cs
  git add ShareGuard.Domain.Tests/PackageDetectorTests.cs
  git commit -m "feat(packaging): add MSIX package detection helper with P/Invoke"
  ```

---

## Task 4: Wire PackageDetector Into Database Path Resolution

Currently, `DependencyInjection.cs` hard-codes the database path to `LocalApplicationData\ShareGuard\history.db`. When running inside MSIX, the container virtualizes `LocalApplicationData` automatically, so the subfolder `ShareGuard` is redundant (and potentially confusing). Use the `PackageDetector` to resolve the correct path.

**Files:**
- Modify: `ShareGuard.Infrastructure/DependencyInjection.cs:21-31`

- [ ] **Step 1: Refactor the database path to use PackageDetector**

  Update the database path section in `AddInfrastructureServices` to build the service provider partially, resolve `IPackageDetector`, then use its `AppDataPath`:

  ```csharp
  public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
  {
      services.AddSingleton<IPackageDetector, PackageDetector>();
      services.AddSingleton<IImageCleaner, ImageSharpCleaner>();

      // Phase 4 IFileStripper registrations
      services.AddSingleton<IFileStripper, ImageSharpStripper>();
      services.AddSingleton<IFileStripper, OfficeOpenXmlStripper>();
      services.AddSingleton<IFileStripper, PdfMetadataStripper>();

      // Resolve the package detector to determine the correct data folder
      var detector = new PackageDetector();
      string dbFolder = detector.AppDataPath;
      string dbPath = Path.Combine(dbFolder, "history.db");

      // Ensure directory exists
      Directory.CreateDirectory(dbFolder);

      // Add context factory to guarantee thread safety during parallel operations
      services.AddDbContextFactory<ShareGuardDbContext>(options =>
          options.UseSqlite($"Data Source={dbPath};Default Timeout=5"));

      // Register repository
      services.AddSingleton<IHistoryRepository, HistoryRepository>();

      return services;
  }
  ```

  > **Why `new PackageDetector()` directly?** The DI container isn't built yet when we're registering services. Since `PackageDetector` has no dependencies, constructing it inline is safe and avoids a chicken-and-egg problem. The singleton in the container is the canonical instance for all other consumers.

- [ ] **Step 2: Run existing tests to verify no regression**

  ```powershell
  dotnet test --verbosity normal
  ```

  Expected: All existing tests pass. The behavior on developer machines is unchanged because `PackageDetector.IsPackaged` returns `false`, making `AppDataPath` resolve to `LocalApplicationData\ShareGuard` — the same path as before.

- [ ] **Step 3: Commit**

  ```bash
  git add ShareGuard.Infrastructure/DependencyInjection.cs
  git commit -m "refactor(packaging): use PackageDetector for database path resolution"
  ```

---

## Task 5: Create Self-Signed Development Certificate

MSIX packages cannot be installed unless signed with a trusted certificate. For local development and testing, you create a self-signed certificate and import it into the Trusted People store.

**Files:**
- Create: `scripts/New-DevCertificate.ps1`

- [ ] **Step 1: Write the certificate creation script**

  Create `scripts/New-DevCertificate.ps1`:

  ```powershell
  <#
  .SYNOPSIS
      Creates a self-signed code signing certificate for local MSIX development,
      exports it to PFX, and imports it into the Trusted People store.

  .DESCRIPTION
      The certificate Subject MUST match the Publisher field in Package.appxmanifest
      exactly: CN=ShareGuard

      After running this script, the local machine will trust side-loaded MSIX
      packages signed with this certificate.

  .PARAMETER OutputPath
      Directory to save the exported PFX file. Defaults to ./certs/

  .PARAMETER Password
      Password for the PFX file. Defaults to "ShareGuardDev123!"

  .EXAMPLE
      .\scripts\New-DevCertificate.ps1
      .\scripts\New-DevCertificate.ps1 -OutputPath "C:\certs" -Password "MyPassword"
  #>
  param(
      [string]$OutputPath = (Join-Path $PSScriptRoot "..\certs"),
      [string]$Password = "ShareGuardDev123!"
  )

  $ErrorActionPreference = "Stop"

  # Ensure output directory exists
  New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

  $pfxPath = Join-Path $OutputPath "ShareGuard_Dev.pfx"
  $subject = "CN=ShareGuard"

  Write-Host "Creating self-signed certificate with Subject: $subject" -ForegroundColor Cyan

  # Create the certificate
  $cert = New-SelfSignedCertificate `
      -Type Custom `
      -Subject $subject `
      -KeyUsage DigitalSignature `
      -FriendlyName "ShareGuard Development Certificate" `
      -CertStoreLocation "Cert:\CurrentUser\My" `
      -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}") `
      -NotAfter (Get-Date).AddYears(5)

  Write-Host "Certificate created: $($cert.Thumbprint)" -ForegroundColor Green

  # Export to PFX
  $securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText
  Export-PfxCertificate -Cert $cert -FilePath $pfxPath -Password $securePassword | Out-Null
  Write-Host "PFX exported to: $pfxPath" -ForegroundColor Green

  # Import into Trusted People (requires elevation)
  $isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
      [Security.Principal.WindowsBuiltInRole]::Administrator)

  if ($isAdmin) {
      Import-PfxCertificate -FilePath $pfxPath `
          -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" `
          -Password $securePassword | Out-Null
      Write-Host "Certificate imported into Local Machine -> Trusted People" -ForegroundColor Green
  } else {
      Write-Host ""
      Write-Host "WARNING: Run this script as Administrator to import the certificate" -ForegroundColor Yellow
      Write-Host "         into Trusted People for MSIX side-loading." -ForegroundColor Yellow
      Write-Host ""
      Write-Host "Manual import command (run elevated):" -ForegroundColor Yellow
      Write-Host "  Import-PfxCertificate -FilePath `"$pfxPath`" -CertStoreLocation `"Cert:\LocalMachine\TrustedPeople`" -Password (ConvertTo-SecureString -String `"$Password`" -Force -AsPlainText)" -ForegroundColor White
  }

  Write-Host ""
  Write-Host "=== Summary ===" -ForegroundColor Cyan
  Write-Host "Subject:     $subject"
  Write-Host "Thumbprint:  $($cert.Thumbprint)"
  Write-Host "PFX Path:    $pfxPath"
  Write-Host "PFX Password: $Password"
  Write-Host ""
  Write-Host "Use this PFX to sign the MSIX package:" -ForegroundColor Cyan
  Write-Host "  msbuild ShareGuard.Package\ShareGuard.Package.wapproj /p:Configuration=Release /p:Platform=x64 /p:AppxPackageSigningEnabled=true /p:PackageCertificateKeyFile=`"$pfxPath`" /p:PackageCertificatePassword=`"$Password`""
  ```

- [ ] **Step 2: Add `certs/` to `.gitignore`**

  Append to `.gitignore`:

  ```
  # MSIX signing certificates (sensitive - never commit)
  certs/
  *.pfx
  ```

- [ ] **Step 3: Commit the script (not the certificate)**

  ```bash
  git add scripts/New-DevCertificate.ps1
  git add .gitignore
  git commit -m "feat(packaging): add self-signed certificate creation script"
  ```

---

## Task 6: Create CI Build Script for MSIX Packaging

A PowerShell build script that automates the full pipeline: restore → build → sign → produce `.msix`. This works both locally and in CI (GitHub Actions).

**Files:**
- Create: `scripts/Build-MsixPackage.ps1`

- [ ] **Step 1: Write the build script**

  Create `scripts/Build-MsixPackage.ps1`:

  ```powershell
  <#
  .SYNOPSIS
      Builds and signs the ShareGuard MSIX package.

  .DESCRIPTION
      Restores, builds, and packages the ShareGuard application into a signed
      .msix file using MSBuild and the Windows Application Packaging Project.

  .PARAMETER Configuration
      Build configuration. Default: Release

  .PARAMETER Platform
      Target platform. Default: x64

  .PARAMETER CertificatePath
      Path to the PFX certificate file for signing.

  .PARAMETER CertificatePassword
      Password for the PFX certificate.

  .PARAMETER OutputDirectory
      Directory for the generated MSIX package. Default: ./artifacts/

  .PARAMETER SkipSigning
      If set, builds the package without signing (for CI testing).

  .EXAMPLE
      .\scripts\Build-MsixPackage.ps1 -CertificatePath .\certs\ShareGuard_Dev.pfx -CertificatePassword "ShareGuardDev123!"
      .\scripts\Build-MsixPackage.ps1 -SkipSigning
  #>
  param(
      [string]$Configuration = "Release",
      [string]$Platform = "x64",
      [string]$CertificatePath,
      [string]$CertificatePassword,
      [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\artifacts"),
      [switch]$SkipSigning
  )

  $ErrorActionPreference = "Stop"
  $projectPath = Join-Path $PSScriptRoot "..\ShareGuard.Package\ShareGuard.Package.wapproj"

  if (-not (Test-Path $projectPath)) {
      Write-Error "Packaging project not found at: $projectPath"
      exit 1
  }

  # Ensure output directory exists
  New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

  Write-Host "========================================" -ForegroundColor Cyan
  Write-Host " ShareGuard MSIX Build" -ForegroundColor Cyan
  Write-Host "========================================" -ForegroundColor Cyan
  Write-Host "Configuration: $Configuration"
  Write-Host "Platform:      $Platform"
  Write-Host "Signing:       $(if ($SkipSigning) { 'Disabled' } else { 'Enabled' })"
  Write-Host "Output:        $OutputDirectory"
  Write-Host ""

  # Find MSBuild
  $vswhere = "${env:ProgramFiles(x86)}\Microsoft Visual Studio\Installer\vswhere.exe"
  if (Test-Path $vswhere) {
      $msbuildPath = & $vswhere -latest -requires Microsoft.Component.MSBuild -find "MSBuild\**\Bin\MSBuild.exe" | Select-Object -First 1
  }

  if (-not $msbuildPath -or -not (Test-Path $msbuildPath)) {
      Write-Error "MSBuild not found. Install Visual Studio with the '.NET desktop development' workload."
      exit 1
  }

  Write-Host "Using MSBuild: $msbuildPath" -ForegroundColor Gray

  # Build MSBuild arguments
  $msbuildArgs = @(
      $projectPath,
      "/t:Rebuild",
      "/p:Configuration=$Configuration",
      "/p:Platform=$Platform",
      "/p:UapAppxPackageBuildMode=SideloadOnly",
      "/p:AppxPackageDir=$OutputDirectory\",
      "/p:GenerateAppInstallerFile=false",
      "/verbosity:minimal",
      "/restore"
  )

  if (-not $SkipSigning) {
      if (-not $CertificatePath -or -not (Test-Path $CertificatePath)) {
          Write-Error "Certificate path is required for signed builds. Use -SkipSigning to build without signing, or provide -CertificatePath."
          exit 1
      }
      $msbuildArgs += "/p:AppxPackageSigningEnabled=true"
      $msbuildArgs += "/p:PackageCertificateKeyFile=$CertificatePath"
      if ($CertificatePassword) {
          $msbuildArgs += "/p:PackageCertificatePassword=$CertificatePassword"
      }
  } else {
      $msbuildArgs += "/p:AppxPackageSigningEnabled=false"
  }

  Write-Host ""
  Write-Host "Building MSIX package..." -ForegroundColor Cyan

  & $msbuildPath @msbuildArgs

  if ($LASTEXITCODE -ne 0) {
      Write-Error "MSBuild failed with exit code $LASTEXITCODE"
      exit $LASTEXITCODE
  }

  # Find the output package
  $msixFiles = Get-ChildItem -Path $OutputDirectory -Filter "*.msix" -Recurse
  if ($msixFiles.Count -gt 0) {
      Write-Host ""
      Write-Host "========================================" -ForegroundColor Green
      Write-Host " Build Successful!" -ForegroundColor Green
      Write-Host "========================================" -ForegroundColor Green
      foreach ($file in $msixFiles) {
          $sizeMB = [math]::Round($file.Length / 1MB, 2)
          Write-Host "  $($file.FullName) ($sizeMB MB)"
      }
  } else {
      Write-Host ""
      Write-Host "Build completed but no .msix file found in $OutputDirectory" -ForegroundColor Yellow
      Write-Host "Check the build output above for details." -ForegroundColor Yellow
  }
  ```

- [ ] **Step 2: Add `artifacts/` to `.gitignore`**

  Append to `.gitignore`:

  ```
  # Build artifacts
  artifacts/
  ```

- [ ] **Step 3: Commit**

  ```bash
  git add scripts/Build-MsixPackage.ps1
  git add .gitignore
  git commit -m "feat(packaging): add MSIX build and signing automation script"
  ```

---

## Task 7: Update .gitignore for Packaging Artifacts

Consolidate all packaging-related ignore rules that haven't been added yet.

**Files:**
- Modify: `.gitignore`

- [ ] **Step 1: Add MSIX-specific ignore patterns**

  Append these entries to the end of `.gitignore` (if not already present from earlier tasks):

  ```
  # MSIX Packaging
  *.msix
  *.msixbundle
  *.appx
  *.appxbundle
  *.appxupload
  AppPackages/
  BundleArtifacts/
  certs/
  *.pfx
  artifacts/
  ```

- [ ] **Step 2: Verify the gitignore works**

  ```powershell
  # Should show nothing MSIX-related
  git status --short
  ```

  Expected: No `.msix`, `.pfx`, or `artifacts/` files appear in status output.

- [ ] **Step 3: Commit**

  ```bash
  git add .gitignore
  git commit -m "chore: add MSIX and certificate entries to .gitignore"
  ```

---

## Task 8: Verify End-to-End Packaging (Unsigned Build)

The final validation — build the entire MSIX package without signing to confirm the packaging pipeline works end-to-end. Signing requires the certificate from Task 5, which is a manual step run separately by the developer.

**Files:**
- No new files.

- [ ] **Step 1: Build the solution normally first to verify nothing is broken**

  ```powershell
  dotnet build Shareguard-wpf.slnx -c Release
  ```

  Expected: Build succeeds with no errors.

- [ ] **Step 2: Run all tests to verify no regressions**

  ```powershell
  dotnet test Shareguard-wpf.slnx --verbosity normal
  ```

  Expected: All existing tests pass, including the new `PackageDetectorTests`.

- [ ] **Step 3: Attempt the MSIX packaging build (unsigned)**

  ```powershell
  .\scripts\Build-MsixPackage.ps1 -SkipSigning
  ```

  Expected: Either:
  - **Success**: An unsigned `.msix` file appears in `./artifacts/`.
  - **Expected failure**: MSBuild may fail if the WAP SDK components aren't installed in the current Visual Studio instance. If so, the error message will reference `Microsoft.DesktopBridge.props` or `Microsoft.DesktopBridge.targets`.

  > If the WAP SDK is missing, install the "Universal Windows Platform development" workload in Visual Studio Installer, or the "Windows Application Packaging Project" individual component. This is a one-time environment setup step.

- [ ] **Step 4: Commit final state**

  ```bash
  git add -A
  git commit -m "feat(packaging): complete Phase 6 MSIX packaging and distribution setup"
  ```

---

## Summary of Files Created/Modified

| Action | File |
|--------|------|
| Create | `ShareGuard.Package/ShareGuard.Package.wapproj` |
| Create | `ShareGuard.Package/Package.appxmanifest` |
| Create | `ShareGuard.Package/Assets/*.png` (22 files) |
| Create | `ShareGuard.Domain/Interfaces/IPackageDetector.cs` |
| Create | `ShareGuard.Infrastructure/Services/PackageDetector.cs` |
| Create | `ShareGuard.Domain.Tests/PackageDetectorTests.cs` |
| Create | `scripts/New-DevCertificate.ps1` |
| Create | `scripts/Build-MsixPackage.ps1` |
| Modify | `ShareGuard.Infrastructure/DependencyInjection.cs` |
| Modify | `Shareguard-wpf.slnx` |
| Modify | `.gitignore` |
