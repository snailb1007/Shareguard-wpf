using ShareGuard.Domain.Models;

namespace ShareGuard.Domain.Interfaces;

/// <summary>
/// Contract for cleaning (stripping) privacy-leaking metadata from an image file.
/// Implementations live in the Infrastructure layer.
/// </summary>
public interface IImageCleaner
{
    /// <summary>
    /// Reads the image at <paramref name="sourcePath"/>, strips all metadata profiles,
    /// saves the clean copy to <paramref name="destPath"/>, and returns the list of findings.
    /// </summary>
    Task<List<Finding>> CleanImageAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default);
}
