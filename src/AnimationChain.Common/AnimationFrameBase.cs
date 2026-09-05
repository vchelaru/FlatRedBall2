using System;
using System.Collections.Generic;
using FlatRedBall2.Animation;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// One frame of a texture-based animation, minus the texture handle itself — everything a
/// playback runtime needs (timing, flip flags, per-frame offsets, per-frame color, shapes) without
/// depending on any specific renderer. See <see cref="AnimationFrameBase{TTexture}"/> for the
/// texture-bearing subclass a renderer closes this over.
/// </summary>
public abstract class AnimationFrameBase
{
    /// <summary>Name of the source texture file, as stored in the .achx.</summary>
    public string TextureName = string.Empty;

    /// <summary>How long this frame is displayed.</summary>
    public TimeSpan FrameLength;

    /// <summary>When <c>true</c>, the source region is mirrored along the X axis.</summary>
    public bool FlipHorizontal;

    /// <summary>When <c>true</c>, the source region is mirrored along the Y axis.</summary>
    public bool FlipVertical;

    /// <summary>
    /// When <c>true</c>, the source region is transposed (flipped along the diagonal). Authored
    /// and round-tripped by the Animation Editor; a renderer's draw call decides whether to apply
    /// it — MonoGame's <c>SpriteEffects</c> has no diagonal option, for example.
    /// </summary>
    public bool FlipDiagonal;

    /// <summary>Per-frame X offset applied while this frame is displayed. Unscaled source pixels from the .achx; positive shifts right.</summary>
    public float RelativeX;

    /// <summary>
    /// Per-frame Y offset applied while this frame is displayed. Unscaled source pixels from the .achx.
    /// Raised in Y-up world space (FRB <c>Sprite</c> at <c>Sprite.cs</c>, the Animation Editor preview):
    /// positive <c>RelativeY</c> shifts up. Renderers that draw in Y-down screen space (MonoGame
    /// <c>SpriteBatch</c> via <c>SpriteBatchExtensions.DrawAnimation</c>, Gum child
    /// <c>PixelsFromMiddle</c>) must negate it — positive shifts down there.
    /// </summary>
    public float RelativeY;

    /// <summary>
    /// Per-frame red color channel (0-255), or <c>null</c> if unset. Whether/how a renderer
    /// applies this (e.g. combined with <see cref="ColorOperation"/>) is that renderer's choice.
    /// See <see cref="Green"/>, <see cref="Blue"/>.
    /// </summary>
    public int? Red;

    /// <summary>Per-frame green color channel (0-255), or <c>null</c> if unset. See <see cref="Red"/>.</summary>
    public int? Green;

    /// <summary>Per-frame blue color channel (0-255), or <c>null</c> if unset. See <see cref="Red"/>.</summary>
    public int? Blue;

    /// <summary>Per-frame alpha/transparency channel (0-255), or <c>null</c> if unset. Independent of <see cref="ColorOperation"/>. See <see cref="Red"/>.</summary>
    public int? Alpha;

    /// <summary>How <see cref="Red"/>/<see cref="Green"/>/<see cref="Blue"/> combine with the texture, or <c>null</c> for none.</summary>
    public ColorOperation? ColorOperation;

    /// <summary>
    /// Per-frame shape definitions. Present as data only — the caller is responsible for applying
    /// these to real collision shapes.
    /// </summary>
    public List<AnimationShapeFrame> Shapes { get; } = new();

    /// <summary>The pixel-coordinate region of the texture to render. <c>null</c> means the entire texture.</summary>
    public PixelRectangle? SourceRectangle;
}

/// <summary>
/// <see cref="AnimationFrameBase"/> plus the texture handle itself — the only part that varies by
/// renderer. A renderer closes this generic over its own texture type, e.g.
/// <c>class AnimationFrame : AnimationFrameBase&lt;Texture2D&gt; { }</c>.
/// </summary>
public abstract class AnimationFrameBase<TTexture> : AnimationFrameBase
{
    /// <summary>The texture to display for this frame. May be <c>null</c> if the texture failed to load.</summary>
    public TTexture? Texture;
}
