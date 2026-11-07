using System;
using System.Diagnostics;
using System.Threading;

namespace AnimationEditor.App.Controls;

/// <summary>What the F4 key makes the timeline skip, to find out which part of a frame costs the time. Diagnostics only.</summary>
public enum TimelineProbeMode
{
    /// <summary>Draw everything, each filmstrip as batched draws.</summary>
    Normal,

    /// <summary>Draw everything, each filmstrip cell with its own draws (the earlier shape), to compare against <see cref="Normal"/>.</summary>
    PerCellDraws,

    /// <summary>Draw everything except the filmstrips.</summary>
    NoFilmstrips,

    /// <summary>Draw neither the filmstrips nor any text.</summary>
    NoFilmstripsOrText,

    /// <summary>Draw only the background.</summary>
    BackgroundOnly,

    /// <summary>Draw everything, but make the render thread wait for the GPU after each filmstrip, so its time is the GPU's real time.</summary>
    WaitForGpuAfterFilmstrips,
}

/// <summary>
/// What the filmstrip operation sees on the render thread: how long its draws take, how much of Skia's GPU resource cache
/// is in use against its budget, and whether that use changes from one operation to the next (a cache that keeps
/// changing is uploading textures again). Written by the render thread, read by the F3 overlay.
/// </summary>
internal sealed class FilmstripProbe
{
    private long _ticks;
    private long _changes;
    private long _lastBytes = -1;

    /// <summary>The GPU resource cache's last reading: resources, bytes used, budget. All zero on the CPU path.</summary>
    public volatile int Resources;
    public long Bytes;
    public long Limit;

    /// <summary>Called by an operation that drew in <paramref name="elapsedTicks"/> and saw this cache state.</summary>
    public void Report(long elapsedTicks, int resources, long bytes, long limit)
    {
        Interlocked.Add(ref _ticks, elapsedTicks);
        if (_lastBytes >= 0 && bytes != _lastBytes) Interlocked.Increment(ref _changes);
        _lastBytes = bytes;
        Resources = resources;
        Bytes = bytes;
        Limit = limit;
    }

    /// <summary>The filmstrip drawing time and cache changes since the last call, in milliseconds and count.</summary>
    public (double Milliseconds, long Changes) Take() =>
        (Interlocked.Exchange(ref _ticks, 0) * 1000.0 / Stopwatch.Frequency, Interlocked.Exchange(ref _changes, 0));
}
