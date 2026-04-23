using System;
using System.Collections.Generic;
using System.IO;

namespace FlatRedBall2.Content;

/// <summary>
/// Watches a directory recursively, debounces bursts of changes globally (waits until all writes
/// settle), then for each dirty file invokes a copy step followed by the user callback. Built on
/// top of an injectable <see cref="IDirectoryWatcher"/> so debounce / batch logic is unit-testable
/// without real filesystem timing.
/// </summary>
public class ContentDirectoryWatcher : IDisposable
{
    private readonly IDirectoryWatcher _source;
    private readonly Action<string> _onChanged;
    private readonly Func<string, bool> _copyToDestination;
    private readonly object _lock = new();
    private readonly HashSet<string> _dirtyPaths = new();
    private DateTime? _lastActivity;
    private bool _disposed;

    /// <summary>
    /// How long to wait after the LATEST change event (across all files) before processing the
    /// dirty batch. Editors and build tools fire bursts of events; global debounce avoids reloading
    /// while writes are still in flight. Default 150 ms.
    /// </summary>
    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>
    /// File extensions (case-insensitive, leading dot) that should flow through the watcher even
    /// when the destination file does not yet exist in the build output. Normally the engine
    /// filters out unknown source paths to ignore editor temp/scratch files; that filter blocks
    /// legitimately-new assets too (e.g. dropping a new PNG that a TMX now references). Extensions
    /// in this set are treated as "new file is a real asset" — the engine creates the destination
    /// directory if needed, copies the file, then fires the callback.
    /// <para>
    /// Defaults to <c>.png</c> and <c>.tsx</c> (the common TMX-references-new-asset case). Mutate
    /// this set to customize: <c>watcher.AutoCopyExtensions.Add(".ogg")</c>. Removing an entry
    /// restores the default "require dest to exist" behavior for that extension.
    /// </para>
    /// <para>
    /// Gum file types (<c>.gumx</c>, <c>.gusx</c>, <c>.gutx</c>, <c>.behx</c>, <c>.ganx</c>) are
    /// intentionally NOT included — Gum runs its own hot-reload pipeline and layering the engine's
    /// copy on top would conflict.
    /// </para>
    /// </summary>
    public HashSet<string> AutoCopyExtensions { get; } =
        new(StringComparer.OrdinalIgnoreCase) { ".png", ".tsx" };

    /// <param name="source">Underlying directory event source.</param>
    /// <param name="onChanged">Invoked once per dirty file after copy succeeds.</param>
    /// <param name="copyToDestination">
    /// Per-path copy step. Receives the relative path; returns <c>true</c> to proceed to the
    /// callback, <c>false</c> to skip silently (e.g. source file deleted). Throw
    /// <see cref="IOException"/> to signal "file mid-write" — the path is re-marked dirty and
    /// retried after the next debounce window.
    /// </param>
    public ContentDirectoryWatcher(IDirectoryWatcher source, Action<string> onChanged, Func<string, bool> copyToDestination)
    {
        _source = source;
        _onChanged = onChanged;
        _copyToDestination = copyToDestination;
        _source.Changed += MarkChanged;
    }

    private void MarkChanged(string relativePath)
    {
        lock (_lock)
        {
            _dirtyPaths.Add(relativePath);
            _lastActivity = DateTime.UtcNow;
        }
    }

    /// <summary>Test seam: marks a path dirty as if a file event arrived at <paramref name="when"/>.</summary>
    internal void MarkChangedAt(string relativePath, DateTime when)
    {
        lock (_lock)
        {
            _dirtyPaths.Add(relativePath);
            _lastActivity = when;
        }
    }

    /// <summary>
    /// Called by the engine each frame on the game thread. If the most recent change was longer
    /// ago than <see cref="Debounce"/>, processes every dirty path: copy → callback. Paths whose
    /// copy throws <see cref="IOException"/> are re-marked dirty for retry.
    /// </summary>
    public void Tick(DateTime now)
    {
        List<string>? toProcess = null;
        lock (_lock)
        {
            if (_lastActivity == null || (now - _lastActivity.Value) < Debounce) return;
            toProcess = new List<string>(_dirtyPaths);
            _dirtyPaths.Clear();
            _lastActivity = null;
        }

        List<string>? failed = null;
        foreach (var rel in toProcess)
        {
            try
            {
                if (_copyToDestination(rel))
                    _onChanged(rel);
            }
            catch (IOException)
            {
                (failed ??= new List<string>()).Add(rel);
            }
        }

        if (failed != null)
        {
            lock (_lock)
            {
                foreach (var f in failed) _dirtyPaths.Add(f);
                _lastActivity = now;
            }
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
