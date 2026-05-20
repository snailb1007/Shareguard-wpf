using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using ShareGuard.Application.Services;
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

    [Fact]
    public void AddApplicationServices_ShouldRegisterImageCleanupService()
    {
        var services = new ServiceCollection();

        services.AddApplicationServices();

        var descriptor = services.FirstOrDefault(d => d.ServiceType == typeof(IImageCleanupService));
        Assert.NotNull(descriptor);
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(ImageCleanupService), descriptor.ImplementationType);
    }
}
