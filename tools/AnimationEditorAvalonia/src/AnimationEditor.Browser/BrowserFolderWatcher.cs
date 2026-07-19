using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;
using Avalonia.Threading;

namespace AnimationEditor.Browser;

/// <summary>
/// #535 M3 follow-up: the browser has no <c>FileSystemWatcher</c> equivalent, and
/// <c>FileSystemObserver</c> (the one browser API that comes close) still needs polyfilling in
/// most browsers. Polls <see cref="IEditorFile.GetBasicPropertiesAsync"/>
/// (Size/DateModified -- no need to re-read file content to detect a change) on a timer
/// instead, using the same <see cref="IEditorFolder"/> handle Open Folder already granted, so
/// there's no second permission prompt.
/// </summary>
internal sealed class BrowserFolderWatcher : IDisposable
{
    private readonly IEditorFolder _folder;
    private readonly DispatcherTimer _timer;
    private Dictionary<string, FolderEntrySnapshot> _lastKnown = new();
    private bool _seeded;
    private bool _pollInFlight;
    private bool _disposed;
    private bool _enumerationFailed;

    /// <summary>Fires with the names of every .png whose size or last-modified time changed
    /// since the previous poll. Never fires on the very first poll (that just seeds the baseline).</summary>
    public event Action<IReadOnlyList<string>>? ChangedPngsDetected;

    public BrowserFolderWatcher(IEditorFolder folder, TimeSpan pollInterval)
    {
        _folder = folder;
        _timer = new DispatcherTimer { Interval = pollInterval };
        _timer.Tick += async (_, _) => await PollAsync();
    }

    /// <summary>Seeds the baseline snapshot (reporting nothing) and starts polling -- a no-op if
    /// the seeding poll already found enumeration unsupported in this environment (#763).</summary>
    public async Task StartAsync()
    {
        await PollAsync();
        if (!_enumerationFailed)
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
            await foreach (var file in _folder.GetItemsAsync())
            {
                if (!file.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                current[file.Name] = await file.GetBasicPropertiesAsync();
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
        catch (JSException ex)
        {
            // Same enumeration failure Open Folder itself can hit (#763) -- WASM is
            // single-threaded, so letting this escape the DispatcherTimer.Tick handler would
            // abort the whole runtime, not just watching. Watching this folder just isn't
            // possible this session; disable it instead of retrying every tick forever.
            Console.WriteLine($"[OpenFolder] folder watch disabled -- enumeration failed: {ex.Message}");
            _enumerationFailed = true;
            _timer.Stop();
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
