using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Domain.Tests;

public class FindingTests
{
    [Fact]
    public void Finding_ShouldStoreAllProperties()
    {
        var finding = new Finding("GPS/Location", "GPSLatitude", "37.7749° N");

        Assert.Equal("GPS/Location", finding.Category);
        Assert.Equal("GPSLatitude", finding.FieldName);
        Assert.Equal("37.7749° N", finding.Value);
    }

    [Fact]
    public void Finding_WithSameValues_ShouldBeEqual()
    {
        var a = new Finding("Camera/Device", "Make", "Canon");
        var b = new Finding("Camera/Device", "Make", "Canon");

        Assert.Equal(a, b);
    }

    [Fact]
    public void Finding_WithDifferentValues_ShouldNotBeEqual()
    {
        var a = new Finding("Camera/Device", "Make", "Canon");
        var b = new Finding("Camera/Device", "Make", "Nikon");

        Assert.NotEqual(a, b);
    }
}
