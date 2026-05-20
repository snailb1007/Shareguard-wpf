using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ShareGuard.Application.Services;

namespace ShareGuard.Application.Services;

public class MultiFileProcessorService : IMultiFileProcessorService
{
    private readonly IFileCleanupService _fileCleanupService;

    public MultiFileProcessorService(IFileCleanupService fileCleanupService)
    {
        _fileCleanupService = fileCleanupService;
    }

    public async Task ProcessFilesAsync(
        IEnumerable<string> filePaths,
        IProgress<ProcessingStatus> progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filePaths);

        var filesList = filePaths.ToList();
        if (filesList.Count == 0) return;

        int totalCount = filesList.Count;
        int currentProcessedCount = 0;

        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken
        };

        // Leverage modern Parallel.ForEachAsync
        await Parallel.ForEachAsync(filesList, options, async (filePath, ct) =>
        {
            bool success = false;
            string cleanPath = string.Empty;
            int findingsCount = 0;
            string? errorMessage = null;

            try
            {
                var result = await _fileCleanupService.CleanFileAsync(filePath, ct);
                success = result.IsSuccess;
                cleanPath = result.CleanFilePath ?? string.Empty;
                findingsCount = result.Findings.Count;
                errorMessage = result.ErrorMessage;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
            }

            int currentCount = Interlocked.Increment(ref currentProcessedCount);

            // Thread-safe progress reporting. Progress<T> handles UI-thread marshaling.
            progress?.Report(new ProcessingStatus(
                currentCount,
                totalCount,
                filePath,
                success,
                cleanPath,
                findingsCount,
                errorMessage));
        });
    }
}
