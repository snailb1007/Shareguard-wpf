using ShareGuard.Domain.Models;

namespace ShareGuard.Application.Services;

/// <summary>
/// Orchestrates the complete image cleanup workflow:
/// pre-flight validation → metadata stripping → history logging.
/// </summary>
public interface IImageCleanupService
{
    /// <summary>
    /// Cleans the image at the given path, producing a clean copy
    /// with all privacy-leaking metadata stripped.
    /// </summary>
    Task<StripResult> CleanAsync(string filePath, CancellationToken cancellationToken = default);
}
