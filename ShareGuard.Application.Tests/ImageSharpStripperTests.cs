using NSubstitute;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Domain.Models;
using ShareGuard.Infrastructure.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class ImageSharpStripperTests
{
    [Fact]
    public void CanHandle_ShouldReturnTrueForImages()
    {
        var mockCleaner = Substitute.For<IImageCleaner>();
        var stripper = new ImageSharpStripper(mockCleaner);

        Assert.True(stripper.CanHandle(".jpg"));
        Assert.True(stripper.CanHandle(".jpeg"));
        Assert.True(stripper.CanHandle(".png"));
        Assert.True(stripper.CanHandle(".webp"));
        Assert.False(stripper.CanHandle(".pdf"));
        Assert.False(stripper.CanHandle(".docx"));
    }

    [Fact]
    public async Task StripMetadataAsync_ShouldDelegateToImageCleaner()
    {
        var mockCleaner = Substitute.For<IImageCleaner>();
        var findings = new List<Finding> { new("GPS/Location", "Lat", "1.0") };
        mockCleaner.CleanImageAsync("src", "dest", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(findings));

        var stripper = new ImageSharpStripper(mockCleaner);
        var result = await stripper.StripMetadataAsync("src", "dest", TestContext.Current.CancellationToken);

        Assert.Same(findings, result);
        await mockCleaner.Received(1).CleanImageAsync("src", "dest", Arg.Any<CancellationToken>());
    }
}
