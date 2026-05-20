using Microsoft.Extensions.DependencyInjection;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Services;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        services.AddSingleton<IImageCleanupService, ImageCleanupService>();
        services.AddSingleton<IUrlCleanerService, UrlCleanerService>();

        // Register history service
        services.AddSingleton<IHistoryService, HistoryService>();

        // Register parallel multi-file processor service
        services.AddSingleton<IMultiFileProcessorService, MultiFileProcessorService>();

        return services;
    }
}


