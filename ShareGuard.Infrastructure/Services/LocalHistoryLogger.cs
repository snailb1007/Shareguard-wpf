using System.Text.Json;

namespace ShareGuard.Infrastructure.Services;

/// <summary>
/// Appends a summary of each cleaning operation to a JSON-lines log file
/// at %APPDATA%/ShareGuard/history.jsonl (one JSON object per line).
/// </summary>
public sealed class LocalHistoryLogger
{
    private static readonly string HistoryDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ShareGuard");

    private static readonly string HistoryFile = Path.Combine(HistoryDir, "history.jsonl");

    public async Task LogAsync(string originalPath, string cleanPath, int findingCount, TimeSpan elapsed, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(HistoryDir);

        var entry = new
        {
            Timestamp = DateTimeOffset.UtcNow,
            OriginalPath = originalPath,
            CleanPath = cleanPath,
            FindingCount = findingCount,
            ElapsedMs = elapsed.TotalMilliseconds
        };

        var json = JsonSerializer.Serialize(entry);
        await File.AppendAllTextAsync(HistoryFile, json + Environment.NewLine, cancellationToken);
    }
}
