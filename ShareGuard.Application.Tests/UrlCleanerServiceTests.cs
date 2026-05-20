using ShareGuard.Application.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class UrlCleanerServiceTests
{
    private readonly UrlCleanerService _service = new();

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path/to/page")]
    [InlineData("https://example.com?param1=value1&param2=value2")]
    public void CleanUrl_WithoutTrackingParams_ShouldReturnFalseAndOriginalUrl(string url)
    {
        var result = _service.CleanUrl(url, out var cleanUrl, out var removedCount);

        Assert.False(result);
        Assert.Equal(url, cleanUrl);
        Assert.Equal(0, removedCount);
    }

    [Theory]
    [InlineData("https://example.com?utm_source=google", "https://example.com", 1)]
    [InlineData("http://example.com?utm_medium=email&utm_campaign=summer&other=keep", "http://example.com?other=keep", 2)]
    [InlineData("https://example.com?fbclid=123&gclid=456&igshid=789&_gl=abc&tt_medium=xyz&twclid=def", "https://example.com", 6)]
    public void CleanUrl_WithTrackingParams_ShouldStripParamsAndReturnTrue(string dirtyUrl, string expectedCleanUrl, int expectedRemovedCount)
    {
        var result = _service.CleanUrl(dirtyUrl, out var cleanUrl, out var removedCount);

        Assert.True(result);
        Assert.Equal(expectedCleanUrl, cleanUrl);
        Assert.Equal(expectedRemovedCount, removedCount);
    }

    [Theory]
    [InlineData("https://example.com?UTM_SOURCE=google", "https://example.com", 1)]
    [InlineData("HTTP://example.com?Utm_Medium=email&UTM_campaign=summer&Other=keep", "http://example.com?Other=keep", 2)]
    [InlineData("https://example.com?Fbclid=123&GCLID=456", "https://example.com", 2)]
    public void CleanUrl_WithMixedCaseTrackingParams_ShouldStripCaseInsensitively(string dirtyUrl, string expectedCleanUrl, int expectedRemovedCount)
    {
        var result = _service.CleanUrl(dirtyUrl, out var cleanUrl, out var removedCount);

        Assert.True(result);
        // Note: Flurl normalizes the scheme to lowercase, which is standard. We assert matching of scheme and query params.
        Assert.Equal(expectedCleanUrl.ToLowerInvariant(), cleanUrl.ToLowerInvariant());
        Assert.Equal(expectedRemovedCount, removedCount);
    }

    [Theory]
    [InlineData("ftp://example.com?utm_source=google")]
    [InlineData("file:///C:/path/to/file.txt?utm_source=google")]
    [InlineData("mailto:user@example.com?utm_source=google")]
    [InlineData("custom-scheme://example.com?utm_source=google")]
    public void CleanUrl_WithInvalidScheme_ShouldReturnFalseAndOriginalUrl(string url)
    {
        var result = _service.CleanUrl(url, out var cleanUrl, out var removedCount);

        Assert.False(result);
        Assert.Equal(url, cleanUrl);
        Assert.Equal(0, removedCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CleanUrl_WithNullOrWhitespace_ShouldReturnFalseAndOriginalUrl(string? url)
    {
        var result = _service.CleanUrl(url!, out var cleanUrl, out var removedCount);

        Assert.False(result);
        Assert.Equal(url, cleanUrl);
        Assert.Equal(0, removedCount);
    }

    [Theory]
    [InlineData("https://example.com/page?utm_source=xyz#section", "https://example.com/page#section", 1)]
    [InlineData("http://example.com?utm_medium=email&keep=1#/route/path?utm_source=abc", "http://example.com?keep=1#/route/path?utm_source=abc", 1)]
    [InlineData("https://site.org/#/path?utm_source=xyz", "https://site.org/#/path?utm_source=xyz", 0)]
    public void CleanUrl_WithFragments_ShouldPreserveFragments(string dirtyUrl, string expectedCleanUrl, int expectedRemovedCount)
    {
        var result = _service.CleanUrl(dirtyUrl, out var cleanUrl, out var removedCount);

        if (expectedRemovedCount > 0)
        {
            Assert.True(result);
        }
        else
        {
            Assert.False(result);
        }
        Assert.Equal(expectedCleanUrl, cleanUrl);
        Assert.Equal(expectedRemovedCount, removedCount);
    }

    [Theory]
    [InlineData("not-a-url")]
    [InlineData("http://[invalid]")]
    [InlineData("https://")]
    public void CleanUrl_WithMalformedOrInvalidUrl_ShouldReturnFalseAndOriginalUrl(string url)
    {
        var result = _service.CleanUrl(url, out var cleanUrl, out var removedCount);

        Assert.False(result);
        Assert.Equal(url, cleanUrl);
        Assert.Equal(0, removedCount);
    }
}
