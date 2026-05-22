using ShareGuard.App.Services;
using Xunit;

namespace ShareGuard.App.Tests;

public sealed class NamedPipeListenerServiceTests
{
    [Fact]
    public void ParsePayload_WhenWakeUpPayloadReceived_ReturnsEmptyBatch()
    {
        var paths = NamedPipeListenerService.ParsePayload(NamedPipeListenerService.WakeUpPayload);

        Assert.Empty(paths);
    }

    [Fact]
    public void ParsePayload_WhenPathsReceived_ReturnsTrimmedNonEmptyPaths()
    {
        var paths = NamedPipeListenerService.ParsePayload(" C:\\one.png\r\nC:\\two.pdf\n\n");

        Assert.Equal(["C:\\one.png", "C:\\two.pdf"], paths);
    }
}
