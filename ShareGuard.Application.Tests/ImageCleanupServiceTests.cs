using NSubstitute;
using ShareGuard.Application.Commands;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Application.Tests;

public class ImageCleanupServiceTests
{
    private readonly IImageCleaner _mockCleaner = Substitute.For<IImageCleaner>();
    private readonly IHistoryService _mockHistoryService = Substitute.For<IHistoryService>();

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".png")]
    [InlineData(".webp")]
    public void IsValidExtension_ShouldAcceptSupportedFormats(string extension)
    {
        Assert.True(ImageCleanupService.IsValidExtension(extension));
    }

    [Theory]
    [InlineData(".bmp")]
    [InlineData(".gif")]
    [InlineData(".heic")]
    [InlineData(".pdf")]
    [InlineData("")]
    public void IsValidExtension_ShouldRejectUnsupportedFormats(string extension)
    {
        Assert.False(ImageCleanupService.IsValidExtension(extension));
    }

    [Fact]
    public void GenerateCleanPath_ShouldAppendCleanSuffix()
    {
        var result = ImageCleanupService.GenerateCleanPath(@"C:\photos\vacation.jpg");
        Assert.Equal(@"C:\photos\vacation.clean.jpg", result);
    }

    [Fact]
    public void GenerateCleanPath_WithExistingCleanFile_ShouldAutoIncrement()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"shareguard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var original = Path.Combine(tempDir, "photo.jpg");
            var existing = Path.Combine(tempDir, "photo.clean.jpg");
            File.WriteAllText(original, "fake");
            File.WriteAllText(existing, "fake");

            var result = ImageCleanupService.GenerateCleanPath(original);
            Assert.Equal(Path.Combine(tempDir, "photo.clean (1).jpg"), result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public void GenerateCleanPath_WithMultipleExisting_ShouldIncrementCorrectly()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"shareguard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var original = Path.Combine(tempDir, "photo.jpg");
            File.WriteAllText(original, "fake");
            File.WriteAllText(Path.Combine(tempDir, "photo.clean.jpg"), "fake");
            File.WriteAllText(Path.Combine(tempDir, "photo.clean (1).jpg"), "fake");
            File.WriteAllText(Path.Combine(tempDir, "photo.clean (2).jpg"), "fake");

            var result = ImageCleanupService.GenerateCleanPath(original);
            Assert.Equal(Path.Combine(tempDir, "photo.clean (3).jpg"), result);
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CleanAsync_WithNullOrEmptyPath_ShouldThrowArgumentException(string? path)
    {
        var service = new ImageCleanupService(_mockCleaner, _mockHistoryService);
        await Assert.ThrowsAsync<ArgumentException>(() => service.CleanAsync(path!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CleanAsync_WithInvalidExtension_ShouldReturnFailureAndLogToDb()
    {
        var service = new ImageCleanupService(_mockCleaner, _mockHistoryService);
        var result = await service.CleanAsync(@"C:\docs\report.pdf", TestContext.Current.CancellationToken);
        
        Assert.False(result.IsSuccess);
        Assert.Contains("Unsupported file type", result.ErrorMessage);

        await _mockHistoryService.Received(1).LogHistoryAsync(
            Arg.Is<LogHistoryCommand>(c =>
                c.OriginalPath == @"C:\docs\report.pdf" &&
                c.CleanPath == string.Empty &&
                c.FindingsCount == 0 &&
                !c.IsSuccess &&
                c.ErrorMessage != null && c.ErrorMessage.Contains("Unsupported file type")),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CleanAsync_WithNonexistentFile_ShouldReturnFailureAndLogToDb()
    {
        var service = new ImageCleanupService(_mockCleaner, _mockHistoryService);
        var result = await service.CleanAsync(@"C:\nonexistent\photo.jpg", TestContext.Current.CancellationToken);
        
        Assert.False(result.IsSuccess);
        Assert.Contains("File not found", result.ErrorMessage);

        await _mockHistoryService.Received(1).LogHistoryAsync(
            Arg.Is<LogHistoryCommand>(c =>
                c.OriginalPath == @"C:\nonexistent\photo.jpg" &&
                c.CleanPath == string.Empty &&
                c.FindingsCount == 0 &&
                !c.IsSuccess &&
                c.ErrorMessage != null && c.ErrorMessage.Contains("File not found")),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task CleanAsync_WithSuccessfulClean_ShouldReturnSuccessAndLogToDb()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"shareguard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var original = Path.Combine(tempDir, "photo.jpg");
            await File.WriteAllTextAsync(original, "fake image content", TestContext.Current.CancellationToken);

            var findings = new List<Finding> { new("GPS/Location", "Latitude", "12.34") };
            _mockCleaner.CleanImageAsync(original, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(findings);

            var service = new ImageCleanupService(_mockCleaner, _mockHistoryService);
            var result = await service.CleanAsync(original, TestContext.Current.CancellationToken);

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.CleanFilePath);
            Assert.Equal(findings, result.Findings);

            await _mockHistoryService.Received(1).LogHistoryAsync(
                Arg.Is<LogHistoryCommand>(c =>
                    c.OriginalPath == original &&
                    c.CleanPath == result.CleanFilePath &&
                    c.FindingsCount == 1 &&
                    c.IsSuccess &&
                    c.ErrorMessage == null),
                Arg.Any<CancellationToken>()
            );
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithExceptionDuringClean_ShouldReturnFailureAndLogToDb()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"shareguard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var original = Path.Combine(tempDir, "photo.jpg");
            await File.WriteAllTextAsync(original, "fake image content", TestContext.Current.CancellationToken);

            _mockCleaner.CleanImageAsync(original, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(Task.FromException<List<Finding>>(new InvalidOperationException("Stripping failed")));

            var service = new ImageCleanupService(_mockCleaner, _mockHistoryService);
            var result = await service.CleanAsync(original, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.Contains("Stripping failed", result.ErrorMessage);

            await _mockHistoryService.Received(1).LogHistoryAsync(
                Arg.Is<LogHistoryCommand>(c =>
                    c.OriginalPath == original &&
                    c.CleanPath == string.Empty &&
                    c.FindingsCount == 0 &&
                    !c.IsSuccess &&
                    c.ErrorMessage != null && c.ErrorMessage.Contains("Stripping failed")),
                Arg.Any<CancellationToken>()
            );
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }

    [Fact]
    public async Task CleanAsync_WithExceptionDuringClean_ShouldDeletePartialOutputFile()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), $"shareguard-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        try
        {
            var original = Path.Combine(tempDir, "photo.jpg");
            await File.WriteAllTextAsync(original, "fake image content", TestContext.Current.CancellationToken);

            var cleanPath = ImageCleanupService.GenerateCleanPath(original);

            _mockCleaner.CleanImageAsync(original, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .Returns(async x =>
                {
                    var dest = (string)x[1];
                    await File.WriteAllTextAsync(dest, "corrupt partial output", TestContext.Current.CancellationToken);
                    throw new InvalidOperationException("Stripping failed");
                });

            var service = new ImageCleanupService(_mockCleaner, _mockHistoryService);
            var result = await service.CleanAsync(original, TestContext.Current.CancellationToken);

            Assert.False(result.IsSuccess);
            Assert.False(File.Exists(cleanPath), "Partial output file should have been deleted");
        }
        finally
        {
            Directory.Delete(tempDir, true);
        }
    }
}
