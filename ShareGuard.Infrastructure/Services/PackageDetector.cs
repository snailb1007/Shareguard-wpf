using System.Runtime.InteropServices;
using System.Text;
using ShareGuard.Domain.Interfaces;

namespace ShareGuard.Infrastructure.Services;

/// <summary>
/// Detects MSIX packaging context using the Win32 GetCurrentPackageFullName API.
/// This avoids taking a dependency on the Windows App SDK.
/// </summary>
public sealed class PackageDetector : IPackageDetector
{
    // ERROR_INSUFFICIENT_BUFFER means we ARE packaged (just need a bigger buffer)
    private const int ErrorInsufficientBuffer = 122;

    // APPMODEL_ERROR_NO_PACKAGE means we are NOT packaged
    private const long AppmodelErrorNoPackage = 15700;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int GetCurrentPackageFullName(
        ref int packageFullNameLength,
        StringBuilder? packageFullName);

    private readonly Lazy<bool> _isPackaged = new(DetectPackaged);
    private readonly Lazy<string> _appDataPath;

    public PackageDetector()
    {
        _appDataPath = new Lazy<string>(() =>
        {
            if (IsPackaged)
            {
                // MSIX apps get a virtualized LocalApplicationData automatically.
                // No need to append "ShareGuard" — the container isolates it.
                return Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData);
            }

            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ShareGuard");
        });
    }

    public bool IsPackaged => _isPackaged.Value;

    public string AppDataPath => _appDataPath.Value;

    private static bool DetectPackaged()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return false;
        }

        try
        {
            int length = 0;
            int result = GetCurrentPackageFullName(ref length, null);

            // If we get ERROR_INSUFFICIENT_BUFFER, we are in a package
            // If we get APPMODEL_ERROR_NO_PACKAGE, we are unpackaged
            return result == ErrorInsufficientBuffer;
        }
        catch
        {
            // P/Invoke failure — assume unpackaged
            return false;
        }
    }
}
