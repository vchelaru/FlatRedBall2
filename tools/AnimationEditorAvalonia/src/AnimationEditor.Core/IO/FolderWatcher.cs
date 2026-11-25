using AnimationEditor.Core.HotReload;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Watches a folder (recursively) for changes to paths matching a caller-supplied filter and
/// raises a debounced <see cref="Changed"/> event carrying every changed path and its
/// <see cref="WatcherChangeType"/>. Generalizes <c>PngFolderWatcher</c> (deleted, #843) so the
/// same debounce/atomic-write logic (<see cref="FileChangeCoalescer"/>, already covered by
/// <c>FileChangeCoalescerTests</c>) backs any folder watch — PNGs for the Files panel, and
/// <c>.achx</c> for the Project tree.
/// </summary>
public sealed class FolderWatcher : IDisposable
{
    private readonly Func<string, bool> _pathFilter;
    private readonly FileChangeCoalescer _coalescer = new();
    private readonly Timer _flushTimer;
    private readonly object _lock = new();

    private FileSystemWatcher? _watcher;

    // Linux only: sees the watched folder made again, which its own watcher does not.
    private FileSystemWatcher? _parentWatcher;

    /// <summary>Raised (debounced, ~100ms after the last change settles) with every changed path.</summary>
    public event Action<IReadOnlyList<(string Path, WatcherChangeType Type)>>? Changed;

    /// <summary>
    /// Raised when the underlying <see cref="FileSystemWatcher"/> reports a buffer overflow (too
    /// many changes between flushes, e.g. a large <c>git pull</c>/checkout). The individual
    /// changed paths are lost at that point — callers should treat this as "assume anything under
    /// the watched folder may have changed" and rescan. Any other error raises it too: the folder went away, or, on
    /// Linux, a subfolder that may not be listed. So does, on Linux, the folder made again.
    /// </summary>
    public event Action? Overflowed;

    public FolderWatcher(Func<string, bool> pathFilter)
    {
        _pathFilter = pathFilter;
        _flushTimer = new Timer(_ => FlushCoalescer(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public void Watch(string? folder)
    {
        lock (_lock)
        {
            StopWatcherLocked();
            _parentWatcher?.Dispose();
            _parentWatcher = null;

            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                return;

            StartLocked(folder);
            if (OperatingSystem.IsLinux()) _parentWatcher = WatchForReplacement(folder);
        }
    }

    private void StartLocked(string folder)
    {
        var fsw = new FileSystemWatcher(folder)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
        };

        fsw.Changed += OnChanged;
        fsw.Created += OnCreated;
        fsw.Deleted += OnDeleted;
        fsw.Renamed += OnRenamed;
        fsw.Error += OnError;
        fsw.EnableRaisingEvents = true;

        _watcher = fsw;

        _flushTimer.Change(100, 100);
    }

    // Linux watches the folder itself, not its path: once it is gone nothing is reported, and one made or moved in its
    // place is another folder.
    private FileSystemWatcher? WatchForReplacement(string folder)
    {
        folder = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        if (Path.GetDirectoryName(folder) is not { } parent) return null;

        void OnAppeared(object sender, FileSystemEventArgs e)
        {
            if (e.FullPath != folder) return;
            lock (_lock)
            {
                if (!ReferenceEquals(sender, _parentWatcher)) return;
                StopWatcherLocked();
                try
                {
                    StartLocked(folder);
                }
                catch (Exception ex) when (ex is ArgumentException or IOException)
                {
                    return; // gone again already
                }
            }
            Overflowed?.Invoke();
        }

        try
        {
            var watcher = new FileSystemWatcher(parent) { NotifyFilter = NotifyFilters.DirectoryName };
            watcher.Created += OnAppeared;
            watcher.Renamed += OnAppeared;
            watcher.EnableRaisingEvents = true;
            return watcher;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null; // a parent that may not be listed
        }
    }

    /// <summary>
    /// Watches the folder again when the OS stopped the watcher (the folder went away). A watcher that only reported an
    /// error goes on: starting it again would report the same one.
    /// </summary>
    public void RestartIfStopped()
    {
        string? folder;
        lock (_lock)
            folder = _watcher is { EnableRaisingEvents: false } ? _watcher.Path : null;
        if (folder is not null) Watch(folder);
    }

    public void Dispose()
    {
        _flushTimer.Dispose();
        lock (_lock)
        {
            StopWatcherLocked();
            _parentWatcher?.Dispose();
            _parentWatcher = null;
        }
    }

    private void OnChanged(object sender, FileSystemEventArgs e) => Record(e.FullPath, WatcherChangeType.Modified);
    private void OnCreated(object sender, FileSystemEventArgs e) => Record(e.FullPath, WatcherChangeType.Created);
    private void OnDeleted(object sender, FileSystemEventArgs e) => Record(e.FullPath, WatcherChangeType.Deleted);

    private void OnRenamed(object sender, RenamedEventArgs e)
    {
        // Report both endpoints, same as HotReloadWatcher.AddWatcher's Renamed handler -- let the
        // coalescer's atomic-write detection collapse them back to Modified when a tool renames a
        // file onto itself as part of a save.
        Record(e.OldFullPath, WatcherChangeType.Deleted);
        Record(e.FullPath, WatcherChangeType.Created);
    }

    private void OnError(object sender, ErrorEventArgs e) => Overflowed?.Invoke();

    private void Record(string path, WatcherChangeType type)
    {
        if (!_pathFilter(path)) return;
        _coalescer.Record(path, type, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
    }

    private void FlushCoalescer()
    {
        var events = _coalescer.Flush(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        if (events.Count > 0)
            Changed?.Invoke(events);
    }

    private void StopWatcherLocked()
    {
        _flushTimer.Change(Timeout.Infinite, Timeout.Infinite);

        if (_watcher is null) return;

        _watcher.EnableRaisingEvents = false;
        _watcher.Changed -= OnChanged;
        _watcher.Created -= OnCreated;
        _watcher.Deleted -= OnDeleted;
        _watcher.Renamed -= OnRenamed;
        _watcher.Error -= OnError;
        _watcher.Dispose();
        _watcher = null;
    }
}
