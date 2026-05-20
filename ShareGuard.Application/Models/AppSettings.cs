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
