using System;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareGuard.App.Services;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Models;

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

        // Populate backing fields from loaded settings directly to avoid triggering OnChanged methods during initialization
        _isClipboardMonitorEnabled = _currentSettings.IsClipboardMonitorEnabled;
        _showCleanNotifications = _currentSettings.ShowCleanNotifications;
        _globalHotkey = _currentSettings.GlobalHotkey;
        _useOriginalDirectory = _currentSettings.UseOriginalDirectory;
        _customOutputDirectory = _currentSettings.CustomOutputDirectory;
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
        try
        {
            _settingsService.Save(_currentSettings);
            SettingsChanged?.Invoke(_currentSettings);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error saving settings: {ex.Message}";
        }
    }
}
