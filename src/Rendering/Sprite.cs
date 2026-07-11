using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Animation;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering.Batches;

namespace FlatRedBall2.Rendering;

/// <summary>
/// Textured rectangle drawn each frame by the engine. Implements <see cref="IAttachable"/>
/// so it can be added to an <see cref="Entity"/> via <c>Entity.Add(sprite)</c>; its
/// <see cref="X"/>/<see cref="Y"/>/<see cref="Z"/> are then interpreted as offsets from
/// the parent.
/// <para>
/// Defaults to <see cref="Batches.WorldSpaceBatch"/> (camera-transformed, Y+ up). For HUD
/// or screen-space sprites, assign a screen-space layer or set <see cref="Batch"/> directly.
/// </para>
/// <para>
/// <b>Sizing modes:</b> see <see cref="TextureScale"/> for how <see cref="Width"/> and
/// <see cref="Height"/> are determined. <b>Animation:</b> assign <see cref="AnimationChains"/>
/// then call <see cref="PlayAnimation(string)"/>; per-frame texture, source rectangle, flip
/// flags, and relative offsets are applied automatically while <see cref="Animate"/> is true.
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

    /// <summary>The entity this sprite is attached to, or <c>null</c> if unattached. Set by <see cref="Entity.Add(IAttachable, Layer?)"/>.</summary>
    public Entity? Parent { get; set; }

    /// <summary>
    /// X position. Relative to <see cref="Parent"/> when attached, world when unattached.
    /// <para>
    /// <b>Animated sprites:</b> this value is overwritten with <see cref="AnimationFrame.RelativeX"/>
    /// on every frame switch. Assigning <see cref="X"/> in game code on a sprite that is playing
    /// an animation is clobbered on the next advance — bake the offset into the frames' <c>RelativeX</c>
    /// in the <c>.achx</c> / source asset instead, or attach the sprite to a child entity whose own
    /// <c>X</c> carries the offset.
    /// </para>
    /// </summary>
    public float X { get; set; }

    /// <summary>
    /// Y position (Y+ up in world space). Relative to <see cref="Parent"/> when attached, world when unattached.
    /// <para>
    /// <b>Animated sprites:</b> this value is overwritten with <see cref="AnimationFrame.RelativeY"/>
    /// on every frame switch. Assigning <see cref="Y"/> in game code on a sprite that is playing
    /// an animation is clobbered on the next advance — bake the offset into the frames' <c>RelativeY</c>
    /// in the <c>.achx</c> / source asset instead, or attach the sprite to a child entity whose own
    /// <c>Y</c> carries the offset.
    /// </para>
    /// </summary>
    public float Y { get; set; }

    /// <summary>Per-sprite draw order within its <see cref="Layer"/>. See <see cref="IRenderable.Z"/> for sorting semantics.</summary>
    public float Z { get; set; }

    /// <summary>Final world-space X after walking the parent chain.</summary>
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;

    /// <summary>Final world-space Y after walking the parent chain.</summary>
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;

    /// <summary>Final Z after walking the parent chain. Used for sort order; see <see cref="Z"/>.</summary>
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;

    /// <summary>Rotation about the Z axis. Relative to <see cref="Parent"/> when attached, world when unattached.</summary>
    public Angle Rotation { get; set; }

    /// <summary>Final world-space rotation after walking the parent chain.</summary>
    public Angle AbsoluteRotation => Parent != null ? Parent.AbsoluteRotation + Rotation : Rotation;

    /// <inheritdoc/>
    public Layer? Layer { get; set; }

    /// <summary>
    /// The batch this sprite renders through. Defaults to <see cref="WorldSpaceBatch.Instance"/>
    /// (camera-transformed, Y-flip). Reassign for screen-space rendering or custom blend states.
    /// </summary>
    public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;

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

    /// <summary>Mirrors the texture left-to-right when <c>true</c>. Overwritten by the current animation frame while <see cref="Animate"/> is on.</summary>
    public bool FlipHorizontal { get; set; }

    /// <summary>
    /// Mirrors the texture top-to-bottom when <c>true</c>. Note that <see cref="Batches.WorldSpaceBatch"/>
    /// already applies a base Y-flip to compensate for the camera transform — this property toggles
    /// <em>relative to upright</em>, not relative to texture pixels. Overwritten by the current animation
    /// frame while <see cref="Animate"/> is on.
    /// </summary>
    public bool FlipVertical { get; set; }

    /// <summary>
    /// Mirrors the texture across its diagonal (transposes width/height) when <c>true</c>.
    /// Combines with <see cref="FlipHorizontal"/>/<see cref="FlipVertical"/> in the same D→H→V
    /// order Tiled and the Animation Editor use. MonoGame's <c>SpriteEffects</c> has no diagonal
    /// value, so this is implemented as a rotation offset rather than a texture-sampling flag —
    /// see <see cref="ComputeDrawTransform"/>. Overwritten by the current animation frame while
    /// <see cref="Animate"/> is on.
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
            CurrentFrame = null;
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
    /// Whether the current animation loops. Defaults to <c>true</c>.
    /// When <c>false</c>, the animation stops on its last frame and fires <see cref="AnimationFinished"/>.
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
    /// Use this to read per-frame data the engine does not auto-apply — e.g.
    /// <see cref="AnimationFrame.Red"/>/<see cref="AnimationFrame.Green"/>/<see cref="AnimationFrame.Blue"/> —
    /// and apply it in game code.
    /// </summary>
    public AnimationFrame? CurrentFrame { get; private set; }

    /// <summary>Fired when a non-looping animation reaches its last frame.</summary>
    public event Action? AnimationFinished;

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
                ApplyCurrentFrame();
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
                    ApplyCurrentFrame();
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
        ApplyCurrentFrame();
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
        FlipHorizontal = frame.FlipHorizontal;
        FlipVertical = frame.FlipVertical;
        FlipDiagonal = frame.FlipDiagonal;
        X = frame.RelativeX;
        Y = frame.RelativeY;
        RecalculateDimensions();

        ReconcileShapes(frame);
    }

    private void ReconcileShapes(AnimationFrame frame)
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
                ApplyShapeEntry(name, entry);
            else
                HideShapeIfPresent(name);
        }
    }

    private void ApplyShapeEntry(string name, AnimationShapeFrame entry)
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
                    ApplyRectangle(r, rect);
                    Parent.Add(r);
                }
                else if (existing is AARect r)
                {
                    ApplyRectangle(r, rect);
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
                    ApplyCircle(c, circle);
                    Parent.Add(c);
                }
                else if (existing is Circle c)
                {
                    ApplyCircle(c, circle);
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
                    ApplyPolygon(p, poly);
                    Parent.Add(p);
                }
                else if (existing is Polygon p)
                {
                    ApplyPolygon(p, poly);
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

    private void HideShapeIfPresent(string name)
    {
        var existing = Parent!.FindShapeByName(name);
        if (existing == null) return;
        switch (existing)
        {
            case AARect r:
                r.IsVisible = false;
                Parent.SetDefaultCollision(r, false);
                break;
            case Circle c:
                c.IsVisible = false;
                Parent.SetDefaultCollision(c, false);
                break;
            case Polygon p:
                p.IsVisible = false;
                Parent.SetDefaultCollision(p, false);
                break;
        }
    }

    private static void ApplyRectangle(AARect r, AnimationAARectFrame entry)
    {
        r.Width = entry.Width;
        r.Height = entry.Height;
        r.X = entry.RelativeX;
        r.Y = entry.RelativeY;
        r.IsVisible = true;
    }

    private static void ApplyCircle(Circle c, AnimationCircleFrame entry)
    {
        c.Radius = entry.Radius;
        c.X = entry.RelativeX;
        c.Y = entry.RelativeY;
        c.IsVisible = true;
    }

    private static void ApplyPolygon(Polygon p, AnimationPolygonFrame entry)
    {
        p.SetPoints(entry.Points);
        p.X = entry.RelativeX;
        p.Y = entry.RelativeY;
        p.IsVisible = true;
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

    /// <inheritdoc/>
    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible || Texture == null) return;

        var (rotation, effects) = ComputeDrawTransform();

        // Origin and scale must be relative to the source region, not the full texture.
        float srcW = SourceRectangle?.Width  ?? Texture.Width;
        float srcH = SourceRectangle?.Height ?? Texture.Height;

        var color    = Color * Alpha;
        var origin   = new Vector2(srcW / 2f, srcH / 2f);
        var scale    = new Vector2(Width / srcW, Height / srcH);
        var position = new Vector2(AbsoluteX, AbsoluteY);

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
