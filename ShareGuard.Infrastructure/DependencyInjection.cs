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

        // Phase 4 IFileStripper registrations
        services.AddSingleton<IFileStripper, ImageSharpStripper>();
        services.AddSingleton<IFileStripper, OfficeOpenXmlStripper>();
        services.AddSingleton<IFileStripper, PdfMetadataStripper>();

        // Place SQLite database in AppData folder
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dbFolder = Path.Combine(appData, "ShareGuard");
        string dbPath = Path.Combine(dbFolder, "history.db");

        // Ensure directory exists
        Directory.CreateDirectory(dbFolder);

        // Add context factory to guarantee thread safety during parallel operations
        services.AddDbContextFactory<ShareGuardDbContext>(options =>
            options.UseSqlite($"Data Source={dbPath};Default Timeout=5"));

        // Register repository
        services.AddSingleton<IHistoryRepository, HistoryRepository>();

        return services;
    }
}

