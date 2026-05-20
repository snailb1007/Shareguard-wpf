using ShareGuard.Domain.Entities;

namespace ShareGuard.Domain.Interfaces;

/// <summary>
/// Domain-level interface defining persistence actions for operation history.
/// </summary>
public interface IHistoryRepository
{
    /// <summary>
    /// Persists a new history event record.
    /// </summary>
    Task AddAsync(HistoryEvent historyEvent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Fetches a paged list of history events ordered descending by processing timestamp.
    /// </summary>
    Task<IEnumerable<HistoryEvent>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the total count of history records in the database.
    /// </summary>
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Wipes all history records.
    /// </summary>
    Task ClearAllAsync(CancellationToken cancellationToken = default);
}
