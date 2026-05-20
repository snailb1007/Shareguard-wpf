namespace ShareGuard.Application.Commands;

/// <summary>
/// Command to write a new run summary to the history database.
/// </summary>
public record LogHistoryCommand(
    string FileName,
    string OriginalPath,
    string CleanPath,
    int FindingsCount,
    bool IsSuccess,
    string? ErrorMessage = null);
