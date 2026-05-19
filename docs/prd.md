# ShareGuard WPF — Implementation Research

> **Status:** Research / Feasibility Study
> **Date:** 2026-05-19
> **Target Platform:** Windows 10/11 Desktop (WPF + .NET 10)

---

## 1. Executive Summary

This document explores porting ShareGuard's privacy-first metadata stripping concept from Android to Windows Desktop using WPF. The core value proposition — scrubbing EXIF, GPS, tracking parameters, and affiliate analytics from shared content — translates well to Windows, but the **entry points** differ fundamentally from Android's Share Sheet model.

### Key Differences from Android

| Aspect | Android (Current) | Windows (WPF Target) |
|---|---|---|
| Primary Entry | Share Sheet (`ACTION_SEND`) | File Explorer Context Menu + Drag & Drop |
| Secondary Entry | MainActivity (launcher) | Main Window (system tray resident) |
| Tertiary Entry | Quick Settings Tile | Clipboard Monitor + Hotkey |
| UI Framework | Jetpack Compose | WPF (XAML + Fluent Theme) |
| Architecture | MVVM + UDF (StateFlow) | MVVM + UDF (CommunityToolkit.Mvvm) |
| DI | Hilt | Microsoft.Extensions.DependencyInjection |
| Language | Kotlin | C# 14 (.NET 10) |

---

## 2. Architecture

### 2.1. Pattern

**MVVM + CommunityToolkit.Mvvm** — Microsoft's official MVVM toolkit with source generators.

- `[ObservableProperty]` replaces `StateFlow` for observable state
- `[RelayCommand]` replaces lambda callbacks for user actions
- `IMessenger` (WeakReferenceMessenger) for cross-ViewModel events
- No code-behind logic — all in ViewModels

### 2.2. Proposed Module Layout

```
ShareGuard.WPF/
├── ShareGuard.Core/              # .NET Class Library (platform-agnostic)
│   ├── Models/                   # Domain models
│   ├── Strippers/
│   │   ├── IStripper.cs          # Stripper interface + StripResult
│   │   ├── Image/                # EXIF/IPTC/XMP stripping
│   │   ├── Video/                # MP4 atom stripping
│   │   ├── Url/                  # URL param stripping + redirect unwrap
│   │   ├── Pdf/                  # PDF metadata
│   │   └── Office/               # DOCX/XLSX/PPTX
│   ├── Rules/                    # Rule engine, updater
│   ├── Services/                 # Business logic services
│   └── Utils/                    # Shared utilities
│
├── ShareGuard.App/               # WPF Application
│   ├── App.xaml                  # Application entry + DI setup
│   ├── Views/
│   │   ├── MainWindow.xaml       # Primary UI
│   │   ├── SettingsView.xaml
│   │   ├── HistoryView.xaml
│   │   └── StripResultDialog.xaml
│   ├── ViewModels/
│   │   ├── MainViewModel.cs
│   │   ├── SettingsViewModel.cs
│   │   └── HistoryViewModel.cs
│   ├── Controls/                 # Custom WPF controls
│   ├── Theme/                    # Fluent theme customization
│   ├── Services/
│   │   ├── ClipboardService.cs   # Clipboard monitoring
│   │   ├── TrayService.cs        # System tray management
│   │   └── ShellExtService.cs    # Context menu registration
│   └── Converters/               # Value converters
│
├── ShareGuard.Shell/             # Shell Extension (C++/COM or Sparse Package)
│   └── ContextMenuHandler/       # IExplorerCommand implementation
│
└── ShareGuard.Installer/         # MSIX Packaging Project
    └── Package.appxmanifest
```

### 2.3. Layer Boundaries

```
┌──────────────────────────────────┐
│  UI Layer (XAML Views)           │ ← Data Binding to ViewModels
│  NO business logic               │ ← NO direct service calls
├──────────────────────────────────┤
│  ViewModel Layer                 │ ← Owns UI state ([ObservableProperty])
│  Orchestrates services           │ ← Uses [RelayCommand]
├──────────────────────────────────┤
│  Core Layer (ShareGuard.Core)    │ ← Pure C#, no WPF imports
│  Strippers, Rules, Services      │ ← async/await + IProgress<T>
├──────────────────────────────────┤
│  Platform Layer                  │ ← Shell extensions, clipboard, tray
│  Windows-specific integrations   │ ← Win32 interop where needed
└──────────────────────────────────┘
```

---

## 3. Entry Points (Windows Equivalents)

### 3.1. File Explorer Context Menu (Primary — replaces Share Sheet)

**Goal:** User right-clicks file(s) in Explorer → "Clean with ShareGuard" → stripped copy created.

**Implementation Options:**

| Option | Pros | Cons |
|---|---|---|
| **MSIX + `IExplorerCommand` (C++ COM)** | Native Win11 top-level menu, official API | Requires C++ COM DLL alongside C# app |
| **Sparse Package + Registry** | Works with existing installer, simpler | Legacy "Show more options" on Win11 |
| **Protocol Handler (`shareguard://`)** | Pure C#, no COM needed | No context menu, requires manual invocation |

**Recommendation:** MSIX + `IExplorerCommand` for v1.0. Appears in primary context menu on Win11.

**Manifest Registration:**
```xml
<Extensions>
  <desktop4:Extension Category="windows.fileExplorerContextMenus">
    <desktop4:FileExplorerContextMenus>
      <desktop5:ItemType Type="*">
        <desktop5:Verb Id="CleanWithShareGuard"
                       Clsid="..." />
      </desktop5:ItemType>
    </desktop4:FileExplorerContextMenus>
  </desktop4:Extension>
</Extensions>
```

### 3.2. Drag & Drop onto Main Window

**Goal:** User drags files onto ShareGuard window or system tray icon → process and save cleaned copies.

```csharp
// In MainWindow.xaml
<Border AllowDrop="True"
        Drop="OnFileDrop"
        DragOver="OnDragOver">
    <TextBlock Text="Drop files here to clean" />
</Border>

// In code-behind (minimal — delegates to ViewModel)
private void OnFileDrop(object sender, DragEventArgs e)
{
    if (e.Data.GetDataPresent(DataFormats.FileDrop))
    {
        var files = (string[])e.Data.GetData(DataFormats.FileDrop);
        ViewModel.ProcessFilesCommand.Execute(files);
    }
}
```

### 3.3. Clipboard Monitor (replaces Quick Settings Tile)

**Goal:** Detect URLs copied to clipboard → auto-clean tracking params → replace with clean URL.

```csharp
public class ClipboardService : IDisposable
{
    private readonly DispatcherTimer _timer;

    // Poll-based (safer than WM_CLIPBOARDUPDATE hook)
    public ClipboardService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _timer.Tick += CheckClipboard;
    }

    private string _lastContent = "";

    private void CheckClipboard(object? sender, EventArgs e)
    {
        if (!Clipboard.ContainsText()) return;
        var text = Clipboard.GetText();
        if (text == _lastContent) return;
        _lastContent = text;

        if (Uri.TryCreate(text, UriKind.Absolute, out var uri))
        {
            OnUrlDetected?.Invoke(uri);
        }
    }

    public event Action<Uri>? OnUrlDetected;
}
```

### 3.4. System Tray (Always-On Presence)

**Library:** `Hardcodet.NotifyIcon.Wpf` or `WPF-UI.Tray`

**Features:**
- Left-click → Open main window
- Right-click → Quick actions (Clean clipboard, Open file, Settings)
- Balloon notification after auto-clean

### 3.5. Global Hotkey

**Goal:** `Ctrl+Shift+G` → clean current clipboard content instantly.

```csharp
// Using Win32 RegisterHotKey
[DllImport("user32.dll")]
private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);
```

---

## 4. Tech Stack

### 4.1. Core Framework

| Category | Library | Version | Notes |
|---|---|---|---|
| **Runtime** | .NET 10 | 10.0 | LTS, latest WPF support |
| **UI** | WPF + Fluent Theme | Built-in | .NET 10 native Fluent |
| **UI Enhancement** | WPF-UI (lepo.co) | 3.x | Modern controls (NavigationView, Snackbar, Dialog) |
| **MVVM** | CommunityToolkit.Mvvm | 8.x | Source generators, official MS toolkit |
| **DI** | Microsoft.Extensions.DI | 9.x | Built-in .NET DI container |
| **System Tray** | Hardcodet.NotifyIcon.Wpf | 4.x | WPF-native tray icon |

### 4.2. Metadata Stripping

| Module | Library | License | Notes |
|---|---|---|---|
| **Image Read** | MetadataExtractor | Apache 2.0 | Read-only, for verification (Tier 3) |
| **Image Strip** | ExifTool (via SharpExifTool) | GPL/Artistic | Gold standard, CLI wrapper |
| **Image Strip (Alt)** | System.Drawing / ImageSharp | MIT | For simple EXIF removal without external deps |
| **Video Strip** | FFmpeg (`-map_metadata -1 -c copy`) | LGPL | No re-encoding, fast remux |
| **Video Strip (Alt)** | SharpMp4Parser | MIT | Pure C# MP4 atom manipulation |
| **PDF Strip** | iTextSharp / QuestPDF | AGPL/MIT | PDF metadata manipulation |
| **Office Strip** | System.IO.Compression | Built-in | Native ZIP + XML parsing (same as Android) |

### 4.3. URL Processing

| Component | Approach |
|---|---|
| **URL Parsing** | `System.Uri` + `HttpUtility.ParseQueryString` |
| **Rule Engine** | Port ClearURLs JSON rules → C# rule matcher |
| **Redirect Unwrap** | `HttpClient` with manual redirect handling |
| **Signature Verification** | `System.Security.Cryptography` (Ed25519 via NSec or libsodium-net) |

### 4.4. Storage & Data

| Category | Library | Notes |
|---|---|---|
| **Database** | LiteDB or SQLite (EF Core) | Local history storage |
| **Preferences** | `System.Text.Json` + file-based | Settings persistence |
| **Serialization** | System.Text.Json | Built-in, high-performance |

### 4.5. Packaging & Distribution

| Aspect | Choice |
|---|---|
| **Installer** | MSIX (primary) + WinGet |
| **Auto-Update** | MSIX auto-update or Squirrel.Windows |
| **Shell Integration** | MSIX manifest declarations |
| **Code Signing** | Self-signed (dev) → EV cert (release) |

---

## 5. Feature Mapping: Android → WPF

### 5.1. Core Pipeline (Identical Logic)

```
Files received → Detect content type → Route to Stripper
→ Stripper returns StripResult (findings + cleaned data)
→ If no findings: save cleaned copy silently
→ If findings: show Result Dialog → user confirms → save/replace
```

### 5.2. Feature Parity Matrix

| Feature | Android | WPF | Notes |
|---|---|---|---|
| Image EXIF stripping | ✅ ExifInterface | ✅ ExifTool / ImageSharp | Same capability |
| Video atom stripping | ✅ mp4parser | ✅ SharpMp4Parser / FFmpeg | Direct port available |
| URL param cleaning | ✅ Custom engine | ✅ Port to C# | Same rule JSON |
| PDF metadata | ✅ PdfBox-Android | ✅ iTextSharp | Equivalent |
| Office metadata | ✅ ZIP + XML | ✅ ZIP + XML | Identical approach |
| Redirect unwrapping | ✅ OkHttp | ✅ HttpClient | Native .NET |
| Rule auto-update | ✅ WorkManager | ✅ BackgroundService / Timer | Different trigger |
| Ed25519 verification | ✅ Bundled | ✅ NSec / libsodium-net | Same algorithm |
| History/stats | ✅ Room | ✅ LiteDB / SQLite | Local DB |
| Settings | ✅ DataStore | ✅ JSON file | Simpler on desktop |
| Batch processing | ✅ Coroutines | ✅ Task.WhenAll / Parallel | Native async |
| C2PA detection | ✅ Custom JUMBF | ✅ Port to C# | Binary parsing |

### 5.3. Windows-Exclusive Features

| Feature | Description |
|---|---|
| **File Explorer Context Menu** | Right-click → "Clean with ShareGuard" |
| **Drag & Drop** | Drop files onto window or tray icon |
| **Clipboard Auto-Clean** | Monitor clipboard for URLs, auto-strip |
| **Global Hotkey** | Ctrl+Shift+G → instant clipboard clean |
| **Folder Watch** | Monitor a folder, auto-clean new files |
| **Bulk Processing** | Select entire folder → clean all files |
| **In-Place or Copy** | Option to replace original or save copy |

---

## 6. UI Design

### 6.1. Theme

- **.NET 9 Fluent Theme** — built-in dark/light mode support
- **WPF-UI** — provides NavigationView, Snackbar, ContentDialog
- Accent color follows Windows system settings
- Mica/Acrylic backdrop for modern glass effect

### 6.2. Main Window Layout

```
┌─────────────────────────────────────────┐
│  ☰  ShareGuard            ─  □  ✕      │
├────────┬────────────────────────────────┤
│        │                                │
│  🏠    │  ┌──────────────────────────┐  │
│  Home  │  │                          │  │
│        │  │   Drop files here        │  │
│  📋    │  │   or paste a URL         │  │
│  Clean │  │                          │  │
│        │  │   [Browse Files]         │  │
│  📊    │  └──────────────────────────┘  │
│  History│                               │
│        │  Recent Activity               │
│  ⚙️    │  ├ photo.jpg — 12 tags removed │
│  Settings│ ├ shopee.vn/... — 5 trackers │
│        │  └ report.pdf — author stripped │
│        │                                │
└────────┴────────────────────────────────┘
```

### 6.3. Strip Result Dialog

```
┌────────────────────────────────────┐
│  🛡️ ShareGuard Results            │
├────────────────────────────────────┤
│                                    │
│  📷 photo_2026.jpg                 │
│                                    │
│  ❌ Removed (8 items)              │
│  ├ GPS: 10.762622, 106.660172     │
│  ├ Camera: Samsung Galaxy S24     │
│  ├ Software: One UI 6.1          │
│  └ DateTime: 2026-05-19 14:30    │
│                                    │
│  ✅ Kept (1 item)                  │
│  └ Orientation: Normal            │
│                                    │
│  [Save Clean Copy]  [Replace]     │
│                      [Cancel]      │
└────────────────────────────────────┘
```

---

## 7. Dependency Strategy

### 7.1. Minimal External Dependencies (Recommended for v1.0)

For a lean v1.0, minimize external tool dependencies:

| Stripper | Strategy | External Dep? |
|---|---|---|
| Image | `SixLabors.ImageSharp` for EXIF manipulation | No (pure .NET) |
| Video | `SharpMp4Parser` for atom removal | No (pure .NET) |
| PDF | `System.IO` binary parsing (strip XMP/Info dict) | No (pure .NET) |
| Office | `System.IO.Compression` ZIP + XML | No (built-in) |
| URL | Custom C# rule engine | No |

### 7.2. Maximum Capability (v2.0+)

| Stripper | Strategy | External Dep? |
|---|---|---|
| Image | ExifTool via SharpExifTool | Yes (exiftool.exe bundled) |
| Video | FFmpeg `-map_metadata -1 -c copy` | Yes (ffmpeg.exe bundled) |
| PDF | iTextSharp | NuGet only |

---

## 8. Risks & Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Shell extension requires C++ COM | High dev effort | Use Sparse Package for v1.0, full MSIX for v1.1 |
| ExifTool/FFmpeg bundling increases installer size | +50-80MB | v1.0: pure .NET libs only. v2.0: optional download |
| Clipboard monitoring flagged by AV | User trust issue | Use poll-based (not hook), sign with EV cert |
| No equivalent to Android Share Sheet | Different UX paradigm | Context menu + drag-drop + hotkey covers use cases |
| MSIX distribution limits (no sideloading ease) | Adoption barrier | Also provide classic installer (Inno Setup) |
| Windows Defender SmartScreen blocking | First-run friction | EV code signing certificate required |

---

## 9. Development Roadmap

### Phase 1: Core Engine (Week 1-3)
- [ ] Solution setup (.NET 10, WPF + Core library)
- [ ] Port `IStripper` interface + `StripResult` to C#
- [ ] Implement `ImageStripper` (ImageSharp-based)
- [ ] Implement `UrlStripper` (port rule engine)
- [ ] Implement `OfficeStripper` (ZIP + XML)
- [ ] Unit tests for all strippers

### Phase 2: Basic UI (Week 3-5)
- [ ] Main window with Fluent theme + WPF-UI navigation
- [ ] Drag & drop file processing
- [ ] Strip result dialog
- [ ] Settings view (dark/light, affiliate toggle)
- [ ] System tray with basic menu

### Phase 3: Entry Points (Week 5-7)
- [ ] Clipboard URL monitoring
- [ ] Global hotkey registration
- [ ] File picker / folder browse
- [ ] Batch processing with progress

### Phase 4: Advanced Strippers (Week 7-9)
- [ ] `VideoStripper` (SharpMp4Parser)
- [ ] `PdfStripper`
- [ ] C2PA detection port
- [ ] 3-Tier verification system

### Phase 5: Shell Integration (Week 9-11)
- [ ] MSIX packaging project
- [ ] Context menu registration
- [ ] Auto-update mechanism
- [ ] History database (LiteDB)
- [ ] Rule auto-update with Ed25519

### Phase 6: Polish (Week 11-13)
- [ ] Performance optimization
- [ ] Localization (Vietnamese + English)
- [ ] Code signing
- [ ] Installer variants (MSIX + WinGet + classic)
- [ ] Documentation

---

## 10. Build & Tooling

```xml
<!-- Directory.Build.props -->
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>14</LangVersion>
  </PropertyGroup>
</Project>
```

### NuGet Packages (v1.0)

```xml
<!-- Core -->
<PackageReference Include="CommunityToolkit.Mvvm" Version="8.*" />
<PackageReference Include="Microsoft.Extensions.DependencyInjection" Version="10.*" />
<PackageReference Include="Microsoft.Extensions.Hosting" Version="10.*" />

<!-- UI -->
<PackageReference Include="WPF-UI" Version="3.*" />
<PackageReference Include="Hardcodet.NotifyIcon.Wpf" Version="4.*" />

<!-- Strippers -->
<PackageReference Include="SixLabors.ImageSharp" Version="3.*" />
<PackageReference Include="MetadataExtractor" Version="2.*" />
<PackageReference Include="SharpMp4Parser" Version="1.*" />

<!-- Data -->
<PackageReference Include="LiteDB" Version="5.*" />

<!-- Security -->
<PackageReference Include="NSec.Cryptography" Version="24.*" />

<!-- Serialization -->
<!-- System.Text.Json is built-in -->
```

---

## 11. Code Sharing Strategy

The `ShareGuard.Core` library should be **platform-agnostic** (no WPF or Android dependencies), enabling:

- **Shared rule engine** — Same JSON rules, same parsing logic
- **Shared stripper interfaces** — `IStripper`, `StripResult`, `Finding`
- **Shared URL processing** — Same parameter classification
- **Shared Ed25519 verification** — Same security pipeline

This means the Android Kotlin code must be manually ported to C#, but the **algorithms and rule data** are identical.

---

## 12. Conclusion

### Feasibility: ✅ HIGH

ShareGuard's core value — metadata stripping — is **fully achievable** on WPF/.NET with mature libraries. The main challenges are:

1. **Shell integration** (context menu) requires MSIX packaging + COM interop
2. **No direct Share Sheet equivalent** — compensated by context menu + drag-drop + clipboard + hotkey
3. **Code cannot be shared** between Kotlin and C# — requires full rewrite of business logic

### Recommendation

Start with a **pure .NET implementation** (no ExifTool/FFmpeg external deps) for v1.0 to keep the installer lean and avoid GPL licensing concerns. Add external tools as optional enhancements in v2.0.

The WPF version could actually offer **superior UX** for power users through batch folder processing, global hotkeys, and always-on clipboard monitoring — features that are difficult or impossible on Android.
