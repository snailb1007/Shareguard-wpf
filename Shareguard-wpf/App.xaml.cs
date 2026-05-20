using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShareGuard.App.Services;
using ShareGuard.App.ViewModels;
using ShareGuard.Application;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Models;
using ShareGuard.Application.Services;
using ShareGuard.Infrastructure;
using ShareGuard.Infrastructure.Data;

namespace ShareGuard.App;

/// <summary>
/// Application entry point. Hosts the .NET Generic Host for DI, logging, and configuration.
/// </summary>
public partial class App : System.Windows.Application
{
    private IHost? _host;
    private ITrayIconService? _trayIconService;

    private async void ApplicationStartup(object sender, StartupEventArgs e)
    {
        var builder = Host.CreateApplicationBuilder();

        // Register layer services via extension methods
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices();

        // Presentation layer registrations
        builder.Services.AddSingleton<IClipboardMonitorService, WindowsClipboardMonitorService>();
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<IHotkeyService, HotkeyService>();
        builder.Services.AddSingleton<ITrayIconService, TrayIconService>();
        builder.Services.AddSingleton<SettingsViewModel>();
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

        // Load saved settings and apply them before showing the window
        var settingsService = _host.Services.GetRequiredService<ISettingsService>();
        var settings = settingsService.Load();

        // Initialize tray icon
        _trayIconService = _host.Services.GetRequiredService<ITrayIconService>();
        try
        {
            _trayIconService.Initialize();
        }
        catch (System.Exception ex)
        {
            MessageBox.Show($"Failed to initialize system tray icon: {ex.Message}\n\nRunning without system tray functionality.",
                "Tray Icon Initialization Error", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        var mainWindow = _host.Services.GetRequiredService<MainWindow>();
        var mainViewModel = _host.Services.GetRequiredService<MainViewModel>();
        var settingsViewModel = _host.Services.GetRequiredService<SettingsViewModel>();

        // Wire tray icon events
        if (_trayIconService != null)
        {
            _trayIconService.RestoreRequested += () =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
            };

            _trayIconService.CleanClipboardRequested += () =>
            {
                _ = mainViewModel.CleanClipboardFromTrayAsync();
            };

            _trayIconService.ToggleClipboardMonitorRequested += () =>
            {
                settingsViewModel.IsClipboardMonitorEnabled = !settingsViewModel.IsClipboardMonitorEnabled;
            };

            _trayIconService.SettingsRequested += () =>
            {
                mainWindow.Show();
                mainWindow.WindowState = WindowState.Normal;
                mainWindow.Activate();
                mainWindow.NavigateToSettings();
            };

            _trayIconService.ExitRequested += () =>
            {
                var result = MessageBox.Show(
                    "ShareGuard will stop monitoring the clipboard and the global hotkey will be disabled. Exit?",
                    "Exit ShareGuard",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _trayIconService?.Dispose();
                    mainWindow.ForceClose();
                    Shutdown();
                }
            };
        }

        // Apply saved clipboard monitor state
        mainViewModel.IsMonitoring = settings.IsClipboardMonitorEnabled;
        _trayIconService?.UpdateClipboardMonitorMenuText(settings.IsClipboardMonitorEnabled);

        // Listen for settings changes to sync tray icon state
        settingsViewModel.SettingsChanged += updatedSettings =>
        {
            if (mainViewModel.IsMonitoring != updatedSettings.IsClipboardMonitorEnabled)
            {
                mainViewModel.IsMonitoring = updatedSettings.IsClipboardMonitorEnabled;
            }
            _trayIconService?.UpdateClipboardMonitorMenuText(updatedSettings.IsClipboardMonitorEnabled);
        };

        // Listen for MainViewModel changes to sync settings and tray icon
        mainViewModel.PropertyChanged += (s, ev) =>
        {
            if (ev.PropertyName == nameof(MainViewModel.IsMonitoring))
            {
                if (settingsViewModel.IsClipboardMonitorEnabled != mainViewModel.IsMonitoring)
                {
                    settingsViewModel.IsClipboardMonitorEnabled = mainViewModel.IsMonitoring;
                    _trayIconService?.UpdateClipboardMonitorMenuText(mainViewModel.IsMonitoring);
                }
            }
        };

        mainWindow.Show();
    }

    private void ApplicationExit(object sender, ExitEventArgs e)
    {
        _trayIconService?.Dispose();

        if (_host is not null)
        {
            _host.StopAsync().GetAwaiter().GetResult();
            _host.Dispose();
        }
    }
}
