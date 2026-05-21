namespace ShareGuard.Domain.Interfaces;

/// <summary>
/// Detects whether the application is running inside an MSIX package container.
/// </summary>
public interface IPackageDetector
{
    /// <summary>
    /// True if the app is running inside an MSIX container.
    /// </summary>
    bool IsPackaged { get; }

    /// <summary>
    /// Returns the appropriate AppData path. When packaged, this is the
    /// MSIX-redirected LocalApplicationData folder. When unpackaged,
    /// it falls back to LocalApplicationData\ShareGuard.
    /// </summary>
    string AppDataPath { get; }
}
