using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;

namespace ShareGuard.Infrastructure.Services;

/// <summary>
/// Implements IFileStripper for standard image formats, delegating to the existing IImageCleaner.
/// </summary>
public sealed class ImageSharpStripper : IFileStripper
{
    private readonly IImageCleaner _imageCleaner;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".webp"
    };

    public ImageSharpStripper(IImageCleaner imageCleaner)
    {
        _imageCleaner = imageCleaner;
    }

    public bool CanHandle(string extension)
    {
        return SupportedExtensions.Contains(extension);
    }

    public Task<List<Finding>> StripMetadataAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        return _imageCleaner.CleanImageAsync(sourcePath, destPath, cancellationToken);
    }
}
