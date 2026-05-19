namespace ShareGuard.Domain.Models;

public sealed class StripResult
{
    private StripResult(
        bool isSuccess,
        string? cleanFilePath,
        IReadOnlyList<Finding> findings,
        TimeSpan elapsed,
        string? errorMessage)
    {
        IsSuccess = isSuccess;
        CleanFilePath = cleanFilePath;
        Findings = findings;
        Elapsed = elapsed;
        ErrorMessage = errorMessage;
    }

    public bool IsSuccess { get; }

    public string? CleanFilePath { get; }

    public IReadOnlyList<Finding> Findings { get; }

    public TimeSpan Elapsed { get; }

    public string? ErrorMessage { get; }

    public static StripResult Success(
        string cleanFilePath,
        IReadOnlyList<Finding> findings,
        TimeSpan elapsed)
    {
        return new StripResult(
            isSuccess: true,
            cleanFilePath: cleanFilePath,
            findings: findings,
            elapsed: elapsed,
            errorMessage: null);
    }

    public static StripResult Failure(string errorMessage)
    {
        return new StripResult(
            isSuccess: false,
            cleanFilePath: null,
            findings: Array.Empty<Finding>(),
            elapsed: TimeSpan.Zero,
            errorMessage: errorMessage);
    }
}
