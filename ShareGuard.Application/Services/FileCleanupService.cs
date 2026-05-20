using System.Diagnostics;
using ShareGuard.Application.Commands;
using ShareGuard.Application.Interfaces;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using Microsoft.Extensions.Logging;

namespace ShareGuard.Application.Services;

public sealed class FileCleanupService : IFileCleanupService
{
    private readonly IEnumerable<IFileStripper> _strippers;
    private readonly IHistoryService _historyService;
    private readonly ILogger<FileCleanupService> _logger;

    public FileCleanupService(
        IEnumerable<IFileStripper> strippers,
        IHistoryService historyService,
        ILogger<FileCleanupService> logger)
    {
        _strippers = strippers;
        _historyService = historyService;
        _logger = logger;
    }

    // Test-friendly constructor
    internal FileCleanupService(
        IEnumerable<IFileStripper> strippers,
        IHistoryService historyService)
        : this(strippers, historyService, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileCleanupService>.Instance)
    {
    }

    public async Task<StripResult> CleanFileAsync(string sourcePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return StripResult.Failure("Source path cannot be empty.");
        }

        if (!File.Exists(sourcePath))
        {
            var failResult = StripResult.Failure($"File not found: '{sourcePath}'.");
            await LogToDbAsync(sourcePath, string.Empty, 0, false, failResult.ErrorMessage, cancellationToken);
            return failResult;
        }

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        var stripper = _strippers.FirstOrDefault(s => s.CanHandle(extension));

        if (stripper == null)
        {
            var errMessage = $"Unsupported file type: '{extension}'.";
            var failResult = StripResult.Failure(errMessage);
            await LogToDbAsync(sourcePath, string.Empty, 0, false, errMessage, cancellationToken);
            return failResult;
        }

        // Generate collision-safe clean path (reuse pattern from ImageCleanupService)
        var cleanPath = GenerateCleanPath(sourcePath);

        try
        {
            var stopwatch = Stopwatch.StartNew();

            var findings = await stripper.StripMetadataAsync(sourcePath, cleanPath, cancellationToken);

            // Independent verification gate
            bool verified = MetadataVerifier.VerifyNoSensitiveMetadata(cleanPath);
            if (!verified)
            {
                SafeDeleteFile(cleanPath);
                var verifyErr = "Verification failed: sensitive metadata remains in the clean copy.";
                var failResult = StripResult.Failure(verifyErr);
                await LogToDbAsync(sourcePath, string.Empty, 0, false, verifyErr, cancellationToken);
                return failResult;
            }

            stopwatch.Stop();

            await LogToDbAsync(sourcePath, cleanPath, findings.Count, true, null, cancellationToken);
            return StripResult.Success(cleanPath, findings, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            SafeDeleteFile(cleanPath);
            _logger.LogError(ex, "Failed to clean file: {FilePath}", sourcePath);
            var failResult = StripResult.Failure($"Failed to process file. {ex.Message}");
            await LogToDbAsync(sourcePath, string.Empty, 0, false, ex.Message, cancellationToken);
            return failResult;
        }
    }

    internal static string GenerateCleanPath(string originalPath)
    {
        var dir = Path.GetDirectoryName(originalPath) ?? string.Empty;
        var nameWithoutExt = Path.GetFileNameWithoutExtension(originalPath);
        var ext = Path.GetExtension(originalPath);

        var candidate = Path.Combine(dir, $"{nameWithoutExt}.clean{ext}");
        if (!File.Exists(candidate))
            return candidate;

        var counter = 1;
        while (true)
        {
            candidate = Path.Combine(dir, $"{nameWithoutExt}.clean ({counter}){ext}");
            if (!File.Exists(candidate))
                return candidate;
            counter++;
        }
    }

    private static void SafeDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch { /* best effort */ }
    }

    private async Task LogToDbAsync(string src, string dest, int count, bool ok, string? err, CancellationToken ct)
    {
        try
        {
            var command = new LogHistoryCommand(
                FileName: Path.GetFileName(src),
                OriginalPath: src,
                CleanPath: dest,
                FindingsCount: count,
                IsSuccess: ok,
                ErrorMessage: err
            );
            await _historyService.LogHistoryAsync(command, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to write history log entry to database");
        }
    }
}
