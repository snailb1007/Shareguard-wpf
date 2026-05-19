using System.ComponentModel;
using System.Windows;
using Wpf.Ui.Controls;
using ShareGuard.App.ViewModels;

namespace ShareGuard.App;

/// <summary>
/// Main application window. Handles drag-and-drop events and state-based panel visibility.
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly MainViewModel _viewModel;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;

        // Listen for state changes to toggle panel visibility
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        UpdatePanelVisibility(_viewModel.CurrentState);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.CurrentState))
        {
            UpdatePanelVisibility(_viewModel.CurrentState);
        }
        else if (e.PropertyName == nameof(MainViewModel.ErrorMessage))
        {
            UpdateErrorBanner();
        }
        else if (e.PropertyName == nameof(MainViewModel.OriginalFilePath))
        {
            OriginalPathText.Text = _viewModel.OriginalFilePath ?? string.Empty;
        }
        else if (e.PropertyName == nameof(MainViewModel.CleanFilePath))
        {
            CleanPathText.Text = _viewModel.CleanFilePath ?? string.Empty;
        }
        else if (e.PropertyName == nameof(MainViewModel.Elapsed))
        {
            ElapsedText.Text = $"Completed in {_viewModel.Elapsed.TotalMilliseconds:N0}ms";
        }
    }

    private void UpdatePanelVisibility(AppState state)
    {
        DropZonePanel.Visibility = state == AppState.DropZone ? Visibility.Visible : Visibility.Collapsed;
        ProcessingPanel.Visibility = state == AppState.Processing ? Visibility.Visible : Visibility.Collapsed;
        ResultsPanel.Visibility = state == AppState.Results ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateErrorBanner()
    {
        if (!string.IsNullOrEmpty(_viewModel.ErrorMessage))
        {
            ErrorText.Text = _viewModel.ErrorMessage;
            ErrorBanner.Visibility = Visibility.Visible;
        }
        else
        {
            ErrorBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void Window_DragOver(object sender, DragEventArgs e)
    {
        if (_viewModel.CurrentState != AppState.DropZone)
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
        if (_viewModel.CurrentState != AppState.DropZone)
            return;

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            if (files.Length > 0)
            {
                _viewModel.CleanImageCommand.Execute(files[0]);
            }
        }
        e.Handled = true;
    }
}
