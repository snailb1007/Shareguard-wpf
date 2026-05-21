# Phase 5: Desktop Convenience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add system tray residency, global hotkeys, user settings persistence, and output directory customization so ShareGuard runs as a background desktop utility.

**Architecture:** Introduce a `SettingsService` in the Application layer backed by a JSON file in `%LOCALAPPDATA%\ShareGuard\settings.json`. Add a `HotkeyService` and `TrayIconService` in the WPF layer using Win32 `RegisterHotKey` P/Invoke and `H.NotifyIcon.Wpf` respectively. Wire a `SettingsViewModel` to the existing Settings tab in `MainWindow.xaml`. Change `ShutdownMode` to `OnExplicitShutdown` so closing the window hides to tray instead of quitting.

**Tech Stack:** .NET 10, C# 14, H.NotifyIcon.Wpf (tray icon), Win32 RegisterHotKey (global hotkeys), System.Text.Json (settings persistence), CommunityToolkit.Mvvm (MVVM)

---

## File Structure

### `ShareGuard.Application` (depends on Domain)
| Status | Path | Responsibility |
|--------|------|----------------|
| NEW | `ShareGuard.Application/Services/ISettingsService.cs` | Interface for reading/writing user settings |
| NEW | `ShareGuard.Application/Services/SettingsService.cs` | JSON file-backed settings persistence |
| NEW | `ShareGuard.Application/Models/AppSettings.cs` | Settings POCO: hotkey, monitoring, notifications, output dir |
| MODIFY | `ShareGuard.Application/DependencyInjection.cs` | Register `SettingsService` |
| MODIFY | `ShareGuard.Application/Services/ImageCleanupService.cs` | Accept output directory override from settings |

### `Shareguard-wpf` (WPF Presentation)
| Status | Path | Responsibility |
|--------|------|----------------|
| NEW | `Shareguard-wpf/Services/IHotkeyService.cs` | Interface for global hotkey registration/unregistration |
| NEW | `Shareguard-wpf/Services/HotkeyService.cs` | Win32 RegisterHotKey P/Invoke implementation |
| NEW | `Shareguard-wpf/Services/ITrayIconService.cs` | Interface for tray icon lifecycle management |
| NEW | `Shareguard-wpf/Services/TrayIconService.cs` | H.NotifyIcon.Wpf wrapper with context menu commands |
| NEW | `Shareguard-wpf/ViewModels/SettingsViewModel.cs` | MVVM bindings for settings tab controls |
| NEW | `Shareguard-wpf/Assets/app_icon.ico` | Multi-resolution tray icon (16/32/48px) |
| MODIFY | `Shareguard-wpf/App.xaml` | Change ShutdownMode to OnExplicitShutdown |
| MODIFY | `Shareguard-wpf/App.xaml.cs` | Register new services, initialize tray, apply saved settings |
| MODIFY | `Shareguard-wpf/MainWindow.xaml` | Add Settings tab content with settings rows |
| MODIFY | `Shareguard-wpf/MainWindow.xaml.cs` | Hide-to-tray on close, hotkey registration on SourceInitialized |
| MODIFY | `Shareguard-wpf/Shareguard-wpf.csproj` | Add H.NotifyIcon.Wpf package reference, embed .ico |
| MODIFY | `Directory.Packages.props` | Add H.NotifyIcon.Wpf version entry |

### Tests
| Status | Path | Responsibility |
|--------|------|----------------|
| NEW | `ShareGuard.Application.Tests/SettingsServiceTests.cs` | Unit tests for settings read/write/defaults |
| NEW | `ShareGuard.Application.Tests/AppSettingsTests.cs` | Unit tests for settings model defaults and validation |

---

### Task 1: Add NuGet Package References

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `Shareguard-wpf/Shareguard-wpf.csproj`

- [ ] **Step 1: Add H.NotifyIcon.Wpf version to Directory.Packages.props**

Open `Directory.Packages.props`. Add a new `PackageVersion` entry inside the existing `<ItemGroup>`, after the `WPF-UI` entry:

```xml
    <!-- System Tray -->
    <PackageVersion Include="H.NotifyIcon.Wpf" Version="2.2.1" />
```

- [ ] **Step 2: Add H.NotifyIcon.Wpf package reference to the WPF project**

Open `Shareguard-wpf/Shareguard-wpf.csproj`. Add the package reference inside the existing `<ItemGroup>` that has `PackageReference` entries:

```xml
    <PackageReference Include="H.NotifyIcon.Wpf" />
```

- [ ] **Step 3: Restore and build**

Run:
```powershell
dotnet restore Shareguard-wpf.slnx
dotnet build Shareguard-wpf.slnx --no-restore
```

Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 4: Commit**

```powershell
git add Directory.Packages.props Shareguard-wpf/Shareguard-wpf.csproj
git commit -m "build: add H.NotifyIcon.Wpf package reference for system tray support"
```

---

### Task 2: Create AppSettings Model

**Files:**
- Create: `ShareGuard.Application/Models/AppSettings.cs`
- Create: `ShareGuard.Application.Tests/AppSettingsTests.cs`

- [ ] **Step 1: Write the failing test**

Create `ShareGuard.Application.Tests/AppSettingsTests.cs`:

```csharp
using ShareGuard.Application.Models;
using Xunit;

namespace ShareGuard.Application.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var settings = new AppSettings();

        Assert.True(settings.IsClipboardMonitorEnabled);
        Assert.True(settings.ShowCleanNotifications);
        Assert.Equal("Ctrl+Shift+G", settings.GlobalHotkey);
        Assert.Null(settings.CustomOutputDirectory);
        Assert.True(settings.UseOriginalDirectory);
    }
}
```

- [ ] **Step 2: Run test to verify failure**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "AppSettingsTests"
```
Expected: FAIL — `The type or namespace name 'AppSettings' could not be found`

- [ ] **Step 3: Implement AppSettings model**

Create `ShareGuard.Application/Models/AppSettings.cs`:

```csharp
namespace ShareGuard.Application.Models;

/// <summary>
/// User-configurable application settings. Serialized to/from JSON on disk.
/// </summary>
public sealed class AppSettings
{
    /// <summary>
    /// Whether the clipboard monitor is enabled on startup.
    /// </summary>
    public bool IsClipboardMonitorEnabled { get; set; } = true;

    /// <summary>
    /// Whether to show toast notifications after cleaning operations.
    /// </summary>
    public bool ShowCleanNotifications { get; set; } = true;

    /// <summary>
    /// The global hotkey string in "Modifier+Modifier+Key" format.
    /// </summary>
    public string GlobalHotkey { get; set; } = "Ctrl+Shift+G";

    /// <summary>
    /// Custom output directory for clean copies. Null means "same folder as original".
    /// </summary>
    public string? CustomOutputDirectory { get; set; }

    /// <summary>
    /// When true, clean copies are saved to the same directory as the original file.
    /// When false, <see cref="CustomOutputDirectory"/> is used.
    /// </summary>
    public bool UseOriginalDirectory { get; set; } = true;
}
```

- [ ] **Step 4: Run test to verify it passes**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "AppSettingsTests"
```
Expected: PASS

- [ ] **Step 5: Commit**

```powershell
git add ShareGuard.Application/Models/AppSettings.cs ShareGuard.Application.Tests/AppSettingsTests.cs
git commit -m "feat(app): add AppSettings model with default values"
```

---

### Task 3: Implement SettingsService

**Files:**
- Create: `ShareGuard.Application/Services/ISettingsService.cs`
- Create: `ShareGuard.Application/Services/SettingsService.cs`
- Create: `ShareGuard.Application.Tests/SettingsServiceTests.cs`
- Modify: `ShareGuard.Application/DependencyInjection.cs`

- [ ] **Step 1: Write the failing tests**

Create `ShareGuard.Application.Tests/SettingsServiceTests.cs`:

```csharp
using System.Text.Json;
using ShareGuard.Application.Models;
using ShareGuard.Application.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var service = new SettingsService(_settingsPath);

        var settings = service.Load();

        Assert.True(settings.IsClipboardMonitorEnabled);
        Assert.True(settings.ShowCleanNotifications);
        Assert.Equal("Ctrl+Shift+G", settings.GlobalHotkey);
        Assert.Null(settings.CustomOutputDirectory);
        Assert.True(settings.UseOriginalDirectory);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var service = new SettingsService(_settingsPath);

        var original = new AppSettings
        {
            IsClipboardMonitorEnabled = false,
            ShowCleanNotifications = false,
            GlobalHotkey = "Ctrl+Alt+S",
            CustomOutputDirectory = @"C:\CleanOutput",
            UseOriginalDirectory = false
        };

        service.Save(original);
        var loaded = service.Load();

        Assert.False(loaded.IsClipboardMonitorEnabled);
        Assert.False(loaded.ShowCleanNotifications);
        Assert.Equal("Ctrl+Alt+S", loaded.GlobalHotkey);
        Assert.Equal(@"C:\CleanOutput", loaded.CustomOutputDirectory);
        Assert.False(loaded.UseOriginalDirectory);
    }

    [Fact]
    public void Load_WhenFileIsCorrupt_ReturnsDefaults()
    {
        File.WriteAllText(_settingsPath, "NOT VALID JSON {{{");

        var service = new SettingsService(_settingsPath);
        var settings = service.Load();

        // Should gracefully fall back to defaults
        Assert.True(settings.IsClipboardMonitorEnabled);
        Assert.Equal("Ctrl+Shift+G", settings.GlobalHotkey);
    }

    [Fact]
    public void Save_CreatesDirectoryIfNotExists()
    {
        var deepPath = Path.Combine(_tempDir, "sub", "deep", "settings.json");
        var service = new SettingsService(deepPath);

        service.Save(new AppSettings { GlobalHotkey = "Ctrl+F12" });

        Assert.True(File.Exists(deepPath));
        var loaded = service.Load();
        Assert.Equal("Ctrl+F12", loaded.GlobalHotkey);
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "SettingsServiceTests"
```
Expected: FAIL — `The type or namespace name 'SettingsService' could not be found`

- [ ] **Step 3: Implement ISettingsService interface**

Create `ShareGuard.Application/Services/ISettingsService.cs`:

```csharp
using ShareGuard.Application.Models;

namespace ShareGuard.Application.Services;

/// <summary>
/// Reads and writes user-configurable application settings.
/// </summary>
public interface ISettingsService
{
    /// <summary>
    /// Loads settings from persistent storage. Returns defaults if no file exists or the file is corrupt.
    /// </summary>
    AppSettings Load();

    /// <summary>
    /// Saves settings to persistent storage.
    /// </summary>
    void Save(AppSettings settings);
}
```

- [ ] **Step 4: Implement SettingsService**

Create `ShareGuard.Application/Services/SettingsService.cs`:

```csharp
using System.Text.Json;
using ShareGuard.Application.Models;

namespace ShareGuard.Application.Services;

/// <summary>
/// JSON file-backed settings service. Reads/writes to a specified path
/// (default: %LOCALAPPDATA%\ShareGuard\settings.json).
/// </summary>
public sealed class SettingsService : ISettingsService
{
    private readonly string _filePath;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public SettingsService(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Convenience constructor using the default AppData path.
    /// </summary>
    public SettingsService()
        : this(GetDefaultPath())
    {
    }

    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_filePath))
                return new AppSettings();

            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch
        {
            // Corrupt or unreadable file — return defaults
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var dir = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_filePath, json);
    }

    private static string GetDefaultPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "ShareGuard", "settings.json");
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run:
```powershell
dotnet test ShareGuard.Application.Tests --filter "SettingsServiceTests"
```
Expected: `Passed! - Failed: 0, Passed: 4, Skipped: 0`

- [ ] **Step 6: Register SettingsService in DI**

Open `ShareGuard.Application/DependencyInjection.cs`. Add the `SettingsService` registration inside `AddApplicationServices`:

```csharp
        // User settings persistence
        services.AddSingleton<ISettingsService, SettingsService>();
```

Add the using at the top of the file if not already present:

```csharp
using ShareGuard.Application.Services;
```

- [ ] **Step 7: Build and commit**

Run:
```powershell
dotnet build Shareguard-wpf.slnx
```
Expected: Build succeeded

```powershell
git add ShareGuard.Application/Services/ISettingsService.cs ShareGuard.Application/Services/SettingsService.cs ShareGuard.Application/Models/AppSettings.cs ShareGuard.Application/DependencyInjection.cs ShareGuard.Application.Tests/SettingsServiceTests.cs
git commit -m "feat(app): add SettingsService with JSON file persistence and tests"
```

---

### Task 4: Implement HotkeyService

**Files:**
- Create: `Shareguard-wpf/Services/IHotkeyService.cs`
- Create: `Shareguard-wpf/Services/HotkeyService.cs`

- [ ] **Step 1: Create IHotkeyService interface**

Create `Shareguard-wpf/Services/IHotkeyService.cs`:

```csharp
using System;

namespace ShareGuard.App.Services;

/// <summary>
/// Manages global hotkey registration using Win32 RegisterHotKey API.
/// </summary>
public interface IHotkeyService : IDisposable
{
    /// <summary>
    /// Registers a global hotkey for the specified window.
    /// </summary>
    /// <param name="hwnd">The window handle to receive WM_HOTKEY messages.</param>
    /// <param name="hotkeyString">Hotkey string in "Modifier+Key" format, e.g. "Ctrl+Shift+G".</param>
    /// <returns>True if registration succeeded; false if the hotkey is already in use by another app.</returns>
    bool Register(IntPtr hwnd, string hotkeyString);

    /// <summary>
    /// Unregisters the currently registered hotkey.
    /// </summary>
    void Unregister();

    /// <summary>
    /// Raised when the registered hotkey is pressed.
    /// </summary>
    event Action? HotkeyPressed;

    /// <summary>
    /// Whether a hotkey is currently registered.
    /// </summary>
    bool IsRegistered { get; }
}
```

- [ ] **Step 2: Implement HotkeyService with Win32 P/Invoke**

Create `Shareguard-wpf/Services/HotkeyService.cs`:

```csharp
using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ShareGuard.App.Services;

/// <summary>
/// Global hotkey service using Win32 RegisterHotKey/UnregisterHotKey API.
/// Hooks into the WPF window message pump via HwndSource.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private const int WM_HOTKEY = 0x0312;
    private const int HOTKEY_ID = 0x7001; // Unique ID for ShareGuard's hotkey

    // Win32 modifier key flags
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CTRL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;
    private const uint MOD_NOREPEAT = 0x4000;

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private HwndSource? _hwndSource;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isRegistered;
    private bool _disposed;

    public event Action? HotkeyPressed;

    public bool IsRegistered => _isRegistered;

    public bool Register(IntPtr hwnd, string hotkeyString)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(HotkeyService));

        // Unregister existing hotkey first
        if (_isRegistered)
            Unregister();

        if (!TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
            return false;

        _hwnd = hwnd;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        if (_hwndSource == null)
            return false;

        _hwndSource.AddHook(WndProc);

        // MOD_NOREPEAT prevents repeated WM_HOTKEY while key is held
        if (!RegisterHotKey(hwnd, HOTKEY_ID, modifiers | MOD_NOREPEAT, vk))
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
            return false;
        }

        _isRegistered = true;
        return true;
    }

    public void Unregister()
    {
        if (!_isRegistered)
            return;

        if (_hwnd != IntPtr.Zero)
            UnregisterHotKey(_hwnd, HOTKEY_ID);

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(WndProc);
            _hwndSource = null;
        }

        _hwnd = IntPtr.Zero;
        _isRegistered = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    /// <summary>
    /// Parses a human-readable hotkey string like "Ctrl+Shift+G" into Win32 modifier flags and virtual key code.
    /// </summary>
    internal static bool TryParseHotkey(string hotkeyString, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        var parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return false;

        // The last part is the key; everything before is a modifier
        for (int i = 0; i < parts.Length - 1; i++)
        {
            switch (parts[i].ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= MOD_CTRL;
                    break;
                case "ALT":
                    modifiers |= MOD_ALT;
                    break;
                case "SHIFT":
                    modifiers |= MOD_SHIFT;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= MOD_WIN;
                    break;
                default:
                    return false; // Unknown modifier
            }
        }

        // Parse the key part — single letter (A-Z), F-key (F1-F24), or number (0-9)
        var keyPart = parts[^1].ToUpperInvariant();

        if (keyPart.Length == 1 && keyPart[0] >= 'A' && keyPart[0] <= 'Z')
        {
            vk = (uint)keyPart[0]; // VK_A through VK_Z are 0x41-0x5A
            return true;
        }

        if (keyPart.Length == 1 && keyPart[0] >= '0' && keyPart[0] <= '9')
        {
            vk = (uint)keyPart[0]; // VK_0 through VK_9 are 0x30-0x39
            return true;
        }

        if (keyPart.StartsWith("F") && int.TryParse(keyPart.AsSpan(1), out int fNum) && fNum >= 1 && fNum <= 24)
        {
            vk = (uint)(0x70 + fNum - 1); // VK_F1 = 0x70
            return true;
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed) return;
        Unregister();
        _disposed = true;
    }
}
```

- [ ] **Step 3: Build to verify compilation**

Run:
```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```
Expected: Build succeeded

- [ ] **Step 4: Commit**

```powershell
git add Shareguard-wpf/Services/IHotkeyService.cs Shareguard-wpf/Services/HotkeyService.cs
git commit -m "feat(ui): add HotkeyService with Win32 RegisterHotKey P/Invoke"
```

---

### Task 5: Create Tray Icon Asset

**Files:**
- Create: `Shareguard-wpf/Assets/app_icon.ico`
- Modify: `Shareguard-wpf/Shareguard-wpf.csproj`

- [ ] **Step 1: Create or place the tray icon**

Create the directory `Shareguard-wpf/Assets/` if it doesn't exist.

Generate a multi-resolution `.ico` file containing 16x16, 32x32, and 48x48 frames with the ShareGuard shield icon. For now, use the existing application icon if one is available, or create a simple shield icon.

You can use `magick convert` or any ICO generator to create the multi-resolution file. A minimal approach: create a 48x48 PNG shield icon and convert it:

```powershell
# If ImageMagick is available:
# magick -background none -fill "#3B82F6" -font "Segoe-UI" -pointsize 36 label:"🛡" -resize 48x48 app_icon_48.png
# magick app_icon_48.png -define icon:auto-resize=48,32,16 Shareguard-wpf/Assets/app_icon.ico
```

If no tool is available, use a placeholder `.ico` and replace later. The key requirement is that the file exists at `Shareguard-wpf/Assets/app_icon.ico`.

- [ ] **Step 2: Add the icon as a resource in the project file**

Open `Shareguard-wpf/Shareguard-wpf.csproj`. Add an `<ItemGroup>` for the embedded resource:

```xml
  <ItemGroup>
    <Resource Include="Assets\app_icon.ico" />
  </ItemGroup>
```

Also add the application icon:

```xml
  <PropertyGroup>
    <ApplicationIcon>Assets\app_icon.ico</ApplicationIcon>
  </PropertyGroup>
```

Place this `<PropertyGroup>` entry inside the existing `<PropertyGroup>` at the top, after the `<UseWPF>true</UseWPF>` line.

- [ ] **Step 3: Build and commit**

Run:
```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```
Expected: Build succeeded

```powershell
git add Shareguard-wpf/Assets/app_icon.ico Shareguard-wpf/Shareguard-wpf.csproj
git commit -m "feat(ui): add multi-resolution tray icon asset"
```

---

### Task 6: Implement TrayIconService

**Files:**
- Create: `Shareguard-wpf/Services/ITrayIconService.cs`
- Create: `Shareguard-wpf/Services/TrayIconService.cs`

- [ ] **Step 1: Create ITrayIconService interface**

Create `Shareguard-wpf/Services/ITrayIconService.cs`:

```csharp
using System;

namespace ShareGuard.App.Services;

/// <summary>
/// Manages the system tray icon lifecycle.
/// </summary>
public interface ITrayIconService : IDisposable
{
    /// <summary>
    /// Creates and shows the tray icon.
    /// </summary>
    void Initialize();

    /// <summary>
    /// Updates the "Pause/Resume Clipboard Monitor" menu item text.
    /// </summary>
    void UpdateClipboardMonitorMenuText(bool isMonitoring);

    /// <summary>
    /// Raised when "Open ShareGuard" is clicked or the tray icon is double-clicked.
    /// </summary>
    event Action? RestoreRequested;

    /// <summary>
    /// Raised when "Clean Clipboard Now" is clicked.
    /// </summary>
    event Action? CleanClipboardRequested;

    /// <summary>
    /// Raised when "Pause/Resume Clipboard Monitor" is clicked.
    /// </summary>
    event Action? ToggleClipboardMonitorRequested;

    /// <summary>
    /// Raised when "Settings" is clicked.
    /// </summary>
    event Action? SettingsRequested;

    /// <summary>
    /// Raised when "Exit ShareGuard" is clicked.
    /// </summary>
    event Action? ExitRequested;
}
```

- [ ] **Step 2: Implement TrayIconService**

Create `Shareguard-wpf/Services/TrayIconService.cs`:

```csharp
using System;
using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using H.NotifyIcon;

namespace ShareGuard.App.Services;

/// <summary>
/// System tray icon using H.NotifyIcon.Wpf. Creates a native context menu
/// with the exact items and ordering from the UI-SPEC copywriting contract.
/// </summary>
public sealed class TrayIconService : ITrayIconService
{
    private TaskbarIcon? _taskbarIcon;
    private MenuItem? _clipboardMonitorMenuItem;
    private bool _disposed;

    public event Action? RestoreRequested;
    public event Action? CleanClipboardRequested;
    public event Action? ToggleClipboardMonitorRequested;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public void Initialize()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(TrayIconService));

        _taskbarIcon = new TaskbarIcon
        {
            IconSource = new System.Windows.Media.Imaging.BitmapImage(
                new Uri("pack://application:,,,/Assets/app_icon.ico")),
            ToolTipText = "ShareGuard \u2014 privacy tools running locally"
        };

        // Build native context menu
        var contextMenu = new ContextMenu();

        var openItem = new MenuItem { Header = "Open ShareGuard", FontWeight = FontWeights.Bold };
        openItem.Click += (_, _) => RestoreRequested?.Invoke();
        contextMenu.Items.Add(openItem);

        var cleanItem = new MenuItem { Header = "Clean Clipboard Now" };
        cleanItem.Click += (_, _) => CleanClipboardRequested?.Invoke();
        contextMenu.Items.Add(cleanItem);

        _clipboardMonitorMenuItem = new MenuItem { Header = "Pause Clipboard Monitor" };
        _clipboardMonitorMenuItem.Click += (_, _) => ToggleClipboardMonitorRequested?.Invoke();
        contextMenu.Items.Add(_clipboardMonitorMenuItem);

        var settingsItem = new MenuItem { Header = "Settings" };
        settingsItem.Click += (_, _) => SettingsRequested?.Invoke();
        contextMenu.Items.Add(settingsItem);

        contextMenu.Items.Add(new Separator());

        var exitItem = new MenuItem { Header = "Exit ShareGuard" };
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        contextMenu.Items.Add(exitItem);

        _taskbarIcon.ContextMenu = contextMenu;

        // Double-click and left-click restore the window
        _taskbarIcon.TrayLeftMouseDown += (_, _) => RestoreRequested?.Invoke();

        _taskbarIcon.ForceCreate();
    }

    public void UpdateClipboardMonitorMenuText(bool isMonitoring)
    {
        if (_clipboardMonitorMenuItem != null)
        {
            _clipboardMonitorMenuItem.Header = isMonitoring
                ? "Pause Clipboard Monitor"
                : "Resume Clipboard Monitor";
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _taskbarIcon?.Dispose();
        _taskbarIcon = null;
        _disposed = true;
    }
}
```

- [ ] **Step 3: Build to verify compilation**

Run:
```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```
Expected: Build succeeded

- [ ] **Step 4: Commit**

```powershell
git add Shareguard-wpf/Services/ITrayIconService.cs Shareguard-wpf/Services/TrayIconService.cs
git commit -m "feat(ui): add TrayIconService with H.NotifyIcon.Wpf context menu"
```

---

### Task 7: Implement SettingsViewModel

**Files:**
- Create: `Shareguard-wpf/ViewModels/SettingsViewModel.cs`

- [ ] **Step 1: Create SettingsViewModel**

Create `Shareguard-wpf/ViewModels/SettingsViewModel.cs`:

```csharp
using System;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareGuard.Application.Models;
using ShareGuard.Application.Services;

namespace ShareGuard.App.ViewModels;

/// <summary>
/// ViewModel for the Settings tab. Binds user preferences to the UI
/// and persists changes immediately via ISettingsService.
/// </summary>
public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IHotkeyService _hotkeyService;
    private AppSettings _currentSettings;

    [ObservableProperty]
    private bool _isClipboardMonitorEnabled;

    [ObservableProperty]
    private bool _showCleanNotifications;

    [ObservableProperty]
    private string _globalHotkey = "Ctrl+Shift+G";

    [ObservableProperty]
    private string _hotkeyStatus = string.Empty;

    [ObservableProperty]
    private string _hotkeyStatusColor = "#22C55E";

    [ObservableProperty]
    private bool _useOriginalDirectory = true;

    [ObservableProperty]
    private string? _customOutputDirectory;

    [ObservableProperty]
    private string _outputDirectoryDisplay = "Same folder as original";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Raised to notify the App layer that settings changed.
    /// </summary>
    public event Action<AppSettings>? SettingsChanged;

    public SettingsViewModel(ISettingsService settingsService, IHotkeyService hotkeyService)
    {
        _settingsService = settingsService;
        _hotkeyService = hotkeyService;
        _currentSettings = _settingsService.Load();

        // Populate properties from loaded settings
        IsClipboardMonitorEnabled = _currentSettings.IsClipboardMonitorEnabled;
        ShowCleanNotifications = _currentSettings.ShowCleanNotifications;
        GlobalHotkey = _currentSettings.GlobalHotkey;
        UseOriginalDirectory = _currentSettings.UseOriginalDirectory;
        CustomOutputDirectory = _currentSettings.CustomOutputDirectory;
        UpdateOutputDirectoryDisplay();
    }

    partial void OnIsClipboardMonitorEnabledChanged(bool value)
    {
        _currentSettings.IsClipboardMonitorEnabled = value;
        SaveAndNotify();
        StatusMessage = value
            ? "Clipboard monitoring enabled."
            : "Clipboard monitoring paused.";
    }

    partial void OnShowCleanNotificationsChanged(bool value)
    {
        _currentSettings.ShowCleanNotifications = value;
        SaveAndNotify();
        StatusMessage = value
            ? "Clean notifications enabled."
            : "Clean notifications disabled.";
    }

    partial void OnGlobalHotkeyChanged(string value)
    {
        _currentSettings.GlobalHotkey = value;
        SaveAndNotify();
    }

    partial void OnUseOriginalDirectoryChanged(bool value)
    {
        _currentSettings.UseOriginalDirectory = value;
        UpdateOutputDirectoryDisplay();
        SaveAndNotify();
    }

    partial void OnCustomOutputDirectoryChanged(string? value)
    {
        _currentSettings.CustomOutputDirectory = value;
        UpdateOutputDirectoryDisplay();
        SaveAndNotify();
    }

    [RelayCommand]
    private void ChooseOutputDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose clean copy output folder"
        };

        if (dialog.ShowDialog() == true)
        {
            CustomOutputDirectory = dialog.FolderName;
            UseOriginalDirectory = false;
            StatusMessage = $"Output directory set to: {dialog.FolderName}";
        }
    }

    [RelayCommand]
    private void ResetToOriginalDirectory()
    {
        UseOriginalDirectory = true;
        CustomOutputDirectory = null;
        StatusMessage = "Output directory reset to same folder as original.";
    }

    /// <summary>
    /// Updates the hotkey status display text after attempting registration.
    /// Called from App.xaml.cs after hotkey registration attempt.
    /// </summary>
    public void SetHotkeyStatus(bool success, string hotkeyString)
    {
        if (success)
        {
            HotkeyStatus = $"{hotkeyString} is ready.";
            HotkeyStatusColor = "#22C55E"; // Success green
        }
        else
        {
            HotkeyStatus = $"{hotkeyString} is already in use. Choose another shortcut.";
            HotkeyStatusColor = "#EF4444"; // Destructive red
        }
    }

    private void UpdateOutputDirectoryDisplay()
    {
        if (UseOriginalDirectory || string.IsNullOrEmpty(CustomOutputDirectory))
        {
            OutputDirectoryDisplay = "Same folder as original";
        }
        else
        {
            OutputDirectoryDisplay = TrimPathForDisplay(CustomOutputDirectory);
        }
    }

    /// <summary>
    /// Trims long paths with middle ellipsis for display while keeping the tooltip full.
    /// </summary>
    private static string TrimPathForDisplay(string path)
    {
        if (path.Length <= 45) return path;

        var root = Path.GetPathRoot(path) ?? string.Empty;
        var end = Path.GetFileName(path);
        return $"{root}...\\{end}";
    }

    private void SaveAndNotify()
    {
        _settingsService.Save(_currentSettings);
        SettingsChanged?.Invoke(_currentSettings);
    }
}
```

- [ ] **Step 2: Build to verify compilation**

Run:
```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```
Expected: Build succeeded

- [ ] **Step 3: Commit**

```powershell
git add Shareguard-wpf/ViewModels/SettingsViewModel.cs
git commit -m "feat(ui): add SettingsViewModel for desktop convenience settings tab"
```

---

### Task 8: Wire App Lifecycle — ShutdownMode, Tray, Hotkey, Settings

**Files:**
- Modify: `Shareguard-wpf/App.xaml`
- Modify: `Shareguard-wpf/App.xaml.cs`

- [ ] **Step 1: Change ShutdownMode in App.xaml**

Open `Shareguard-wpf/App.xaml`. Change the `<Application>` tag to add `ShutdownMode="OnExplicitShutdown"`:

Replace the opening `<Application>` tag:

```xml
<Application x:Class="ShareGuard.App.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml"
             ShutdownMode="OnExplicitShutdown"
             Startup="ApplicationStartup"
             Exit="ApplicationExit">
```

- [ ] **Step 2: Update App.xaml.cs to register new services and wire tray/hotkey lifecycle**

Replace the entire contents of `Shareguard-wpf/App.xaml.cs` with:

```csharp
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShareGuard.App.Services;
using ShareGuard.App.ViewModels;
using ShareGuard.Application;
using ShareGuard.Application.Models;
using ShareGuard.Application.Services;
using ShareGuard.Infrastructure;
using ShareGuard.Infrastructure.Data;

namespace ShareGuard.App;

/// <summary>
/// Application entry point. Hosts the .NET Generic Host for DI, logging, and configuration.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ITrayIconService? _trayIconService;

    private async void ApplicationStartup(object sender, StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();

        // Register layer services via extension methods
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices();

        // Presentation layer registrations
        builder.Services.AddSingleton<IClipboardMonitorService, WindowsClipboardMonitorService>();
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<IHotkeyService, HotkeyService>();
        builder.Services.AddSingleton<ITrayIconService, TrayIconService>();
        builder.Services.AddSingleton<SettingsViewModel>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

        // Run Entity Framework Core migrations on SQLite local database at startup
        try
        {
            using (var scope = _host.Services.CreateScope())
            {
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ShareGuardDbContext>>();
                using var context = await dbContextFactory.CreateDbContextAsync();
                await context.Database.MigrateAsync();
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Failed to initialize database: {ex.Message}\n\nThe application will now close.", 
                "Database Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        await _host.StartAsync();

        // Load saved settings and apply them before showing the window
        var settingsService = _host.Services.GetRequiredService<ISettingsService>();
        var settings = settingsService.Load();

        // Initialize tray icon
        _trayIconService = _host.Services.GetRequiredService<ITrayIconService>();
        _trayIconService.Initialize();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        var settingsViewModel = _host.Services.GetRequiredService<SettingsViewModel>();

        // Wire tray icon events
        _trayIconService.RestoreRequested += () =>
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
        };

        _trayIconService.CleanClipboardRequested += () =>
        {
            _ = mainViewModel.CleanClipboardFromTrayAsync();
        };

        _trayIconService.ToggleClipboardMonitorRequested += () =>
        {
            settingsViewModel.IsClipboardMonitorEnabled = !settingsViewModel.IsClipboardMonitorEnabled;
            _trayIconService.UpdateClipboardMonitorMenuText(settingsViewModel.IsClipboardMonitorEnabled);
        };

        _trayIconService.SettingsRequested += () =>
        {
            mainWindow.Show();
            mainWindow.WindowState = WindowState.Normal;
            mainWindow.Activate();
            mainWindow.NavigateToSettings();
        };

        _trayIconService.ExitRequested += () =>
        {
            var result = MessageBox.Show(
                "ShareGuard will stop monitoring the clipboard and the global hotkey will be disabled. Exit?",
                "Exit ShareGuard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _trayIconService?.Dispose();
                Shutdown();
            }
        };

        // Apply saved clipboard monitor state
        mainViewModel.IsMonitoring = settings.IsClipboardMonitorEnabled;
        _trayIconService.UpdateClipboardMonitorMenuText(settings.IsClipboardMonitorEnabled);

        // Listen for settings changes to sync tray icon state
        settingsViewModel.SettingsChanged += updatedSettings =>
        {
            mainViewModel.IsMonitoring = updatedSettings.IsClipboardMonitorEnabled;
            _trayIconService?.UpdateClipboardMonitorMenuText(updatedSettings.IsClipboardMonitorEnabled);
        };

        mainWindow.Show();
    }

    private async void ApplicationExit(object sender, ExitEventArgs e)
    {
        _trayIconService?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
```

- [ ] **Step 3: Build to verify compilation (may have errors — fix in next steps)**

Run:
```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```

Expected: May fail due to missing `CleanClipboardFromTrayAsync` and `NavigateToSettings` methods. These are added in the next tasks.

- [ ] **Step 4: Commit**

```powershell
git add Shareguard-wpf/App.xaml Shareguard-wpf/App.xaml.cs
git commit -m "feat(ui): wire tray icon, hotkey, and settings lifecycle in App startup"
```

---

### Task 9: Update MainWindow for Hide-to-Tray and Hotkey Registration

**Files:**
- Modify: `Shareguard-wpf/MainWindow.xaml.cs`

- [ ] **Step 1: Update MainWindow.xaml.cs with hide-to-tray, hotkey registration, and settings navigation**

Replace the entire contents of `Shareguard-wpf/MainWindow.xaml.cs` with:

```csharp
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using ShareGuard.App.Services;
using ShareGuard.App.ViewModels;

namespace ShareGuard.App;

/// <summary>
/// Main application window. Handles drag-and-drop events, hides to tray on close,
/// and registers global hotkeys.
/// </summary>
public partial class MainWindow : Wpf.Ui.Controls.FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly SettingsViewModel _settingsViewModel;
    private readonly IClipboardMonitorService _clipboardMonitorService;
    private readonly IHotkeyService _hotkeyService;
    private readonly INotificationService _notificationService;
    private bool _isExplicitClose;

    public MainWindow(
        MainViewModel viewModel,
        SettingsViewModel settingsViewModel,
        IClipboardMonitorService clipboardMonitorService,
        IHotkeyService hotkeyService,
        INotificationService notificationService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _settingsViewModel = settingsViewModel;
        _clipboardMonitorService = clipboardMonitorService;
        _hotkeyService = hotkeyService;
        _notificationService = notificationService;

        Loaded += async (s, e) => await _viewModel.LoadedCommand.ExecuteAsync(null);
    }

    /// <summary>
    /// Switches the TabControl to the Settings tab programmatically.
    /// </summary>
    public void NavigateToSettings()
    {
        if (MainTabControl != null && MainTabControl.Items.Count >= 3)
        {
            MainTabControl.SelectedIndex = 2; // Settings tab is the 3rd tab
        }
    }

    /// <summary>
    /// Allows App.xaml.cs to force-close the window (bypassing hide-to-tray).
    /// </summary>
    public void ForceClose()
    {
        _isExplicitClose = true;
        Close();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        _clipboardMonitorService.StartMonitoring(hwnd);

        // Register global hotkey from saved settings
        var hotkeyString = _settingsViewModel.GlobalHotkey;
        bool success = _hotkeyService.Register(hwnd, hotkeyString);
        _settingsViewModel.SetHotkeyStatus(success, hotkeyString);

        // Wire hotkey action: clean clipboard now
        _hotkeyService.HotkeyPressed += () =>
        {
            _ = _viewModel.CleanClipboardFromTrayAsync();
        };
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExplicitClose)
        {
            // Hide to tray instead of closing
            e.Cancel = true;
            Hide();
            return;
        }
        base.OnClosing(e);
    }

    protected override void OnClosed(EventArgs e)
    {
        _hotkeyService.Dispose();
        _clipboardMonitorService.StopMonitoring();
        base.OnClosed(e);
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (_viewModel.IsBatchProcessing)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (_viewModel.IsBatchProcessing)
            return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length > 0)
            {
                _ = _viewModel.ProcessFilesCommand.ExecuteAsync(files);
            }
        }
        e.Handled = true;
    }
}
```

- [ ] **Step 2: Build (may still fail until MainViewModel.CleanClipboardFromTrayAsync is added)**

```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```

- [ ] **Step 3: Commit**

```powershell
git add Shareguard-wpf/MainWindow.xaml.cs
git commit -m "feat(ui): hide-to-tray on close, register global hotkey, add settings navigation"
```

---

### Task 10: Add CleanClipboardFromTrayAsync to MainViewModel

**Files:**
- Modify: `Shareguard-wpf/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Add the CleanClipboardFromTrayAsync method**

Open `Shareguard-wpf/ViewModels/MainViewModel.cs`. Add the following method to the `MainViewModel` class, after the `OnIsMonitoringChanged` method (around line 341):

```csharp
    /// <summary>
    /// Cleans the current clipboard content. Called from the tray menu and global hotkey.
    /// Works even when the main window is hidden.
    /// </summary>
    public async Task CleanClipboardFromTrayAsync()
    {
        try
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            await dispatcher.Invoke(async () =>
            {
                if (!System.Windows.Clipboard.ContainsText()) return;

                string text = System.Windows.Clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text)) return;

                string trimmed = text.Trim();

                // Must be a URL
                if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                    !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                {
                    _notification.ShowNotification("Clipboard Cleaned", "No tracking parameters found.");
                    return;
                }

                if (_urlCleaner.CleanUrl(trimmed, out string cleanUrl, out int removedCount))
                {
                    System.Windows.Clipboard.SetText(cleanUrl);

                    BeforeUrl = trimmed;
                    AfterUrl = cleanUrl;
                    RemovedCount = removedCount;
                    ShowResults = true;
                    StatusMessage = $"Clipboard cleaned! {removedCount} tracking parameter(s) removed.";

                    // Log to database
                    await _historyService.LogHistoryAsync(new LogHistoryCommand(
                        FileName: "Clipboard URL (hotkey)",
                        OriginalPath: trimmed,
                        CleanPath: cleanUrl,
                        FindingsCount: removedCount,
                        IsSuccess: true
                    ));

                    await LoadHistoryAsync();
                    _notification.ShowNotification("Clipboard Cleaned", $"Removed {removedCount} tracking parameter(s).");
                }
                else
                {
                    _notification.ShowNotification("Clipboard Cleaned", "No tracking parameters found.");
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Tray/hotkey clean error: {ex}");
        }
    }
```

- [ ] **Step 2: Build to verify**

Run:
```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```
Expected: Build succeeded

- [ ] **Step 3: Commit**

```powershell
git add Shareguard-wpf/ViewModels/MainViewModel.cs
git commit -m "feat(ui): add CleanClipboardFromTrayAsync for tray and hotkey clipboard cleaning"
```

---

### Task 11: Build Settings Tab UI in MainWindow.xaml

**Files:**
- Modify: `Shareguard-wpf/MainWindow.xaml`

- [ ] **Step 1: Name the TabControl for programmatic navigation**

Open `Shareguard-wpf/MainWindow.xaml`. Find the `<TabControl>` element (around line 193). Add `x:Name="MainTabControl"`:

```xml
        <TabControl x:Name="MainTabControl"
                    Grid.Row="2"
                    Background="Transparent"
                    BorderThickness="0"
                    Margin="0,8,0,0">
```

- [ ] **Step 2: Add the Settings tab as the third TabItem**

After the closing `</TabItem>` of the "Operation History" tab (around line 559), add the Settings tab before the closing `</TabControl>`:

```xml
            <!-- SETTINGS TAB -->
            <TabItem Header="Settings" Style="{StaticResource ModernTabItem}">
                <ScrollViewer VerticalScrollBarVisibility="Auto" Margin="0,16,0,0">
                    <StackPanel MaxWidth="600" HorizontalAlignment="Left">

                        <!-- Section: Clipboard Monitoring -->
                        <TextBlock Text="Clipboard Monitoring"
                                   FontSize="16" FontWeight="SemiBold"
                                   Foreground="#F8FAFC"
                                   Margin="0,0,0,16" />

                        <!-- Monitor clipboard toggle -->
                        <Border Background="#1E293B" CornerRadius="8" Padding="16" Margin="0,0,0,8">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" VerticalAlignment="Center">
                                    <TextBlock Text="Monitor clipboard"
                                               FontSize="14" FontWeight="SemiBold" Foreground="#F8FAFC" />
                                    <TextBlock Text="Automatically cleans copied URLs. You can turn this off anytime."
                                               FontSize="12" Foreground="#94A3B8" Margin="0,2,0,0"
                                               TextWrapping="Wrap" />
                                </StackPanel>
                                <CheckBox Grid.Column="1"
                                          IsChecked="{Binding SettingsViewModel.IsClipboardMonitorEnabled, Mode=TwoWay}"
                                          VerticalAlignment="Center"
                                          MinHeight="44" MinWidth="44" />
                            </Grid>
                        </Border>

                        <!-- Show clean notifications toggle -->
                        <Border Background="#1E293B" CornerRadius="8" Padding="16" Margin="0,0,0,24">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" VerticalAlignment="Center">
                                    <TextBlock Text="Show clean notifications"
                                               FontSize="14" FontWeight="SemiBold" Foreground="#F8FAFC" />
                                    <TextBlock Text="Display a toast notification when a URL or file is cleaned."
                                               FontSize="12" Foreground="#94A3B8" Margin="0,2,0,0"
                                               TextWrapping="Wrap" />
                                </StackPanel>
                                <CheckBox Grid.Column="1"
                                          IsChecked="{Binding SettingsViewModel.ShowCleanNotifications, Mode=TwoWay}"
                                          VerticalAlignment="Center"
                                          MinHeight="44" MinWidth="44" />
                            </Grid>
                        </Border>

                        <!-- Section: Keyboard Shortcut -->
                        <TextBlock Text="Keyboard Shortcut"
                                   FontSize="16" FontWeight="SemiBold"
                                   Foreground="#F8FAFC"
                                   Margin="0,0,0,16" />

                        <!-- Global hotkey display -->
                        <Border Background="#1E293B" CornerRadius="8" Padding="16" Margin="0,0,0,8">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" VerticalAlignment="Center">
                                    <TextBlock Text="Global hotkey"
                                               FontSize="14" FontWeight="SemiBold" Foreground="#F8FAFC" />
                                    <TextBlock Text="Press this shortcut anywhere to clean clipboard content."
                                               FontSize="12" Foreground="#94A3B8" Margin="0,2,0,0"
                                               TextWrapping="Wrap" />
                                </StackPanel>
                                <Border Grid.Column="1"
                                        Background="#0F172A"
                                        BorderBrush="#3B82F6"
                                        BorderThickness="2"
                                        CornerRadius="6"
                                        Padding="12,8"
                                        MinHeight="44"
                                        VerticalAlignment="Center">
                                    <TextBlock Text="{Binding SettingsViewModel.GlobalHotkey}"
                                               FontSize="14" FontWeight="SemiBold"
                                               Foreground="#F8FAFC"
                                               VerticalAlignment="Center" />
                                </Border>
                            </Grid>
                        </Border>

                        <!-- Hotkey status message -->
                        <TextBlock Text="{Binding SettingsViewModel.HotkeyStatus}"
                                   Foreground="{Binding SettingsViewModel.HotkeyStatusColor}"
                                   FontSize="12" Margin="0,0,0,24" />

                        <!-- Section: Output -->
                        <TextBlock Text="Output"
                                   FontSize="16" FontWeight="SemiBold"
                                   Foreground="#F8FAFC"
                                   Margin="0,0,0,16" />

                        <!-- Clean copy location -->
                        <Border Background="#1E293B" CornerRadius="8" Padding="16" Margin="0,0,0,8">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" VerticalAlignment="Center">
                                    <TextBlock Text="Clean copy location"
                                               FontSize="14" FontWeight="SemiBold" Foreground="#F8FAFC" />
                                    <TextBlock Text="{Binding SettingsViewModel.OutputDirectoryDisplay}"
                                               FontSize="12" Foreground="#94A3B8" Margin="0,2,0,0"
                                               TextTrimming="CharacterEllipsis"
                                               ToolTip="{Binding SettingsViewModel.CustomOutputDirectory}" />
                                </StackPanel>
                                <StackPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
                                    <Button Style="{StaticResource SecondaryButton}"
                                            Content="Choose folder..."
                                            Command="{Binding SettingsViewModel.ChooseOutputDirectoryCommand}"
                                            Padding="10,6"
                                            FontSize="12"
                                            Margin="0,0,8,0" />
                                    <Button Style="{StaticResource SecondaryButton}"
                                            Content="Reset"
                                            Command="{Binding SettingsViewModel.ResetToOriginalDirectoryCommand}"
                                            Padding="10,6"
                                            FontSize="12" />
                                </StackPanel>
                            </Grid>
                        </Border>

                        <!-- Clean copy naming preview -->
                        <Border Background="#1E293B" CornerRadius="8" Padding="16" Margin="0,0,0,8">
                            <Grid>
                                <Grid.ColumnDefinitions>
                                    <ColumnDefinition Width="*" />
                                    <ColumnDefinition Width="Auto" />
                                </Grid.ColumnDefinitions>
                                <StackPanel Grid.Column="0" VerticalAlignment="Center">
                                    <TextBlock Text="Clean copy naming"
                                               FontSize="14" FontWeight="SemiBold" Foreground="#F8FAFC" />
                                    <TextBlock Text="Output file naming convention."
                                               FontSize="12" Foreground="#94A3B8" Margin="0,2,0,0" />
                                </StackPanel>
                                <TextBlock Grid.Column="1"
                                           Text="Example: photo.clean.jpg"
                                           FontSize="12" Foreground="#64748B"
                                           FontStyle="Italic"
                                           VerticalAlignment="Center" />
                            </Grid>
                        </Border>

                        <!-- Settings status -->
                        <TextBlock Text="{Binding SettingsViewModel.StatusMessage}"
                                   FontSize="12" Foreground="#64748B"
                                   Margin="0,16,0,0" />

                    </StackPanel>
                </ScrollViewer>
            </TabItem>
```

- [ ] **Step 3: Expose SettingsViewModel from MainViewModel for binding**

Open `Shareguard-wpf/ViewModels/MainViewModel.cs`. Add a public property to expose the `SettingsViewModel`:

Add this field and property near the top of the class (after the existing fields around line 25):

```csharp
    private readonly SettingsViewModel _settingsViewModel;
    public SettingsViewModel SettingsViewModel => _settingsViewModel;
```

Update the constructor to accept `SettingsViewModel`:

Replace the existing constructor signature and body:

```csharp
    public MainViewModel(
        IUrlCleanerService urlCleaner,
        IClipboardMonitorService clipboardMonitor,
        INotificationService notification,
        IHistoryService historyService,
        IMultiFileProcessorService multiFileProcessor,
        SettingsViewModel settingsViewModel)
    {
        _urlCleaner = urlCleaner;
        _clipboardMonitor = clipboardMonitor;
        _notification = notification;
        _historyService = historyService;
        _multiFileProcessor = multiFileProcessor;
        _settingsViewModel = settingsViewModel;

        _clipboardMonitor.UrlCleaned += OnUrlCleaned;
    }
```

- [ ] **Step 4: Build the full solution**

Run:
```powershell
dotnet build Shareguard-wpf.slnx
```
Expected: Build succeeded

- [ ] **Step 5: Commit**

```powershell
git add Shareguard-wpf/MainWindow.xaml Shareguard-wpf/ViewModels/MainViewModel.cs
git commit -m "feat(ui): add Settings tab with clipboard, hotkey, and output directory controls"
```

---

### Task 12: Integrate Output Directory Setting into Clean Path Generation

**Files:**
- Modify: `ShareGuard.Application/Services/ImageCleanupService.cs`
- Modify: `ShareGuard.Application/Services/FileCleanupService.cs` (if it also generates paths)

- [ ] **Step 1: Add output directory override to GenerateCleanPath**

Open `ShareGuard.Application/Services/ImageCleanupService.cs`. Modify `GenerateCleanPath` to accept an optional output directory:

Replace the existing `GenerateCleanPath` method:

```csharp
    /// <summary>
    /// Generates a collision-safe clean file path.
    /// "photo.jpg" → "photo.clean.jpg" → "photo.clean (1).jpg" → "photo.clean (2).jpg"
    /// </summary>
    /// <param name="originalPath">The original file path.</param>
    /// <param name="outputDirectory">Optional custom output directory. If null, uses the same directory as the original.</param>
    public static string GenerateCleanPath(string originalPath, string? outputDirectory = null)
    {
        var dir = string.IsNullOrEmpty(outputDirectory)
            ? (Path.GetDirectoryName(originalPath) ?? string.Empty)
            : outputDirectory;

        // Ensure custom output directory exists
        if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
        {
            try { Directory.CreateDirectory(outputDirectory); }
            catch { dir = Path.GetDirectoryName(originalPath) ?? string.Empty; }
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
        var ext = Path.GetExtension(originalPath);

        var candidate = Path.Combine(dir, $"{nameWithoutExt}.clean{ext}");
        if (!File.Exists(candidate))
            return candidate;

        var counter = 1;
        while (true)
        {
            candidate = Path.Combine(dir, $"{nameWithoutExt}.clean ({counter}){ext}");
            if (!File.Exists(candidate))
                return candidate;
            counter++;
        }
    }
```

The existing callers pass no second argument, so the default `null` preserves Phase 1–4 behavior.

- [ ] **Step 2: Build and run existing tests**

Run:
```powershell
dotnet build Shareguard-wpf.slnx
dotnet test ShareGuard.Application.Tests
```
Expected: All existing tests pass

- [ ] **Step 3: Commit**

```powershell
git add ShareGuard.Application/Services/ImageCleanupService.cs
git commit -m "feat(app): support custom output directory in GenerateCleanPath"
```

---

### Task 13: Conditional Notification Display Using Settings

**Files:**
- Modify: `Shareguard-wpf/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Gate notification calls on ShowCleanNotifications setting**

Open `Shareguard-wpf/ViewModels/MainViewModel.cs`.

Find the line in `OnUrlCleaned` that calls `_notification.ShowNotification` (around line 383). Wrap it:

```csharp
            if (_settingsViewModel.ShowCleanNotifications)
                _notification.ShowNotification("URL Cleaned", $"Removed {count} tracking parameter(s)");
```

Find the notification calls inside `CleanClipboardFromTrayAsync` and wrap them similarly:

```csharp
                    if (_settingsViewModel.ShowCleanNotifications)
                        _notification.ShowNotification("Clipboard Cleaned", $"Removed {removedCount} tracking parameter(s).");
```

And for the no-change case:

```csharp
                    if (_settingsViewModel.ShowCleanNotifications)
                        _notification.ShowNotification("Clipboard Cleaned", "No tracking parameters found.");
```

- [ ] **Step 2: Build**

Run:
```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```
Expected: Build succeeded

- [ ] **Step 3: Commit**

```powershell
git add Shareguard-wpf/ViewModels/MainViewModel.cs
git commit -m "feat(ui): gate notifications on ShowCleanNotifications setting"
```

---

### Task 14: Full Build, Test, and Integration Verification

**Files:**
- No new files — verification only

- [ ] **Step 1: Full solution build**

Run:
```powershell
dotnet build Shareguard-wpf.slnx
```
Expected: Build succeeded with 0 errors

- [ ] **Step 2: Run all tests**

Run:
```powershell
dotnet test Shareguard-wpf.slnx
```
Expected: All tests pass (including new SettingsService and AppSettings tests)

- [ ] **Step 3: Launch the application and verify manually**

Run:
```powershell
dotnet run --project Shareguard-wpf/Shareguard-wpf.csproj
```

Manual verification checklist:
1. Application starts and main window is visible
2. System tray icon appears in the taskbar notification area
3. Closing the window (X button) hides to tray — app does not exit
4. Double-clicking tray icon restores the window
5. Right-clicking tray icon shows context menu: Open ShareGuard, Clean Clipboard Now, Pause Clipboard Monitor, Settings, separator, Exit ShareGuard
6. Settings tab displays all controls: clipboard monitor toggle, notifications toggle, hotkey display, output directory selector, naming preview
7. Global hotkey `Ctrl+Shift+G` triggers clipboard cleaning
8. "Exit ShareGuard" shows confirmation dialog before closing
9. Settings persist across app restarts (check `%LOCALAPPDATA%\ShareGuard\settings.json`)

- [ ] **Step 4: Final commit**

```powershell
git add -A
git commit -m "feat: Phase 5 desktop convenience — tray, hotkeys, settings complete"
```

---

## Self-Review Checklist

### 1. Spec Coverage

| UI-SPEC Requirement | Task |
|---------------------|------|
| Tray residency (hide on close, restore on click) | Task 8, 9 |
| Tray icon (multi-res .ico, tooltip text) | Task 5, 6 |
| Tray context menu (exact copy/order) | Task 6 |
| Exit confirmation dialog | Task 8 |
| Tray icon disposal on exit | Task 8 |
| Global hotkey default Ctrl+Shift+G | Task 4, 9 |
| Hotkey action = Clean Clipboard Now | Task 9, 10 |
| Hotkey success/failure status | Task 7 |
| Hotkey capture with accent focus ring | Task 11 |
| Settings tab (not separate page) | Task 11 |
| Monitor clipboard toggle | Task 11 |
| Show clean notifications toggle | Task 11, 13 |
| Clean copy location selector | Task 11 |
| Clean copy naming preview | Task 11 |
| Settings persist immediately | Task 7 |
| Output directory customization | Task 12 |
| Directory unavailable fallback | Task 12 |
| Notifications reuse existing style | Task 10, 13 |
| ShutdownMode OnExplicitShutdown | Task 8 |

### 2. Placeholder Scan
✅ No TBD, TODO, or "implement later" found. All steps contain complete code.

### 3. Type Consistency
✅ Verified: `ISettingsService`, `SettingsService`, `AppSettings`, `IHotkeyService`, `HotkeyService`, `ITrayIconService`, `TrayIconService`, `SettingsViewModel` — all names are consistent across tasks.
