using System.Diagnostics;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Threading;
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
/// Implements single-instance detection via a named Mutex and routes file paths from
/// subsequent instances through a Named Pipe to the primary instance.
/// </summary>
public partial class App : System.Windows.Application
{
    private const string MutexName = @"Global\ShareGuard_SingleInstance_Mutex";
    private const string PipeName = "ShareGuard_ContextMenu_Pipe";

    private IHost? _host;
    private ITrayIconService? _trayIconService;
    private Mutex? _instanceMutex;
    private INamedPipeListenerService? _pipeListenerService;
    private CancellationTokenSource? _appCts;

    /// <summary>
    /// Win32 P/Invoke to bring a window to the foreground reliably, even when
    /// the calling process is not the foreground application (e.g. Explorer).
    /// </summary>
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    private async void ApplicationStartup(object sender, StartupEventArgs e)
    {
        // ------------------------------------------------------------------
        // 1. Parse command-line args for /clean "path1" "path2" …
        // ------------------------------------------------------------------
        var cleanPaths = ParseCleanArguments(e.Args);

        // ------------------------------------------------------------------
        // 2. Single-instance detection via named Mutex
        // ------------------------------------------------------------------
        _instanceMutex = new Mutex(true, MutexName, out bool createdNew);

        if (!createdNew)
        {
            // Second instance – forward paths to the running instance via Named Pipe
            Debug.WriteLine("[App] Second instance detected. Routing paths through Named Pipe.");
            await SendPathsToPrimaryInstanceAsync(cleanPaths);
            Shutdown();
            return;
        }

        // ------------------------------------------------------------------
        // First instance – proceed with normal startup
        // ------------------------------------------------------------------
        var builder = Host.CreateApplicationBuilder();

        // Register layer services via extension methods
        builder.Services.AddApplicationServices();
        builder.Services.AddInfrastructureServices();

        // Presentation layer registrations
        builder.Services.AddSingleton<IClipboardMonitorService, WindowsClipboardMonitorService>();
        builder.Services.AddSingleton<INotificationService, NotificationService>();
        builder.Services.AddSingleton<IHotkeyService, HotkeyService>();
        builder.Services.AddSingleton<ITrayIconService, TrayIconService>();
        builder.Services.AddSingleton<INamedPipeListenerService, NamedPipeListenerService>();
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

        // ------------------------------------------------------------------
        // 3. Start Named Pipe listener for incoming paths from second instances
        // ------------------------------------------------------------------
        _pipeListenerService = _host.Services.GetRequiredService<INamedPipeListenerService>();
        _appCts = new CancellationTokenSource();

        _pipeListenerService.FilesReceived += paths =>
        {
            Debug.WriteLine($"[App] FilesReceived via Named Pipe: {paths.Length} path(s).");
            Dispatcher.InvokeAsync(() =>
            {
                BringWindowToForeground(mainWindow);
                if (paths.Length > 0)
                {
                    _ = mainViewModel.ProcessFilesCommand.ExecuteAsync(paths);
                }
            });
        };

        // Fire-and-forget the listener loop; it runs until _appCts is cancelled.
        _ = _pipeListenerService.StartListeningAsync(_appCts.Token);

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

        // ------------------------------------------------------------------
        // 4. If /clean paths were provided on first launch, queue them for
        //    processing after a short delay so the UI can render first.
        // ------------------------------------------------------------------
        if (cleanPaths.Length > 0)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                BringWindowToForeground(mainWindow);
                _ = mainViewModel.ProcessFilesCommand.ExecuteAsync(cleanPaths);
            };
            timer.Start();
        }
    }

    private async void ApplicationExit(object sender, ExitEventArgs e)
    {
        // Stop the Named Pipe listener
        _pipeListenerService?.StopListening();
        _appCts?.Cancel();
        _appCts?.Dispose();

        // Dispose pipe listener (releases resources)
        _pipeListenerService?.Dispose();

        _trayIconService?.Dispose();

        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        // Release the single-instance Mutex
        if (_instanceMutex is not null)
        {
            try
            {
                _instanceMutex.ReleaseMutex();
            }
            catch (ApplicationException)
            {
                // Mutex was not owned – safe to ignore (e.g. second instance path).
            }
            _instanceMutex.Dispose();
        }
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>
    /// Parses command-line arguments for the <c>/clean</c> flag followed by
    /// one or more (possibly quoted) file paths.
    /// </summary>
    private static string[] ParseCleanArguments(string[] args)
    {
        var paths = new List<string>();
        bool foundClean = false;

        for (int i = 0; i < args.Length; i++)
        {
            if (!foundClean)
            {
                if (args[i].Equals("/clean", StringComparison.OrdinalIgnoreCase))
                {
                    foundClean = true;
                }
                continue;
            }

            // Everything after /clean is a file path (quotes already stripped by shell).
            var path = args[i].Trim('"');
            if (path.Length > 0)
            {
                paths.Add(path);
            }
        }

        return paths.ToArray();
    }

    /// <summary>
    /// Sends file paths to the primary (already-running) ShareGuard instance
    /// through the Named Pipe. Used by the second instance before exiting.
    /// </summary>
    private static async Task SendPathsToPrimaryInstanceAsync(string[] paths)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".",
                PipeName,
                PipeDirection.Out,
                PipeOptions.Asynchronous,
                TokenImpersonationLevel.Identification);

            await client.ConnectAsync(3000); // 3-second timeout

            var payloadText = paths.Length == 0
                ? NamedPipeListenerService.WakeUpPayload
                : string.Join('\n', paths);
            var payload = Encoding.UTF8.GetBytes(payloadText);
            await client.WriteAsync(payload);
            await client.FlushAsync();

            Debug.WriteLine(paths.Length == 0
                ? "[App] Sent wake-up request to primary instance."
                : $"[App] Sent {paths.Length} path(s) to primary instance.");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[App] Failed to send paths to primary instance: {ex.Message}");
        }
    }

    /// <summary>
    /// Brings the specified window to the foreground using a combination of
    /// Win32 <c>SetForegroundWindow</c> and WPF <c>Activate()</c> for reliable
    /// focus-stealing even when another process (e.g. Explorer) has focus.
    /// </summary>
    private static void BringWindowToForeground(Window window)
    {
        window.Show();
        window.WindowState = WindowState.Normal;

        var interopHelper = new System.Windows.Interop.WindowInteropHelper(window);
        SetForegroundWindow(interopHelper.Handle);

        window.Activate();
    }
}
