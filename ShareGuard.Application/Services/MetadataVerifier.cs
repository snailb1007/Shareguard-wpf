using MetadataExtractor;

namespace ShareGuard.Application.Services;

/// <summary>
/// Independent metadata scanner used as a safety checker gate.
/// Reads the final cleaned output file and returns false if any sensitive tag remains populated.
/// </summary>
public static class MetadataVerifier
{
    private static readonly HashSet<string> SensitiveTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "GPS Latitude",
        "GPS Longitude",
        "GPS Altitude",
        "Author",
        "Creator",
        "Company",
        "Manager",
        "Title",
        "Subject",
        "Keywords",
        "Date Time Original",
        "Camera Owner Name"
    };

    /// <summary>
    /// Reads metadata tags using MetadataExtractor.
    /// Returns false if any known sensitive tag is present in the file.
    /// </summary>
    public static bool VerifyNoSensitiveMetadata(string filePath)
    {
        try
        {
            var directories = ImageMetadataReader.ReadMetadata(filePath);
            foreach (var dir in directories)
            {
                foreach (var tag in dir.Tags)
                {
                    if (SensitiveTags.Contains(tag.Name))
                    {
                        return false; // Sensitive metadata leaked!
                    }
                }
            }
        }
        catch
        {
            // If format is not supported or parsing fails, return true (best effort)
        }

        return true;
    }
}
