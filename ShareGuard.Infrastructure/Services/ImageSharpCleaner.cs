using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Metadata.Profiles.Exif;

namespace ShareGuard.Infrastructure.Services;

/// <summary>
/// Strips EXIF, IPTC, and XMP metadata profiles from images using SixLabors.ImageSharp.
/// Populates a categorized list of findings for UI display.
/// </summary>
public sealed class ImageSharpCleaner : IImageCleaner
{
    // Maps EXIF tag groups to human-readable categories
    private static readonly Dictionary<ExifTag, string> GpsTags = new()
    {
        { ExifTag.GPSLatitude, "GPS/Location" },
        { ExifTag.GPSLongitude, "GPS/Location" },
        { ExifTag.GPSAltitude, "GPS/Location" },
        { ExifTag.GPSLatitudeRef, "GPS/Location" },
        { ExifTag.GPSLongitudeRef, "GPS/Location" },
        { ExifTag.GPSTimestamp, "GPS/Location" },
        { ExifTag.GPSDateStamp, "GPS/Location" },
    };

    private static readonly HashSet<ExifTag> CameraTags =
    [
        ExifTag.Make,
        ExifTag.Model,
        ExifTag.LensModel,
        ExifTag.LensMake,
        ExifTag.LensSerialNumber,
        ExifTag.FocalLength,
        ExifTag.FNumber,
        ExifTag.ISOSpeedRatings,
        ExifTag.ExposureTime,
    ];

    private static readonly HashSet<ExifTag> DateTags =
    [
        ExifTag.DateTime,
        ExifTag.DateTimeOriginal,
        ExifTag.DateTimeDigitized,
    ];

    public async Task<List<Finding>> CleanImageAsync(string sourcePath, string destPath, CancellationToken cancellationToken = default)
    {
        using var image = await Image.LoadAsync(sourcePath, cancellationToken);
        var findings = new List<Finding>();

        // Extract EXIF findings before stripping
        if (image.Metadata.ExifProfile is { } exif)
        {
            foreach (var value in exif.Values)
            {
                var category = CategorizeExifTag(value.Tag);
                findings.Add(new Finding(category, value.Tag.ToString(), FormatExifValue(value)));
            }
            image.Metadata.ExifProfile = null;
        }

        // Extract IPTC findings before stripping
        if (image.Metadata.IptcProfile is { } iptc)
        {
            foreach (var value in iptc.Values)
            {
                findings.Add(new Finding("Software/XMP", $"IPTC:{value.Tag}", value.Value));
            }
            image.Metadata.IptcProfile = null;
        }

        // Extract XMP findings before stripping
        if (image.Metadata.XmpProfile is not null)
        {
            findings.Add(new Finding("Software/XMP", "XMP Profile", "XMP metadata document"));
            image.Metadata.XmpProfile = null;
        }

        await image.SaveAsync(destPath, cancellationToken);
        return findings;
    }

    private static string CategorizeExifTag(ExifTag tag)
    {
        if (GpsTags.ContainsKey(tag)) return "GPS/Location";
        if (CameraTags.Contains(tag)) return "Camera/Device";
        if (DateTags.Contains(tag)) return "Date & Time";
        return "Software/XMP";
    }

    private static string FormatExifValue(IExifValue value)
    {
        var raw = value.GetValue();
        return raw?.ToString() ?? "(empty)";
    }
}
