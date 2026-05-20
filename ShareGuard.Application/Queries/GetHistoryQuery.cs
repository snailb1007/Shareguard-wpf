namespace ShareGuard.Application.Queries;

/// <summary>
/// Query to request paginated history results.
/// </summary>
public record GetHistoryQuery(int PageNumber, int PageSize);
