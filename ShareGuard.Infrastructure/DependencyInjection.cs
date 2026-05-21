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
        services.AddSingleton<IPackageDetector, PackageDetector>();
        services.AddSingleton<IImageCleaner, ImageSharpCleaner>();

        // Phase 4 IFileStripper registrations
        services.AddSingleton<IFileStripper, ImageSharpStripper>();
        services.AddSingleton<IFileStripper, OfficeOpenXmlStripper>();
        services.AddSingleton<IFileStripper, PdfMetadataStripper>();

        // Resolve the package detector to determine the correct data folder.
        // PackageDetector has no dependencies so constructing it inline is safe —
        // the DI container isn't built yet when we're registering services.
        var detector = new PackageDetector();
        string dbFolder = detector.AppDataPath;
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

