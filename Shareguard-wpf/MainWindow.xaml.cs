using System;
using System.ComponentModel;
using System.Windows;
using ShareGuard.App.Services;
using ShareGuard.App.ViewModels;
using Wpf.Ui.Controls;

namespace ShareGuard.App;

/// <summary>
/// Main application window. Handles drag-and-drop events.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;
    private readonly IClipboardMonitorService _clipboardMonitorService;

    public MainWindow(MainViewModel viewModel, IClipboardMonitorService clipboardMonitorService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _clipboardMonitorService = clipboardMonitorService;

        Loaded += async (s, e) => await _viewModel.LoadedCommand.ExecuteAsync(null);
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

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        _clipboardMonitorService.StartMonitoring(hwnd);
    }

    protected override void OnClosed(EventArgs e)
    {
        _clipboardMonitorService.StopMonitoring();
        base.OnClosed(e);
    }
}
