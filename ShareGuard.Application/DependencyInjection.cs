using Microsoft.Extensions.DependencyInjection;
using ShareGuard.Application.Services;

namespace ShareGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IImageCleanupService, ImageCleanupService>();
        return services;
    }
}
