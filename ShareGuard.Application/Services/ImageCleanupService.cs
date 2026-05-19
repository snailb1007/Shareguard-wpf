using System.Diagnostics;
using Microsoft.Extensions.Logging;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using ShareGuard.Infrastructure.Services;

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
    private readonly LocalHistoryLogger _historyLogger;
    private readonly ILogger<ImageCleanupService> _logger;

    public ImageCleanupService(IImageCleaner cleaner, LocalHistoryLogger historyLogger, ILogger<ImageCleanupService> logger)
    {
        _cleaner = cleaner;
        _historyLogger = historyLogger;
        _logger = logger;
    }

    // Overload for tests that don't need a logger
    internal ImageCleanupService(IImageCleaner cleaner, LocalHistoryLogger historyLogger)
        : this(cleaner, historyLogger, Microsoft.Extensions.Logging.Abstractions.NullLogger<ImageCleanupService>.Instance)
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
        var dir = Path.GetDirectoryName(originalPath)!;
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
        // Pre-flight: validate extension
        var extension = Path.GetExtension(filePath);
        if (!IsValidExtension(extension))
        {
            return StripResult.Failure($"Unsupported file type: '{extension}'. Supported formats: JPEG, PNG, WEBP.");
        }

        // Pre-flight: validate file exists
        if (!File.Exists(filePath))
        {
            return StripResult.Failure($"File not found: '{filePath}'.");
        }

        try
        {
            var stopwatch = Stopwatch.StartNew();

            var destPath = GenerateCleanPath(filePath);
            var findings = await _cleaner.CleanImageAsync(filePath, destPath, cancellationToken);

            stopwatch.Stop();
            var elapsed = stopwatch.Elapsed;

            // Log to local history (fire-and-forget is acceptable for logging)
            try
            {
                await _historyLogger.LogAsync(filePath, destPath, findings.Count, elapsed, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to write history log entry");
            }

            return StripResult.Success(destPath, findings, elapsed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean image: {FilePath}", filePath);
            return StripResult.Failure($"Failed to process image. {ex.Message}");
        }
    }
}
