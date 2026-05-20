using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NSubstitute;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Application.Tests;

public class MultiFileProcessorServiceTests
{
    private class SyncProgress<T> : IProgress<T>
    {
        private readonly Action<T> _handler;
        private readonly object _lock = new();

        public SyncProgress(Action<T> handler)
        {
            _handler = handler;
        }

        public void Report(T value)
        {
            lock (_lock)
            {
                _handler(value);
            }
        }
    }

    [Fact]
    public async Task ProcessFilesAsync_WithEmptyList_ShouldReturnImmediately()
    {
        // Arrange
        var mockCleanupService = Substitute.For<IImageCleanupService>();
        var service = new MultiFileProcessorService(mockCleanupService);
        var reported = new List<ProcessingStatus>();
        var progress = new SyncProgress<ProcessingStatus>(reported.Add);

        // Act
        await service.ProcessFilesAsync(new List<string>(), progress, CancellationToken.None);

        // Assert
        Assert.Empty(reported);
        await mockCleanupService.DidNotReceiveWithAnyArgs().CleanAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ProcessFilesAsync_WithMultipleFiles_ShouldProcessInParallelAndReportProgress()
    {
        // Arrange
        var mockCleanupService = Substitute.For<IImageCleanupService>();
        var service = new MultiFileProcessorService(mockCleanupService);
        var files = new List<string> { "file1.png", "file2.jpg", "file3.webp" };

        mockCleanupService.CleanAsync("file1.png", Arg.Any<CancellationToken>())
            .Returns(StripResult.Success("file1.clean.png", Array.Empty<Finding>(), TimeSpan.FromMilliseconds(10)));
        mockCleanupService.CleanAsync("file2.jpg", Arg.Any<CancellationToken>())
            .Returns(StripResult.Success("file2.clean.jpg", Array.Empty<Finding>(), TimeSpan.FromMilliseconds(20)));
        mockCleanupService.CleanAsync("file3.webp", Arg.Any<CancellationToken>())
            .Returns(StripResult.Success("file3.clean.webp", Array.Empty<Finding>(), TimeSpan.FromMilliseconds(30)));

        var reported = new List<ProcessingStatus>();
        var progress = new SyncProgress<ProcessingStatus>(reported.Add);

        // Act
        await service.ProcessFilesAsync(files, progress, CancellationToken.None);

        // Assert
        Assert.Equal(3, reported.Count);

        // Verify all files were processed
        Assert.Contains(reported, r => r.FilePath == "file1.png" && r.Success && r.CleanPath == "file1.clean.png");
        Assert.Contains(reported, r => r.FilePath == "file2.jpg" && r.Success && r.CleanPath == "file2.clean.jpg");
        Assert.Contains(reported, r => r.FilePath == "file3.webp" && r.Success && r.CleanPath == "file3.clean.webp");

        // Verify total counts and current counts are correct
        Assert.All(reported, r => Assert.Equal(3, r.TotalCount));
        var currentCounts = reported.Select(r => r.CurrentCount).ToList();
        Assert.Contains(1, currentCounts);
        Assert.Contains(2, currentCounts);
        Assert.Contains(3, currentCounts);
    }

    [Fact]
    public async Task ProcessFilesAsync_WhenServiceThrowsException_ShouldCatchAndReportError()
    {
        // Arrange
        var mockCleanupService = Substitute.For<IImageCleanupService>();
        var service = new MultiFileProcessorService(mockCleanupService);
        var files = new List<string> { "corrupt.png" };

        mockCleanupService.CleanAsync("corrupt.png", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException<StripResult>(new InvalidOperationException("Metadata is corrupt")));

        var reported = new List<ProcessingStatus>();
        var progress = new SyncProgress<ProcessingStatus>(reported.Add);

        // Act
        await service.ProcessFilesAsync(files, progress, CancellationToken.None);

        // Assert
        Assert.Single(reported);
        var status = reported[0];
        Assert.Equal("corrupt.png", status.FilePath);
        Assert.False(status.Success);
        Assert.Equal("Metadata is corrupt", status.ErrorMessage);
        Assert.Equal(string.Empty, status.CleanPath);
        Assert.Equal(0, status.FindingsCount);
    }

    [Fact]
    public async Task ProcessFilesAsync_WhenCleanAsyncReturnsFailure_ShouldReportFailureAndErrorMessage()
    {
        // Arrange
        var mockCleanupService = Substitute.For<IImageCleanupService>();
        var service = new MultiFileProcessorService(mockCleanupService);
        var files = new List<string> { "unsupported.gif" };

        mockCleanupService.CleanAsync("unsupported.gif", Arg.Any<CancellationToken>())
            .Returns(StripResult.Failure("Unsupported format"));

        var reported = new List<ProcessingStatus>();
        var progress = new SyncProgress<ProcessingStatus>(reported.Add);

        // Act
        await service.ProcessFilesAsync(files, progress, CancellationToken.None);

        // Assert
        Assert.Single(reported);
        var status = reported[0];
        Assert.Equal("unsupported.gif", status.FilePath);
        Assert.False(status.Success);
        Assert.Equal("Unsupported format", status.ErrorMessage);
        Assert.Equal(string.Empty, status.CleanPath);
    }

    [Fact]
    public async Task ProcessFilesAsync_WithCancellation_ShouldCancelProcessing()
    {
        // Arrange
        var mockCleanupService = Substitute.For<IImageCleanupService>();
        var service = new MultiFileProcessorService(mockCleanupService);
        var files = new List<string> { "file1.png", "file2.jpg" };

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var reported = new List<ProcessingStatus>();
        var progress = new SyncProgress<ProcessingStatus>(reported.Add);

        // Act & Assert
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ProcessFilesAsync(files, progress, cts.Token));
    }

    [Fact]
    public async Task ProcessFilesAsync_WithNullFilePaths_ShouldThrowArgumentNullException()
    {
        // Arrange
        var mockCleanupService = Substitute.For<IImageCleanupService>();
        var service = new MultiFileProcessorService(mockCleanupService);
        var progress = new SyncProgress<ProcessingStatus>(_ => { });

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            service.ProcessFilesAsync(null!, progress, CancellationToken.None));
    }
}
