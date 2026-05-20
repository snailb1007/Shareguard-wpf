using ShareGuard.Domain.Models;
using Xunit;

namespace ShareGuard.Domain.Tests;

public class StripResultTests
{
    [Fact]
    public void StripResult_Success_ShouldStoreProperties()
    {
        var findings = new List<Finding>
        {
            new("GPS/Location", "GPSLatitude", "37.7749° N"),
            new("Camera/Device", "Make", "Canon")
        };

        var result = StripResult.Success(
            cleanFilePath: @"C:\photos\image.clean.jpg",
            findings: findings,
            elapsed: TimeSpan.FromMilliseconds(250));

        Assert.True(result.IsSuccess);
        Assert.Equal(@"C:\photos\image.clean.jpg", result.CleanFilePath);
        Assert.Equal(2, result.Findings.Count);
        Assert.Equal(TimeSpan.FromMilliseconds(250), result.Elapsed);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void StripResult_Failure_ShouldStoreErrorMessage()
    {
        var result = StripResult.Failure("File not found");

        Assert.False(result.IsSuccess);
        Assert.Empty(result.Findings);
        Assert.Equal("File not found", result.ErrorMessage);
        Assert.Null(result.CleanFilePath);
    }
}
