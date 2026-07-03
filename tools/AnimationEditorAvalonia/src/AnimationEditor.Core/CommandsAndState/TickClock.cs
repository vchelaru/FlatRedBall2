using System;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Converts a monotonic timestamp source (e.g. <c>Stopwatch.GetTimestamp</c>) into the
/// real seconds elapsed between successive <see cref="Tick"/> calls.
/// <para>
/// Playback must advance by true wall-clock time, not by an assumed fixed step: a UI
/// timer set to "16 ms" actually fires later than that (OS timer resolution + UI-thread
/// work), so crediting a hardcoded 16 ms per tick makes an animation run slow. Feeding
/// <see cref="Tick"/>'s real delta into the playback controller keeps it stopwatch-accurate.
/// </para>
/// </summary>
public class TickClock
{
    private readonly Func<long> _timestamp;
    private readonly double _secondsPerTick;
    private long _last;
    private bool _hasBaseline;

    /// <param name="timestamp">Monotonic tick counter, e.g. <c>Stopwatch.GetTimestamp</c>.</param>
    /// <param name="frequency">Ticks per second for <paramref name="timestamp"/>, e.g. <c>Stopwatch.Frequency</c>.</param>
    public TickClock(Func<long> timestamp, long frequency)
    {
        _timestamp = timestamp;
        _secondsPerTick = 1.0 / frequency;
    }

    /// <summary>
    /// Real seconds since the previous <see cref="Tick"/>. Returns 0 on the first call
    /// after construction or <see cref="Reset"/>, since there is no prior baseline.
    /// </summary>
    public double Tick()
    {
        long now = _timestamp();
        if (!_hasBaseline)
        {
            _last = now;
            _hasBaseline = true;
            return 0;
        }

        double delta = (now - _last) * _secondsPerTick;
        _last = now;
        return delta;
    }

    /// <summary>
    /// Drops the baseline so the next <see cref="Tick"/> returns 0. Call when resuming
    /// after a pause so the paused span is not credited as one huge delta.
    /// </summary>
    public void Reset() => _hasBaseline = false;
}
