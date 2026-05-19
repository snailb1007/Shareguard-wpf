# Phase 0: Product Baseline Design

## Overview
Set up the foundational solution architecture for ShareGuard WPF. The baseline will enforce Clean Architecture boundaries, initialize the MVVM framework, set up Dependency Injection, and establish project-wide configuration standards.

## Project Structure
The solution will be divided into the following projects to strictly separate concerns:

1. **ShareGuard.Domain** (Class Library):
   - Contains core entities, interfaces, and domain exceptions.
   - **Dependencies:** None.

2. **ShareGuard.Application** (Class Library):
   - Contains business logic, use cases, and service implementations.
   - **Dependencies:** `ShareGuard.Domain`.

3. **ShareGuard.Infrastructure** (Class Library):
   - Contains file system access, metadata extraction, and database integrations.
   - **Dependencies:** `ShareGuard.Domain`, `ShareGuard.Application`.

4. **Shareguard-wpf** (WPF Application):
   - The Presentation layer (UI shell, Views, ViewModels).
   - **Dependencies:** `ShareGuard.Application`, `ShareGuard.Infrastructure` (solely for DI registration at startup).

## Testing Foundation
Test projects will be created immediately to encourage test-driven development:
- **ShareGuard.Domain.Tests** (xUnit)
- **ShareGuard.Application.Tests** (xUnit)

## Core Technologies & Libraries
- **Target Framework:** .NET 10, C# 14
- **MVVM Framework:** `CommunityToolkit.Mvvm` (source-generator based).
- **Dependency Injection & Hosting:** `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection`.
- **Logging:** Basic `Microsoft.Extensions.Logging` using `ILogger<T>` (no complex sinks like Serilog in this phase).

## Configuration & Conventions
1. **Centralized Configuration (`Directory.Build.props`):**
   - Place at the root of the solution to enforce common settings across all projects.
   - `<Nullable>enable</Nullable>`
   - `<ImplicitUsings>enable</ImplicitUsings>`
   - `<LangVersion>14.0</LangVersion>` (or `preview` based on .NET 10 availability).

2. **Dependency Injection Pattern:**
   - Each layer will manage its own DI registrations via extension methods (e.g., `AddApplicationServices()`, `AddInfrastructureServices()`).
   - The WPF App will host the `IHostBuilder` and call these extension methods in `App.xaml.cs`.

## Verification
- The solution compiles successfully.
- Projects correctly reference one another without circular dependencies or architectural violations.
- `App.xaml.cs` successfully builds the Host and resolves a basic `MainWindowViewModel`.
- Test projects can run and discover tests.
