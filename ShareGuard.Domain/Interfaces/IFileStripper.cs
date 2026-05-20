using ShareGuard.Domain.Models;

namespace ShareGuard.Domain.Interfaces;

/// <summary>
/// Contract for stripping metadata profiles from specialized file types.
/// Implementations reside in the Infrastructure layer.
/// </summary>
public interface IFileStripper
{
    /// <summary>
    /// Checks if this stripper is capable of handling the specified file extension.
    /// </summary>
    bool CanHandle(string extension);

    /// <summary>
    /// Strips metadata from the file at <paramref name="sourcePath"/>, saving the clean
    /// copy to <paramref name="destPath"/>, and returns a list of stripped findings.
    /// </summary>
    Task<List<Finding>> StripMetadataAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default);
}
