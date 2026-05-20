using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ShareGuard.Domain.Interfaces;
using ShareGuard.Infrastructure.Data;
using ShareGuard.Infrastructure.Repositories;
using ShareGuard.Infrastructure.Services;

namespace ShareGuard.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services)
    {
        services.AddSingleton<IImageCleaner, ImageSharpCleaner>();

        // Place SQLite database in AppData folder
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dbFolder = Path.Combine(appData, "ShareGuard");
        string dbPath = Path.Combine(dbFolder, "history.db");

        // Ensure directory exists
        Directory.CreateDirectory(dbFolder);

        // Add context factory to guarantee thread safety during parallel operations
        services.AddDbContextFactory<ShareGuardDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath};Busy Timeout=5000"));

        // Register repository
        services.AddSingleton<IHistoryRepository, HistoryRepository>();

        return services;
    }
}

