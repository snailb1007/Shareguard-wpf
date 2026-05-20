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
