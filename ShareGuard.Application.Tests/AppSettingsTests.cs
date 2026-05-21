using ShareGuard.Application.Models;
using Xunit;

namespace ShareGuard.Application.Tests;

public class AppSettingsTests
{
    [Fact]
    public void Defaults_ShouldHaveExpectedValues()
    {
        var settings = new AppSettings();

        Assert.True(settings.IsClipboardMonitorEnabled);
        Assert.True(settings.ShowCleanNotifications);
        Assert.Equal("Ctrl+Shift+G", settings.GlobalHotkey);
        Assert.Null(settings.CustomOutputDirectory);
        Assert.True(settings.UseOriginalDirectory);
    }
}
