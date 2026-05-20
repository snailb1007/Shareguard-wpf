using ShareGuard.Domain.Models;

namespace ShareGuard.Application.Services;

/// <summary>
/// General file metadata cleaning service orchestrating file type routing and verification.
/// </summary>
public interface IFileCleanupService
{
    /// <summary>
    /// Routes the file to the correct IFileStripper, strips metadata to a clean copy,
    /// runs the MetadataVerifier safety check, and writes the event to the history DB.
    /// </summary>
    Task<StripResult> CleanFileAsync(string sourcePath, CancellationToken cancellationToken = default);
}
