namespace FlatRedBall.AnimationChain;

/// <summary>
/// Controls playback of an <see cref="AnimationChain"/> from an <see cref="AnimationChainList"/>.
/// Call <see cref="Play(string)"/>, advance with <see cref="Update"/>, then read
/// <see cref="CurrentFrame"/> to draw the current texture and source rectangle.
/// </summary>
/// <remarks>
/// One <see cref="AnimationPlayer"/> per on-screen sprite is the typical usage. The player
/// holds a reference to the <see cref="AnimationChainList"/> but does not own it — dispose
/// textures via <see cref="AchxLoader.Dispose"/>.
/// </remarks>
public class AnimationPlayer
{
    private AnimationChainList? _chains;
    private int _currentChainIndex = -1;
    private int _currentFrameIndex;
    private double _timeIntoAnimation;

    /// <summary>
    /// The frame currently being displayed, or <c>null</c> if no animation is playing.
    /// Read this in your Draw method.
    /// </summary>
    public AnimationFrame? CurrentFrame
    {
        get
        {
            if (_currentChainIndex < 0 || _chains == null) return null;
            var chain = _chains[_currentChainIndex];
            return chain.Count > 0 ? chain[_currentFrameIndex] : null;
        }
    }

    /// <summary>The currently playing <see cref="AnimationChain"/>, or <c>null</c> if none.</summary>
    public AnimationChain? CurrentChain =>
        _currentChainIndex >= 0 && _chains != null ? _chains[_currentChainIndex] : null;

    /// <summary>When <c>true</c> (the default), <see cref="Update"/> advances frames.</summary>
    public bool Animate { get; set; } = true;

    /// <summary>When <c>true</c> (the default), the animation wraps back to frame 0 at the end.</summary>
    public bool IsLooping { get; set; } = true;

    /// <summary>Multiplier applied to frame time. 2.0 plays twice as fast; 0.5 plays half-speed.</summary>
    public float AnimationSpeed { get; set; } = 1f;

    /// <summary>Raised once when a non-looping animation reaches its last frame.</summary>
    public event Action? AnimationFinished;

    /// <param name="chains">The animation list to play from. May be empty; <see cref="Play(string)"/> will no-op.</param>
    public AnimationPlayer(AnimationChainList chains)
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
        if (_chains == null) return;

        for (int i = 0; i < _chains.Count; i++)
        {
            if (_chains[i].Name == chainName)
            {
                if (_currentChainIndex == i) return; // already playing — no restart
                _currentChainIndex = i;
                _currentFrameIndex = 0;
                _timeIntoAnimation = 0;
                Animate = true;
                return;
            }
        }
    }

    /// <summary>
    /// Starts playing the specified <paramref name="chain"/> from frame 0. If the chain is
    /// already playing, this is a no-op.
    /// </summary>
    public void Play(AnimationChain chain)
    {
        ArgumentNullException.ThrowIfNull(chain);
        if (_chains == null) return;

        for (int i = 0; i < _chains.Count; i++)
        {
            if (ReferenceEquals(_chains[i], chain))
            {
                if (_currentChainIndex == i) return;
                _currentChainIndex = i;
                _currentFrameIndex = 0;
                _timeIntoAnimation = 0;
                Animate = true;
                return;
            }
        }
    }

    /// <summary>
    /// Advances the animation by <paramref name="elapsed"/> real time (scaled by
    /// <see cref="AnimationSpeed"/>). Call this once per game Update tick.
    /// </summary>
    public void Update(TimeSpan elapsed)
    {
        if (!Animate || _currentChainIndex < 0 || _chains == null) return;

        var chain = _chains[_currentChainIndex];
        if (chain.Count == 0) return;

        _timeIntoAnimation += elapsed.TotalSeconds * AnimationSpeed;

        double totalLength = chain.TotalLength.TotalSeconds;
        if (totalLength <= 0) return;

        if (IsLooping)
        {
            while (_timeIntoAnimation >= totalLength)
                _timeIntoAnimation -= totalLength;
        }
        else
        {
            if (_timeIntoAnimation >= totalLength)
            {
                _timeIntoAnimation = totalLength;
                Animate = false;
                AnimationFinished?.Invoke();
            }
        }

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
}
