# Phase 0: Product Baseline - Context

**Gathered:** 2026-05-20
**Status:** Ready for planning
**Source:** PRD Express Path (docs/superpowers/specs/2026-05-20-phase0-product-baseline-design.md)

<domain>
## Phase Boundary

Establish the foundational solution architecture for ShareGuard WPF. The baseline enforces Clean Architecture boundaries, initializes the MVVM framework, sets up Dependency Injection, and establishes project-wide configuration standards. This phase produces a buildable, compilable solution with correct project references and test infrastructure — no feature logic yet.

</domain>

<decisions>
## Implementation Decisions

### Solution Structure
- Solution divided into 4 projects: ShareGuard.Domain, ShareGuard.Application, ShareGuard.Infrastructure, Shareguard-wpf (WPF Application)
- ShareGuard.Domain: Class Library, contains core entities, interfaces, domain exceptions. No dependencies.
- ShareGuard.Application: Class Library, contains business logic, use cases, service implementations. Depends on ShareGuard.Domain.
- ShareGuard.Infrastructure: Class Library, file system access, metadata extraction, database integrations. Depends on ShareGuard.Domain, ShareGuard.Application.
- Shareguard-wpf: WPF Application (Presentation layer). Depends on ShareGuard.Application and ShareGuard.Infrastructure (solely for DI registration).

### Testing Foundation
- ShareGuard.Domain.Tests (xUnit) — unit tests for domain layer
- ShareGuard.Application.Tests (xUnit) — unit tests for application layer

### Target Framework & Language
- .NET 10, C# 14
- `<Nullable>enable</Nullable>`
- `<ImplicitUsings>enable</ImplicitUsings>`
- `<LangVersion>14.0</LangVersion>` (or `preview`)

### Core Libraries
- MVVM Framework: `CommunityToolkit.Mvvm` (source-generator based)
- DI & Hosting: `Microsoft.Extensions.Hosting`, `Microsoft.Extensions.DependencyInjection`
- Logging: `Microsoft.Extensions.Logging` using `ILogger<T>` (no Serilog in this phase)

### Configuration & Conventions
- Centralized `Directory.Build.props` at solution root for common settings
- Each layer manages its own DI registrations via extension methods (e.g., `AddApplicationServices()`, `AddInfrastructureServices()`)
- WPF App hosts `IHostBuilder` and calls these extension methods in `App.xaml.cs`

### Architectural Constraints (from architecture.md)
- Privacy-First & Local-Only: all processing done locally, no cloud uploads
- Dependency Inversion: Core defines interfaces, App/Platform layers implement
- UDF-style ViewModels: `[ObservableProperty]` for state, `[RelayCommand]` for events
- Immutability of Source Files: NEVER modify original user files
- UI Decoupling: Views must have zero business logic

### Agent's Discretion
- Internal folder structure within each class library project (Models/, Services/, etc.)
- Exact namespace conventions (follow C# standard: project name as root namespace)
- Initial placeholder domain models to validate the architecture
- Test project configuration details (xUnit version, test runner settings)

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Architecture
- `docs/architecture.md` — Approved architecture blueprint (Clean Architecture + MVVM, solution structure, tech stack, implementation rules)

### Phase 0 Spec
- `docs/superpowers/specs/2026-05-20-phase0-product-baseline-design.md` — Phase 0 design document (project structure, testing foundation, core technologies, verification criteria)

### Product Design
- `docs/superpowers/specs/2026-05-19-shareguard-wpf-product-design.md` — Full product design & research (entry points, Android parity concepts, tracer-bullet roadmap)

</canonical_refs>

<specifics>
## Specific Ideas

- The existing `Shareguard-wpf` project is a bare WPF template (no DI, no MVVM toolkit, default MainWindow). It needs to be restructured.
- The existing `.slnx` solution file only references `Shareguard-wpf/Shareguard-wpf.csproj` — needs to be updated for all new projects.
- Current csproj uses `Shareguard_wpf` as RootNamespace (with underscore). Consider aligning namespaces to use `.` format consistent with C# conventions.
- App.xaml.cs currently uses `System.Configuration` and `System.Data` — these should be replaced with DI/Hosting setup.

</specifics>

<deferred>
## Deferred Ideas

- WPF-UI (lepo.co) Fluent Theme integration — Phase 1+
- Platform-specific services (Clipboard, Tray, Hotkeys) — Phase 5+
- Shell extension / MSIX packaging — Phase 6+
- Domain model porting from mobile — Phase 1 will bring `ShareItem`, `StripResult`, etc.

</deferred>

---

*Phase: 00-product-baseline*
*Context gathered: 2026-05-20 via PRD Express Path*
