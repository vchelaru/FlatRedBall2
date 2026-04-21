using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Animation;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering.Batches;

namespace FlatRedBall2.Rendering;

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

    // IAttachable
    public Entity? Parent { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float AbsoluteX => Parent != null ? Parent.AbsoluteX + X : X;
    public float AbsoluteY => Parent != null ? Parent.AbsoluteY + Y : Y;
    public float AbsoluteZ => Parent != null ? Parent.AbsoluteZ + Z : Z;

    // Rotation
    public Angle Rotation { get; set; }
    public Angle AbsoluteRotation => Parent != null ? Parent.AbsoluteRotation + Rotation : Rotation;

    // IRenderable
    public Layer? Layer { get; set; }
    public IRenderBatch Batch { get; set; } = WorldSpaceBatch.Instance;
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

    public Color Color { get; set; } = Color.White;
    public float Alpha { get; set; } = 1f;
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

    // Flip
    public bool FlipHorizontal { get; set; }
    public bool FlipVertical { get; set; }

    // Animation

    /// <summary>
    /// The collection of named animation chains available to this sprite.
    /// Assign before calling <see cref="PlayAnimation(string)"/>.
    /// </summary>
    public AnimationChainList? AnimationChains
    {
        get => _animationChains;
        set
        {
            _animationChains = value;
            _currentChainIndex = -1;
        }
    }

    /// <summary>Whether the sprite is currently advancing through an animation each frame.</summary>
    public bool Animate { get; set; }

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

    /// <summary>Fired when a non-looping animation reaches its last frame.</summary>
    public event Action? AnimationFinished;

    /// <summary>
    /// Starts playing the named animation from the beginning.
    /// If <see cref="AnimationChains"/> is <c>null</c> or the name is missing, this is a no-op.
    /// Calling this with the current chain name still restarts to frame 0.
    /// </summary>
    public void PlayAnimation(string name)
    {
        if (_animationChains == null) return;

        for (int i = 0; i < _animationChains.Count; i++)
        {
            if (_animationChains[i].Name == name)
            {
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

        float totalLength = chain.TotalLength;
        if (totalLength <= 0f) return;

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
            t -= chain[i].FrameLength;
            if (t <= 0)
            {
                _currentFrameIndex = i;
                break;
            }
        }

        ApplyCurrentFrame();
    }

    /// <summary>
    /// Applies the current frame to render state. This updates <see cref="Texture"/>,
    /// <see cref="SourceRectangle"/>, flip flags, and relative offsets. When
    /// <see cref="TextureScale"/> is non-null, width/height are recalculated from the frame's
    /// source rectangle.
    /// </summary>
    private void ApplyCurrentFrame()
    {
        if (_animationChains == null || _currentChainIndex < 0) return;
        var chain = _animationChains[_currentChainIndex];
        if (chain.Count == 0) return;

        var frame = chain[System.Math.Clamp(_currentFrameIndex, 0, chain.Count - 1)];
        _texture = frame.Texture;
        _sourceRectangle = frame.SourceRectangle;
        FlipHorizontal = frame.FlipHorizontal;
        FlipVertical = frame.FlipVertical;
        X = frame.RelativeX;
        Y = frame.RelativeY;
        RecalculateDimensions();
    }

    public void Draw(SpriteBatch spriteBatch, Camera camera)
    {
        if (!IsVisible || Texture == null) return;

        // When the batch's transform flips Y (world Y+ up → screen Y+ down), sprite texture pixels
        // would appear upside-down without compensation. FlipVertically is the base effect that
        // cancels the camera flip. User-facing FlipVertical XORs this, producing a net upside-down
        // appearance relative to normal. FlipHorizontal is purely additive and unaffected by the Y-flip.
        var effects = (Batch?.FlipsY ?? true) ? SpriteEffects.FlipVertically : SpriteEffects.None;
        if (FlipHorizontal) effects |= SpriteEffects.FlipHorizontally;
        if (FlipVertical)   effects ^= SpriteEffects.FlipVertically;

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
            -AbsoluteRotation.Radians,
            origin,
            scale,
            effects,
            0f);
    }

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
