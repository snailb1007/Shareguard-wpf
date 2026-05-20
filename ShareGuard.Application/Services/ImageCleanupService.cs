using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ShareGuard.Application.Commands;
using ShareGuard.Application.Interfaces;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;

namespace ShareGuard.Application.Services;

/// <summary>
/// Coordinates image cleanup: pre-flight checks, metadata stripping via
/// <see cref="IImageCleaner"/>, collision-safe naming, and history logging.
/// </summary>
public sealed class ImageCleanupService : IImageCleanupService
{
    private static readonly HashSet<string> SupportedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };

    private readonly IImageCleaner _cleaner;
    private readonly IHistoryService _historyService;
    private readonly ILogger<ImageCleanupService> _logger;

    public ImageCleanupService(IImageCleaner cleaner, IHistoryService historyService, ILogger<ImageCleanupService> logger)
    {
        _cleaner = cleaner;
        _historyService = historyService;
        _logger = logger;
    }

    // Overload for tests that don't need a logger
    internal ImageCleanupService(IImageCleaner cleaner, IHistoryService historyService)
        : this(cleaner, historyService, Microsoft.Extensions.Logging.Abstractions.NullLogger<ImageCleanupService>.Instance)
    {
    }

    public static bool IsValidExtension(string extension)
        => SupportedExtensions.Contains(extension);

    /// <summary>
    /// Generates a collision-safe clean file path.
    /// "photo.jpg" → "photo.clean.jpg" → "photo.clean (1).jpg" → "photo.clean (2).jpg"
    /// </summary>
    public static string GenerateCleanPath(string originalPath)
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

    public async Task<StripResult> CleanAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new ArgumentException("Source path cannot be null or empty.", nameof(filePath));
        }

        // Pre-flight: validate extension
        var extension = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
        if (!IsValidExtension(extension))
        {
            var failResult = StripResult.Failure($"Unsupported file type: '{extension}'. Supported formats: JPEG, PNG, WEBP.");
            await LogToDbAsync(filePath, string.Empty, 0, false, failResult.ErrorMessage, cancellationToken);
            return failResult;
        }

        // Pre-flight: validate file exists
        if (!File.Exists(filePath))
        {
            var failResult = StripResult.Failure($"File not found: '{filePath}'.");
            await LogToDbAsync(filePath, string.Empty, 0, false, failResult.ErrorMessage, cancellationToken);
            return failResult;
        }

        string destPath = string.Empty;
        try
        {
            var stopwatch = Stopwatch.StartNew();

            destPath = GenerateCleanPath(filePath);
            var findings = await _cleaner.CleanImageAsync(filePath, destPath, cancellationToken);

            stopwatch.Stop();
            var elapsed = stopwatch.Elapsed;

            // Log successful clean to database
            await LogToDbAsync(filePath, destPath, findings.Count, true, null, cancellationToken);

            return StripResult.Success(destPath, findings, elapsed);
        }
        catch (Exception ex)
        {
            try
            {
                if (!string.IsNullOrEmpty(destPath) && File.Exists(destPath))
                {
                    File.Delete(destPath);
                }
            }
            catch
            {
                // ignore
            }

            _logger.LogError(ex, "Failed to clean image: {FilePath}", filePath);
            var errorResult = StripResult.Failure($"Failed to process image. {ex.Message}");
            await LogToDbAsync(filePath, string.Empty, 0, false, ex.Message, cancellationToken);
            return errorResult;
        }
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
