using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Animation;

/// <summary>
/// One frame of a texture-flipping animation. Holds a reference to the texture,
/// the source region in pixel coordinates, flip flags, and how long this frame displays.
/// </summary>
public class AnimationFrame
{
    /// <summary>The texture to display for this frame.</summary>
    public Texture2D? Texture;

    /// <summary>Name of the source texture file, used during loading.</summary>
    public string TextureName = string.Empty;

    /// <summary>How long this frame is displayed.</summary>
    public TimeSpan FrameLength;

    /// <summary>
    /// The pixel-coordinate region of <see cref="Texture"/> to render.
    /// Null means the entire texture.
    /// </summary>
    public Rectangle? SourceRectangle;

    /// <summary>When <c>true</c>, the source region is mirrored along the X axis at draw time.</summary>
    public bool FlipHorizontal;

    /// <summary>When <c>true</c>, the source region is mirrored along the Y axis at draw time.</summary>
    public bool FlipVertical;

    /// <summary>
    /// Local X offset applied to the sprite while this frame is displayed.
    /// Replaces the sprite's X each frame switch, so the character can shift
    /// position per-frame (e.g. a kick frame that leans the character forward).
    /// </summary>
    public float RelativeX;

    /// <summary>
    /// Local Y offset applied to the sprite while this frame is displayed.
    /// Replaces the sprite's Y each frame switch.
    /// </summary>
    public float RelativeY;

    /// <summary>
    /// Optional per-frame red channel, 0–255, or <c>null</c> when unset. <b>Not</b> applied by the
    /// engine: read it via <see cref="FlatRedBall2.Rendering.Sprite.CurrentFrame"/> and apply it in
    /// game code (tint, flash, etc.). See <see cref="Green"/>, <see cref="Blue"/>.
    /// </summary>
    public int? Red;

    /// <summary>Optional per-frame green channel, 0–255, or <c>null</c> when unset. See <see cref="Red"/>.</summary>
    public int? Green;

    /// <summary>Optional per-frame blue channel, 0–255, or <c>null</c> when unset. See <see cref="Red"/>.</summary>
    public int? Blue;

    /// <summary>
    /// Optional per-frame alpha (transparency) channel, 0–255, or <c>null</c> when unset. Straight
    /// transparency, independent of <see cref="ColorOperation"/>. Like the color channels it is <b>not</b>
    /// applied by the engine: read it via <see cref="FlatRedBall2.Rendering.Sprite.CurrentFrame"/> and apply
    /// it in game code (fade, dissolve, etc.).
    /// </summary>
    public int? Alpha;

    /// <summary>
    /// Optional per-frame color operation for how <see cref="Red"/>/<see cref="Green"/>/<see cref="Blue"/>
    /// combine with the texture, or <c>null</c> for none. Not applied by the engine — read it (with the
    /// channels) via <see cref="FlatRedBall2.Rendering.Sprite.CurrentFrame"/> and apply it in game code.
    /// </summary>
    public ColorOperation? ColorOperation;

    /// <summary>
    /// Per-frame shape definitions reconciled against the parent entity at frame switch time.
    /// Each entry must have a non-empty unique <see cref="AnimationShapeFrame.Name"/>. See
    /// <see cref="AnimationChainList"/> for the ownership rule that decides which entity shapes
    /// the animation system is allowed to touch.
    /// </summary>
    public List<AnimationShapeFrame> Shapes { get; } = new();
}
