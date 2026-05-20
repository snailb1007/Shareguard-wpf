using NSubstitute;
using ShareGuard.Application.Commands;
using ShareGuard.Application.Queries;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Entities;
using ShareGuard.Domain.Interfaces;
using Xunit;

namespace ShareGuard.Application.Tests;

public class HistoryServiceTests
{
    private readonly IHistoryRepository _mockRepo = Substitute.For<IHistoryRepository>();

    [Fact]
    public async Task LogHistoryAsync_ShouldMapFieldsAndCallRepository()
    {
        // Arrange
        var service = new HistoryService(_mockRepo);
        var command = new LogHistoryCommand(
            FileName: "test.png",
            OriginalPath: @"C:\original\test.png",
            CleanPath: @"C:\clean\test.clean.png",
            FindingsCount: 3,
            IsSuccess: true,
            ErrorMessage: null
        );

        // Act
        await service.LogHistoryAsync(command, TestContext.Current.CancellationToken);

        // Assert
        await _mockRepo.Received(1).AddAsync(Arg.Is<HistoryEvent>(e =>
            e.FileName == command.FileName &&
            e.OriginalPath == command.OriginalPath &&
            e.CleanPath == command.CleanPath &&
            e.FindingsCount == command.FindingsCount &&
            e.IsSuccess == command.IsSuccess &&
            e.ErrorMessage == command.ErrorMessage &&
            e.Id != Guid.Empty &&
            (DateTime.UtcNow - e.ProcessedAt).TotalSeconds < 5
        ), TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task LogHistoryAsync_WithNullCommand_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = new HistoryService(_mockRepo);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.LogHistoryAsync(null!, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(0, 10)]
    [InlineData(-1, 10)]
    [InlineData(1, 0)]
    [InlineData(1, -5)]
    public async Task GetHistoryAsync_WithInvalidArguments_ShouldThrowArgumentException(int pageNumber, int pageSize)
    {
        // Arrange
        var service = new HistoryService(_mockRepo);
        var query = new GetHistoryQuery(pageNumber, pageSize);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => service.GetHistoryAsync(query, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHistoryAsync_WithNullQuery_ShouldThrowArgumentNullException()
    {
        // Arrange
        var service = new HistoryService(_mockRepo);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => service.GetHistoryAsync(null!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetHistoryAsync_WithValidArguments_ShouldCallRepositoryPaged()
    {
        // Arrange
        var service = new HistoryService(_mockRepo);
        var query = new GetHistoryQuery(2, 20);
        var expectedEvents = new[] { new HistoryEvent { FileName = "paged.png" } };
        _mockRepo.GetPagedAsync(2, 20, TestContext.Current.CancellationToken).Returns(expectedEvents);

        // Act
        var result = await service.GetHistoryAsync(query, TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(expectedEvents, result);
        await _mockRepo.Received(1).GetPagedAsync(2, 20, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task GetTotalCountAsync_ShouldReturnRepositoryCount()
    {
        // Arrange
        var service = new HistoryService(_mockRepo);
        _mockRepo.GetTotalCountAsync(TestContext.Current.CancellationToken).Returns(42);

        // Act
        var result = await service.GetTotalCountAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(42, result);
        await _mockRepo.Received(1).GetTotalCountAsync(TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ClearHistoryAsync_ShouldCallRepositoryClear()
    {
        // Arrange
        var service = new HistoryService(_mockRepo);

        // Act
        await service.ClearHistoryAsync(TestContext.Current.CancellationToken);

        // Assert
        await _mockRepo.Received(1).ClearAllAsync(TestContext.Current.CancellationToken);
    }
}
