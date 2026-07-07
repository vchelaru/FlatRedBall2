using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace AnimationEditor.Browser;

/// <summary>
/// #535 M3 follow-up: the browser has no <c>FileSystemWatcher</c> equivalent, and
/// <c>FileSystemObserver</c> (the one browser API that comes close) needs the native JS
/// <c>FileSystemDirectoryHandle</c>, which Avalonia's <see cref="IStorageFolder"/> keeps
/// internal (see docs/BROWSER_SPIKE_FINDINGS.md) -- there's no supported way to reach it from
/// here. Polls <see cref="IStorageFile.GetBasicPropertiesAsync"/> (Size/DateModified -- no need
/// to re-read file content to detect a change) on a timer instead, using the same
/// <see cref="IStorageFolder"/> handle Open Folder already granted, so there's no second
/// permission prompt.
/// </summary>
internal sealed class BrowserFolderWatcher : IDisposable
{
    private readonly IStorageFolder _folder;
    private readonly DispatcherTimer _timer;
    private Dictionary<string, FolderEntrySnapshot> _lastKnown = new();
    private bool _seeded;
    private bool _pollInFlight;
    private bool _disposed;

    /// <summary>Fires with the names of every .png whose size or last-modified time changed
    /// since the previous poll. Never fires on the very first poll (that just seeds the baseline).</summary>
    public event Action<IReadOnlyList<string>>? ChangedPngsDetected;

    public BrowserFolderWatcher(IStorageFolder folder, TimeSpan pollInterval)
    {
        _folder = folder;
        _timer = new DispatcherTimer { Interval = pollInterval };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    /// <summary>Seeds the baseline snapshot (reporting nothing) and starts polling.</summary>
    public async Task StartAsync()
    {
        await PollAsync();
        _timer.Start();
    }

    // Enumerating a folder and fetching each file's properties is async and can plausibly
    // outlast the poll interval (many files, a throttled background tab). DispatcherTimer
    // re-arms on its own schedule regardless of whether the previous tick's task finished, so
    // without this guard two overlapping polls would race on _lastKnown/_seeded.
    private async Task PollAsync()
    {
        if (_pollInFlight || _disposed) return;
        _pollInFlight = true;
        try
        {
            var current = new Dictionary<string, FolderEntrySnapshot>();
            await foreach (var item in _folder.GetItemsAsync())
            {
                if (item is not IStorageFile file) continue;
                if (!file.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;

                var props = await file.GetBasicPropertiesAsync();
                current[file.Name] = new FolderEntrySnapshot(props.Size, props.DateModified);
            }

            // Dispose() may have been called while the enumeration above was in flight (e.g.
            // the user opened a different folder mid-poll) -- don't publish a stale snapshot.
            if (_disposed) return;

            if (_seeded)
            {
                var changed = FolderSnapshotDiff.FindChanged(_lastKnown, current).ToList();
                if (changed.Count > 0)
                    ChangedPngsDetected?.Invoke(changed);
            }

            _seeded = true;
            _lastKnown = current;
        }
        finally
        {
            _pollInFlight = false;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer.Stop();
    }
}
