using System;
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
}
