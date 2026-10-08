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

    /// <summary>The currently playing <see cref="AnimationChain{TFrame}"/>, or <c>null</c> if none.</summary>
    public AnimationChain<TFrame>? CurrentChain =>
        _currentChainIndex >= 0 ? _chains[_currentChainIndex] : null;

    /// <summary>Name of the currently playing chain, or <c>null</c> if none is active.</summary>
    public string? CurrentChainName => CurrentChain?.Name;

    /// <summary>
    /// Current frame index within <see cref="CurrentChain"/>.
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

            _currentFrameIndex = value;
            _timeIntoAnimation = GetTimeAtFrameStart(chain, value);
        }
    }

    /// <summary>
    /// Elapsed time into the current animation.
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

    /// <summary>Multiplier applied to frame time. 2.0 plays twice as fast; 0.5 plays half-speed.</summary>
    public float AnimationSpeed { get; set; } = 1f;

    /// <summary>Raised once when a non-looping animation reaches its last frame.</summary>
    public event Action? AnimationFinished;

    /// <summary>
    /// Raised for each <see cref="AnimationFrameBase.Events"/> entry when playback enters its frame:
    /// <see cref="Play(string)"/> switching chains enters frame 0 immediately; a large
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
                ResetPlaybackPosition();
                Animate = true;
                IsLooping = _chains[i].Loop;
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
                ResetPlaybackPosition();
                Animate = true;
                IsLooping = chain.Loop;
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

        double totalLength = chain.TotalLength.TotalSeconds;
        if (totalLength <= 0) return;

        int previousFrameIndex = _currentFrameIndex;
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
            var (start, length) = FrameEventRange.Get(previousFrameIndex, _currentFrameIndex, wraps, chain.Count);
            for (int j = 0; j < length; j++)
                if (!RaiseEvents(chain[(start + j) % chain.Count], playbackVersion))
                    break;
        }
        if (finished && playbackVersion == _playbackVersion)
            AnimationFinished?.Invoke();
    }

    private void RaiseFrameZeroEvents()
    {
        _playbackVersion++;
        var chain = _chains[_currentChainIndex];
        if (chain.Count > 0)
            RaiseEvents(chain[0], _playbackVersion);
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
        _currentFrameIndex = 0;
        _timeIntoAnimation = 0;
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
        double totalLength = chain.TotalLength.TotalSeconds;
        if (totalLength <= 0)
        {
            _timeIntoAnimation = 0;
            _currentFrameIndex = 0;
            return;
        }

        _timeIntoAnimation = IsLooping
            ? seconds % totalLength
            : seconds >= totalLength ? totalLength : seconds;

        UpdateFrameIndexFromTime(chain);
    }

    private void UpdateFrameIndexFromTime(AnimationChain<TFrame> chain)
    {
        // Find the frame at the current accumulated time.
        double t = _timeIntoAnimation;
        _currentFrameIndex = chain.Count - 1; // default to last if time overshoots
        for (int i = 0; i < chain.Count; i++)
        {
            t -= chain[i].FrameLength.TotalSeconds;
            if (t <= 0)
            {
                _currentFrameIndex = i;
                break;
            }
        }
    }

    private static double GetTimeAtFrameStart(AnimationChain<TFrame> chain, int frameIndex)
    {
        double time = 0;
        for (int i = 0; i < frameIndex; i++)
            time += chain[i].FrameLength.TotalSeconds;
        return time;
    }
}
