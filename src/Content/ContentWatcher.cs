using System;
using System.IO;

namespace FlatRedBall2.Content;

/// <summary>
/// Status result of a content watch registration attempt via <see cref="Screen.TryWatchContent"/>
/// or <see cref="Screen.TryWatchContentDirectory"/>.
/// </summary>
public enum ContentWatchRegistrationStatus
{
    /// <summary>
    /// Content watch registration succeeded. The file or directory is being monitored for changes.
    /// </summary>
    Registered,

    /// <summary>
    /// Content watch registration failed because the engine's <see cref="FlatRedBallService.SourceContentRoot"/>
    /// is not set. This is typical in shipping builds where content is pre-built and embedded;
    /// hot-reload is disabled in those scenarios.
    /// </summary>
    SourceContentRootUnavailable,
}

/// <summary>
/// Wraps an <see cref="IFileWatcher"/> with debouncing and game-thread dispatch so a content file
/// can be safely reloaded from the engine's <c>Update</c> tick. Editors typically fire several
/// change events per save (write + rename + flush); the debounce window collapses those bursts
/// into a single callback.
/// <para>
/// Construct manually with an <see cref="IFileWatcher"/>, or via <see cref="Screen.WatchContent(string, Action)"/>
/// which constructs one with a default <c>FileSystemFileWatcher</c> and registers it for
/// per-frame ticking + automatic disposal on screen change.
/// </para>
/// </summary>
public class ContentWatcher : IDisposable
{
    private readonly IFileWatcher _source;
    private readonly Action _onChanged;
    private readonly Func<bool>? _copyToDestination;
    private readonly object _lock = new();
    private DateTime? _dirtyAt;
    private bool _disposed;

    /// <summary>
    /// How long to wait after the most recent change event before invoking the callback. Editors
    /// fire bursts of events around each save, so debouncing avoids reloading mid-write. Default
    /// 150 ms is a balance between responsive iteration and not catching a partial file.
    /// </summary>
    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <param name="source">Underlying file event source.</param>
    /// <param name="onChanged">Invoked on the game thread after a debounced change settles.</param>
    /// <param name="copyToDestination">
    /// Optional pre-callback step. The screen-level <c>WatchContent</c> overload passes a delegate
    /// that copies the source file to its build-output path before the callback runs, so callbacks
    /// reading the standard output path see fresh content. Return <c>true</c> to proceed to the
    /// callback, <c>false</c> to skip it silently (e.g. source file deleted). Throw
    /// <see cref="IOException"/> to signal "file mid-write" — the change is re-marked dirty
    /// and retried.
    /// </param>
    public ContentWatcher(IFileWatcher source, Action onChanged, Func<bool>? copyToDestination = null)
    {
        _source = source;
        _onChanged = onChanged;
        _copyToDestination = copyToDestination;
        _source.Changed += MarkChanged;
    }

    private void MarkChanged()
    {
        lock (_lock) _dirtyAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Test seam: marks the watcher dirty as if a file event arrived at <paramref name="when"/>.
    /// Production code uses <see cref="MarkChanged"/> via the underlying file watcher.
    /// </summary>
    internal void MarkChangedAt(DateTime when)
    {
        lock (_lock) _dirtyAt = when;
    }

    /// <summary>
    /// Called by the engine each frame on the game thread. If a change has been pending longer
    /// than <see cref="Debounce"/>, invokes the callback. If the callback throws
    /// <see cref="IOException"/> (typically the file is still being written), re-marks dirty so
    /// the retry happens after another debounce window.
    /// </summary>
    public void Tick(DateTime now)
    {
        DateTime? d;
        lock (_lock) d = _dirtyAt;
        if (d == null || (now - d.Value) < Debounce) return;

        lock (_lock) _dirtyAt = null;

        try
        {
            if (_copyToDestination != null && !_copyToDestination()) return;
            _onChanged();
        }
        catch (IOException)
        {
            lock (_lock) _dirtyAt = now;
        }
    }

    /// <summary>Stops watching, unhooks the source event, and disposes the underlying watcher. Idempotent.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _source.Changed -= MarkChanged;
        _source.Dispose();
    }
}
