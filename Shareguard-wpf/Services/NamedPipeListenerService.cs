using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ShareGuard.App.Services;

/// <summary>
/// Named-pipe server that accepts one connection at a time, reads newline-delimited
/// UTF-8 file paths, and raises <see cref="FilesReceived"/> for each batch.
/// </summary>
public sealed class NamedPipeListenerService : INamedPipeListenerService
{
    /// <summary>
    /// Well-known pipe name shared with the shell extension / second-instance launcher.
    /// </summary>
    private const string PipeName = "ShareGuard_ContextMenu_Pipe";

    private CancellationTokenSource? _cts;
    private bool _disposed;

    /// <inheritdoc />
    public event Action<string[]>? FilesReceived;

    /// <inheritdoc />
    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Link the external token with an internal one so StopListening() also cancels.
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _cts.Token;

        Debug.WriteLine($"[NamedPipeListener] Listening on pipe '{PipeName}'...");

        while (!linkedToken.IsCancellationRequested)
        {
            // A new NamedPipeServerStream must be created for every connection because
            // the pipe handle is invalidated after Disconnect.
            NamedPipeServerStream? pipeServer = null;
            try
            {
                pipeServer = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Byte,
                    PipeOptions.Asynchronous);

                Debug.WriteLine("[NamedPipeListener] Waiting for connection...");
                await pipeServer.WaitForConnectionAsync(linkedToken).ConfigureAwait(false);

                Debug.WriteLine("[NamedPipeListener] Client connected. Reading data...");

                using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
                var content = await reader.ReadToEndAsync(linkedToken).ConfigureAwait(false);

                if (!string.IsNullOrWhiteSpace(content))
                {
                    var paths = content
                        .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim('\r', ' '))
                        .Where(p => p.Length > 0)
                        .ToArray();

                    if (paths.Length > 0)
                    {
                        Debug.WriteLine($"[NamedPipeListener] Received {paths.Length} path(s).");
                        FilesReceived?.Invoke(paths);
                    }
                }

                pipeServer.Disconnect();
            }
            catch (OperationCanceledException) when (linkedToken.IsCancellationRequested)
            {
                // Graceful shutdown – exit the loop silently.
                Debug.WriteLine("[NamedPipeListener] Cancellation requested. Shutting down.");
                break;
            }
            catch (Exception ex)
            {
                // Log unexpected errors but keep the listener alive.
                // Delay before retrying to prevent tight-loop CPU saturation on persistent errors.
                Debug.WriteLine($"[NamedPipeListener] Error: {ex}");
                try
                {
                    await Task.Delay(1000, linkedToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
            finally
            {
                if (pipeServer is not null)
                {
                    await pipeServer.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        Debug.WriteLine("[NamedPipeListener] Listening loop ended.");
    }

    /// <inheritdoc />
    public void StopListening()
    {
        if (_cts is { IsCancellationRequested: false })
        {
            Debug.WriteLine("[NamedPipeListener] StopListening requested.");
            _cts.Cancel();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        StopListening();
        _cts?.Dispose();
        _cts = null;
        _disposed = true;
    }
}
