using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Animation;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering.Batches;
using KeyframeColor = FlatRedBall2.AnimationEditorCommon.KeyframeColor;
using KeyframeEvaluator = FlatRedBall2.AnimationEditorCommon.KeyframeEvaluator;
using KeyframeShapeValue = FlatRedBall2.AnimationEditorCommon.KeyframeShapeValue;

namespace FlatRedBall2.Rendering;

/// <summary>
/// Textured rectangle drawn each frame by the engine. Implements <see cref="IAttachable"/>
/// so it can be added to an <see cref="Entity"/> via <c>Entity.Add(sprite)</c>; its
/// <see cref="X"/>/<see cref="Y"/>/<see cref="Z"/> are then interpreted as offsets from
/// the parent.
/// <para>
/// Defaults to <see cref="Batches.WorldSpaceBatch"/> (camera-transformed, Y+ up). For HUD
/// or screen-space sprites, set <see cref="Batch"/> to <see cref="Batches.ScreenSpaceBatch"/>;
/// <see cref="Layer.IsScreenSpace"/> only affects Gum elements.
/// </para>
/// <para>
/// <b>Pixel snapping:</b> add a world-space sprite to <see cref="Camera.SnappedSprites"/> to have
/// each camera round its <i>draw</i> position to that camera's screen pixel grid. <see cref="X"/>/<see cref="Y"/>
/// stay as simulated; the sprite carries no snapping state of its own, and screen-space sprites are never snapped.
/// </para>
/// <para>
/// <b>Sizing modes:</b> see <see cref="TextureScale"/> for how <see cref="Width"/> and
/// <see cref="Height"/> are determined. <b>Animation:</b> assign <see cref="AnimationChains"/>
/// then call <see cref="PlayAnimation(string)"/>; per-frame texture, source rectangle, flip
/// flags, relative offsets and color are applied automatically while <see cref="Animate"/> is true;
/// <see cref="FrameControlledProperties"/> hands flip, offset or color back to game code.
/// </para>
/// </summary>
public class Sprite : IRenderable, IAttachable
{
    private const float FallbackSize = 32f;

    // Backing fields
    private Texture2D? _texture;
    private Rectangle? _sourceRectangle;
    private float _width = FallbackSize;
    private float _height = FallbackSize;
    private float? _textureScale = 1f;

    // Animation state
    private AnimationChainList? _animationChains;
    private int _currentChainIndex = -1;
    private int _currentFrameIndex;
    private double _timeIntoAnimation;
    // Offset and color at the current playback time, keyframe-interpolated (#1478). Recomputed by
    // ApplyCurrentFrame; equal to the current frame's own values unless the .achx stores a curve.
    private (float X, float Y) _frameOffset;
    private KeyframeColor _frameColor;
    // Bumped whenever playback switches chain, so event dispatch can tell a handler replaced the animation.
    private int _playbackVersion;

    /// <summary>The entity this sprite is attached to, or <c>null</c> if unattached. Set by <see cref="Entity.Add(IAttachable, Layer?)"/>.</summary>
    public Entity? Parent { get; set; }

    /// <summary>
    /// X position. Relative to <see cref="Parent"/> when attached, world when unattached.
    /// <para>
    /// <b>Animated sprites:</b> while <see cref="SpriteFrameProperties.Offset"/> is in
    /// <see cref="FrameControlledProperties"/> (the default), this value is overwritten with
    /// <see cref="AnimationFrame.RelativeX"/> on every frame switch, so assigning <see cref="X"/> in game
    /// code is clobbered on the next advance. Clear that flag to own X/Y in game code, bake the offset into
    /// the frames' <c>RelativeX</c> in the <c>.achx</c> / source asset, or attach the sprite to a child
    /// entity whose own <c>X</c> carries the offset.
    /// </para>
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Y position (Y+ up in world space). Relative to <see cref="Parent"/> when attached, world when unattached.
    /// <para>
    /// <b>Animated sprites:</b> while <see cref="SpriteFrameProperties.Offset"/> is in
    /// <see cref="FrameControlledProperties"/> (the default), this value is overwritten with
    /// <see cref="AnimationFrame.RelativeY"/> on every frame switch, so assigning <see cref="Y"/> in game
    /// code is clobbered on the next advance. Clear that flag to own X/Y in game code, bake the offset into
    /// the frames' <c>RelativeY</c> in the <c>.achx</c> / source asset, or attach the sprite to a child
    /// entity whose own <c>Y</c> carries the offset.
    /// </para>
    /// </summary>
    public float Y { get; set; }

    /// <summary>Per-sprite draw order within its <see cref="Layer"/>. See <see cref="IRenderable.Z"/> for sorting semantics.</summary>
    public float Z { get; set; }

    /// <summary>
    /// Final world-space X after walking the parent chain. When attached, <see cref="X"/>/<see cref="Y"/>
    /// are rotated by the parent's <see cref="AbsoluteRotation"/> before being added to the
    /// parent's absolute position — an attached sprite orbits its parent's origin as the parent
    /// rotates (lever-arm), it does not keep a fixed world offset.
    /// </summary>
    public float AbsoluteX => Parent != null ? AttachmentMath.ComposeAbsolute(Parent, X, Y).X : X;

    /// <summary>Final world-space Y after walking the parent chain. See <see cref="AbsoluteX"/> for the rotation-composed formula.</summary>
    public float AbsoluteY => Parent != null ? AttachmentMath.ComposeAbsolute(Parent, X, Y).Y : Y;

    /// <summary>Final Z after walking the parent chain. Used for sort order; see <see cref="Z"/>.</summary>
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;

    /// <summary>Rotation about the Z axis. Relative to <see cref="Parent"/> when attached, world when unattached.</summary>
    public Angle Rotation { get; set; }

    /// <summary>Final world-space rotation after walking the parent chain.</summary>
    public Angle AbsoluteRotation => Parent != null ? Parent.AbsoluteRotation + Rotation : Rotation;

    /// <inheritdoc/>
    public Layer? Layer { get; set; }

    private IRenderBatch _batch = WorldSpaceBatch.Instance;

    /// <summary>
    /// The batch this sprite renders through. Defaults to <see cref="WorldSpaceBatch.Instance"/>
    /// (camera-transformed, Y-flip). Reassign for screen-space rendering or custom blend states.
    /// <para>
    /// <b>Add color:</b> while <see cref="AddColor"/> is non-zero, the getter transparently swaps
    /// <see cref="WorldSpaceBatch.Instance"/>/<see cref="ScreenSpaceBatch.Instance"/> for their
    /// <see cref="WorldSpaceAddColorBatch"/>/<see cref="ScreenSpaceAddColorBatch"/> counterparts —
    /// the value you assigned here is preserved and returned as-is once <see cref="AddColor"/> returns
    /// to zero. A custom (non-default) batch is returned unchanged; add color is not rendered
    /// in that case.
    /// </para>
    /// </summary>
    public IRenderBatch Batch
    {
        get
        {
            if (AddColor != Vector3.Zero)
            {
                if (ReferenceEquals(_batch, WorldSpaceBatch.Instance)) return WorldSpaceAddColorBatch.Instance;
                if (ReferenceEquals(_batch, ScreenSpaceBatch.Instance)) return ScreenSpaceAddColorBatch.Instance;
            }
            return _batch;
        }
        set => _batch = value;
    }

    /// <inheritdoc/>
    public string? Name { get; set; }

    // Visual

    /// <summary>
    /// The texture to render. Setting this triggers a dimension recalculation when
    /// <see cref="TextureScale"/> is non-null.
    /// </summary>
    public Texture2D? Texture
    {
        get => _texture;
        set
        {
            _texture = value;
            RecalculateDimensions();
        }
    }

    /// <summary>
    /// The rendered width of the sprite in world units. When <see cref="TextureScale"/> is
    /// non-null this is computed automatically and the setter is a no-op; set
    /// <see cref="TextureScale"/> to <c>null</c> first to use explicit sizing.
    /// </summary>
    public float Width
    {
        get => _width;
        set
        {
            if (_textureScale.HasValue) return;
            _width = value;
        }
    }

    /// <summary>
    /// The rendered height of the sprite in world units. When <see cref="TextureScale"/> is
    /// non-null this is computed automatically and the setter is a no-op; set
    /// <see cref="TextureScale"/> to <c>null</c> first to use explicit sizing.
    /// </summary>
    public float Height
    {
        get => _height;
        set
        {
            if (_textureScale.HasValue) return;
            _height = value;
        }
    }

    /// <summary>
    /// Controls how <see cref="Width"/> and <see cref="Height"/> are determined.
    /// <list type="bullet">
    ///   <item><description>
    ///     <b>Non-null (default <c>1f</c>)</b> — texture-derived mode: Width and Height are
    ///     automatically set to <c>sourceSize * TextureScale</c> whenever the texture or
    ///     source rectangle changes. Useful for pixel-art games; e.g. <c>TextureScale = 2f</c>
    ///     gives a clean 2× upscale. When no texture is assigned, falls back to
    ///     <c>32 * TextureScale</c> so the sprite is visible as a placeholder.
    ///   </description></item>
    ///   <item><description>
    ///     <b>Null</b> — explicit mode: Width and Height are fully under caller control.
    ///     Set this to null before assigning Width/Height when you need a size that differs
    ///     from the texture dimensions.
    ///   </description></item>
    /// </list>
    /// <para>
    /// <b>Note:</b> TextureScale wins over explicit Width/Height. If code sets both,
    /// set <c>TextureScale</c> last (or set it to null to activate explicit mode).
    /// </para>
    /// </summary>
    public float? TextureScale
    {
        get => _textureScale;
        set
        {
            _textureScale = value;
            RecalculateDimensions();
        }
    }

    /// <summary>
    /// Color tint multiplied with the texture pixels. Default <c>White</c> (no tint).
    /// Multiplied by <see cref="Alpha"/> at draw time.
    /// </summary>
    public Color Color { get; set; } = Color.White;

    /// <summary>
    /// Per-channel offset added to the texture's RGB before <see cref="Color"/> and <see cref="Alpha"/>
    /// multiply the result, so a sprite can be brightened past its texture color (white hit flash) or
    /// darkened. Channels are in the range -1..1 (<c>X</c>=red, <c>Y</c>=green, <c>Z</c>=blue); the
    /// shaded result is clamped to 0..1. Fully transparent texels are unaffected. Default
    /// <see cref="Vector3.Zero"/> (no offset, normal batch).
    /// <para>
    /// Animation frames with <see cref="Animation.ColorOperation.Add"/> assign this property on every
    /// frame change (non-Add frames reset it to zero) while <see cref="SpriteFrameProperties.Color"/> is in
    /// <see cref="FrameControlledProperties"/>, which is the default. To drive it from game code on an
    /// animated sprite, clear that flag first. A non-zero value moves the sprite onto the add-color
    /// batch, see <see cref="Batch"/>; that shader path draws each add-color sprite immediately, so
    /// keep it to the sprites that need it.
    /// </para>
    /// </summary>
    public Vector3 AddColor { get; set; }

    /// <summary>
    /// Opacity in the range 0 (fully transparent) to 1 (fully opaque). Default <c>1f</c>.
    /// Multiplied with <see cref="Color"/> at draw time, so values outside [0, 1] over- or
    /// under-saturate the result.
    /// </summary>
    public float Alpha { get; set; } = 1f;

    /// <summary>
    /// When <c>false</c>, this sprite is skipped at draw time. Does not affect <see cref="Parent"/>
    /// visibility — see <see cref="Entity.IsAbsoluteVisible"/> for the resolved chain.
    /// </summary>
    public bool IsVisible { get; set; } = true;

    /// <summary>
    /// The sub-region of the texture to render. Setting this triggers a dimension
    /// recalculation when <see cref="TextureScale"/> is non-null. When null, the full
    /// texture is used.
    /// </summary>
    public Rectangle? SourceRectangle
    {
        get => _sourceRectangle;
        set
        {
            _sourceRectangle = value;
            RecalculateDimensions();
        }
    }

    /// <summary>Mirrors the texture left-to-right when <c>true</c>. Overwritten by each animation frame change unless <see cref="SpriteFrameProperties.Flip"/> is cleared from <see cref="FrameControlledProperties"/>.</summary>
    public bool FlipHorizontal { get; set; }

    /// <summary>
    /// Mirrors the texture top-to-bottom when <c>true</c>. Note that <see cref="Batches.WorldSpaceBatch"/>
    /// already applies a base Y-flip to compensate for the camera transform — this property toggles
    /// <em>relative to upright</em>, not relative to texture pixels. Overwritten by each animation
    /// frame change unless <see cref="SpriteFrameProperties.Flip"/> is cleared from <see cref="FrameControlledProperties"/>.
    /// </summary>
    public bool FlipVertical { get; set; }

    /// <summary>
    /// Mirrors the texture across its diagonal (transposes width/height) when <c>true</c>.
    /// Combines with <see cref="FlipHorizontal"/>/<see cref="FlipVertical"/> in the same D→H→V
    /// order Tiled and the AnimationEditor use. MonoGame's <c>SpriteEffects</c> has no diagonal
    /// value, so this is implemented as a rotation offset rather than a texture-sampling flag —
    /// see <see cref="ComputeDrawTransform"/>. Overwritten by each animation frame change unless
    /// <see cref="SpriteFrameProperties.Flip"/> is cleared from <see cref="FrameControlledProperties"/>.
    /// </summary>
    public bool FlipDiagonal { get; set; }

    // Animation

    /// <summary>
    /// The collection of named animation chains available to this sprite.
    /// Assign before calling <see cref="PlayAnimation(string)"/>.
    /// <para>
    /// Per-frame <c>RelativeX</c>/<c>RelativeY</c> offsets and per-frame shapes both require the
    /// sprite to be attached to a parent <see cref="Entity"/>. Offsets are applied to the sprite's
    /// position, which is interpreted relative to its parent; without a parent the offsets become
    /// absolute world coordinates and snap the sprite around between frames. Shape reconciliation
    /// looks up and creates shapes on the parent — without a parent it is silently a no-op
    /// (though name-validation errors still surface).
    /// </para>
    /// </summary>
    public AnimationChainList? AnimationChains
    {
        get => _animationChains;
        set
        {
            _animationChains = value;
            _currentChainIndex = -1;
            _playbackVersion++;
            // The frame's add color goes with the frame; a game-owned AddColor (Color group off) stays.
            if (CurrentFrame != null && _frameControlledProperties.HasFlag(SpriteFrameProperties.Color))
                AddColor = Vector3.Zero;
            CurrentFrame = null;
        }
    }

    private SpriteFrameProperties _frameControlledProperties = SpriteFrameProperties.All;

    /// <summary>
    /// Which property groups animation frames write onto this sprite. Default
    /// <see cref="SpriteFrameProperties.All"/>. Clear a group to take it over in game code, for example
    /// <c>FrameControlledProperties &amp;= ~SpriteFrameProperties.Flip</c> so an enemy can face the player
    /// regardless of the art frames' flips. Texture and source rectangle always follow the frame.
    /// <para>
    /// Setting a group that was off applies the current frame's values for it immediately. Clearing a
    /// group leaves the property at whatever the last frame wrote.
    /// </para>
    /// </summary>
    public SpriteFrameProperties FrameControlledProperties
    {
        get => _frameControlledProperties;
        set
        {
            var newlyEnabled = value & ~_frameControlledProperties;
            _frameControlledProperties = value;
            if (newlyEnabled != SpriteFrameProperties.None && CurrentFrame != null)
                ApplyFrameProperties(CurrentFrame, newlyEnabled);
        }
    }

    /// <summary>Whether the sprite is currently advancing through an animation each frame.</summary>
    public bool Animate { get; set; }

    /// <summary>
    /// When <c>true</c>, this sprite's animation continues to advance even while
    /// <see cref="Screen.IsPaused"/> is <c>true</c>. Default <c>false</c>.
    /// Use for UI animations or effects that must keep running during gameplay pause.
    /// </summary>
    public bool ShouldAnimationAdvanceOnPause { get; set; }

    /// <summary>
    /// Whether the current animation loops. Seeded from the played chain's
    /// <see cref="AnimationChain.Loop"/> each time <see cref="PlayAnimation(string)"/> or
    /// <see cref="PlayAnimation(AnimationChain)"/> switches to a new chain; set this afterward to
    /// override per-instance. When <c>false</c>, the animation stops on its last frame and fires
    /// <see cref="AnimationFinished"/>.
    /// </summary>
    public bool IsLooping { get; set; } = true;

    /// <summary>Multiplier applied to animation playback speed. Default is <c>1f</c> (normal speed).</summary>
    public float AnimationSpeed { get; set; } = 1f;

    /// <summary>The currently playing <see cref="AnimationChain"/>, or null if no animation is active.</summary>
    public AnimationChain? CurrentAnimation =>
        _animationChains != null && _currentChainIndex >= 0 && _currentChainIndex < _animationChains.Count
            ? _animationChains[_currentChainIndex]
            : null;

    /// <summary>
    /// The <see cref="AnimationFrame"/> currently displayed, or <c>null</c> when no animation is playing.
    /// <see cref="AnimationFrame.ColorOperation"/> Multiply and <see cref="AnimationFrame.Alpha"/>
    /// are applied at draw time (see <see cref="SpriteFrameColor.Apply(AnimationFrame, Color)"/> and <see cref="Draw"/>), and
    /// Add is assigned to <see cref="AddColor"/>, all while <see cref="SpriteFrameProperties.Color"/> is in
    /// <see cref="FrameControlledProperties"/>. <see cref="AnimationFrame.RelativeX"/>/<see cref="AnimationFrame.RelativeY"/>
    /// are likewise applied automatically (see <see cref="X"/>/<see cref="Y"/>).
    /// </summary>
    public AnimationFrame? CurrentFrame { get; private set; }

    /// <summary>Fired when a non-looping animation reaches its last frame.</summary>
    public event Action? AnimationFinished;

    /// <summary>
    /// Fired when a looping animation wraps from its last frame back toward its first (FRB1
    /// <c>JustCycled</c>). Non-looping chains never raise it; use <see cref="AnimationFinished"/>.
    /// <list type="bullet">
    /// <item>Raised at most once per update, even when a large delta spans several loops.</item>
    /// <item>A single-frame looping chain "loops" each time its frame length elapses.</item>
    /// <item>Follows <see cref="AnimationSpeed"/>, <see cref="Animate"/>, and screen pause exactly as
    /// frame advance does; nothing is raised while the animation is not advancing.</item>
    /// <item><see cref="PlayAnimation(string)"/> never raises it.</item>
    /// <item>Raised after <see cref="FrameChanged"/> and <see cref="FrameEventRaised"/> for the same
    /// update. A handler of either that switches animation suppresses it.</item>
    /// </list>
    /// </summary>
    public event Action? AnimationLooped;

    /// <summary>
    /// Fired when the displayed <see cref="CurrentFrame"/> changes (FRB1 <c>JustChangedFrame</c>);
    /// <see cref="CurrentFrame"/> is already the new frame during the handler.
    /// <list type="bullet">
    /// <item>Raised at most once per update, even when a large delta skips several frames; use
    /// <see cref="FrameEventRaised"/> to see every frame crossed.</item>
    /// <item>A loop that lands on the same frame (single-frame chain, or a delta of whole loops) is
    /// not a frame change, though <see cref="AnimationLooped"/> still fires.</item>
    /// <item><see cref="PlayAnimation(string)"/> switching to a different chain raises it
    /// (frame 0 of the new chain is now shown). Re-playing the chain already playing does not.</item>
    /// <item>On a non-looping chain it is raised before <see cref="AnimationFinished"/>.</item>
    /// <item>Follows <see cref="AnimationSpeed"/>, <see cref="Animate"/>, and screen pause exactly as
    /// frame advance does.</item>
    /// <item>Raised before <see cref="FrameEventRaised"/> for the same update. A handler that switches
    /// animation suppresses the rest of the old chain's notifications for that update.</item>
    /// </list>
    /// </summary>
    public event Action? FrameChanged;

    /// <summary>
    /// Raised for each <see cref="AnimationFrame.Events"/> entry when playback enters its frame, so
    /// game code reacts by event name instead of by frame index. Frame entry is defined as:
    /// <list type="bullet">
    /// <item><see cref="PlayAnimation(string)"/> switching to a chain enters frame 0 and raises its
    /// events immediately, before returning. Re-playing the chain that is already playing raises nothing.</item>
    /// <item>A large delta (low FPS, a hitch, high <see cref="AnimationSpeed"/>) still raises the events
    /// of every frame it skipped over, in playback order, ending with the frame it lands on.</item>
    /// <item>A looping chain re-enters frame 0 on each wrap. A delta spanning more than one full loop
    /// raises each frame's events once, not once per loop.</item>
    /// <item>On a non-looping chain, the last frame's events are raised before <see cref="AnimationFinished"/>.</item>
    /// </list>
    /// During the handler, <see cref="CurrentFrame"/> is the frame playback landed on, which may be
    /// later than the frame that owns the event. A handler that switches animation stops the rest of
    /// the old chain's events for that tick (and its <see cref="AnimationFinished"/>).
    /// </summary>
    public event Action<AnimationFrameEvent>? FrameEventRaised;

    /// <summary>
    /// Starts playing the named animation. If the named animation is already playing, this is a no-op —
    /// the current frame and time are preserved so calling this every frame does not restart the animation.
    /// The name must match a chain in <see cref="AnimationChains"/>.
    /// </summary>
    public void PlayAnimation(string name)
    {
        if (_animationChains == null) return;

        for (int i = 0; i < _animationChains.Count; i++)
        {
            if (_animationChains[i].Name == name)
            {
                if (_currentChainIndex == i) return;
                _currentChainIndex = i;
                _currentFrameIndex = 0;
                _timeIntoAnimation = 0;
                Animate = true;
                IsLooping = _animationChains[i].Loop;
                ApplyCurrentFrame();
                RaiseFrameZeroEvents();
                return;
            }
        }
    }

    /// <summary>
    /// Starts playing the specified animation chain from the beginning.
    /// If the chain exists in <see cref="AnimationChains"/> it is used directly;
    /// otherwise a temporary single-chain list is created.
    /// </summary>
    public void PlayAnimation(AnimationChain chain)
    {
        // Try to find the chain in the existing list
        if (_animationChains != null)
        {
            for (int i = 0; i < _animationChains.Count; i++)
            {
                if (ReferenceEquals(_animationChains[i], chain))
                {
                    if (_currentChainIndex == i) return;
                    _currentChainIndex = i;
                    _currentFrameIndex = 0;
                    _timeIntoAnimation = 0;
                    Animate = true;
                    IsLooping = chain.Loop;
                    ApplyCurrentFrame();
                    RaiseFrameZeroEvents();
                    return;
                }
            }
        }

        // Chain not in list — create a temporary single-chain list
        var tempList = new AnimationChainList();
        tempList.Add(chain);
        _animationChains = tempList;
        _currentChainIndex = 0;
        _currentFrameIndex = 0;
        _timeIntoAnimation = 0;
        Animate = true;
        IsLooping = chain.Loop;
        ApplyCurrentFrame();
        RaiseFrameZeroEvents();
    }

    internal void AnimateSelf(double deltaSeconds)
    {
        if (!Animate || _currentChainIndex < 0 || _animationChains == null) return;

        var chain = _animationChains[_currentChainIndex];
        if (chain.Count == 0) return;

        _timeIntoAnimation += deltaSeconds * AnimationSpeed;

        // Convert TimeSpan totals to seconds once at the boundary; per-frame math stays in double.
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

        // Find frame index from accumulated time
        double t = _timeIntoAnimation;
        _currentFrameIndex = chain.Count - 1; // default to last frame
        for (int i = 0; i < chain.Count; i++)
        {
            t -= chain[i].FrameLength.TotalSeconds;
            if (t <= 0)
            {
                _currentFrameIndex = i;
                break;
            }
        }

        ApplyCurrentFrame();

        int playbackVersion = _playbackVersion;
        if (_currentFrameIndex != previousFrameIndex)
        {
            FrameChanged?.Invoke();
            if (playbackVersion != _playbackVersion) return;
        }

        if (!RaiseEnteredFrameEvents(chain, previousFrameIndex, wraps)) return;
        if (wraps > 0)
        {
            AnimationLooped?.Invoke();
            if (playbackVersion != _playbackVersion) return;
        }
        if (finished)
            AnimationFinished?.Invoke();
    }

    private void RaiseFrameZeroEvents()
    {
        _playbackVersion++;
        int playbackVersion = _playbackVersion;
        FrameChanged?.Invoke();
        if (playbackVersion != _playbackVersion) return;
        var chain = _animationChains![_currentChainIndex];
        if (chain.Count > 0)
            RaiseEvents(chain[0], playbackVersion);
    }

    // Returns false once a handler has switched animation.
    private bool RaiseEnteredFrameEvents(AnimationChain chain, int previousFrameIndex, int wraps)
    {
        if (FrameEventRaised == null) return true;
        var (start, length) = FlatRedBall2.AnimationEditorCommon.FrameEventRange.Get(previousFrameIndex, _currentFrameIndex, wraps, chain.Count);
        int playbackVersion = _playbackVersion;
        for (int j = 0; j < length; j++)
            if (!RaiseEvents(chain[(start + j) % chain.Count], playbackVersion))
                return false;
        return true;
    }

    // Returns false once a handler has switched animation, so the caller stops raising the old chain's events.
    private bool RaiseEvents(AnimationFrame frame, int playbackVersion)
    {
        var events = frame.Events;
        for (int k = 0; k < events.Count; k++)
        {
            FrameEventRaised?.Invoke(events[k]);
            if (playbackVersion != _playbackVersion) return false;
        }
        return true;
    }

    private void ApplyCurrentFrame()
    {
        if (_animationChains == null || _currentChainIndex < 0) return;
        var chain = _animationChains[_currentChainIndex];
        if (chain.Count == 0) return;

        var frame = chain[System.Math.Clamp(_currentFrameIndex, 0, chain.Count - 1)];
        CurrentFrame = frame;
        _texture = frame.Texture;
        _sourceRectangle = frame.SourceRectangle;
        int index = System.Math.Clamp(_currentFrameIndex, 0, chain.Count - 1);
        double frameStart = 0;
        for (int i = 0; i < index; i++)
            frameStart += chain[i].FrameLength.TotalSeconds;
        double elapsed = System.Math.Max(0, _timeIntoAnimation - frameStart);

        // Evaluates the offset and color at the current playback time. With the default snap curve these
        // are exactly the current frame's own values.
        _frameOffset = KeyframeEvaluator.EvaluateOffset(chain, IsLooping, index, elapsed);
        _frameColor = KeyframeEvaluator.EvaluateColor(chain, IsLooping, index, elapsed);
        ApplyFrameProperties(frame, _frameControlledProperties);
        RecalculateDimensions();

        ReconcileShapes(frame, chain, index, elapsed);
    }

    // Writes only the groups in `groups` from the frame onto the sprite.
    private void ApplyFrameProperties(AnimationFrame frame, SpriteFrameProperties groups)
    {
        if (groups.HasFlag(SpriteFrameProperties.Flip))
        {
            FlipHorizontal = frame.FlipHorizontal;
            FlipVertical = frame.FlipVertical;
            FlipDiagonal = frame.FlipDiagonal;
        }
        if (groups.HasFlag(SpriteFrameProperties.Offset))
        {
            X = _frameOffset.X;
            Y = _frameOffset.Y;
        }
        if (groups.HasFlag(SpriteFrameProperties.Color))
        {
            AddColor = frame.ColorOperation == Animation.ColorOperation.Add
                ? SpriteFrameColor.GetAddOffset(_frameColor)
                : Vector3.Zero;
        }
    }

    private void ReconcileShapes(AnimationFrame frame, AnimationChain chain, int frameIndex, double elapsedInFrame)
    {
        if (_animationChains == null) return;

        // Validate frame shape entries before short-circuiting on missing parent or empty owned
        // set — empty/duplicate names are programmer errors that should surface even without
        // a parent entity attached.
        Dictionary<string, AnimationShapeFrame>? frameShapes = null;
        for (int i = 0; i < frame.Shapes.Count; i++)
        {
            var s = frame.Shapes[i];
            if (string.IsNullOrEmpty(s.Name))
                throw new InvalidOperationException(
                    "AnimationFrame contains a shape with an empty Name. Names are required for per-frame shape reconciliation.");
            frameShapes ??= new Dictionary<string, AnimationShapeFrame>();
            if (frameShapes.ContainsKey(s.Name))
                throw new InvalidOperationException(
                    $"AnimationFrame contains duplicate shape name '{s.Name}'. Names must be unique within a frame.");
            frameShapes[s.Name] = s;
        }

        if (Parent == null) return;

        var ownedNames = _animationChains.GetOwnedShapeNames();
        if (ownedNames.Count == 0) return;

        foreach (var name in ownedNames)
        {
            AnimationShapeFrame? entry = null;
            frameShapes?.TryGetValue(name, out entry);
            if (entry != null)
            {
                var points = entry is AnimationPolygonFrame
                    ? KeyframeEvaluator.EvaluateShapePoints(chain, IsLooping, frameIndex, elapsedInFrame, entry)
                    : null;
                ApplyShapeEntry(name, entry, KeyframeEvaluator.EvaluateShape(chain, IsLooping, frameIndex, elapsedInFrame, entry), points);
            }
            else
                DisableCollisionIfPresent(name);
        }
    }

    // `value` is the entry's offset and size glided by its stored curves, `points` a polygon's glided points (null for
    // other kinds); with the default snap curve they are the entry's own.
    private void ApplyShapeEntry(string name, AnimationShapeFrame entry, KeyframeShapeValue value, IReadOnlyList<System.Numerics.Vector2>? points)
    {
        var existing = Parent!.FindShapeByName(name);
        switch (entry)
        {
            case AnimationAARectFrame rect:
            {
                if (existing == null)
                {
                    if (!_animationChains!.AutoCreateShapes)
                        throw new InvalidOperationException(
                            $"Animation frame references shape '{name}' which is not on the entity, and AutoCreateShapes is false.");
                    var r = new AARect { Name = name };
                    ApplyRectangle(r, value);
                    r.IsVisible = true;
                    Parent.Add(r);
                }
                else if (existing is AARect r)
                {
                    ApplyRectangle(r, value);
                    Parent.SetDefaultCollision(r, true);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Animation frame says '{name}' is a Rectangle but entity has it as {existing.GetType().Name}.");
                }
                break;
            }
            case AnimationCircleFrame circle:
            {
                if (existing == null)
                {
                    if (!_animationChains!.AutoCreateShapes)
                        throw new InvalidOperationException(
                            $"Animation frame references shape '{name}' which is not on the entity, and AutoCreateShapes is false.");
                    var c = new Circle { Name = name };
                    ApplyCircle(c, value);
                    c.IsVisible = true;
                    Parent.Add(c);
                }
                else if (existing is Circle c)
                {
                    ApplyCircle(c, value);
                    Parent.SetDefaultCollision(c, true);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Animation frame says '{name}' is a Circle but entity has it as {existing.GetType().Name}.");
                }
                break;
            }
            case AnimationPolygonFrame poly:
            {
                if (existing == null)
                {
                    if (!_animationChains!.AutoCreateShapes)
                        throw new InvalidOperationException(
                            $"Animation frame references shape '{name}' which is not on the entity, and AutoCreateShapes is false.");
                    var p = new Polygon { Name = name };
                    ApplyPolygon(p, value, points!);
                    p.IsVisible = true;
                    Parent.Add(p);
                }
                else if (existing is Polygon p)
                {
                    ApplyPolygon(p, value, points!);
                    Parent.SetDefaultCollision(p, true);
                }
                else
                {
                    throw new InvalidOperationException(
                        $"Animation frame says '{name}' is a Polygon but entity has it as {existing.GetType().Name}.");
                }
                break;
            }
        }
    }

    // Only disables collision — visibility is never the animation system's business once a shape
    // exists. A shape's IsVisible is entirely game code's to control (see issue #775).
    private void DisableCollisionIfPresent(string name)
    {
        var existing = Parent!.FindShapeByName(name);
        if (existing == null) return;
        switch (existing)
        {
            case AARect r:
                Parent.SetDefaultCollision(r, false);
                break;
            case Circle c:
                Parent.SetDefaultCollision(c, false);
                break;
            case Polygon p:
                Parent.SetDefaultCollision(p, false);
                break;
        }
    }

    // Geometry-only — callers that just created the shape set IsVisible = true themselves.
    private static void ApplyRectangle(AARect r, KeyframeShapeValue value)
    {
        r.Width = value.SizeX;
        r.Height = value.SizeY;
        r.X = value.X;
        r.Y = value.Y;
    }

    private static void ApplyCircle(Circle c, KeyframeShapeValue value)
    {
        c.Radius = value.SizeX;
        c.X = value.X;
        c.Y = value.Y;
    }

    private static void ApplyPolygon(Polygon p, KeyframeShapeValue value, IReadOnlyList<System.Numerics.Vector2> points)
    {
        p.SetPoints(points);
        p.X = value.X;
        p.Y = value.Y;
    }

    // Rotation (radians) handed to SpriteBatch.Draw. SpriteBatch rotates corner offsets with the
    // standard matrix in its own Y-down space, and the world batch then re-applies the camera's
    // Y-flip — so the SIGN here decides on-screen spin direction. Extracted so the rotation
    // convention is unit-testable without a GraphicsDevice (see SpriteRotationTests).
    // Positive AbsoluteRotation must spin COUNTERCLOCKWISE to match the Polygon and the documented
    // Angle convention (Angle.cs, PathFollower). The camera's Y-flip already supplies the only sign
    // inversion, so the angle passes straight through — a prior extra negation (#378) made sprites
    // spin clockwise (opposite a polygon hitbox on the same entity).
    internal float RenderRotationRadians => AbsoluteRotation.Radians;

    // Rotation + SpriteEffects handed to SpriteBatch.Draw, folding in FlipDiagonal.
    //
    // MonoGame's SpriteEffects has no diagonal/transpose value, and SpriteBatch offers no way to
    // swap which UV axis a corner samples — effects only mirror an axis in place; rotation only
    // moves the already-UV-mapped quad. A diagonal flip is a reflection (determinant -1), while
    // rotation alone is not (determinant +1), so no rotation/effects combination reproduces it —
    // EXCEPT that composing a rotation with a single-axis mirror IS a reflection, and any 2D
    // reflection can be written that way. Concretely: a +/-90 degree rotation offset plus (at
    // most) SpriteEffects.FlipVertically reproduces the exact diagonal-transpose matrix
    // (0, b, c, 0) that AnimationEditorAvalonia's FlipScaleCalculator.ComputeMatrix uses for the
    // SkiaSharp preview (issue #593 requires editor/runtime parity) — diagonal-only there is the
    // plain swap (x,y)->(y,x), which fixes the top-left/bottom-right corners and swaps
    // top-right/bottom-left, matching Tiled's actual diagonal-flip semantics. Width/Height,
    // origin, and scale are untouched — the 90 degree rotation is what swaps the on-screen
    // footprint, not a manual axis swap. See SpriteDiagonalFlipTests for the derivation check
    // against that matrix.
    //
    // Non-diagonal case unchanged: FlipVertically is the base effect that cancels the camera's
    // Y-flip (Batch.FlipsY); user-facing FlipVertical XORs it (net upside-down relative to
    // upright); FlipHorizontal is purely additive and unaffected by the Y-flip.
    internal (float rotation, SpriteEffects effects) ComputeDrawTransform()
    {
        bool effectiveFlipVertical = (Batch?.FlipsY ?? true) ^ FlipVertical;

        if (FlipDiagonal)
        {
            float rotation = RenderRotationRadians + (effectiveFlipVertical ? -MathHelper.PiOver2 : MathHelper.PiOver2);
            var effects = (FlipHorizontal == effectiveFlipVertical) ? SpriteEffects.FlipVertically : SpriteEffects.None;
            return (rotation, effects);
        }
        else
        {
            var effects = effectiveFlipVertical ? SpriteEffects.FlipVertically : SpriteEffects.None;
            if (FlipHorizontal) effects |= SpriteEffects.FlipHorizontally;
            return (RenderRotationRadians, effects);
        }
    }

    // The vertex color handed to SpriteBatch.Draw: Color, plus the frame's multiply color/alpha when
    // the Color group is frame-controlled, times Alpha. Extracted so it is testable without a GraphicsDevice.
    internal Color ComputeDrawColor()
    {
        var frame = CurrentFrame;
        var baseColor = frame != null && _frameControlledProperties.HasFlag(SpriteFrameProperties.Color)
            ? SpriteFrameColor.Apply(frame.ColorOperation, _frameColor, Color)
            : Color;
        return baseColor * Alpha;
    }

    // The world position handed to SpriteBatch.Draw: AbsoluteX/Y, rounded to the camera's pixel grid
    // when this sprite is in camera.SnappedSprites and draws through a world-space batch. Screen-space
    // batches have no camera grid, so they are never snapped. Extracted so it is testable without a
    // GraphicsDevice.
    internal Vector2 ComputeDrawPosition(Camera camera)
    {
        var position = new Vector2(AbsoluteX, AbsoluteY);
        if (camera.HasSnappedSprites
            && Batch is WorldSpaceBatch or WorldSpaceAddColorBatch
            && camera.SnappedSprites.Contains(this))
        {
            position = camera.SnapWorldPosition(position, Width, Height, AbsoluteRotation.Radians);
        }
        return position;
    }

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible || Texture == null) return;

        var (rotation, effects) = ComputeDrawTransform();

        // Origin and scale must be relative to the source region, not the full texture.
        float srcW = SourceRectangle?.Width  ?? Texture.Width;
        float srcH = SourceRectangle?.Height ?? Texture.Height;

        var color    = ComputeDrawColor();
        var origin   = new Vector2(srcW / 2f, srcH / 2f);
        var scale    = new Vector2(Width / srcW, Height / srcH);
        var position = ComputeDrawPosition(camera);

        // Only the add-color batches run the shader; set the offset for every sprite drawn through
        // one (even a zero AddColor) so a stale offset from the previous sprite can't leak.
        if (Batch is WorldSpaceAddColorBatch or ScreenSpaceAddColorBatch)
        {
            AddColorEffect.Get(spriteBatch.GraphicsDevice)
                .Parameters["ColorOffset"].SetValue(AddColor);
        }

        spriteBatch.Draw(
            Texture,
            position,
            SourceRectangle,
            color,
            rotation,
            origin,
            scale,
            effects,
            0f);
    }

    /// <summary>
    /// Detaches this sprite from its parent entity (which also unregisters it from the screen).
    /// Sprites do not own their <see cref="Texture"/> or <see cref="AnimationChains"/> — those
    /// remain managed by the <c>ContentLoader</c> that loaded them.
    /// </summary>
    public void Destroy()
    {
        if (Parent is Entity entity)
            entity.Remove(this);
        else
            Parent = null;
    }

    private void RecalculateDimensions()
    {
        if (!_textureScale.HasValue) return;
        float scale = _textureScale.Value;

        float sourceW, sourceH;
        if (_sourceRectangle is Rectangle r)
        {
            sourceW = r.Width;
            sourceH = r.Height;
        }
        else if (_texture is Texture2D tex)
        {
            sourceW = tex.Width;
            sourceH = tex.Height;
        }
        else
        {
            sourceW = FallbackSize;
            sourceH = FallbackSize;
        }

        _width  = sourceW * scale;
        _height = sourceH * scale;
    }
}
