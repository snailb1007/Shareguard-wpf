namespace ShareGuard.Domain.Models;

/// <summary>
/// Represents a single metadata field that was found and stripped from an image.
/// Grouped by human-readable category (GPS/Location, Camera/Device, Date & Time, Software/XMP).
/// </summary>
public sealed record Finding(string Category, string FieldName, string Value);
