using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShareGuard.Domain.Entities;
using ShareGuard.Infrastructure.Data;
using ShareGuard.Infrastructure.Repositories;
using Xunit;

namespace ShareGuard.Application.Tests;

public class HistoryRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly DbContextOptions<ShareGuardDbContext> _contextOptions;

    public HistoryRepositoryTests()
    {
        // Use SQLite in-memory to test exact SQL behavior
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        _contextOptions = new DbContextOptionsBuilder<ShareGuardDbContext>()
            .UseSqlite(_connection)
            .Options;

        // Ensure database schema is created
        using var context = new ShareGuardDbContext(_contextOptions);
        context.Database.EnsureCreated();
    }

    [Fact]
    public async Task AddAsync_ShouldPersistEvent()
    {
        var factory = new TestDbContextFactory(_contextOptions);
        var sut = new HistoryRepository(factory);
        var evt = new HistoryEvent
        {
            Id = Guid.NewGuid(),
            FileName = "photo.jpg",
            OriginalPath = "C:\\photo.jpg",
            CleanPath = "C:\\photo.clean.jpg",
            ProcessedAt = DateTime.UtcNow,
            FindingsCount = 4,
            IsSuccess = true
        };

        await sut.AddAsync(evt, TestContext.Current.CancellationToken);

        using var context = new ShareGuardDbContext(_contextOptions);
        var count = await context.HistoryEvents.CountAsync(TestContext.Current.CancellationToken);
        var entity = await context.HistoryEvents.SingleOrDefaultAsync(e => e.Id == evt.Id, TestContext.Current.CancellationToken);

        Assert.Equal(1, count);
        Assert.NotNull(entity);
        Assert.Equal("photo.jpg", entity.FileName);
    }

    [Fact]
    public async Task GetPagedAsync_ShouldReturnPagedResultsDescending()
    {
        var factory = new TestDbContextFactory(_contextOptions);
        var sut = new HistoryRepository(factory);

        var baseTime = DateTime.UtcNow;
        for (int i = 1; i <= 5; i++)
        {
            await sut.AddAsync(new HistoryEvent
            {
                Id = Guid.NewGuid(),
                FileName = $"file{i}.jpg",
                OriginalPath = "C:\\",
                CleanPath = "C:\\",
                ProcessedAt = baseTime.AddMinutes(i),
                IsSuccess = true
            }, TestContext.Current.CancellationToken);
        }

        var results = (await sut.GetPagedAsync(1, 3, TestContext.Current.CancellationToken)).ToList();

        Assert.Equal(3, results.Count);
        Assert.Equal("file5.jpg", results[0].FileName); // Latest first
        Assert.Equal("file4.jpg", results[1].FileName);
        Assert.Equal("file3.jpg", results[2].FileName);
    }

    [Fact]
    public async Task ClearAllAsync_ShouldEmptyDatabase()
    {
        var factory = new TestDbContextFactory(_contextOptions);
        var sut = new HistoryRepository(factory);
        await sut.AddAsync(new HistoryEvent
        {
            Id = Guid.NewGuid(),
            FileName = "test.jpg",
            ProcessedAt = DateTime.UtcNow,
            IsSuccess = true
        }, TestContext.Current.CancellationToken);

        await sut.ClearAllAsync(TestContext.Current.CancellationToken);

        var count = await sut.GetTotalCountAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, count);
    }

    public void Dispose()
    {
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}

public class TestDbContextFactory : IDbContextFactory<ShareGuardDbContext>
{
    private readonly DbContextOptions<ShareGuardDbContext> _options;

    public TestDbContextFactory(DbContextOptions<ShareGuardDbContext> options)
    {
        _options = options;
    }

    public ShareGuardDbContext CreateDbContext()
    {
        return new ShareGuardDbContext(_options);
    }
}
