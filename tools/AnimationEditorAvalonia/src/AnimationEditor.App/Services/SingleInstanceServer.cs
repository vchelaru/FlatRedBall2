using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;

namespace AnimationEditor.App.Services;

/// <summary>
/// Implements single-instance enforcement using a named <see cref="Mutex"/> and a named pipe.
///
/// <para>The first process to acquire the mutex starts a background pipe server. Subsequent
/// processes find the mutex held, write their command-line argument (the .achx path) to the
/// pipe, and exit immediately.</para>
///
/// <para>The running instance reads the path from the pipe and raises
/// <see cref="FileOpenRequested"/> so the UI layer can open it as a new tab.</para>
/// </summary>
public sealed class SingleInstanceServer : IDisposable
{
    private const string MutexName   = "AnimationEditorAvalonia_SingleInstance";
    private const string PipeName    = "AnimationEditorAvalonia_IPC";

    private readonly Mutex _mutex;
    private readonly string _pipeName;
    private CancellationTokenSource? _cts;
    private Task? _listenerTask;

    /// <summary>
    /// Whether this process successfully claimed the single-instance mutex.
    /// When <c>false</c> the caller should send the file path to the running
    /// instance via <see cref="SendToRunningInstanceAsync"/> and then exit.
    /// </summary>
    public bool IsOwner { get; }

    /// <summary>
    /// Raised on a background thread when the running instance receives a file
    /// path from a second process. The UI layer must marshal to the UI thread.
    /// </summary>
    public event Action<string>? FileOpenRequested;

    /// <summary>
    /// Raised on a background thread when a second process connects without a file path (a plain
    /// re-launch). The UI layer must marshal to the UI thread and bring its window forward.
    /// </summary>
    public event Action? ActivationRequested;

    public SingleInstanceServer() : this(MutexName, PipeName) { }

    // Test-only: lets tests isolate the named system mutex so it can't collide
    // with a real running AnimationEditor instance.
    internal SingleInstanceServer(string mutexName) : this(mutexName, PipeName) { }

    // Test-only: also isolates the pipe, since a real running AnimationEditor listens on the default one.
    internal SingleInstanceServer(string mutexName, string pipeName)
    {
        _pipeName = pipeName;
        _mutex = new Mutex(initiallyOwned: true, name: mutexName, out bool createdNew);
        IsOwner = createdNew;
    }

    /// <summary>
    /// Starts the background pipe listener. Must only be called when <see cref="IsOwner"/> is <c>true</c>.
    /// </summary>
    public void StartListening()
    {
        if (!IsOwner) return;
        _cts = new CancellationTokenSource();
        _listenerTask = Task.Run(() => ListenLoopAsync(_cts.Token));
    }

    private async Task ListenLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(
                    _pipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    transmissionMode: PipeTransmissionMode.Byte,
                    options: PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(ct);

                using var reader = new StreamReader(server);
                var path = await reader.ReadLineAsync(ct);
                if (!string.IsNullOrEmpty(path))
                    FileOpenRequested?.Invoke(path);
                else
                    ActivationRequested?.Invoke();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Connection errors on the listener side are transient — keep listening.
                await Task.Delay(200, CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// Attempts to reach the running instance via the named pipe and, if <paramref name="filePath"/>
    /// is non-null, forwards it to be opened as a tab. Called by the second process before it exits.
    ///
    /// <para>Returns whether the pipe connection itself succeeded. This is a reachability signal
    /// only, not proof the primary's UI is responsive: <see cref="ListenLoopAsync"/> runs on a
    /// background thread independent of the UI thread, so a wedged UI thread can still leave the
    /// pipe listener accepting connections fine. A caller that gets <c>false</c> back needs a
    /// second, OS-level signal (see <c>PrimaryInstanceHangChecker</c> in the App layer) before
    /// concluding the primary is actually hung.</para>
    /// </summary>
    public static Task<bool> SendToRunningInstanceAsync(string? filePath) =>
        SendToRunningInstanceAsync(filePath, PipeName);

    internal static async Task<bool> SendToRunningInstanceAsync(string? filePath, string pipeName)
    {
        try
        {
            using var client = new NamedPipeClientStream(
                ".", pipeName, PipeDirection.Out, PipeOptions.Asynchronous);

            // 2-second timeout — if the server isn't listening yet, give up gracefully.
            await client.ConnectAsync(2000);

            // Always send a line: an empty one is a plain re-launch, which the primary answers by
            // bringing its window forward. Closing without writing breaks the pipe under the
            // listener's read, so it could not tell a re-launch from a failed connection.
            using var writer = new StreamWriter(client) { AutoFlush = true };
            await writer.WriteLineAsync(filePath ?? string.Empty);

            return true;
        }
        catch
        {
            // Could not reach the running instance.
            return false;
        }
    }

    public void Dispose()
    {
        _cts?.Cancel();
        _cts?.Dispose();

        // Only the owning process holds the mutex; a second instance never
        // acquired it, so calling ReleaseMutex there throws "Object
        // synchronization method was called from an unsynchronized block of code."
        if (IsOwner)
        {
            try { _mutex.ReleaseMutex(); }
            catch (ApplicationException) { /* not held / abandoned — nothing to release */ }
        }
        _mutex.Dispose();
    }
}
