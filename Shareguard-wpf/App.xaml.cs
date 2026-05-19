using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShareGuard.App.ViewModels;
using ShareGuard.Application;
using ShareGuard.Infrastructure;

namespace ShareGuard.App;

/// <summary>
/// Application entry point. Hosts the .NET Generic Host for DI, logging, and configuration.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;

    private async void Application_Startup(object sender, StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();

        // Register layer services via extension methods
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices();

        // Presentation layer registrations
        builder.Services.AddSingleton<MainViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
        await _host.StartAsync();

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        mainWindow.Show();
    }

    private async void Application_Exit(object sender, ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}
