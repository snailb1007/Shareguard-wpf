using System;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace ShareGuard.App.Services;

/// <summary>
/// Implementation of IHotkeyService using Win32 RegisterHotKey and UnregisterHotKey APIs.
/// </summary>
public sealed class HotkeyService : IHotkeyService
{
    private const int HOTKEY_ID = 9000;
    private const int WM_HOTKEY = 0x0312;

    // Win32 Modifier flags
    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
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
        {
            throw new ObjectDisposedException(nameof(HotkeyService));
        }

        if (hwnd == IntPtr.Zero)
        {
            throw new ArgumentException("Window handle cannot be zero.", nameof(hwnd));
        }

        // Unregister any existing hotkey first
        Unregister();

        if (!TryParseHotkey(hotkeyString, out uint modifiers, out uint vk))
        {
            return false;
        }

        _hwndSource = HwndSource.FromHwnd(hwnd);
        if (_hwndSource == null)
        {
            return false;
        }

        if (!RegisterHotKey(hwnd, HOTKEY_ID, modifiers, vk))
        {
            _hwndSource = null;
            return false;
        }

        _hwnd = hwnd;
        _hwndSource.AddHook(HwndHook);
        _isRegistered = true;
        return true;
    }

    public void Unregister()
    {
        if (!_isRegistered)
        {
            return;
        }

        if (_hwnd != IntPtr.Zero)
        {
            UnregisterHotKey(_hwnd, HOTKEY_ID);
        }

        if (_hwndSource != null)
        {
            _hwndSource.RemoveHook(HwndHook);
            _hwndSource = null;
        }

        _hwnd = IntPtr.Zero;
        _isRegistered = false;
    }

    private IntPtr HwndHook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY && wParam.ToInt32() == HOTKEY_ID)
        {
            HotkeyPressed?.Invoke();
            handled = true;
        }
        return IntPtr.Zero;
    }

    private bool TryParseHotkey(string hotkeyString, out uint modifiers, out uint vk)
    {
        modifiers = 0;
        vk = 0;

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            return false;
        }

        string[] parts = hotkeyString.Split('+', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return false;
        }

        // The last part is the key itself
        string keyStr = parts[^1].Trim();

        // Parse keyStr using KeyInterop or Enum.TryParse
        if (!Enum.TryParse<Key>(keyStr, true, out Key key) || key == Key.None)
        {
            // Handle special key names if needed, e.g. "Esc" -> Key.Escape, etc.
            if (keyStr.Equals("Esc", StringComparison.OrdinalIgnoreCase))
            {
                key = Key.Escape;
            }
            else if (keyStr.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || 
                     keyStr.Equals("Shift", StringComparison.OrdinalIgnoreCase) || 
                     keyStr.Equals("Alt", StringComparison.OrdinalIgnoreCase) || 
                     keyStr.Equals("Win", StringComparison.OrdinalIgnoreCase))
            {
                // If they just entered modifiers and no key, it's invalid
                return false;
            }
            else
            {
                return false;
            }
        }

        try
        {
            vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        }
        catch
        {
            return false;
        }

        if (vk == 0)
        {
            return false;
        }

        // All parts before the last one are modifiers
        for (int i = 0; i < parts.Length - 1; i++)
        {
            string modifierStr = parts[i].Trim();
            if (modifierStr.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || 
                modifierStr.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= MOD_CONTROL;
            }
            else if (modifierStr.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= MOD_SHIFT;
            }
            else if (modifierStr.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= MOD_ALT;
            }
            else if (modifierStr.Equals("Win", StringComparison.OrdinalIgnoreCase) || 
                     modifierStr.Equals("Windows", StringComparison.OrdinalIgnoreCase))
            {
                modifiers |= MOD_WIN;
            }
            else
            {
                // Unknown modifier
                return false;
            }
        }

        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Unregister();
        _disposed = true;
    }
}
