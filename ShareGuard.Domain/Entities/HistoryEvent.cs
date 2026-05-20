namespace ShareGuard.Domain.Entities;

/// <summary>
/// Represents a record of a file or URL cleaning action in the local database.
/// </summary>
public class HistoryEvent
{
    public Guid Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string OriginalPath { get; set; } = string.Empty;
    public string CleanPath { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
    public int FindingsCount { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
}
