namespace ShareGuard.Application.Services;

/// <summary>
/// Status update containing information about a finished file inside a batch.
/// </summary>
public record ProcessingStatus(
    int CurrentCount,
    int TotalCount,
    string FilePath,
    bool Success,
    string CleanPath,
    int FindingsCount,
    string? ErrorMessage = null);

/// <summary>
/// Coordinates batch operations by cleaning multiple files in parallel.
/// </summary>
public interface IMultiFileProcessorService
{
    /// <summary>
    /// Strips metadata from multiple files concurrently using Parallel.ForEachAsync.
    /// Reports progress safely to the calling thread.
    /// </summary>
    Task ProcessFilesAsync(
        IEnumerable<string> filePaths,
        IProgress<ProcessingStatus> progress,
        CancellationToken cancellationToken = default);
}
