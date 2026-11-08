using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// The sequence preview's playheads: one <see cref="SequenceClock"/> per selected sequence, each running on its own and
/// looping by its own sequence's loop flag, the way each selected chain of an <c>.achx</c> tab does. There is no shared
/// clock and no realigning; sequences of different lengths drift apart.
/// </summary>
public sealed class SequencePlayback
{
    private readonly Dictionary<string, SequenceClock> _clocks = new();

    /// <summary>True while any selected sequence is still advancing.</summary>
    public bool IsPlaying => _clocks.Values.Any(clock => clock.IsPlaying);

    /// <summary>The slowest and fastest <see cref="Speed"/>, the range the <c>.achx</c> transport's Speed field allows.</summary>
    public const double MinSpeed = 0.1, MaxSpeed = 10;

    private double _speed = 1;

    /// <summary>Playback speed of every selected sequence (1 = runtime speed), held within <see cref="MinSpeed"/>..<see cref="MaxSpeed"/>.</summary>
    public double Speed
    {
        get => _speed;
        set => _speed = double.IsNaN(value) ? 1 : Math.Clamp(value, MinSpeed, MaxSpeed);
    }

    /// <summary>The playhead of sequence <paramref name="name"/>; 0 when it is not selected.</summary>
    public double TimeOf(string name) => _clocks.TryGetValue(name, out var clock) ? clock.Time : 0;

    /// <summary>
    /// Follows the selection. A selection that keeps none of the current sequences starts over, stopped at 0 (selecting
    /// another sequence starts it from its beginning). Otherwise the kept ones carry on untouched, and each added one
    /// starts at 0, playing when the kept ones are.
    /// </summary>
    public void Select(IReadOnlyList<string> names, Func<string, double> length)
    {
        bool keepsOne = names.Any(_clocks.ContainsKey);
        bool playing = IsPlaying;
        foreach (var gone in _clocks.Keys.Where(name => !names.Contains(name)).ToList())
            _clocks.Remove(gone);
        if (!keepsOne)
        {
            _clocks.Clear();
            playing = false;
        }
        foreach (var name in names)
        {
            if (_clocks.ContainsKey(name)) continue;
            var clock = new SequenceClock();
            _clocks[name] = clock;
            if (playing) clock.Play(length(name));
        }
    }

    /// <summary>Follows a rename so the sequence keeps its clock.</summary>
    public void Rename(string from, string to)
    {
        if (from == to || !_clocks.Remove(from, out var clock)) return;
        _clocks[to] = clock;
    }

    /// <summary>Plays every selected sequence; one that sits at its end starts again from 0.</summary>
    public void Play(Func<string, double> length)
    {
        foreach (var (name, clock) in _clocks) clock.Play(length(name));
    }

    public void Pause()
    {
        foreach (var clock in _clocks.Values) clock.Pause();
    }

    /// <summary>Pauses every sequence and rewinds it to 0.</summary>
    public void Stop()
    {
        foreach (var clock in _clocks.Values) clock.Stop();
    }

    /// <summary>Moves one sequence's playhead; the others keep theirs.</summary>
    public void Seek(string name, double time, double length)
    {
        if (_clocks.TryGetValue(name, out var clock)) clock.Seek(time, length);
    }

    /// <summary>Clamps every playhead into its sequence's length, after an edit shortened one.</summary>
    public void Clamp(Func<string, double> length)
    {
        foreach (var (name, clock) in _clocks) clock.Seek(clock.Time, length(name));
    }

    /// <summary>
    /// Advances every playing sequence by the real time since the last tick, scaled by <see cref="Speed"/>, each by its own
    /// length and loop flag. A stalled tick is capped before it is scaled.
    /// </summary>
    public void AdvanceTick(double elapsedSeconds, Func<string, double> length, Func<string, bool> loop)
    {
        foreach (var (name, clock) in _clocks) clock.AdvanceTick(elapsedSeconds, length(name), loop(name), Speed);
    }
}
