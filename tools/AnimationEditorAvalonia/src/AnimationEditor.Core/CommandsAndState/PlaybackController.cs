using FlatRedBall2.Animation.Content;
using System;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Pure playback state machine: tracks animation time, advances the current frame
/// index, and fires <see cref="FrameIndexChanged"/> when the frame changes.
/// <para>
/// Has no timer or threading dependency — callers tick it by calling
/// <see cref="Advance"/> from whatever timer they control. This makes the
/// controller fully unit-testable without a running event loop.
/// </para>
/// </summary>
public class PlaybackController
{
    private double _animTime;
    private int _currentFrameIndex;
    private AnimationChainSave? _chain;

    private double _frameStartTime;

    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>The chain currently being played, or <c>null</c> when none is set.</summary>
    public AnimationChainSave? Chain => _chain;

    /// <summary>Current frame index into the active chain.</summary>
    public int CurrentFrameIndex => _currentFrameIndex;

    /// <summary>Accumulated playback time in seconds (wraps at chain total time).</summary>
    public double AnimTime => _animTime;

    /// <summary>
    /// Elapsed time within the current frame, in seconds. Resets to zero each time
    /// the frame advances or <see cref="Reset"/> is called.
    /// </summary>
    public double FrameElapsed => _animTime - _frameStartTime;

    /// <summary>
    /// When <c>false</c>, <see cref="Advance"/> is a no-op so the animation is paused.
    /// Defaults to <c>true</c>.
    /// </summary>
    public bool IsPlaying { get; private set; } = true;

    /// <summary>
    /// Multiplier applied to the delta inside <see cref="Advance"/>.
    /// 1.0 = normal speed, 2.0 = double, 0.5 = half.
    /// </summary>
    public double SpeedMultiplier { get; set; } = 1.0;

    // ── Events ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fired whenever the current frame index changes.
    /// The argument is the new frame index.
    /// </summary>
    public event Action<int>? FrameIndexChanged;

    /// <summary>
    /// Fired on every <see cref="Advance"/> call while playing, and on <see cref="Reset"/>.
    /// Subscribers can read <see cref="FrameElapsed"/> to drive smooth per-frame UI.
    /// </summary>
    public event Action? PlaybackTicked;

    /// <summary>
    /// Fired only when <see cref="IsPlaying"/> actually changes value, so a transport
    /// button can keep its play/pause icon in sync. The argument is the new state.
    /// </summary>
    public event Action<bool>? IsPlayingChanged;

    // ── Commands ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Set the animation chain to play. Resets time and frame index to zero.
    /// Pass <c>null</c> to stop playback.
    /// </summary>
    public void SetChain(AnimationChainSave? chain)
    {
        _chain = chain;
        Reset();
    }

    /// <summary>Resume playback (undoes <see cref="Pause"/>).</summary>
    public void Play()
    {
        if (IsPlaying) return;
        IsPlaying = true;
        IsPlayingChanged?.Invoke(true);
    }

    /// <summary>Pause playback at the current frame without resetting time or frame index.</summary>
    public void Pause()
    {
        if (!IsPlaying) return;
        IsPlaying = false;
        IsPlayingChanged?.Invoke(false);
    }

    /// <summary>
    /// Jumps playback to <paramref name="frameIndex"/> at <paramref name="fraction"/> of the way
    /// through that frame (0 = its start, 1 = its end), without changing <see cref="IsPlaying"/>.
    /// Used by timeline scrubbing, which seeks while paused. Index and fraction are clamped to
    /// valid ranges; a no-op when no chain is set. Fires <see cref="FrameIndexChanged"/> if the
    /// frame changed and always fires <see cref="PlaybackTicked"/>.
    /// </summary>
    public void SeekToFrame(int frameIndex, double fraction = 0)
    {
        var chain = _chain;
        if (chain is null || chain.Frames.Count == 0) return;

        int idx = Math.Clamp(frameIndex, 0, chain.Frames.Count - 1);
        double frac = Math.Clamp(fraction, 0.0, 1.0);

        double start = 0;
        for (int i = 0; i < idx; i++)
            start += FrameLength(chain.Frames[i]);

        // Set _frameStartTime before events so FrameElapsed is correct inside handlers.
        _frameStartTime = start;
        _animTime = start + frac * FrameLength(chain.Frames[idx]);

        if (idx != _currentFrameIndex)
        {
            _currentFrameIndex = idx;
            FrameIndexChanged?.Invoke(idx);
        }

        PlaybackTicked?.Invoke();
    }

    /// <summary>Reset time and frame index to zero without changing the active chain.</summary>
    public void Reset()
    {
        _animTime = 0;
        _frameStartTime = 0;
        _currentFrameIndex = 0;
        FrameIndexChanged?.Invoke(0);
        PlaybackTicked?.Invoke();
    }

    /// <summary>
    /// Advance the animation by <paramref name="deltaSeconds"/>.
    /// Call this from a timer tick; the method is a no-op when no chain is set
    /// or the chain has only one frame.
    /// </summary>
    public void Advance(double deltaSeconds)
    {
        if (!IsPlaying) return;

        var chain = _chain;
        if (chain is null || chain.Frames.Count <= 1) return;

        _animTime += deltaSeconds * SpeedMultiplier;

        double totalTime = 0;
        foreach (var f in chain.Frames)
            totalTime += FrameLength(f);
        if (totalTime <= 0) return;

        _animTime %= totalTime;

        double t = 0;
        int newIdx = chain.Frames.Count - 1;
        double newFrameStart = 0;
        for (int i = 0; i < chain.Frames.Count; i++)
        {
            double fl = FrameLength(chain.Frames[i]);
            if (_animTime < t + fl) { newIdx = i; newFrameStart = t; break; }
            t += fl;
        }

        // Update _frameStartTime before events so FrameElapsed is already correct
        // when FrameIndexChanged and PlaybackTicked handlers run.
        _frameStartTime = newFrameStart;

        if (newIdx != _currentFrameIndex)
        {
            _currentFrameIndex = newIdx;
            FrameIndexChanged?.Invoke(newIdx);
        }

        PlaybackTicked?.Invoke();
    }

    // Zero/negative authored lengths fall back to 100 ms so playback still steps through them.
    private static double FrameLength(AnimationFrameSave frame) =>
        frame.FrameLength > 0 ? frame.FrameLength : 0.1;
}
