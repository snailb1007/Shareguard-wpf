namespace ShareGuard.Domain.Interfaces;

/// <summary>
/// Service contract for cleaning tracking and marketing parameters from URLs.
/// </summary>
public interface IUrlCleanerService
{
    /// <summary>
    /// Strips tracking/marketing query parameters from the specified URL.
    /// </summary>
    /// <param name="dirtyUrl">The input URL containing potential tracking parameters.</param>
    /// <param name="cleanUrl">The clean URL without the tracking parameters.</param>
    /// <param name="removedCount">The number of tracking parameters that were removed.</param>
    /// <returns>True if any tracking parameters were removed, false otherwise.</returns>
    bool CleanUrl(string dirtyUrl, out string cleanUrl, out int removedCount);
}
