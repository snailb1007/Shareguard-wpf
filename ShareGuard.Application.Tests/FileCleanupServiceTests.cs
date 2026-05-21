using NSubstitute;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Models;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Application.Tests;

public class FileCleanupServiceTests
{
    private readonly IFileStripper _mockStripper = Substitute.For<IFileStripper>();
    private readonly IHistoryService _mockHistory = Substitute.For<IHistoryService>();
    private readonly ISettingsService _mockSettings = Substitute.For<ISettingsService>();

    public FileCleanupServiceTests()
    {
        _mockSettings.Load().Returns(new AppSettings());
    }

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

            var service = new FileCleanupService(new[] { _mockStripper }, _mockHistory, _mockSettings);
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
        var service = new FileCleanupService(new[] { _mockStripper }, _mockHistory, _mockSettings);
        var result = await service.CleanFileAsync("", TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void GenerateCleanPath_WithCustomOutputDirectory_ShouldUseCustomDirectory()
    {
        // Arrange
        var customDir = Path.Combine(Path.GetTempPath(), $"custom-out-{Guid.NewGuid():N}");
        var settings = new AppSettings
        {
            UseOriginalDirectory = false,
            CustomOutputDirectory = customDir
        };
        _mockSettings.Load().Returns(settings);

        var service = new FileCleanupService(new[] { _mockStripper }, _mockHistory, _mockSettings);
        var originalPath = Path.Combine(Path.GetTempPath(), "original_image.jpg");

        try
        {
            // Act
            var cleanPath = service.GenerateCleanPath(originalPath);

            // Assert
            var expectedPath = Path.Combine(customDir, "original_image.clean.jpg");
            Assert.Equal(expectedPath, cleanPath);
            Assert.True(Directory.Exists(customDir));
        }
        finally
        {
            if (Directory.Exists(customDir))
            {
                Directory.Delete(customDir, true);
            }
        }
    }
}
