using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;
using NSubstitute;
using Xunit;

namespace ShareGuard.Application.Tests;

public class HistoryServiceCompilationTests
{
    [Fact]
    public void TestCompile()
    {
        var mockRepo = Substitute.For<IHistoryRepository>();
        var service = new HistoryService(mockRepo);
        Assert.NotNull(service);
    }
}

