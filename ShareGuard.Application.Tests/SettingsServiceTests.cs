using System;
using System.IO;
using ShareGuard.Application.Models;
using ShareGuard.Application.Services;
using Xunit;

namespace ShareGuard.Application.Tests;

public class SettingsServiceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _settingsPath;

    public SettingsServiceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sg-settings-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _settingsPath = Path.Combine(_tempDir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    [Fact]
    public void Load_WhenFileDoesNotExist_ReturnsDefaults()
    {
        var service = new SettingsService(_settingsPath);

        var settings = service.Load();

        Assert.True(settings.IsClipboardMonitorEnabled);
        Assert.True(settings.ShowCleanNotifications);
        Assert.Equal("Ctrl+Shift+G", settings.GlobalHotkey);
        Assert.Null(settings.CustomOutputDirectory);
        Assert.True(settings.UseOriginalDirectory);
    }

    [Fact]
    public void Save_ThenLoad_RoundTrips()
    {
        var service = new SettingsService(_settingsPath);

        var original = new AppSettings
        {
            IsClipboardMonitorEnabled = false,
            ShowCleanNotifications = false,
            GlobalHotkey = "Ctrl+Alt+S",
            CustomOutputDirectory = @"C:\CleanOutput",
            UseOriginalDirectory = false
        };

        service.Save(original);
        var loaded = service.Load();

        Assert.False(loaded.IsClipboardMonitorEnabled);
        Assert.False(loaded.ShowCleanNotifications);
        Assert.Equal("Ctrl+Alt+S", loaded.GlobalHotkey);
        Assert.Equal(@"C:\CleanOutput", loaded.CustomOutputDirectory);
        Assert.False(loaded.UseOriginalDirectory);
    }

    [Fact]
    public void Load_WhenFileIsCorrupt_ReturnsDefaults()
    {
        File.WriteAllText(_settingsPath, "NOT VALID JSON {{{");

        var service = new SettingsService(_settingsPath);
        var settings = service.Load();

        // Should gracefully fall back to defaults
        Assert.True(settings.IsClipboardMonitorEnabled);
        Assert.Equal("Ctrl+Shift+G", settings.GlobalHotkey);
    }

    [Fact]
    public void Load_AfterFirstRead_UsesCachedSettings()
    {
        File.WriteAllText(_settingsPath, """{"GlobalHotkey":"Ctrl+Alt+S"}""");

        var service = new SettingsService(_settingsPath);
        var first = service.Load();

        File.WriteAllText(_settingsPath, """{"GlobalHotkey":"Ctrl+F12"}""");
        var second = service.Load();

        Assert.Equal("Ctrl+Alt+S", first.GlobalHotkey);
        Assert.Equal("Ctrl+Alt+S", second.GlobalHotkey);
    }

    [Fact]
    public void Save_CreatesDirectoryIfNotExists()
    {
        var deepPath = Path.Combine(_tempDir, "sub", "deep", "settings.json");
        var service = new SettingsService(deepPath);

        service.Save(new AppSettings { GlobalHotkey = "Ctrl+F12" });

        Assert.True(File.Exists(deepPath));
        var loaded = service.Load();
        Assert.Equal("Ctrl+F12", loaded.GlobalHotkey);
    }
}
