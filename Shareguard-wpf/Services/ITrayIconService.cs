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
