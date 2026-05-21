using ShareGuard.Infrastructure.Services;
using Xunit;

namespace ShareGuard.Domain.Tests;

public class PackageDetectorTests
{
    [Fact]
    public void IsPackaged_WhenRunningFromTestHost_ReturnsFalse()
    {
        // The test host process is not an MSIX packaged app,
        // so IsPackaged should always return false in unit tests.
        var detector = new PackageDetector();

        bool result = detector.IsPackaged;

        Assert.False(result);
    }

    [Fact]
    public void AppDataPath_WhenUnpackaged_ReturnsLocalApplicationData()
    {
        var detector = new PackageDetector();
        string expected = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShareGuard");

        string result = detector.AppDataPath;

        Assert.Equal(expected, result);
    }
}
