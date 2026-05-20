using NSubstitute;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Application.Tests;

public class FileCleanupServiceTests
{
    private readonly IFileStripper _mockStripper = Substitute.For<IFileStripper>();
    private readonly IHistoryService _mockHistory = Substitute.For<IHistoryService>();

    [Fact]
    public async Task CleanFileAsync_WithUnsupportedExtension_ShouldReturnFailedResult()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"shareguard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var testFile = Path.Combine(tempDir, "test.xyz");
            await File.WriteAllTextAsync(testFile, "fake content", TestContext.Current.CancellationToken);

            _mockStripper.CanHandle(".xyz").Returns(false);

            var service = new FileCleanupService(new[] { _mockStripper }, _mockHistory);
            var result = await service.CleanFileAsync(testFile, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Contains("Unsupported file type", result.ErrorMessage);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CleanFileAsync_WithEmptyPath_ShouldReturnFailedResult()
    {
        var service = new FileCleanupService(new[] { _mockStripper }, _mockHistory);
        var result = await service.CleanFileAsync("", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
    }
}
