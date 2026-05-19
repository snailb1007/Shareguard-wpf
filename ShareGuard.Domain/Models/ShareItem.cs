namespace ShareGuard.Domain.Models;

public sealed record ShareItem
{
    public required string FullPath { get; init; }

    public required string FileName { get; init; }

    public required long SizeBytes { get; init; }

    public required string Extension { get; init; }

    public static ShareItem FromPath(string path)
    {
        var fileInfo = new FileInfo(path);

        return new ShareItem
        {
            FullPath = fileInfo.FullName,
            FileName = fileInfo.Name,
            SizeBytes = fileInfo.Length,
            Extension = fileInfo.Extension.ToLowerInvariant()
        };
    }
}
