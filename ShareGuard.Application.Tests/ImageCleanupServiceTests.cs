using NSubstitute;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using ShareGuard.Infrastructure.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class ImageCleanupServiceTests
{
    private readonly IImageCleaner _mockCleaner = Substitute.For<IImageCleaner>();
    private readonly LocalHistoryLogger _logger = new();

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

    [Fact]
    public async Task CleanAsync_WithInvalidExtension_ShouldReturnFailure()
    {
        var service = new ImageCleanupService(_mockCleaner, _logger);
        var result = await service.CleanAsync(@"C:\docs\report.pdf", TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Contains("Unsupported file type", result.ErrorMessage);
    }

    [Fact]
    public async Task CleanAsync_WithNonexistentFile_ShouldReturnFailure()
    {
        var service = new ImageCleanupService(_mockCleaner, _logger);
        var result = await service.CleanAsync(@"C:\nonexistent\photo.jpg", TestContext.Current.CancellationToken);
        Assert.False(result.IsSuccess);
        Assert.Contains("File not found", result.ErrorMessage);
    }
}
