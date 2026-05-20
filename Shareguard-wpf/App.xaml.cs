using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShareGuard.App.Services;
using ShareGuard.App.ViewModels;
using ShareGuard.Application;
using ShareGuard.Infrastructure;
using ShareGuard.Infrastructure.Data;

namespace ShareGuard.App;

/// <summary>
/// Application entry point. Hosts the .NET Generic Host for DI, logging, and configuration.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    private async void ApplicationStartup(object sender, StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();

        // Register layer services via extension methods
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices();

        // Presentation layer registrations
        builder.Services.AddSingleton<IClipboardMonitorService, WindowsClipboardMonitorService>();
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();

        // Run Entity Framework Core migrations on SQLite local database at startup
        try
        {
            using (var scope = _host.Services.CreateScope())
            {
                var dbContextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ShareGuardDbContext>>();
                using var context = await dbContextFactory.CreateDbContextAsync();
                await context.Database.MigrateAsync();
            }
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Failed to initialize database: {ex.Message}\n\nThe application will now close.", 
                "Database Initialization Error", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private async void ApplicationExit(object sender, ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
