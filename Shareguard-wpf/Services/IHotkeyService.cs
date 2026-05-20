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
