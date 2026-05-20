using ShareGuard.Application.Models;

namespace ShareGuard.Application.Interfaces;

public interface ISettingsService
{
    AppSettings Load();
    void Save(AppSettings settings);
}
