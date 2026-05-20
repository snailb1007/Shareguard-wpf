using ShareGuard.Domain.Entities;
using ShareGuard.Domain.Interfaces;
using Xunit;

namespace ShareGuard.Domain.Tests;

public class HistoryRepositoryCompilationTests
{
    [Fact]
    public void VerifyEntitiesExist()
    {
        var evt = new HistoryEvent { Id = Guid.NewGuid(), FileName = "test.png" };
        Assert.NotNull(evt);
        Assert.Equal("test.png", evt.FileName);
    }
}
