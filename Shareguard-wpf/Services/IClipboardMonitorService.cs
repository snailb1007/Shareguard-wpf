using System;

namespace ShareGuard.App.Services;

/// <summary>
/// Service that monitors the system clipboard for URLs, cleans them, and writes them back.
/// </summary>
public interface IClipboardMonitorService
{
    /// <summary>
    /// Event raised when a URL is successfully cleaned from the clipboard.
    /// </summary>
    event Action<string, string, int>? UrlCleaned;

    /// <summary>
    /// Starts monitoring the clipboard using the specified window handle.
    /// </summary>
    /// <param name="hwndSource">The window handle (HWND) used to register the clipboard listener and hooks.</param>
    void StartMonitoring(IntPtr hwndSource);

    /// <summary>
    /// Stops monitoring the clipboard and removes all hooks and listeners.
    /// </summary>
    void StopMonitoring();

    /// <summary>
    /// Gets a value indicating whether the service is currently monitoring the clipboard.
    /// </summary>
    bool IsMonitoring { get; }
}
