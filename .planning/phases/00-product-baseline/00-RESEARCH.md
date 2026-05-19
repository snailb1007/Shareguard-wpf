# Phase 0: Product Baseline — Research

**Researched:** 2026-05-20
**Status:** Complete

## 1. .NET 10 Generic Host in WPF

### Modern Pattern (2025/2026)

Use `Host.CreateApplicationBuilder()` (not the legacy `Host.CreateDefaultBuilder()`).

**App.xaml changes:**
- Remove `StartupUri="MainWindow.xaml"`
- Wire `Startup` and `Exit` events for host lifecycle

**App.xaml.cs pattern:**
```csharp
public partial class App : Application
{
    private IHost? _host;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();

        // Layer-specific DI registration via extension methods
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices();

        // Presentation layer registrations
        builder.Services.AddSingleton<MainWindow>();
        builder.Services.AddSingleton<MainViewModel>();

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

### Key Points
- `Host.CreateApplicationBuilder()` auto-loads `appsettings.json` (if present)
- Provides `ILogger<T>`, `IConfiguration`, `IHostEnvironment` out of the box
- Required packages: `Microsoft.Extensions.Hosting`
- MainWindow must accept its ViewModel via constructor injection
- No third-party hosting libraries needed for standard WPF DI

## 2. CommunityToolkit.Mvvm Source Generators

### Setup
- NuGet: `CommunityToolkit.Mvvm` (latest 8.x)
- ViewModels inherit from `ObservableObject` (partial class required for source generators)

### Key Attributes
```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _title;          // Generates public Title property with change notification

    [RelayCommand]
    private void DoSomething()       // Generates public DoSomethingCommand (IRelayCommand)
    {
    }
}
```

### Patterns
- `[ObservableProperty]` replaces manual `OnPropertyChanged` boilerplate
- `[RelayCommand]` replaces manual `ICommand` implementations
- `IMessenger` (WeakReferenceMessenger) for cross-ViewModel communication
- Partial classes are REQUIRED for source generators

## 3. Directory.Build.props & Central Package Management

### Directory.Build.props (solution root)
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

### Directory.Packages.props (solution root)
```xml
<Project>
  <ItemGroup>
    <!-- MVVM -->
    <PackageVersion Include="CommunityToolkit.Mvvm" Version="8.4.0" />
    <!-- Hosting & DI -->
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <!-- Testing -->
    <PackageVersion Include="xunit.v3" Version="1.1.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.5.1" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.0" />
  </ItemGroup>
</Project>
```

### Important: WPF projects override TargetFramework
WPF projects need `net10.0-windows` instead of `net10.0`. Individual csproj files can override:
```xml
<PropertyGroup>
  <TargetFramework>net10.0-windows</TargetFramework>
  <UseWPF>true</UseWPF>
</PropertyGroup>
```

Class library projects that are platform-agnostic keep `net10.0`.

## 4. xUnit v3 Test Project Setup

### Modern approach (2026)
- Install templates: `dotnet new install xunit.v3.templates`
- Create project: `dotnet new xunit3`
- Primary package: `xunit.v3` (includes core, assert, analyzers)
- Self-contained executable test runner (no VSTest adapter needed)
- For VS compatibility: add `Microsoft.NET.Test.Sdk` and `xunit.runner.visualstudio`

### Test project csproj pattern
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="xunit.v3" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="coverlet.collector" />
  </ItemGroup>
</Project>
```

## 5. Clean Architecture DI Extension Method Pattern

Each layer exposes a `IServiceCollection` extension method:

```csharp
// ShareGuard.Application/DependencyInjection.cs
namespace ShareGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Register application services
        return services;
    }
}

// ShareGuard.Infrastructure/DependencyInjection.cs
namespace ShareGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        // Register infrastructure services
        return services;
    }
}
```

This keeps the WPF app lean — it only calls these extension methods without knowing internals.

## 6. Solution File (.slnx) Format

The existing `.slnx` format is the modern XML-based solution file (replaces legacy `.sln`).

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

## 7. Namespace Considerations

Current project uses `Shareguard_wpf` as RootNamespace (underscore from project name). For consistency:
- Domain: `ShareGuard.Domain`
- Application: `ShareGuard.Application`
- Infrastructure: `ShareGuard.Infrastructure`
- WPF App: `ShareGuard.App` (override RootNamespace in csproj)

## 8. Risks & Mitigations

| Risk | Mitigation |
|------|-----------|
| .NET 10 preview instability | Use `LangVersion=preview` if `14.0` not yet recognized |
| CommunityToolkit.Mvvm version conflicts | Pin via Directory.Packages.props CPM |
| WPF TargetFramework mismatch | WPF overrides to `net10.0-windows`, class libs use `net10.0` |
| Circular project references | Enforce strict dependency direction: Domain ← Application ← Infrastructure, WPF references Application + Infrastructure |

## RESEARCH COMPLETE
