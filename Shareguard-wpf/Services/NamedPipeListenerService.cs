using System;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
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
    public const string WakeUpPayload = "[WAKEUP]";

    /// <summary>
    /// Well-known pipe name shared with the shell extension / second-instance launcher.
    /// </summary>
    private const string DefaultPipeName = "ShareGuard_ContextMenu_Pipe";

    private readonly string _pipeName;
    private CancellationTokenSource? _cts;
    private bool _disposed;

    public NamedPipeListenerService()
        : this(DefaultPipeName)
    {
    }

    public NamedPipeListenerService(string pipeName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pipeName);
        _pipeName = pipeName;
    }

    /// <inheritdoc />
    public event Action<string[]>? FilesReceived;

    /// <inheritdoc />
    public async Task StartListeningAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Link the external token with an internal one so StopListening() also cancels.
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var linkedToken = _cts.Token;

        Debug.WriteLine($"[NamedPipeListener] Listening on pipe '{_pipeName}'...");

        while (!linkedToken.IsCancellationRequested)
        {
            // A new NamedPipeServerStream must be created for every connection because
            // the pipe handle is invalidated after Disconnect.
            NamedPipeServerStream? pipeServer = null;
            try
            {
                pipeServer = CreatePipeServer(_pipeName);

                Debug.WriteLine("[NamedPipeListener] Waiting for connection...");
                await pipeServer.WaitForConnectionAsync(linkedToken).ConfigureAwait(false);

                Debug.WriteLine("[NamedPipeListener] Client connected. Reading data...");

                if (!IsCurrentUserClient(pipeServer))
                {
                    Debug.WriteLine("[NamedPipeListener] Rejected pipe client from a different user.");
                    pipeServer.Disconnect();
                    continue;
                }

                using var reader = new StreamReader(pipeServer, Encoding.UTF8, leaveOpen: true);
                var content = await reader.ReadToEndAsync(linkedToken).ConfigureAwait(false);
                var paths = ParsePayload(content);

                if (content.Trim().Equals(WakeUpPayload, StringComparison.Ordinal))
                {
                    Debug.WriteLine("[NamedPipeListener] Received wake-up request.");
                    FilesReceived?.Invoke(paths);
                }
                else if (paths.Length > 0)
                {
                    Debug.WriteLine($"[NamedPipeListener] Received {paths.Length} path(s).");
                    FilesReceived?.Invoke(paths);
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

    public static string[] ParsePayload(string content)
    {
        if (content.Trim().Equals(WakeUpPayload, StringComparison.Ordinal))
        {
            return [];
        }

        return content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim('\r', ' '))
            .Where(p => p.Length > 0)
            .ToArray();
    }

    private static NamedPipeServerStream CreatePipeServer(string pipeName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return new NamedPipeServerStream(
                pipeName,
                PipeDirection.In,
                maxNumberOfServerInstances: 1,
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous);
        }

        return CreatePipeServerWindows(pipeName);
    }

    [SupportedOSPlatform("windows")]
    private static NamedPipeServerStream CreatePipeServerWindows(string pipeName)
    {
        var currentSid = WindowsIdentity.GetCurrent().User
            ?? throw new InvalidOperationException("Unable to resolve current Windows user SID.");

        var pipeSecurity = new PipeSecurity();
        pipeSecurity.AddAccessRule(new PipeAccessRule(
            currentSid,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));

        return NamedPipeServerStreamAcl.Create(
            pipeName,
            PipeDirection.In,
            maxNumberOfServerInstances: 1,
            transmissionMode: PipeTransmissionMode.Byte,
            options: PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            pipeSecurity);
    }

    private static bool IsCurrentUserClient(NamedPipeServerStream pipeServer)
    {
        if (!OperatingSystem.IsWindows())
        {
            return true;
        }

        return IsCurrentUserClientWindows(pipeServer);
    }

    [SupportedOSPlatform("windows")]
    private static bool IsCurrentUserClientWindows(NamedPipeServerStream pipeServer)
    {
        try
        {
            SecurityIdentifier? clientSid = null;
            pipeServer.RunAsClient(() => clientSid = WindowsIdentity.GetCurrent().User);

            var currentSid = WindowsIdentity.GetCurrent().User;
            return clientSid is not null && currentSid is not null && clientSid.Equals(currentSid);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            Debug.WriteLine($"[NamedPipeListener] Failed to verify pipe client identity: {ex.Message}");
            return false;
        }
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
