using System;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// Controls playback of an <see cref="AnimationChain{TFrame}"/> from an
/// <see cref="AnimationChainList{TFrame}"/>. Call <see cref="Play(string)"/>, advance with
/// <see cref="Update"/>, then read <see cref="CurrentFrame"/> to draw the current frame.
/// </summary>
/// <remarks>
/// One <see cref="AnimationPlayer{TFrame}"/> per on-screen sprite is the typical usage. The player
/// holds a reference to the <see cref="AnimationChainList{TFrame}"/> but does not own it.
/// </remarks>
public class AnimationPlayer<TFrame> where TFrame : AnimationFrameBase
{
    private readonly AnimationChainList<TFrame> _chains;
    private int _currentChainIndex = -1;
    private int _currentFrameIndex;
    // Which showing of the frame this is within a cycle (PlaybackTimeline visits), for frame events.
    private int _currentVisitIndex;
    // Cycle time (see PlaybackTimeline); under PingPong a cycle is twice the chain's forward length.
    private double _timeIntoAnimation;
    // Bumped whenever playback switches chain, so event dispatch can tell a handler replaced the animation.
    private int _playbackVersion;

    /// <summary>
    /// The frame currently being displayed, or <c>null</c> if no animation is playing.
    /// Read this in your Draw method.
    /// </summary>
    public TFrame? CurrentFrame
    {
        get
        {
            if (_currentChainIndex < 0) return null;
            var chain = _chains[_currentChainIndex];
            return chain.Count > 0 ? chain[_currentFrameIndex] : null;
        }
    }

    /// <summary>
    /// The frame offset to draw at the current playback time, with keyframe interpolation applied
    /// (see <see cref="KeyframeEvaluator"/>). Equals <see cref="CurrentFrame"/>'s own
    /// <c>RelativeX</c>/<c>RelativeY</c> unless the file stores an offset curve. (0, 0) when nothing is playing.
    /// </summary>
    public (float X, float Y) CurrentOffset =>
        CurrentChain is { Count: > 0 } chain
            ? KeyframeEvaluator.EvaluateOffset(chain, GlidesAcrossLoop, _currentFrameIndex, ElapsedInCurrentFrame(chain))
            : (0f, 0f);

    /// <summary>
    /// The frame color channels to draw at the current playback time, with keyframe interpolation
    /// applied. Equals <see cref="CurrentFrame"/>'s own channels unless the file stores a color curve.
    /// All <c>null</c> when nothing is playing.
    /// </summary>
    public KeyframeColor CurrentColor =>
        CurrentChain is { Count: > 0 } chain
            ? KeyframeEvaluator.EvaluateColor(chain, GlidesAcrossLoop, _currentFrameIndex, ElapsedInCurrentFrame(chain))
            : default;

    /// <summary>The currently playing <see cref="AnimationChain{TFrame}"/>, or <c>null</c> if none.</summary>
    public AnimationChain<TFrame>? CurrentChain =>
        _currentChainIndex >= 0 ? _chains[_currentChainIndex] : null;

    /// <summary>Name of the currently playing chain, or <c>null</c> if none is active.</summary>
    public string? CurrentChainName => CurrentChain?.Name;

    private bool GlidesAcrossLoop => PlaybackTimeline.GlidesAcrossLoop(IsLooping, IsPingPong);

    /// <summary>
    /// Current frame index within <see cref="CurrentChain"/>. Setting it seeks to the moment the frame starts
    /// showing in the first half of the cycle (under <see cref="IsReversed"/>, its forward end).
    /// </summary>
    /// <exception cref="InvalidOperationException">No non-empty chain is active.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Value is outside the current chain's frame range.</exception>
    public int CurrentFrameIndex
    {
        get => _currentFrameIndex;
        set
        {
            var chain = RequireCurrentPlayableChain();
            if ((uint)value >= (uint)chain.Count)
                throw new ArgumentOutOfRangeException(nameof(value));

            double forward = GetTimeAtFrameStart(chain, value) + (IsReversed ? chain[value].FrameLength.TotalSeconds : 0);
            _currentFrameIndex = value;
            _timeIntoAnimation = PlaybackTimeline.ToCycleTime(forward, chain.TotalLength.TotalSeconds, IsReversed);
            _currentVisitIndex = IsReversed ? chain.Count - 1 - value : value;
        }
    }

    /// <summary>
    /// Elapsed time into the current cycle (see <see cref="IsPingPong"/>: a ping-pong cycle is twice the chain's length).
    /// Setting this seeks playback time and updates <see cref="CurrentFrameIndex"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">No non-empty chain is active.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Value is negative.</exception>
    public TimeSpan TimeIntoAnimation
    {
        get => TimeSpan.FromSeconds(_timeIntoAnimation);
        set
        {
            if (value < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value));

            var chain = RequireCurrentPlayableChain();
            SeekToTime(chain, value.TotalSeconds);
        }
    }

    /// <summary>When <c>true</c> (the default), <see cref="Update"/> advances frames.</summary>
    public bool Animate { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, the animation wraps back to frame 0 at the end. Seeded from the played
    /// chain's <see cref="AnimationChain{TFrame}.Loop"/> each time <see cref="Play(string)"/> or
    /// <see cref="Play(AnimationChain{TFrame})"/> switches to a new chain; set this afterward to
    /// override per-instance.
    /// </summary>
    public bool IsLooping { get; set; } = true;

    /// <summary>
    /// When <c>true</c>, each cycle plays the chain forward and then mirrored (backward), so a cycle is twice the
    /// chain's length and the end frames show for twice their length across the turnaround. Keyframe glides play
    /// backward on the way back. When <see cref="IsLooping"/> is <c>false</c> one full cycle plays, then it stops.
    /// Seeded from <see cref="AnimationChain{TFrame}.PingPong"/> by <see cref="Play(string)"/> like
    /// <see cref="IsLooping"/>; set it afterward to override per instance. See <see cref="PlaybackTimeline"/>.
    /// </summary>
    public bool IsPingPong { get; set; }

    /// <summary>
    /// When <c>true</c>, the chain's forward timeline plays mirrored: it starts at the end of the last frame and runs
    /// to the start of the first, keyframe glides included. With <see cref="IsPingPong"/> the mirrored half comes
    /// first. Seeded from <see cref="AnimationChain{TFrame}.Reversed"/> by <see cref="Play(string)"/> like
    /// <see cref="IsLooping"/>; set it afterward to override per instance.
    /// </summary>
    public bool IsReversed { get; set; }

    /// <summary>Multiplier applied to frame time. 2.0 plays twice as fast; 0.5 plays half-speed.</summary>
    public float AnimationSpeed { get; set; } = 1f;

    /// <summary>Raised once when a non-looping animation reaches the end of its cycle.</summary>
    public event Action? AnimationFinished;

    /// <summary>
    /// Raised for each <see cref="AnimationFrameBase.Events"/> entry when playback enters its frame (in either direction;
    /// a ping-pong's turnaround and loop point keep the same frame, so they raise nothing):
    /// <see cref="Play(string)"/> switching chains enters the first frame shown (the last frame under
    /// <see cref="IsReversed"/>) immediately; a large
    /// <see cref="Update"/> delta raises every skipped frame's events in order, ending with the frame
    /// it lands on; a loop wrap re-enters frame 0, and a delta spanning more than one loop raises
    /// each frame once; a non-looping chain's last-frame events are raised before
    /// <see cref="AnimationFinished"/>. A handler that switches chain stops the rest of the old
    /// chain's events for that update. <see cref="Reset"/>, <see cref="Stop"/>, and seeking raise nothing.
    /// </summary>
    public event Action<FlatRedBall2.Animation.AnimationFrameEvent>? FrameEventRaised;

    /// <param name="chains">The animation list to play from. May be empty; <see cref="Play(string)"/> will no-op.</param>
    public AnimationPlayer(AnimationChainList<TFrame> chains)
    {
        _chains = chains ?? throw new ArgumentNullException(nameof(chains));
    }

    /// <summary>
    /// Starts playing the chain named <paramref name="chainName"/> from frame 0. If the chain
    /// is already playing, this is a no-op (no restart). If the chain name is not found, the
    /// call is silently ignored and the current animation continues.
    /// </summary>
    public void Play(string chainName)
    {
        for (int i = 0; i < _chains.Count; i++)
        {
            if (_chains[i].Name == chainName)
            {
                if (_currentChainIndex == i) return; // already playing — no restart
                _currentChainIndex = i;
                Animate = true;
                SeedFrom(_chains[i]);
                ResetPlaybackPosition();
                RaiseFrameZeroEvents();
                return;
            }
        }
    }

    /// <summary>
    /// Starts playing the specified <paramref name="chain"/> from frame 0. If the chain is
    /// already playing, this is a no-op.
    /// </summary>
    public void Play(AnimationChain<TFrame> chain)
    {
        ArgumentNullException.ThrowIfNull(chain);

        for (int i = 0; i < _chains.Count; i++)
        {
            if (ReferenceEquals(_chains[i], chain))
            {
                if (_currentChainIndex == i) return;
                _currentChainIndex = i;
                Animate = true;
                SeedFrom(chain);
                ResetPlaybackPosition();
                RaiseFrameZeroEvents();
                return;
            }
        }
    }

    /// <summary>Pauses playback at the current frame and time.</summary>
    public void Pause() => Animate = false;

    /// <summary>Resumes playback from the current frame and time.</summary>
    public void Resume()
    {
        if (_currentChainIndex < 0) return;
        Animate = true;
    }

    /// <summary>Rewinds to the first frame and pauses playback.</summary>
    public void Stop()
    {
        Reset();
        Animate = false;
    }

    /// <summary>Rewinds to the first frame without changing <see cref="Animate"/>.</summary>
    public void Reset() => ResetPlaybackPosition();

    /// <summary>
    /// Advances the animation by <paramref name="elapsed"/> real time (scaled by
    /// <see cref="AnimationSpeed"/>). Call this once per game Update tick.
    /// </summary>
    public void Update(TimeSpan elapsed)
    {
        if (!Animate || _currentChainIndex < 0) return;

        var chain = _chains[_currentChainIndex];
        if (chain.Count == 0) return;

        _timeIntoAnimation += elapsed.TotalSeconds * AnimationSpeed;

        double totalLength = PlaybackTimeline.CycleLength(chain.TotalLength.TotalSeconds, IsPingPong);
        if (totalLength <= 0) return;

        int previousVisitIndex = _currentVisitIndex;
        int wraps = 0;
        bool finished = false;
        if (IsLooping)
        {
            while (_timeIntoAnimation >= totalLength)
            {
                _timeIntoAnimation -= totalLength;
                wraps++;
            }
        }
        else
        {
            if (_timeIntoAnimation >= totalLength)
            {
                _timeIntoAnimation = totalLength;
                Animate = false;
                finished = true;
            }
        }

        UpdateFrameIndexFromTime(chain);

        int playbackVersion = _playbackVersion;
        if (FrameEventRaised != null)
        {
            int visits = PlaybackTimeline.VisitCount(chain.Count, IsPingPong);
            var (start, length) = FrameEventRange.Get(previousVisitIndex, _currentVisitIndex, wraps, visits);
            for (int j = 0; j < length; j++)
            {
                int visit = (start + j) % visits;
                if (!PlaybackTimeline.EntersNewFrame(visit, chain.Count, IsPingPong, IsReversed)) continue;
                if (!RaiseEvents(chain[PlaybackTimeline.FrameOfVisit(visit, chain.Count, IsPingPong, IsReversed)], playbackVersion))
                    break;
            }
        }
        if (finished && playbackVersion == _playbackVersion)
            AnimationFinished?.Invoke();
    }

    private void RaiseFrameZeroEvents()
    {
        _playbackVersion++;
        var chain = _chains[_currentChainIndex];
        if (chain.Count > 0)
            RaiseEvents(chain[_currentFrameIndex], _playbackVersion);
    }

    private void SeedFrom(AnimationChain<TFrame> chain)
    {
        IsLooping = chain.Loop;
        IsPingPong = chain.PingPong;
        IsReversed = chain.Reversed;
    }

    // Returns false once a handler has switched chain, so the caller stops raising the old chain's events.
    private bool RaiseEvents(TFrame frame, int playbackVersion)
    {
        var events = frame.Events;
        for (int k = 0; k < events.Count; k++)
        {
            FrameEventRaised?.Invoke(events[k]);
            if (playbackVersion != _playbackVersion) return false;
        }
        return true;
    }

    private void ResetPlaybackPosition()
    {
        _timeIntoAnimation = 0;
        _currentVisitIndex = 0;
        _currentFrameIndex = 0;
        if (_currentChainIndex >= 0 && _chains[_currentChainIndex].Count > 0)
            UpdateFrameIndexFromTime(_chains[_currentChainIndex]);
    }

    private AnimationChain<TFrame> RequireCurrentPlayableChain()
    {
        if (_currentChainIndex < 0)
            throw new InvalidOperationException("No animation chain is currently active.");

        var chain = _chains[_currentChainIndex];
        if (chain.Count == 0)
            throw new InvalidOperationException("The current animation chain has no frames.");

        return chain;
    }

    private void SeekToTime(AnimationChain<TFrame> chain, double seconds)
    {
        double totalLength = PlaybackTimeline.CycleLength(chain.TotalLength.TotalSeconds, IsPingPong);
        if (totalLength <= 0)
        {
            _timeIntoAnimation = 0;
            UpdateFrameIndexFromTime(chain);
            return;
        }

        _timeIntoAnimation = IsLooping
            ? seconds % totalLength
            : seconds >= totalLength ? totalLength : seconds;

        UpdateFrameIndexFromTime(chain);
    }

    private PlaybackPosition Locate(AnimationChain<TFrame> chain) =>
        PlaybackTimeline.Locate(chain, static f => f.FrameLength.TotalSeconds, _timeIntoAnimation, IsPingPong, IsReversed);

    private void UpdateFrameIndexFromTime(AnimationChain<TFrame> chain)
    {
        var at = Locate(chain);
        _currentFrameIndex = at.FrameIndex;
        _currentVisitIndex = at.Visit;
    }

    // Forward seconds into the current frame at the current cycle time (falls while the timeline plays mirrored).
    private double ElapsedInCurrentFrame(AnimationChain<TFrame> chain)
    {
        double forward = PlaybackTimeline.Map(_timeIntoAnimation, chain.TotalLength.TotalSeconds, IsPingPong, IsReversed).ForwardTime;
        return Math.Clamp(forward - GetTimeAtFrameStart(chain, _currentFrameIndex), 0, chain[_currentFrameIndex].FrameLength.TotalSeconds);
    }

    private static double GetTimeAtFrameStart(AnimationChain<TFrame> chain, int frameIndex)
    {
        double time = 0;
        for (int i = 0; i < frameIndex; i++)
            time += chain[i].FrameLength.TotalSeconds;
        return time;
    }
}
