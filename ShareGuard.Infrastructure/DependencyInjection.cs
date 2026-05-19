using Microsoft.Extensions.DependencyInjection;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Infrastructure.Services;

namespace ShareGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IImageCleaner, ImageSharpCleaner>();
        services.AddSingleton<LocalHistoryLogger>();
        return services;
    }
}
