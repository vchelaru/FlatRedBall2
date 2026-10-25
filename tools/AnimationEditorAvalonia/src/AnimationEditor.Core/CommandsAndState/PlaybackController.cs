using FlatRedBall2.AnimationEditorCommon;
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
    // Position in the chain's PingPong/Reverse visit sequence (PlaybackOrder); _currentFrameIndex is the frame it shows.
    private int _currentVisitIndex;
    private AnimationChainSave? _chain;

    private double _frameStartTime;

    // ── Public state ──────────────────────────────────────────────────────────

    /// <summary>The chain currently being played, or <c>null</c> when none is set.</summary>
    public AnimationChainSave? Chain => _chain;

    /// <summary>Current frame index into the active chain.</summary>
    public int CurrentFrameIndex => _currentFrameIndex;

    /// <summary>
    /// Whether the current frame is playing backward: the chain's <see cref="AnimationChainSave.Reverse"/>, or the
    /// back half of a <see cref="AnimationChainSave.PingPong"/> cycle (#1538). Sample keyframe curves with it.
    /// </summary>
    public bool IsBackward => _chain is { Frames.Count: > 0 } chain && Visit(chain, _currentVisitIndex).IsBackward;

    /// <summary>Accumulated playback time in seconds (wraps at one pass of the chain, a full round trip under PingPong).</summary>
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

    // Backing value used only while no chain is set (Loop has nowhere to persist to yet).
    private bool _loopWithoutChain = true;

    /// <summary>
    /// When <c>true</c> (the default), <see cref="Advance"/> wraps back to the start once the
    /// chain's total duration elapses. When <c>false</c>, playback freezes on the last frame and
    /// pauses (firing <see cref="IsPlayingChanged"/>) instead of wrapping.
    /// <para>
    /// This is a live proxy onto the active chain's own <see cref="AnimationChainSave.Loop"/>
    /// (issue #1120) -- reading it returns whatever <see cref="Chain"/> has persisted, and setting
    /// it writes straight back into that same chain, so any other UI bound to the same chain (e.g.
    /// the Inspector's Loop checkbox) can never disagree with this control. Switching chains via
    /// <see cref="SetChain"/> automatically reflects the newly selected chain's own value -- no
    /// explicit reseeding needed. When no chain is set, falls back to a local default so
    /// <see cref="Advance"/> still has a sensible value to read.
    /// </para>
    /// </summary>
    public bool Loop
    {
        get => _chain?.Loop ?? _loopWithoutChain;
        set
        {
            if (_chain is not null)
                _chain.Loop = value;
            else
                _loopWithoutChain = value;
        }
    }

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

    /// <summary>
    /// Resume playback (undoes <see cref="Pause"/>). When <see cref="Loop"/> is <c>false</c> and
    /// playback had already reached the end (frozen on the last frame), restarts from the
    /// beginning instead of resuming a no-op animation — so repeatedly pressing play/pause acts
    /// like a replay button instead of requiring a manual seek back to the start first.
    /// </summary>
    public void Play()
    {
        if (IsPlaying) return;

        if (!Loop && _chain is not null && _chain.Frames.Count > 0)
        {
            double totalTime = PassLength(_chain);
            if (totalTime > 0 && _animTime >= totalTime)
                Reset();
        }

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
    /// valid ranges; a no-op when no chain is set. Under PingPong/Reverse it seeks to the first time
    /// the frame is shown. <paramref name="fraction"/> is through time; a timeline click measures left to right
    /// across the cell, so use <see cref="SeekToCell"/> for that. Fires <see cref="FrameIndexChanged"/> if the
    /// frame changed and always fires <see cref="PlaybackTicked"/>.
    /// </summary>
    public void SeekToFrame(int frameIndex, double fraction = 0)
    {
        var chain = _chain;
        if (chain is null || chain.Frames.Count == 0) return;

        int idx = Math.Clamp(frameIndex, 0, chain.Frames.Count - 1);
        double frac = Math.Clamp(fraction, 0.0, 1.0);

        int visits = PlaybackOrder.VisitCount(chain.Frames.Count, chain.Loop, chain.PingPong);
        double start = 0;
        int visit = 0;
        for (; visit < visits; visit++)
        {
            int frame = Visit(chain, visit).FrameIndex;
            if (frame == idx) break;
            start += FrameLength(chain.Frames[frame]);
        }
        _currentVisitIndex = visit;

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

    /// <summary>
    /// Timeline scrubbing: seeks to <paramref name="frameIndex"/> at <paramref name="cellFraction"/> across its cell (0 = left
    /// edge). A frame that plays backward (#1538) runs from its cell's right edge, so the fraction is mirrored into time;
    /// otherwise this is <see cref="SeekToFrame"/>.
    /// </summary>
    public void SeekToCell(int frameIndex, double cellFraction)
    {
        var chain = _chain;
        if (chain is null || chain.Frames.Count == 0) return;
        int idx = Math.Clamp(frameIndex, 0, chain.Frames.Count - 1);
        int visits = PlaybackOrder.VisitCount(chain.Frames.Count, chain.Loop, chain.PingPong);
        bool backward = false;
        for (int v = 0; v < visits; v++)
        {
            var visit = Visit(chain, v);
            if (visit.FrameIndex != idx) continue;
            backward = visit.IsBackward;
            break;
        }
        SeekToFrame(idx, backward ? 1 - Math.Clamp(cellFraction, 0, 1) : cellFraction);
    }

    /// <summary>
    /// Reset time to zero and the frame to the first one shown (frame 0, or the last frame under Reverse) without
    /// changing the active chain.
    /// </summary>
    public void Reset()
    {
        _animTime = 0;
        _frameStartTime = 0;
        _currentVisitIndex = 0;
        _currentFrameIndex = _chain is { Frames.Count: > 0 } chain ? Visit(chain, 0).FrameIndex : 0;
        FrameIndexChanged?.Invoke(_currentFrameIndex);
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

        double totalTime = PassLength(chain);
        if (totalTime <= 0) return;

        _animTime += deltaSeconds * SpeedMultiplier;

        bool reachedEnd = _animTime >= totalTime;
        if (reachedEnd)
            _animTime = Loop ? _animTime % totalTime : totalTime;

        int visits = PlaybackOrder.VisitCount(chain.Frames.Count, chain.Loop, chain.PingPong);
        double t = 0;
        int newVisit = visits - 1;
        int newIdx = Visit(chain, newVisit).FrameIndex;
        double newFrameStart = totalTime - FrameLength(chain.Frames[newIdx]);
        for (int v = 0; v < visits; v++)
        {
            int frame = Visit(chain, v).FrameIndex;
            double fl = FrameLength(chain.Frames[frame]);
            if (_animTime < t + fl) { newVisit = v; newIdx = frame; newFrameStart = t; break; }
            t += fl;
        }
        _currentVisitIndex = newVisit;

        // Update _frameStartTime before events so FrameElapsed is already correct
        // when FrameIndexChanged and PlaybackTicked handlers run.
        _frameStartTime = newFrameStart;

        if (newIdx != _currentFrameIndex)
        {
            _currentFrameIndex = newIdx;
            FrameIndexChanged?.Invoke(newIdx);
        }

        PlaybackTicked?.Invoke();

        // Pausing after the tick so subscribers see the final frame/time before the transport
        // button flips to "play" — matches how a manual Pause() behaves mid-playback.
        if (reachedEnd && !Loop) Pause();
    }

    private static PlaybackVisit Visit(AnimationChainSave chain, int visitIndex) =>
        PlaybackOrder.VisitAt(visitIndex, chain.Frames.Count, chain.PingPong, chain.Reverse);

    private static double PassLength(AnimationChainSave chain) => Rendering.ChainClock.TotalLength(chain);

    // Zero/negative authored lengths fall back to 100 ms so playback still steps through them.
    internal static double FrameLength(AnimationFrameSave frame) =>
        frame.FrameLength > 0 ? frame.FrameLength : 0.1;
}
