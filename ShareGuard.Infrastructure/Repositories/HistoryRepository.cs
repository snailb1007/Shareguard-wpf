using Microsoft.EntityFrameworkCore;
using ShareGuard.Domain.Entities;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Infrastructure.Data;

namespace ShareGuard.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of IHistoryRepository. Uses DbContextFactory to ensure thread safety
/// when multiple operations are processed in parallel.
/// </summary>
public class HistoryRepository : IHistoryRepository
{
    private readonly IDbContextFactory<ShareGuardDbContext> _contextFactory;

    public HistoryRepository(IDbContextFactory<ShareGuardDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task AddAsync(HistoryEvent historyEvent, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.HistoryEvents.AddAsync(historyEvent, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IEnumerable<HistoryEvent>> GetPagedAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.HistoryEvents
            .AsNoTracking()
            .OrderByDescending(e => e.ProcessedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        return await context.HistoryEvents.CountAsync(cancellationToken);
    }

    public async Task ClearAllAsync(CancellationToken cancellationToken = default)
    {
        using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await context.HistoryEvents.ExecuteDeleteAsync(cancellationToken);
    }
}
