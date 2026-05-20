using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.App.Services;

/// <summary>
/// Windows-specific clipboard monitor service using Win32 API.
/// </summary>
public sealed class WindowsClipboardMonitorService : IClipboardMonitorService, IDisposable
{
    private readonly IUrlCleanerService _urlCleanerService;
    private HwndSource? _hwndSource;
    private IntPtr _hwnd = IntPtr.Zero;
    private bool _isMonitoring;
    private bool _disposed;
    private string? _lastCleanedUrl;

    public event Action<string, string, int>? UrlCleaned;

    public bool IsMonitoring => _isMonitoring;

    public WindowsClipboardMonitorService(IUrlCleanerService urlCleanerService)
    {
        _urlCleanerService = urlCleanerService ?? throw new ArgumentNullException(nameof(urlCleanerService));
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AddClipboardFormatListener(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);

    private const int WM_CLIPBOARDUPDATE = 0x031D;

    /// <summary>
    /// Starts monitoring the clipboard by hooking into the window messages and registering a Clipboard Format Listener.
    /// </summary>
    /// <param name="hwndSource">The window handle (HWND) to monitor.</param>
    public void StartMonitoring(IntPtr hwndSource)
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(WindowsClipboardMonitorService));
        }

        if (_isMonitoring)
        {
            return;
        }

        if (hwndSource == IntPtr.Zero)
        {
            throw new ArgumentException("Window handle cannot be zero.", nameof(hwndSource));
        }

        _hwnd = hwndSource;
        _hwndSource = HwndSource.FromHwnd(_hwnd);
        if (_hwndSource == null)
        {
            throw new InvalidOperationException("Could not obtain HwndSource from the specified window handle.");
        }

        _hwndSource.AddHook(ClipboardHook);

        if (!AddClipboardFormatListener(_hwnd))
        {
            int error = Marshal.GetLastWin32Error();
            throw new InvalidOperationException($"Failed to add clipboard format listener. Win32 Error Code: {error}");
        }

        _isMonitoring = true;
    }

    /// <summary>
    /// Stops monitoring the clipboard by removing hooks and listeners.
    /// </summary>
    public void StopMonitoring()
    {
        if (!_isMonitoring)
        {
            return;
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(ClipboardHook);
            _hwndSource = null;
        }

        if (_hwnd != IntPtr.Zero)
        {
            RemoveClipboardFormatListener(_hwnd);
            _hwnd = IntPtr.Zero;
        }

        _isMonitoring = false;
    }

    private IntPtr ClipboardHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_CLIPBOARDUPDATE)
        {
            ProcessClipboard();
        }
        return IntPtr.Zero;
    }

    private void ProcessClipboard()
    {
        try
        {
            // System.Windows.Clipboard must be accessed on STA thread (which is guaranteed inside the WPF window message loop hook).
            if (!Clipboard.ContainsText())
            {
                return;
            }

            string text = Clipboard.GetText();
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            string trimmed = text.Trim();

            // Quick check: must look like an HTTP or HTTPS URL to proceed
            if (!trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // Self-triggered write loop prevention
            if (trimmed == _lastCleanedUrl)
            {
                return;
            }

            if (_urlCleanerService.CleanUrl(trimmed, out string cleanUrl, out int removedCount))
            {
                _lastCleanedUrl = cleanUrl;
                Clipboard.SetText(cleanUrl);
                UrlCleaned?.Invoke(trimmed, cleanUrl, removedCount);
            }
        }
        catch (Exception ex)
        {
            // Fail-safe to avoid crashing the hosting process during clipboard access errors.
            System.Diagnostics.Debug.WriteLine($"Clipboard Monitoring error: {ex}");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopMonitoring();
        _disposed = true;
    }
}
