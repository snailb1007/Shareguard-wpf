using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Models;

namespace ShareGuard.App.ViewModels;

public enum AppState
{
    DropZone,
    Processing,
    Results
}

public partial class MainViewModel : ObservableObject
{
    private readonly IImageCleanupService _cleanupService;

    [ObservableProperty]
    private string _title = "ShareGuard";

    [ObservableProperty]
    private AppState _currentState = AppState.DropZone;

    [ObservableProperty]
    private string? _originalFilePath;

    [ObservableProperty]
    private string? _cleanFilePath;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private TimeSpan _elapsed;

    public ObservableCollection<Finding> Findings { get; } = [];

    // Grouped findings for UI display (category → findings)
    public ObservableCollection<FindingGroup> FindingGroups { get; } = [];

    public MainViewModel(IImageCleanupService cleanupService)
    {
        _cleanupService = cleanupService;
    }

    [RelayCommand]
    private async Task CleanImageAsync(string filePath, CancellationToken cancellationToken)
    {
        CurrentState = AppState.Processing;
        OriginalFilePath = filePath;
        ErrorMessage = null;
        Findings.Clear();
        FindingGroups.Clear();

        var result = await Task.Run(() => _cleanupService.CleanAsync(filePath, cancellationToken), cancellationToken);

        if (result.IsSuccess)
        {
            CleanFilePath = result.CleanFilePath;
            Elapsed = result.Elapsed;

            foreach (var finding in result.Findings)
            {
                Findings.Add(finding);
            }

            // Group findings by category
            var groups = result.Findings
                .GroupBy(f => f.Category)
                .Select(g => new FindingGroup(g.Key, g.ToList()))
                .ToList();

            foreach (var group in groups)
            {
                FindingGroups.Add(group);
            }

            CurrentState = AppState.Results;
        }
        else
        {
            ErrorMessage = result.ErrorMessage;
            CurrentState = AppState.DropZone;
        }
    }

    [RelayCommand]
    private void BrowseFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select an image to clean",
            Filter = "Image files (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp|All files (*.*)|*.*",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
            CleanImageCommand.Execute(dialog.FileName);
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        if (CleanFilePath is not null && File.Exists(CleanFilePath))
        {
            Process.Start("explorer.exe", $"/select,\"{CleanFilePath}\"");
        }
    }

    [RelayCommand]
    private void GoBack()
    {
        CurrentState = AppState.DropZone;
        OriginalFilePath = null;
        CleanFilePath = null;
        ErrorMessage = null;
        Findings.Clear();
        FindingGroups.Clear();
    }
}

/// <summary>
/// Groups findings by their category for UI display in expandable sections.
/// </summary>
public sealed record FindingGroup(string Category, IReadOnlyList<Finding> Items)
{
    public int Count => Items.Count;
    public string DisplayHeader => $"{Category} ({Count})";
}
