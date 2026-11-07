using FlatRedBall2.AnimationEditorCommon;
using System;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// The sequence preview's playhead: a time in seconds and whether it is advancing. The length and loop flag are passed
/// on each call because edits change them while the clock runs.
/// </summary>
public sealed class SequenceClock
{
    public double Time { get; private set; }

    public bool IsPlaying { get; private set; }

    /// <summary>Starts advancing. At the end, starts again from 0. Does nothing for an empty sequence.</summary>
    public void Play(double length)
    {
        if (length <= 0) return;
        if (Time >= length) Time = 0;
        IsPlaying = true;
    }

    public void Pause() => IsPlaying = false;

    /// <summary>Pauses and rewinds to 0.</summary>
    public void Stop()
    {
        IsPlaying = false;
        Time = 0;
    }

    /// <summary>Moves the playhead to <paramref name="time"/>, clamped to 0..<paramref name="length"/>.</summary>
    public void Seek(double time, double length) => Time = Math.Clamp(time, 0, Math.Max(0, length));

    /// <summary>
    /// Advances by <paramref name="seconds"/> while playing. Past the end it wraps when <paramref name="loop"/>, and
    /// otherwise stops at the end.
    /// </summary>
    public void Advance(double seconds, double length, bool loop)
    {
        if (!IsPlaying) return;
        double next = Time + seconds;
        if (next >= length && !loop) IsPlaying = false;
        Time = SequenceTimeline.WrapTime(next, length, loop);
    }
}
