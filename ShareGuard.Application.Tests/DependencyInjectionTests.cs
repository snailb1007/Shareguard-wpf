using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ShareGuard.Application.Tests;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplicationServices_ShouldReturnServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddApplicationServices();

        Assert.Same(services, result);
    }
}
