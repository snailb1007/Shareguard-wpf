using System;
using System.IO;
using System.Text.Json;
using ShareGuard.Application.Interfaces;
using ShareGuard.Application.Models;

namespace ShareGuard.Application.Services;

public sealed class SettingsService : ISettingsService
{
    private readonly string _filePath;
    private AppSettings? _cachedSettings;

    public SettingsService(string? filePath = null)
    {
        _filePath = filePath ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ShareGuard",
            "settings.json");
    }

    public AppSettings Load()
    {
        if (_cachedSettings is not null)
        {
            return Clone(_cachedSettings);
        }

        try
        {
            if (!File.Exists(_filePath))
            {
                _cachedSettings = new AppSettings();
                return Clone(_cachedSettings);
            }

            var json = File.ReadAllText(_filePath);
            _cachedSettings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
            return Clone(_cachedSettings);
        }
        catch
        {
            // Fallback gracefully on parsing errors
            _cachedSettings = new AppSettings();
            return Clone(_cachedSettings);
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var directory = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
            _cachedSettings = Clone(settings);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to save settings to {_filePath}", ex);
        }
    }

    private static AppSettings Clone(AppSettings settings)
        => new()
        {
            IsClipboardMonitorEnabled = settings.IsClipboardMonitorEnabled,
            ShowCleanNotifications = settings.ShowCleanNotifications,
            GlobalHotkey = settings.GlobalHotkey,
            CustomOutputDirectory = settings.CustomOutputDirectory,
            UseOriginalDirectory = settings.UseOriginalDirectory
        };
}
