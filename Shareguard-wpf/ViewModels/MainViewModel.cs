using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ShareGuard.App.Services;
using ShareGuard.Application.Commands;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Queries;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;
using System.Collections.ObjectModel;
using System.IO;

namespace ShareGuard.App.ViewModels;

public partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IUrlCleanerService _urlCleaner;
    private readonly IClipboardMonitorService _clipboardMonitor;
    private readonly INotificationService _notification;
    private readonly IHistoryService _historyService;
    private readonly IMultiFileProcessorService _multiFileProcessor;

    [ObservableProperty]
    private string _title = "ShareGuard";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    // URL Cleaner properties (Phase 2)
    [ObservableProperty]
    private string _manualUrlInput = string.Empty;

    [ObservableProperty]
    private string _beforeUrl = string.Empty;

    [ObservableProperty]
    private string _afterUrl = string.Empty;

    [ObservableProperty]
    private int _removedCount;

    [ObservableProperty]
    private bool _showResults;

    [ObservableProperty]
    private bool _isMonitoring;

    // Batch File Processing properties (Phase 3)
    [ObservableProperty]
    private bool _isBatchProcessing;

    [ObservableProperty]
    private int _totalFilesToProcess;

    [ObservableProperty]
    private int _currentFilesProcessed;

    [ObservableProperty]
    private double _batchProgressPercentage;

    [ObservableProperty]
    private string _currentFileName = string.Empty;

    [ObservableProperty]
    private bool _showBatchResults;

    public ObservableCollection<BatchItemResultViewModel> CurrentBatchResults { get; } = [];

    // History Database properties (Phase 3)
    public ObservableCollection<HistoryEventViewModel> HistoryEvents { get; } = [];

    [ObservableProperty]
    private int _currentHistoryPage = 1;

    [ObservableProperty]
    private int _totalHistoryPages = 1;

    private const int HistoryPageSize = 10;



    public MainViewModel(
        IUrlCleanerService urlCleaner,
        IClipboardMonitorService clipboardMonitor,
        INotificationService notification,
        IHistoryService historyService,
        IMultiFileProcessorService multiFileProcessor)
    {
        _urlCleaner = urlCleaner;
        _clipboardMonitor = clipboardMonitor;
        _notification = notification;
        _historyService = historyService;
        _multiFileProcessor = multiFileProcessor;

        _clipboardMonitor.UrlCleaned += OnUrlCleaned;
    }

    [RelayCommand]
    private async Task Loaded()
    {
        StatusMessage = "ShareGuard is ready to protect your privacy.";
        await LoadHistoryAsync();
    }

    // Manual URL Cleaning Command
    [RelayCommand]
    private async Task CleanManualUrl()
    {
        if (string.IsNullOrWhiteSpace(ManualUrlInput))
        {
            StatusMessage = "Please enter a URL to clean.";
            return;
        }

        if (_urlCleaner.CleanUrl(ManualUrlInput, out var cleanUrl, out var removed))
        {
            BeforeUrl = ManualUrlInput;
            AfterUrl = cleanUrl;
            RemovedCount = removed;
            ShowResults = true;
            StatusMessage = $"Cleaned! {removed} tracking parameter(s) removed.";

            // Save URL cleaning operation to SQLite database
            try
            {
                await _historyService.LogHistoryAsync(new LogHistoryCommand(
                    FileName: "Manual URL input",
                    OriginalPath: ManualUrlInput,
                    CleanPath: cleanUrl,
                    FindingsCount: removed,
                    IsSuccess: true
                ));
                await LoadHistoryAsync();
            }
            catch (Exception ex)
            {
                StatusMessage = $"Cleaned! (Failed to write history: {ex.Message})";
            }
        }
        else
        {
            BeforeUrl = ManualUrlInput;
            AfterUrl = ManualUrlInput;
            RemovedCount = 0;
            ShowResults = true;
            StatusMessage = "URL is already clean — no tracking parameters found.";
        }
    }

    // Batch File Processing Command
    [RelayCommand]
    private async Task ProcessFiles(IEnumerable<string> filePaths)
    {
        if (IsBatchProcessing) return;

        var pathList = filePaths.ToList();
        if (pathList.Count == 0) return;

        IsBatchProcessing = true;
        ShowBatchResults = false;
        CurrentBatchResults.Clear();
        BatchProgressPercentage = 0;
        CurrentFilesProcessed = 0;
        TotalFilesToProcess = pathList.Count;
        StatusMessage = $"Processing {TotalFilesToProcess} file(s)...";

        var progress = new Progress<ProcessingStatus>(status =>
        {
            CurrentFilesProcessed = status.CurrentCount;
            BatchProgressPercentage = ((double)status.CurrentCount / status.TotalCount) * 100;
            CurrentFileName = Path.GetFileName(status.FilePath);
            StatusMessage = $"Processing file {status.CurrentCount} of {status.TotalCount}...";

            CurrentBatchResults.Add(new BatchItemResultViewModel
            {
                FileName = Path.GetFileName(status.FilePath),
                OriginalPath = status.FilePath,
                CleanPath = status.CleanPath,
                FindingsCount = status.FindingsCount,
                Success = status.Success
            });
        });

        try
        {
            await _multiFileProcessor.ProcessFilesAsync(pathList, progress);
            StatusMessage = $"Completed batch cleaning of {TotalFilesToProcess} file(s).";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Batch processing failed: {ex.Message}";
        }
        finally
        {
            IsBatchProcessing = false;
            ShowBatchResults = true;
            await LoadHistoryAsync(); // Reload SQL history
        }
    }

    // Opens file browser to select multiple files
    [RelayCommand]
    private async Task BrowseFiles()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Multiselect = true,
            Filter = "Image Files (*.jpg;*.jpeg;*.png;*.webp)|*.jpg;*.jpeg;*.png;*.webp"
        };

        if (dialog.ShowDialog() == true)
        {
            await ProcessFiles(dialog.FileNames);
        }
    }

    [RelayCommand]
    private void ClearBatchResults()
    {
        ShowBatchResults = false;
        CurrentBatchResults.Clear();
        StatusMessage = "Ready";
    }

    // Database History Pagination Commands
    [RelayCommand]
    private async Task LoadHistoryAsync()
    {
        try
        {
            int totalCount = await _historyService.GetTotalCountAsync();
            TotalHistoryPages = Math.Max(1, (int)Math.Ceiling((double)totalCount / HistoryPageSize));

            if (CurrentHistoryPage > TotalHistoryPages)
            {
                CurrentHistoryPage = TotalHistoryPages;
            }

            var query = new GetHistoryQuery(CurrentHistoryPage, HistoryPageSize);
            var items = await _historyService.GetHistoryAsync(query);

            HistoryEvents.Clear();
            foreach (var item in items)
            {
                HistoryEvents.Add(new HistoryEventViewModel
                {
                    Id = item.Id,
                    FileName = item.FileName,
                    OriginalPath = item.OriginalPath,
                    CleanPath = item.CleanPath,
                    ProcessedAt = item.ProcessedAt.ToLocalTime().ToString("g"),
                    FindingsCount = item.FindingsCount,
                    IsSuccess = item.IsSuccess
                });
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load history: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NextHistoryPage()
    {
        if (CurrentHistoryPage < TotalHistoryPages)
        {
            CurrentHistoryPage++;
            await LoadHistoryAsync();
        }
    }

    [RelayCommand]
    private async Task PrevHistoryPage()
    {
        if (CurrentHistoryPage > 1)
        {
            CurrentHistoryPage--;
            await LoadHistoryAsync();
        }
    }

    [RelayCommand]
    private async Task ClearHistory()
    {
        try
        {
            await _historyService.ClearHistoryAsync();
            CurrentHistoryPage = 1;
            await LoadHistoryAsync();
            StatusMessage = "History logs cleared successfully.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to clear history: {ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenFileFolder(string? path)
    {
        if (string.IsNullOrEmpty(path)) return;

        if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || 
            path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to open URL: {ex.Message}";
            }
            return;
        }

        if (!File.Exists(path)) return;
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{path}\"");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open directory: {ex.Message}";
        }
    }

    partial void OnIsMonitoringChanged(bool value)
    {
        StatusMessage = value 
            ? "Clipboard monitoring active — copy a URL to auto-clean it." 
            : "Clipboard monitoring paused.";
    }

    private async void OnUrlCleaned(string before, string after, int count)
    {
        if (!IsMonitoring) return;

        try
        {
            // Log to database on background thread first
            await _historyService.LogHistoryAsync(new LogHistoryCommand(
                FileName: "Clipboard URL",
                OriginalPath: before,
                CleanPath: after,
                FindingsCount: count,
                IsSuccess: true
            ));

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                // Marshal to UI thread and await completion of the task
                await dispatcher.Invoke(async () =>
                {
                    BeforeUrl = before;
                    AfterUrl = after;
                    RemovedCount = count;
                    ShowResults = true;
                    StatusMessage = $"Auto-cleaned! {count} tracking parameter(s) stripped from clipboard.";
                    await LoadHistoryAsync();
                });
            }
            else
            {
                // Fallback when dispatcher is null (e.g. running in unit tests)
                BeforeUrl = before;
                AfterUrl = after;
                RemovedCount = count;
                ShowResults = true;
                StatusMessage = $"Auto-cleaned! {count} tracking parameter(s) stripped from clipboard.";
                await LoadHistoryAsync();
            }

            _notification.ShowNotification("URL Cleaned", $"Removed {count} tracking parameter(s)");
        }
        catch (Exception ex)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                dispatcher.Invoke(() =>
                {
                    BeforeUrl = before;
                    AfterUrl = after;
                    RemovedCount = count;
                    ShowResults = true;
                    StatusMessage = $"Auto-cleaned! (History logging error: {ex.Message})";
                });
            }
            else
            {
                BeforeUrl = before;
                AfterUrl = after;
                RemovedCount = count;
                ShowResults = true;
                StatusMessage = $"Auto-cleaned! (History logging error: {ex.Message})";
            }
        }
    }



    public void Dispose()
    {
        _clipboardMonitor.UrlCleaned -= OnUrlCleaned;
        GC.SuppressFinalize(this);
    }
}

public class BatchItemResultViewModel
{
    public string FileName { get; init; } = string.Empty;
    public string OriginalPath { get; init; } = string.Empty;
    public string CleanPath { get; init; } = string.Empty;
    public int FindingsCount { get; init; }
    public bool Success { get; init; }
    public string StatusText => Success ? "Cleaned" : "Failed";
    public string StatusColor => Success ? "#22C55E" : "#EF4444";
}

public class HistoryEventViewModel
{
    public Guid Id { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string OriginalPath { get; init; } = string.Empty;
    public string CleanPath { get; init; } = string.Empty;
    public string ProcessedAt { get; init; } = string.Empty;
    public int FindingsCount { get; init; }
    public bool IsSuccess { get; init; }
    public string StatusText => IsSuccess ? "Cleaned" : "Failed";
    public string StatusColor => IsSuccess ? "#22C55E" : "#EF4444";
}


