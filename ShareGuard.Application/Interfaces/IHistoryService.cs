using ShareGuard.Application.Commands;
using ShareGuard.Application.Queries;
using ShareGuard.Domain.Entities;

namespace ShareGuard.Application.Interfaces;

/// <summary>
/// Orchestrates reads and writes to the operation history repository.
/// </summary>
public interface IHistoryService
{
    Task LogHistoryAsync(LogHistoryCommand command, CancellationToken cancellationToken = default);
    Task<IEnumerable<HistoryEvent>> GetHistoryAsync(GetHistoryQuery query, CancellationToken cancellationToken = default);
    Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default);
    Task ClearHistoryAsync(CancellationToken cancellationToken = default);
}
