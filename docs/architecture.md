# ShareGuard WPF — Architecture Blueprint

> **Date:** 2026-05-19
> **Status:** Approved
> **Target:** WPF (.NET 10), C# 14

## 1. Architectural Pattern
ShareGuard WPF employs a **Clean Architecture** combined with the **Model-View-ViewModel (MVVM)** pattern.
The primary goal is strict separation of concerns, ensuring that the core privacy logic (strippers, rules, findings) remains platform-agnostic, while the WPF application layer handles UI state and Windows-specific services.

### Core Principles
- **Privacy-First & Local-Only:** All processing is done locally. No cloud uploads.
- **Dependency Inversion:** The Core layer defines interfaces for Windows-specific services (e.g., Clipboard, File System). The App/Platform layers implement them.
- **UDF-style ViewModels:** State flows down to the UI via `[ObservableProperty]`, and events flow up via `[RelayCommand]`.

## 2. Solution Structure & Module Layout

The solution is divided into the following logical projects:

### 2.1. `ShareGuard.Core`
**Type:** .NET Class Library (Platform-Agnostic)
**Responsibility:** Pure business logic, domain models, stripping algorithms, and rule engines.
- **Models:** `ShareItem`, `StripResult`, `Finding`, `AffiliateMode`
- **Strippers:** `IStripper`, `ImageStripper`, `VideoStripper`, `UrlStripper`, `PdfStripper`, `OfficeStripper`
- **Rules:** ClearURLs JSON rule parsing and matching
- **Verification:** 3-Tier verification system (Pre-flight, Structural, Privacy leak check)
*Note: MUST NOT reference WPF, Win32 APIs, or UI components.*

### 2.2. `ShareGuard.App`
**Type:** WPF Application
**Responsibility:** User Interface, ViewModels, DI container setup, and application lifecycle.
- **Views:** `MainWindow`, `SettingsView`, `HistoryView`, `StripResultDialog` (WPF-UI / Fluent Theme)
- **ViewModels:** `MainViewModel`, `SettingsViewModel`, etc., using `CommunityToolkit.Mvvm`
- **Dependency Injection:** Configured in `App.xaml.cs` via `Microsoft.Extensions.DependencyInjection`

### 2.3. `ShareGuard.Platform.Windows`
**Type:** .NET Class Library (Windows-Specific)
**Responsibility:** Native Windows integrations and hardware/OS-level services.
- **Services:** `ClipboardService` (monitoring), `TrayService` (system tray), File Picker wrappers, Registry access.
- **Hotkeys:** Global hotkey registration (e.g., Win32 `RegisterHotKey`)

### 2.4. `ShareGuard.Shell` / `ShareGuard.Installer`
**Type:** Shell Extension / MSIX Packaging Project
**Responsibility:** System-level entry points and distribution.
- **Context Menu:** MSIX + `IExplorerCommand` for Windows 11 "Clean with ShareGuard" right-click action.
- **Installer:** MSIX package manifest (`Package.appxmanifest`), packaging configurations.

## 3. Data Flow & Execution Pipeline

The core stripping pipeline (the "Tracer-Bullet" flow) ensures the original file is never modified. 

```text
Input (Dropped File / Clipboard URL)
 └──> ShareItem Created
       └──> StripperRouter determines content type
             └──> Specific Stripper (e.g., ImageStripper)
                   ├──> 1. Pre-flight Check
                   ├──> 2. Strip Sensitive Data (Metadata/Params)
                   ├──> 3. Verification Check (Privacy Leak Detection)
                   └──> StripResult Generated
                         └──> Result UI / ViewModels
                               └──> Clean Copy Saved & History Logged
```

## 4. Tech Stack & Dependencies

- **Runtime:** .NET 10.0 (C# 14)
- **UI Framework:** WPF with native Fluent Theme + WPF-UI (`lepo.co`)
- **MVVM Framework:** `CommunityToolkit.Mvvm` 8.x
- **Dependency Injection:** `Microsoft.Extensions.DependencyInjection` 9.x
- **Local Database:** `LiteDB` (for history summaries without original content)
- **Metadata Libraries:** 
  - Image: `SixLabors.ImageSharp`, `MetadataExtractor`
  - Video: `SharpMp4Parser`
  - PDF: `iTextSharp`
- **Security:** `NSec.Cryptography` (Ed25519 verification for rules)

## 5. Implementation Rules & Best Practices

1. **Immutability of Source Files:** The application MUST NEVER modify the original user file. Always output to a clean copy (`.shareguard` or user-defined output folder).
2. **Minimal External Dependencies:** For v1.0, avoid bundling external executables (like ExifTool or FFmpeg) to maintain a lean installer and avoid licensing issues. Use pure C# libraries where possible.
3. **Cross-Platform Parity:** Core logic, domain concepts, and rule parsing should mirror the Android mobile implementation conceptually to prevent cross-platform drift.
4. **UI Decoupling:** Views must have zero business logic. All logic resides in ViewModels and Services.
