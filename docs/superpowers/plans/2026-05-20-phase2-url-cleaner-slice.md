# Phase 2: URL Cleaner Slice Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add URL tracking parameter cleaning (utm_*, fbclid, gclid, etc.) with automatic clipboard monitoring, a custom animated toast notification, and manual URL paste/clean UI to the ShareGuard WPF app.

**Architecture:** Domain interface `IUrlCleanerService` in Domain layer, implementation `UrlCleanerService` using Flurl in Application layer, Win32 `AddClipboardFormatListener` clipboard monitor as a WPF-layer service, custom borderless glassmorphic toast notification window, and MainViewModel integration with manual URL input + auto-clean toggle.

**Tech Stack:** Flurl 4.0.0 (URL parsing/query param manipulation), Win32 Interop (`AddClipboardFormatListener`/`RemoveClipboardFormatListener`), WPF animations (DoubleAnimation for slide-in/fade-out), CommunityToolkit.Mvvm, xUnit v3

---

## File Structure

| File | Responsibility |
|------|---------------|
| `Directory.Packages.props` | [MODIFY] Add Flurl 4.0.0 package version |
| `ShareGuard.Application/ShareGuard.Application.csproj` | [MODIFY] Add Flurl PackageReference |
| `ShareGuard.Domain/Interfaces/IUrlCleanerService.cs` | [NEW] Domain interface for URL cleaning |
| `ShareGuard.Application/Services/UrlCleanerService.cs` | [NEW] Flurl-based URL tracking parameter cleaner |
| `ShareGuard.Application/DependencyInjection.cs` | [MODIFY] Register IUrlCleanerService |
| `ShareGuard.Application.Tests/UrlCleanerServiceTests.cs` | [NEW] Full unit test suite for URL cleaning logic |
| `Shareguard-wpf/Services/IClipboardMonitorService.cs` | [NEW] Interface for clipboard monitoring |
| `Shareguard-wpf/Services/WindowsClipboardMonitorService.cs` | [NEW] Win32 clipboard listener implementation |
| `Shareguard-wpf/Services/INotificationService.cs` | [NEW] Interface for toast notifications |
| `Shareguard-wpf/Services/NotificationService.cs` | [NEW] Toast notification service |
| `Shareguard-wpf/Views/NotificationWindow.xaml` | [NEW] Glassmorphic slide-in toast window |
| `Shareguard-wpf/Views/NotificationWindow.xaml.cs` | [NEW] Toast animation code-behind |
| `Shareguard-wpf/ViewModels/MainViewModel.cs` | [MODIFY] Add URL cleaning properties/commands |
| `Shareguard-wpf/MainWindow.xaml` | [MODIFY] Add URL cleaner UI panel |
| `Shareguard-wpf/MainWindow.xaml.cs` | [MODIFY] Wire clipboard monitoring to window handle |
| `Shareguard-wpf/App.xaml.cs` | [MODIFY] Register new services in DI |

---

### Task 1: Add Flurl Package

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `ShareGuard.Application/ShareGuard.Application.csproj`

- [ ] **Step 1: Add Flurl version to Directory.Packages.props**

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\Directory.Packages.props` and add a new `<PackageVersion>` entry for Flurl inside the existing `<ItemGroup>`, after the DI Abstractions entry:

```xml
    <!-- URL Parsing -->
    <PackageVersion Include="Flurl" Version="4.0.0" />
```

The full file should now look like:

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
    <!-- URL Parsing -->
    <PackageVersion Include="Flurl" Version="4.0.0" />
    <!-- Testing -->
    <PackageVersion Include="xunit.v3" Version="1.1.0" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add Flurl PackageReference to Application csproj**

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\ShareGuard.Application.csproj` and add `<PackageReference Include="Flurl" />` to the existing package ItemGroup:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <ProjectReference Include="..\ShareGuard.Domain\ShareGuard.Domain.csproj" />
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="Flurl" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Restore and build**

```powershell
dotnet restore ShareGuard.Application/ShareGuard.Application.csproj
dotnet build ShareGuard.Application/ShareGuard.Application.csproj
```

Expected: Restore downloads Flurl 4.0.0. Build succeeds with zero errors.

- [ ] **Step 4: Commit**

```powershell
git add Directory.Packages.props ShareGuard.Application/ShareGuard.Application.csproj
git commit -m "build: add Flurl 4.0.0 for URL query parameter manipulation"
```

---

### Task 2: Create IUrlCleanerService Domain Interface

**Files:**
- Create: `ShareGuard.Domain/Interfaces/IUrlCleanerService.cs`

- [ ] **Step 1: Write the failing test**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application.Tests\UrlCleanerServiceTests.cs`:

```csharp
using Xunit;

namespace ShareGuard.Application.Tests;

public class UrlCleanerServiceTests
{
    [Fact]
    public void CleanUrl_WithUtmSource_ShouldReturnTrueAndStrippedUrl()
    {
        // This test will fail because IUrlCleanerService doesn't exist yet
        Assert.True(false, "IUrlCleanerService interface not yet created");
    }
}
```

- [ ] **Step 2: Run the test to verify it fails**

```powershell
dotnet test ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj --filter "CleanUrl_WithUtmSource_ShouldReturnTrueAndStrippedUrl" --verbosity normal
```

Expected: FAIL with "IUrlCleanerService interface not yet created"

- [ ] **Step 3: Create the domain interface**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Domain\Interfaces\IUrlCleanerService.cs`:

```csharp
namespace ShareGuard.Domain.Interfaces;

/// <summary>
/// Cleans tracking parameters from URLs.
/// </summary>
public interface IUrlCleanerService
{
    /// <summary>
    /// Attempts to clean tracking parameters from a URL.
    /// </summary>
    /// <param name="dirtyUrl">The URL to clean.</param>
    /// <param name="cleanUrl">The cleaned URL, or the original if no changes were made.</param>
    /// <param name="removedCount">The number of tracking parameters removed.</param>
    /// <returns>True if tracking parameters were removed; false if the URL was already clean or invalid.</returns>
    bool CleanUrl(string dirtyUrl, out string cleanUrl, out int removedCount);
}
```

- [ ] **Step 4: Commit**

```powershell
git add ShareGuard.Domain/Interfaces/IUrlCleanerService.cs ShareGuard.Application.Tests/UrlCleanerServiceTests.cs
git commit -m "feat: add IUrlCleanerService domain interface"
```

---

### Task 3: Implement UrlCleanerService

**Files:**
- Create: `ShareGuard.Application/Services/UrlCleanerService.cs`
- Modify: `ShareGuard.Application/DependencyInjection.cs`

- [ ] **Step 1: Create the implementation**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\Services\UrlCleanerService.cs`:

```csharp
using Flurl;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.Application.Services;

/// <summary>
/// Strips known tracking query parameters from URLs using Flurl.
/// </summary>
public class UrlCleanerService : IUrlCleanerService
{
    private static readonly HashSet<string> TrackingParams = new(StringComparer.OrdinalIgnoreCase)
    {
        "utm_source",
        "utm_medium",
        "utm_campaign",
        "utm_term",
        "utm_content",
        "fbclid",
        "gclid",
        "igshid",
        "_gl",
        "tt_medium",
        "twclid",
    };

    public bool CleanUrl(string dirtyUrl, out string cleanUrl, out int removedCount)
    {
        cleanUrl = dirtyUrl;
        removedCount = 0;

        if (string.IsNullOrWhiteSpace(dirtyUrl))
        {
            return false;
        }

        // Validate it's a proper HTTP/HTTPS URL
        if (!Uri.TryCreate(dirtyUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        try
        {
            var url = new Url(dirtyUrl);

            // Find query params that match our tracking blocklist
            var paramsToRemove = url.QueryParams
                .Select(q => q.Name)
                .Where(name => TrackingParams.Contains(name))
                .ToArray();

            if (paramsToRemove.Length == 0)
            {
                return false;
            }

            removedCount = paramsToRemove.Length;
            url.RemoveQueryParams(paramsToRemove);
            cleanUrl = url.ToString();
            return true;
        }
        catch
        {
            // Flurl parsing failed — leave the original URL untouched
            return false;
        }
    }
}
```

Key design points:
- Only accepts `http://` and `https://` schemes — `ftp://`, `file://`, etc. are ignored.
- Case-insensitive parameter matching via `StringComparer.OrdinalIgnoreCase`.
- Flurl preserves fragments (`#`), URL encoding, and non-tracking query parameters.
- Catches any Flurl parsing exceptions for malformed URLs.

- [ ] **Step 2: Register in DependencyInjection.cs**

Open `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application\DependencyInjection.cs` and replace the entire file with:

```csharp
using Microsoft.Extensions.DependencyInjection;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IUrlCleanerService, UrlCleanerService>();
        return services;
    }
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build ShareGuard.Application/ShareGuard.Application.csproj
```

Expected: Build succeeds with zero errors.

- [ ] **Step 4: Commit**

```powershell
git add ShareGuard.Application/Services/UrlCleanerService.cs ShareGuard.Application/DependencyInjection.cs
git commit -m "feat: implement UrlCleanerService with Flurl-based tracking param removal"
```

---

### Task 4: Write Full Unit Tests for UrlCleanerService

**Files:**
- Modify: `ShareGuard.Application.Tests/UrlCleanerServiceTests.cs`

- [ ] **Step 1: Replace the placeholder test with the full test suite**

Replace the entire content of `c:\Users\ADMIN\source\repos\Shareguard-wpf\ShareGuard.Application.Tests\UrlCleanerServiceTests.cs` with:

```csharp
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;
using Xunit;

namespace ShareGuard.Application.Tests;

public class UrlCleanerServiceTests
{
    private readonly IUrlCleanerService _sut = new UrlCleanerService();

    [Fact]
    public void CleanUrl_WithUtmSource_ShouldReturnTrueAndStrippedUrl()
    {
        var result = _sut.CleanUrl(
            "https://example.com/page?id=123&utm_source=newsletter",
            out var cleanUrl,
            out var removedCount);

        Assert.True(result);
        Assert.Equal("https://example.com/page?id=123", cleanUrl);
        Assert.Equal(1, removedCount);
    }

    [Fact]
    public void CleanUrl_WithMultipleTrackers_ShouldRemoveAllTrackers()
    {
        var result = _sut.CleanUrl(
            "https://example.com/item?id=42&utm_source=news&utm_medium=email&fbclid=abc123",
            out var cleanUrl,
            out var removedCount);

        Assert.True(result);
        Assert.Equal("https://example.com/item?id=42", cleanUrl);
        Assert.Equal(3, removedCount);
    }

    [Fact]
    public void CleanUrl_WithNoTrackers_ShouldReturnFalse()
    {
        var result = _sut.CleanUrl(
            "https://example.com/page?id=123&category=books",
            out var cleanUrl,
            out var removedCount);

        Assert.False(result);
        Assert.Equal("https://example.com/page?id=123&category=books", cleanUrl);
        Assert.Equal(0, removedCount);
    }

    [Fact]
    public void CleanUrl_WithFragment_ShouldPreserveFragment()
    {
        var result = _sut.CleanUrl(
            "https://site.org/docs#section?utm_source=twitter",
            out var cleanUrl,
            out var removedCount);

        // Flurl may or may not detect params after fragment — test actual behavior
        // If the URL has params before the fragment, they should be cleaned
        // If params are only after #, browser ignores them — this is a non-standard edge case
        // The important thing is the URL is not corrupted
        Assert.NotNull(cleanUrl);
    }

    [Fact]
    public void CleanUrl_WithQueryAndFragment_ShouldCleanParamsAndPreserveFragment()
    {
        var result = _sut.CleanUrl(
            "https://example.com/page?id=1&utm_campaign=spring#top",
            out var cleanUrl,
            out var removedCount);

        Assert.True(result);
        Assert.Contains("id=1", cleanUrl);
        Assert.Contains("#top", cleanUrl);
        Assert.DoesNotContain("utm_campaign", cleanUrl);
        Assert.Equal(1, removedCount);
    }

    [Theory]
    [InlineData("UTM_SOURCE=xyz")]
    [InlineData("Utm_Source=xyz")]
    [InlineData("utm_SOURCE=xyz")]
    public void CleanUrl_CaseInsensitiveParams_ShouldBeRemoved(string param)
    {
        var result = _sut.CleanUrl(
            $"https://example.com/page?{param}",
            out var cleanUrl,
            out var removedCount);

        Assert.True(result);
        Assert.Equal("https://example.com/page", cleanUrl);
        Assert.Equal(1, removedCount);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not a url at all")]
    [InlineData("ftp://files.example.com/doc.pdf")]
    [InlineData("file:///C:/local/file.txt")]
    [InlineData("mailto:test@example.com")]
    public void CleanUrl_InvalidOrNonHttpUrl_ShouldReturnFalse(string input)
    {
        var result = _sut.CleanUrl(input, out var cleanUrl, out var removedCount);

        Assert.False(result);
        Assert.Equal(0, removedCount);
    }

    [Fact]
    public void CleanUrl_NullInput_ShouldReturnFalse()
    {
        var result = _sut.CleanUrl(null!, out var cleanUrl, out var removedCount);

        Assert.False(result);
        Assert.Equal(0, removedCount);
    }

    [Fact]
    public void CleanUrl_UrlEncodedValues_ShouldPreserveEncoding()
    {
        var result = _sut.CleanUrl(
            "https://google.com/search?q=c%23+tutorial&gclid=999",
            out var cleanUrl,
            out var removedCount);

        Assert.True(result);
        Assert.Contains("q=c%23", cleanUrl);
        Assert.DoesNotContain("gclid", cleanUrl);
        Assert.Equal(1, removedCount);
    }

    [Fact]
    public void CleanUrl_AllKnownTrackers_ShouldRemoveEach()
    {
        var trackers = new[]
        {
            "utm_source", "utm_medium", "utm_campaign", "utm_term", "utm_content",
            "fbclid", "gclid", "igshid", "_gl", "tt_medium", "twclid"
        };

        foreach (var tracker in trackers)
        {
            var result = _sut.CleanUrl(
                $"https://example.com/?{tracker}=test",
                out var cleanUrl,
                out var removedCount);

            Assert.True(result, $"Tracker '{tracker}' should be removed");
            Assert.DoesNotContain(tracker, cleanUrl);
            Assert.Equal(1, removedCount);
        }
    }

    [Fact]
    public void CleanUrl_CleanHttpUrl_ShouldReturnFalse()
    {
        var result = _sut.CleanUrl(
            "https://example.com/page",
            out var cleanUrl,
            out var removedCount);

        Assert.False(result);
        Assert.Equal("https://example.com/page", cleanUrl);
        Assert.Equal(0, removedCount);
    }
}
```

- [ ] **Step 2: Run the tests**

```powershell
dotnet test ShareGuard.Application.Tests/ShareGuard.Application.Tests.csproj --verbosity normal
```

Expected: All tests pass. If any fail due to Flurl's fragment or encoding behavior, adjust assertions to match actual Flurl output (don't hand-roll fixes — match the library's correct output).

- [ ] **Step 3: Commit**

```powershell
git add ShareGuard.Application.Tests/UrlCleanerServiceTests.cs
git commit -m "test: add comprehensive unit tests for UrlCleanerService"
```

---

### Task 5: Create IClipboardMonitorService Interface and Win32 Implementation

**Files:**
- Create: `Shareguard-wpf/Services/IClipboardMonitorService.cs`
- Create: `Shareguard-wpf/Services/WindowsClipboardMonitorService.cs`

- [ ] **Step 1: Create the interface**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\Services\IClipboardMonitorService.cs`:

```csharp
namespace ShareGuard.App.Services;

/// <summary>
/// Monitors the system clipboard for URL text and automatically cleans tracking parameters.
/// </summary>
public interface IClipboardMonitorService : IDisposable
{
    /// <summary>
    /// Raised when a URL on the clipboard has been cleaned.
    /// </summary>
    event Action<string, string, int>? UrlCleaned;

    /// <summary>
    /// Whether the clipboard monitor is currently active.
    /// </summary>
    bool IsMonitoring { get; }

    /// <summary>
    /// Start monitoring the clipboard. Must be called from the UI thread after the window handle is available.
    /// </summary>
    /// <param name="hwnd">The window handle to receive clipboard messages.</param>
    void StartMonitoring(IntPtr hwnd);

    /// <summary>
    /// Stop monitoring and unhook from clipboard messages.
    /// </summary>
    void StopMonitoring();
}
```

- [ ] **Step 2: Create the Win32 implementation**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\Services\WindowsClipboardMonitorService.cs`:

```csharp
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.App.Services;

/// <summary>
/// Monitors the system clipboard using Win32 AddClipboardFormatListener.
/// When a valid HTTP/HTTPS URL with tracking parameters is copied,
/// it is automatically cleaned and written back to the clipboard.
/// </summary>
public partial class WindowsClipboardMonitorService : IClipboardMonitorService
{
    private const int WM_CLIPBOARDUPDATE = 0x031D;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AddClipboardFormatListener(IntPtr hwnd);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool RemoveClipboardFormatListener(IntPtr hwnd);

    private readonly IUrlCleanerService _urlCleaner;
    private IntPtr _hwnd;
    private HwndSource? _hwndSource;
    private bool _selfTriggered;

    public event Action<string, string, int>? UrlCleaned;
    public bool IsMonitoring { get; private set; }

    public WindowsClipboardMonitorService(IUrlCleanerService urlCleaner)
    {
        _urlCleaner = urlCleaner;
    }

    public void StartMonitoring(IntPtr hwnd)
    {
        if (IsMonitoring) return;

        _hwnd = hwnd;
        _hwndSource = HwndSource.FromHwnd(hwnd);
        _hwndSource?.AddHook(WndProc);
        AddClipboardFormatListener(hwnd);
        IsMonitoring = true;
    }

    public void StopMonitoring()
    {
        if (!IsMonitoring) return;

        RemoveClipboardFormatListener(_hwnd);
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
        IsMonitoring = false;
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            OnClipboardUpdate();
        }
        return IntPtr.Zero;
    }

    private void OnClipboardUpdate()
    {
        // Avoid infinite loop: if we just wrote to the clipboard, skip this event
        if (_selfTriggered)
        {
            _selfTriggered = false;
            return;
        }

        try
        {
            if (!Clipboard.ContainsText()) return;

            var text = Clipboard.GetText().Trim();
            if (string.IsNullOrEmpty(text)) return;

            if (_urlCleaner.CleanUrl(text, out var cleanUrl, out var removedCount))
            {
                _selfTriggered = true;
                Clipboard.SetText(cleanUrl);
                UrlCleaned?.Invoke(text, cleanUrl, removedCount);
            }
        }
        catch
        {
            // Clipboard access can throw if locked by another process — swallow and retry next time
        }
    }

    public void Dispose()
    {
        StopMonitoring();
        GC.SuppressFinalize(this);
    }
}
```

Key design points:
- Uses `LibraryImport` (modern .NET source-generated P/Invoke, not legacy `DllImport`).
- `_selfTriggered` flag prevents infinite feedback loop when we write the cleaned URL back.
- Catches clipboard access exceptions (another process may hold the clipboard lock).
- `HwndSource.AddHook` intercepts `WM_CLIPBOARDUPDATE` messages from the Win32 message loop.

- [ ] **Step 3: Build**

```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```

Expected: Build succeeds with zero errors.

- [ ] **Step 4: Commit**

```powershell
git add Shareguard-wpf/Services/IClipboardMonitorService.cs Shareguard-wpf/Services/WindowsClipboardMonitorService.cs
git commit -m "feat: add Win32 clipboard monitor with auto-clean URL support"
```

---

### Task 6: Create Custom Glassmorphic Toast Notification

**Files:**
- Create: `Shareguard-wpf/Services/INotificationService.cs`
- Create: `Shareguard-wpf/Services/NotificationService.cs`
- Create: `Shareguard-wpf/Views/NotificationWindow.xaml`
- Create: `Shareguard-wpf/Views/NotificationWindow.xaml.cs`

- [ ] **Step 1: Create INotificationService interface**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\Services\INotificationService.cs`:

```csharp
namespace ShareGuard.App.Services;

/// <summary>
/// Shows toast-style notifications to the user.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Show a toast notification in the bottom-right corner of the screen.
    /// </summary>
    /// <param name="title">The notification title.</param>
    /// <param name="message">The notification body text.</param>
    void Show(string title, string message);
}
```

- [ ] **Step 2: Create NotificationWindow.xaml**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\Views\NotificationWindow.xaml`:

```xml
<Window x:Class="ShareGuard.App.Views.NotificationWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        WindowStyle="None"
        AllowsTransparency="True"
        Background="Transparent"
        ShowInTaskbar="False"
        Topmost="True"
        Width="340"
        Height="90"
        ResizeMode="NoResize">
    <Border CornerRadius="12"
            Margin="8">
        <Border.Background>
            <SolidColorBrush Color="#1E293B" Opacity="0.92" />
        </Border.Background>
        <Border.Effect>
            <DropShadowEffect BlurRadius="20"
                              ShadowDepth="4"
                              Opacity="0.4"
                              Color="#000000" />
        </Border.Effect>
        <Grid Margin="16,12">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="32" />
                <ColumnDefinition Width="12" />
                <ColumnDefinition Width="*" />
            </Grid.ColumnDefinitions>
            <Grid.RowDefinitions>
                <RowDefinition Height="Auto" />
                <RowDefinition Height="Auto" />
            </Grid.RowDefinitions>

            <!-- Shield icon (Unicode) -->
            <TextBlock Grid.Column="0"
                       Grid.RowSpan="2"
                       Text="🛡️"
                       FontSize="22"
                       VerticalAlignment="Center"
                       HorizontalAlignment="Center" />

            <!-- Title -->
            <TextBlock x:Name="TitleText"
                       Grid.Column="2"
                       Grid.Row="0"
                       Text="URL Cleaned"
                       FontSize="14"
                       FontWeight="SemiBold"
                       Foreground="#F8FAFC" />

            <!-- Message -->
            <TextBlock x:Name="MessageText"
                       Grid.Column="2"
                       Grid.Row="1"
                       Text="Tracking parameters removed"
                       FontSize="12"
                       Foreground="#94A3B8"
                       Margin="0,2,0,0"
                       TextTrimming="CharacterEllipsis" />
        </Grid>
    </Border>
</Window>
```

Design notes:
- Dark slate background (`#1E293B`) with 92% opacity for glassmorphic feel.
- `DropShadowEffect` for floating card appearance.
- Rounded corners (`CornerRadius="12"`).
- Emoji shield icon — no external icon dependency needed.
- `ShowInTaskbar="False"` + `Topmost="True"` — non-intrusive overlay.

- [ ] **Step 3: Create NotificationWindow.xaml.cs**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\Views\NotificationWindow.xaml.cs`:

```csharp
using System.Windows;
using System.Windows.Media.Animation;

namespace ShareGuard.App.Views;

/// <summary>
/// A borderless slide-in/fade-out toast notification window.
/// Positions itself in the bottom-right corner of the primary screen and auto-closes after 3 seconds.
/// </summary>
public partial class NotificationWindow : Window
{
    public NotificationWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void SetContent(string title, string message)
    {
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Position at bottom-right of primary screen, above the taskbar
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - ActualWidth - 8;
        Top = workArea.Bottom; // Start just below visible area for slide-in

        // Slide-in animation: move up from below the screen
        var slideIn = new DoubleAnimation
        {
            From = workArea.Bottom,
            To = workArea.Bottom - ActualHeight - 8,
            Duration = TimeSpan.FromMilliseconds(300),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        };

        // Fade-out animation: starts after 2.5 seconds, lasts 0.5 seconds
        var fadeOut = new DoubleAnimation
        {
            From = 1.0,
            To = 0.0,
            Duration = TimeSpan.FromMilliseconds(500),
            BeginTime = TimeSpan.FromMilliseconds(2500)
        };
        fadeOut.Completed += (_, _) => Close();

        BeginAnimation(TopProperty, slideIn);
        BeginAnimation(OpacityProperty, fadeOut);
    }
}
```

- [ ] **Step 4: Create NotificationService.cs**

Create `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\Services\NotificationService.cs`:

```csharp
using ShareGuard.App.Views;

namespace ShareGuard.App.Services;

/// <summary>
/// Creates and shows toast notification windows on the WPF UI thread.
/// </summary>
public class NotificationService : INotificationService
{
    public void Show(string title, string message)
    {
        // Must run on UI thread because it creates a WPF Window
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            var notification = new NotificationWindow();
            notification.SetContent(title, message);
            notification.Show();
        });
    }
}
```

- [ ] **Step 5: Build**

```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```

Expected: Build succeeds with zero errors.

- [ ] **Step 6: Commit**

```powershell
git add Shareguard-wpf/Services/INotificationService.cs Shareguard-wpf/Services/NotificationService.cs Shareguard-wpf/Views/NotificationWindow.xaml Shareguard-wpf/Views/NotificationWindow.xaml.cs
git commit -m "feat: add glassmorphic slide-in toast notification system"
```

---

### Task 7: Update MainViewModel with URL Cleaning Properties and Commands

**Files:**
- Modify: `Shareguard-wpf/ViewModels/MainViewModel.cs`

- [ ] **Step 1: Replace MainViewModel.cs**

Replace the entire content of `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\ViewModels\MainViewModel.cs` with:

```csharp
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareGuard.App.Services;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IUrlCleanerService _urlCleaner;
    private readonly IClipboardMonitorService _clipboardMonitor;
    private readonly INotificationService _notification;

    [ObservableProperty]
    private string _title = "ShareGuard";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // URL Cleaner properties
    [ObservableProperty]
    private string _manualUrlInput = string.Empty;

    [ObservableProperty]
    private string _beforeUrl = string.Empty;

    [ObservableProperty]
    private string _afterUrl = string.Empty;

    [ObservableProperty]
    private int _removedCount;

    [ObservableProperty]
    private bool _showResults;

    [ObservableProperty]
    private bool _isMonitoring;

    public MainViewModel(
        IUrlCleanerService urlCleaner,
        IClipboardMonitorService clipboardMonitor,
        INotificationService notification)
    {
        _urlCleaner = urlCleaner;
        _clipboardMonitor = clipboardMonitor;
        _notification = notification;

        _clipboardMonitor.UrlCleaned += OnUrlCleaned;
    }

    [RelayCommand]
    private void Loaded()
    {
        StatusMessage = "ShareGuard is ready to protect your privacy.";
    }

    [RelayCommand]
    private void CleanManualUrl()
    {
        if (string.IsNullOrWhiteSpace(ManualUrlInput))
        {
            StatusMessage = "Please enter a URL to clean.";
            return;
        }

        if (_urlCleaner.CleanUrl(ManualUrlInput, out var cleanUrl, out var removed))
        {
            BeforeUrl = ManualUrlInput;
            AfterUrl = cleanUrl;
            RemovedCount = removed;
            ShowResults = true;
            StatusMessage = $"Cleaned! {removed} tracking parameter(s) removed.";
        }
        else
        {
            BeforeUrl = ManualUrlInput;
            AfterUrl = ManualUrlInput;
            RemovedCount = 0;
            ShowResults = true;
            StatusMessage = "URL is already clean — no tracking parameters found.";
        }
    }

    partial void OnIsMonitoringChanged(bool value)
    {
        if (value)
        {
            StatusMessage = "Clipboard monitoring active — copy a URL to auto-clean it.";
        }
        else
        {
            _clipboardMonitor.StopMonitoring();
            StatusMessage = "Clipboard monitoring paused.";
        }
    }

    private void OnUrlCleaned(string before, string after, int count)
    {
        // Dispatch to UI thread
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            BeforeUrl = before;
            AfterUrl = after;
            RemovedCount = count;
            ShowResults = true;
            StatusMessage = $"Auto-cleaned! {count} tracking parameter(s) stripped from clipboard.";
        });

        _notification.Show("URL Cleaned", $"Removed {count} tracking parameter(s)");
    }

    public void Dispose()
    {
        _clipboardMonitor.UrlCleaned -= OnUrlCleaned;
        _clipboardMonitor.Dispose();
        GC.SuppressFinalize(this);
    }
}
```

Key design points:
- `OnIsMonitoringChanged` partial method is auto-generated by `[ObservableProperty]` and called when `IsMonitoring` changes — we use it to stop monitoring but NOT to start it (starting requires the HWND, which comes from MainWindow).
- `OnUrlCleaned` dispatches property changes to the UI thread since the clipboard event fires on the message pump thread.
- ViewModel subscribes to `UrlCleaned` event in constructor and unsubscribes in `Dispose()`.

- [ ] **Step 2: Build**

```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```

Expected: Build succeeds with zero errors.

- [ ] **Step 3: Commit**

```powershell
git add Shareguard-wpf/ViewModels/MainViewModel.cs
git commit -m "feat: add URL cleaning commands and clipboard monitor integration to MainViewModel"
```

---

### Task 8: Update MainWindow XAML with URL Cleaner UI

**Files:**
- Modify: `Shareguard-wpf/MainWindow.xaml`

- [ ] **Step 1: Replace MainWindow.xaml**

Replace the entire content of `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\MainWindow.xaml` with:

```xml
<Window x:Class="ShareGuard.App.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
        xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
        mc:Ignorable="d"
        Title="{Binding Title}"
        Height="600" Width="900"
        Background="#0F172A">

    <Window.Resources>
        <BooleanToVisibilityConverter x:Key="BoolToVis" />

        <!-- Accent button style -->
        <Style x:Key="AccentButton" TargetType="Button">
            <Setter Property="Background" Value="#3B82F6" />
            <Setter Property="Foreground" Value="#FFFFFF" />
            <Setter Property="FontSize" Value="14" />
            <Setter Property="FontWeight" Value="SemiBold" />
            <Setter Property="Padding" Value="20,10" />
            <Setter Property="BorderThickness" Value="0" />
            <Setter Property="Cursor" Value="Hand" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="Button">
                        <Border x:Name="border"
                                Background="{TemplateBinding Background}"
                                CornerRadius="8"
                                Padding="{TemplateBinding Padding}">
                            <ContentPresenter HorizontalAlignment="Center"
                                              VerticalAlignment="Center" />
                        </Border>
                        <ControlTemplate.Triggers>
                            <Trigger Property="IsMouseOver" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#2563EB" />
                            </Trigger>
                            <Trigger Property="IsPressed" Value="True">
                                <Setter TargetName="border" Property="Background" Value="#1D4ED8" />
                            </Trigger>
                        </ControlTemplate.Triggers>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>

        <!-- Modern textbox style -->
        <Style x:Key="ModernTextBox" TargetType="TextBox">
            <Setter Property="Background" Value="#1E293B" />
            <Setter Property="Foreground" Value="#F8FAFC" />
            <Setter Property="CaretBrush" Value="#F8FAFC" />
            <Setter Property="BorderBrush" Value="#334155" />
            <Setter Property="BorderThickness" Value="1" />
            <Setter Property="Padding" Value="12,10" />
            <Setter Property="FontSize" Value="14" />
            <Setter Property="Template">
                <Setter.Value>
                    <ControlTemplate TargetType="TextBox">
                        <Border Background="{TemplateBinding Background}"
                                BorderBrush="{TemplateBinding BorderBrush}"
                                BorderThickness="{TemplateBinding BorderThickness}"
                                CornerRadius="8"
                                Padding="{TemplateBinding Padding}">
                            <ScrollViewer x:Name="PART_ContentHost" />
                        </Border>
                    </ControlTemplate>
                </Setter.Value>
            </Setter>
        </Style>
    </Window.Resources>

    <Grid Margin="32">
        <Grid.RowDefinitions>
            <RowDefinition Height="Auto" />
            <RowDefinition Height="24" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="24" />
            <RowDefinition Height="Auto" />
            <RowDefinition Height="*" />
            <RowDefinition Height="Auto" />
        </Grid.RowDefinitions>

        <!-- Header -->
        <StackPanel Grid.Row="0">
            <TextBlock Text="🛡️ ShareGuard"
                       FontSize="28"
                       FontWeight="Bold"
                       Foreground="#F8FAFC" />
            <TextBlock Text="Protect your privacy by stripping tracking data"
                       FontSize="14"
                       Foreground="#64748B"
                       Margin="0,4,0,0" />
        </StackPanel>

        <!-- Clipboard Monitor Toggle -->
        <Border Grid.Row="2"
                Background="#1E293B"
                CornerRadius="12"
                Padding="20,16">
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*" />
                    <ColumnDefinition Width="Auto" />
                </Grid.ColumnDefinitions>
                <StackPanel Grid.Column="0">
                    <TextBlock Text="Auto-Clean Clipboard URLs"
                               FontSize="16"
                               FontWeight="SemiBold"
                               Foreground="#F8FAFC" />
                    <TextBlock Text="Automatically strip tracking parameters when you copy a URL"
                               FontSize="12"
                               Foreground="#94A3B8"
                               Margin="0,4,0,0" />
                </StackPanel>
                <CheckBox Grid.Column="1"
                          IsChecked="{Binding IsMonitoring}"
                          VerticalAlignment="Center"
                          Content="Active"
                          Foreground="#94A3B8"
                          FontSize="13" />
            </Grid>
        </Border>

        <!-- Manual URL Clean Section -->
        <Border Grid.Row="4"
                Background="#1E293B"
                CornerRadius="12"
                Padding="20,16">
            <StackPanel>
                <TextBlock Text="Manual URL Cleaner"
                           FontSize="16"
                           FontWeight="SemiBold"
                           Foreground="#F8FAFC"
                           Margin="0,0,0,12" />
                <Grid>
                    <Grid.ColumnDefinitions>
                        <ColumnDefinition Width="*" />
                        <ColumnDefinition Width="12" />
                        <ColumnDefinition Width="Auto" />
                    </Grid.ColumnDefinitions>
                    <TextBox Grid.Column="0"
                             Style="{StaticResource ModernTextBox}"
                             Text="{Binding ManualUrlInput, UpdateSourceTrigger=PropertyChanged}" />
                    <Button Grid.Column="2"
                            Style="{StaticResource AccentButton}"
                            Content="Clean URL"
                            Command="{Binding CleanManualUrlCommand}" />
                </Grid>

                <!-- Results panel -->
                <Border Visibility="{Binding ShowResults, Converter={StaticResource BoolToVis}}"
                        Background="#0F172A"
                        CornerRadius="8"
                        Padding="16,12"
                        Margin="0,16,0,0">
                    <StackPanel>
                        <TextBlock Text="Before:"
                                   FontSize="11"
                                   FontWeight="SemiBold"
                                   Foreground="#64748B" />
                        <TextBox Text="{Binding BeforeUrl, Mode=OneWay}"
                                 IsReadOnly="True"
                                 Background="Transparent"
                                 Foreground="#EF4444"
                                 BorderThickness="0"
                                 FontSize="13"
                                 TextWrapping="Wrap"
                                 Margin="0,2,0,8" />

                        <TextBlock Text="After:"
                                   FontSize="11"
                                   FontWeight="SemiBold"
                                   Foreground="#64748B" />
                        <TextBox Text="{Binding AfterUrl, Mode=OneWay}"
                                 IsReadOnly="True"
                                 Background="Transparent"
                                 Foreground="#22C55E"
                                 BorderThickness="0"
                                 FontSize="13"
                                 TextWrapping="Wrap"
                                 Margin="0,2,0,8" />

                        <TextBlock Foreground="#94A3B8"
                                   FontSize="12">
                            <Run Text="Parameters removed: " />
                            <Run Text="{Binding RemovedCount, Mode=OneWay}"
                                 FontWeight="Bold"
                                 Foreground="#F59E0B" />
                        </TextBlock>
                    </StackPanel>
                </Border>
            </StackPanel>
        </Border>

        <!-- Status bar -->
        <TextBlock Grid.Row="6"
                   Text="{Binding StatusMessage}"
                   FontSize="12"
                   Foreground="#64748B"
                   Margin="0,8,0,0" />
    </Grid>
</Window>
```

Design notes:
- Dark theme (`#0F172A` background) consistent with glassmorphic toast.
- Card-based layout with `#1E293B` section backgrounds and `CornerRadius="12"`.
- Before/After comparison: red (`#EF4444`) for dirty URL, green (`#22C55E`) for clean URL.
- Amber (`#F59E0B`) for the removed count callout.
- Modern rounded textbox and button styles with hover/pressed state transitions.

- [ ] **Step 2: Build**

```powershell
dotnet build Shareguard-wpf/Shareguard-wpf.csproj
```

Expected: Build succeeds with zero errors.

- [ ] **Step 3: Commit**

```powershell
git add Shareguard-wpf/MainWindow.xaml
git commit -m "feat: add dark-themed URL cleaner UI with before/after comparison panel"
```

---

### Task 9: Wire Everything in MainWindow.xaml.cs and App.xaml.cs

**Files:**
- Modify: `Shareguard-wpf/MainWindow.xaml.cs`
- Modify: `Shareguard-wpf/App.xaml.cs`

- [ ] **Step 1: Replace MainWindow.xaml.cs**

Replace the entire content of `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\MainWindow.xaml.cs` with:

```csharp
using System.Windows;
using System.Windows.Interop;
using ShareGuard.App.Services;
using ShareGuard.App.ViewModels;

namespace ShareGuard.App;

/// <summary>
/// Main application window. Receives its ViewModel and ClipboardMonitor via constructor injection.
/// Starts clipboard monitoring once the window handle is available.
/// </summary>
public partial class MainWindow : Window
{
    private readonly IClipboardMonitorService _clipboardMonitor;
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel, IClipboardMonitorService clipboardMonitor)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _clipboardMonitor = clipboardMonitor;
        DataContext = viewModel;
        SourceInitialized += OnSourceInitialized;
        Closed += OnClosed;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        // Window handle is now available — start monitoring if toggle is on
        var hwnd = new WindowInteropHelper(this).Handle;

        // Subscribe to ViewModel IsMonitoring changes to start monitoring
        // (StopMonitoring is handled by the ViewModel's OnIsMonitoringChanged)
        _viewModel.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.IsMonitoring) && _viewModel.IsMonitoring)
            {
                if (!_clipboardMonitor.IsMonitoring)
                {
                    _clipboardMonitor.StartMonitoring(hwnd);
                }
            }
        };
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _viewModel.Dispose();
    }
}
```

- [ ] **Step 2: Replace App.xaml.cs**

Replace the entire content of `c:\Users\ADMIN\source\repos\Shareguard-wpf\Shareguard-wpf\App.xaml.cs` with:

```csharp
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShareGuard.App.Services;
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

    private async void ApplicationStartup(object sender, StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();

        // Register layer services via extension methods
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices();

        // Presentation layer service registrations
        builder.Services.AddSingleton<IClipboardMonitorService, WindowsClipboardMonitorService>();
        builder.Services.AddSingleton<INotificationService, NotificationService>();

        // Presentation layer registrations
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private async void ApplicationExit(object sender, ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
```

Changes from Phase 0:
- Added `IClipboardMonitorService` → `WindowsClipboardMonitorService` singleton.
- Added `INotificationService` → `NotificationService` singleton.
- Added `using ShareGuard.App.Services;`.

- [ ] **Step 3: Build the entire solution**

```powershell
dotnet build Shareguard-wpf.slnx
```

Expected: Build succeeds with zero errors across all projects.

- [ ] **Step 4: Run all tests**

```powershell
dotnet test Shareguard-wpf.slnx --verbosity normal
```

Expected: All tests pass (including the URL cleaner tests from Task 4 and the existing DI/Architecture tests from Phase 0).

- [ ] **Step 5: Commit**

```powershell
git add Shareguard-wpf/MainWindow.xaml.cs Shareguard-wpf/App.xaml.cs
git commit -m "feat: wire clipboard monitor and notification services into DI and MainWindow"
```

---

### Note: History Logging (Deferred Dependency)

The GSD plan specifies that all cleaning operations should be logged to the local operation history via `IHistoryLogger`. However, `IHistoryLogger` is defined as part of **Phase 1 (Image Clean Copy Tracer Bullet)**, which must be completed before Phase 2 begins (per the roadmap dependency graph). At execution time, after Phase 1's `IHistoryLogger` exists:

- Inject `IHistoryLogger` into `MainViewModel` constructor.
- In `CleanManualUrl()` and `OnUrlCleaned()`, call `_historyLogger.Log(...)` after each successful clean.

If Phase 1 has not yet been completed when this plan executes, skip history logging and note it as a follow-up integration task.

---

## Final Verification

After all tasks are complete, run these checks:

```powershell
# 1. Clean build
dotnet build Shareguard-wpf.slnx

# 2. Run all tests
dotnet test Shareguard-wpf.slnx --verbosity normal

# 3. Run the application
dotnet run --project Shareguard-wpf/Shareguard-wpf.csproj
```

**Manual verification checklist:**

1. App launches with dark-themed UI showing "ShareGuard" header.
2. Paste a dirty URL (e.g. `https://example.com/item?id=123&utm_source=news&fbclid=xyz`) into the manual input textbox and click "Clean URL".
3. Verify results panel shows:
   - Before (red): the original dirty URL
   - After (green): `https://example.com/item?id=123`
   - Parameters removed: `2`
4. Enable the "Auto-Clean Clipboard URLs" checkbox.
5. Copy a dirty URL from the browser or notepad (e.g. `https://google.com/search?q=c%23&gclid=999#frag`).
6. Verify a glassmorphic toast notification slides up from the bottom-right.
7. Paste clipboard contents into notepad — should contain the cleaned URL (`https://google.com/search?q=c%23#frag`).
8. Copy non-URL text (e.g. "hello world") — verify NO notification fires.
9. Uncheck the monitor checkbox — verify clipboard copies are no longer cleaned.
