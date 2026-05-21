using System;
using System.Threading;
using System.Threading.Tasks;

namespace ShareGuard.App.Services;

/// <summary>
/// Listens on a named pipe for incoming file paths from external processes
/// (e.g., shell extension, second app instance).
/// </summary>
public interface INamedPipeListenerService : IDisposable
{
    /// <summary>
    /// Raised when one or more file paths are received over the named pipe.
    /// The array contains the newline-delimited paths sent by the client.
    /// </summary>
    /// <remarks>
    /// This event is raised on a background thread; subscribers must marshal
    /// to the UI thread (e.g., via <c>Dispatcher.Invoke</c>) if needed.
    /// </remarks>
    event Action<string[]>? FilesReceived;

    /// <summary>
    /// Starts the background listening loop. The method returns a task that
    /// completes only when <paramref name="cancellationToken"/> is cancelled
    /// or <see cref="StopListening"/> is called.
    /// </summary>
    Task StartListeningAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Requests a graceful shutdown of the listening loop.
    /// </summary>
    void StopListening();
}
