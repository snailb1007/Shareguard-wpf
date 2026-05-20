using ShareGuard.Application.Commands;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Queries;
using ShareGuard.Domain.Entities;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.Application.Services;

public class HistoryService(IHistoryRepository historyRepository) : IHistoryService
{
    private readonly IHistoryRepository _historyRepository = historyRepository;

    public Task LogHistoryAsync(LogHistoryCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var historyEvent = new HistoryEvent
        {
            Id = Guid.NewGuid(),
            FileName = command.FileName,
            OriginalPath = command.OriginalPath,
            CleanPath = command.CleanPath,
            ProcessedAt = DateTime.UtcNow,
            FindingsCount = command.FindingsCount,
            IsSuccess = command.IsSuccess,
            ErrorMessage = command.ErrorMessage
        };

        return _historyRepository.AddAsync(historyEvent, cancellationToken);
    }

    public Task<IEnumerable<HistoryEvent>> GetHistoryAsync(GetHistoryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.PageNumber <= 0) throw new ArgumentException("Page number must be positive.", nameof(query));
        if (query.PageSize <= 0) throw new ArgumentException("Page size must be positive.", nameof(query));

        return _historyRepository.GetPagedAsync(query.PageNumber, query.PageSize, cancellationToken);
    }

    public Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        return _historyRepository.GetTotalCountAsync(cancellationToken);
    }

    public Task ClearHistoryAsync(CancellationToken cancellationToken = default)
    {
        return _historyRepository.ClearAllAsync(cancellationToken);
    }
}
