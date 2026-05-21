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

    private string? _registeredHotkey;

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
        if (MainTabControl != null && SettingsTabItem != null)
        {
            MainTabControl.SelectedItem = SettingsTabItem;
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

        // Wire hotkey action and listen for settings changes
        _hotkeyService.HotkeyPressed += OnHotkeyPressed;
        _settingsViewModel.SettingsChanged += OnSettingsChanged;

        // Register initial hotkey
        RegisterHotkey(_settingsViewModel.GlobalHotkey);
    }

    private void RegisterHotkey(string hotkeyString)
    {
        if (_registeredHotkey == hotkeyString)
            return;

        _hotkeyService.Unregister();
        var hwnd = new WindowInteropHelper(this).Handle;
        bool success = _hotkeyService.Register(hwnd, hotkeyString);
        _settingsViewModel.SetHotkeyStatus(success, hotkeyString);

        if (success)
        {
            _registeredHotkey = hotkeyString;
        }
        else
        {
            _registeredHotkey = null;
            _notificationService.ShowNotification(
                "Hotkey Registration Failed", 
                $"Could not register hotkey '{hotkeyString}'. It might be in use by another application.");
        }
    }

    private void OnSettingsChanged(ShareGuard.Application.Models.AppSettings settings)
    {
        Dispatcher.BeginInvoke(() =>
        {
            RegisterHotkey(settings.GlobalHotkey);
        });
    }

    private void OnHotkeyPressed()
    {
        _ = _viewModel.CleanClipboardFromTrayAsync();
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
        _settingsViewModel.SettingsChanged -= OnSettingsChanged;
        _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
        _hotkeyService.Unregister();
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
