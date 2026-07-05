using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// One frame of a texture-based animation. Holds a reference to the texture,
/// the source region in pixel coordinates, flip flags, per-frame offsets, and
/// how long this frame displays.
/// </summary>
public class AnimationFrame
{
    /// <summary>The texture to display for this frame. May be <c>null</c> if the texture failed to load.</summary>
    public Texture2D? Texture;

    /// <summary>Name of the source texture file, as stored in the .achx.</summary>
    public string TextureName = string.Empty;

    /// <summary>How long this frame is displayed.</summary>
    public TimeSpan FrameLength;

    /// <summary>
    /// The pixel-coordinate region of <see cref="Texture"/> to render.
    /// <c>null</c> means the entire texture.
    /// </summary>
    public Rectangle? SourceRectangle;

    /// <summary>When <c>true</c>, the source region is mirrored along the X axis.</summary>
    public bool FlipHorizontal;

    /// <summary>When <c>true</c>, the source region is mirrored along the Y axis.</summary>
    public bool FlipVertical;

    /// <summary>When <c>true</c>, the source region is transposed (flipped along the diagonal).</summary>
    public bool FlipDiagonal;

    /// <summary>
    /// Per-frame X offset applied while this frame is displayed. In screen pixels; positive
    /// shifts right. Applied by <see cref="SpriteBatchExtensions.DrawAnimation"/>.
    /// </summary>
    public float RelativeX;

    /// <summary>
    /// Per-frame Y offset applied while this frame is displayed. In screen pixels; positive
    /// shifts down (standard MonoGame screen-space convention).
    /// Applied by <see cref="SpriteBatchExtensions.DrawAnimation"/>.
    /// </summary>
    public float RelativeY;

    /// <summary>
    /// Per-frame red color channel (0-255). <c>null</c> means unset/not authored. Not applied
    /// automatically by <see cref="SpriteBatchExtensions.DrawAnimation"/> — available for game
    /// code to use.
    /// </summary>
    public int? Red;

    /// <summary>
    /// Per-frame green color channel (0-255). <c>null</c> means unset/not authored. Not applied
    /// automatically by <see cref="SpriteBatchExtensions.DrawAnimation"/> — available for game
    /// code to use.
    /// </summary>
    public int? Green;

    /// <summary>
    /// Per-frame blue color channel (0-255). <c>null</c> means unset/not authored. Not applied
    /// automatically by <see cref="SpriteBatchExtensions.DrawAnimation"/> — available for game
    /// code to use.
    /// </summary>
    public int? Blue;

    /// <summary>
    /// Per-frame alpha/transparency channel (0-255). <c>null</c> means unset/not authored. Not
    /// applied automatically by <see cref="SpriteBatchExtensions.DrawAnimation"/> — available for
    /// game code to use.
    /// </summary>
    public int? Alpha;

    /// <summary>
    /// How the color channels combine with the texture. <c>null</c> means none. Not applied
    /// automatically by <see cref="SpriteBatchExtensions.DrawAnimation"/> — available for game
    /// code to use.
    /// </summary>
    public ColorOperation? ColorOperation;

    /// <summary>
    /// Per-frame shape definitions. Present as data only — the caller is responsible for
    /// applying these to collision shapes. See <see cref="AnimationShapeFrame"/>.
    /// </summary>
    public List<AnimationShapeFrame> Shapes { get; } = new();
}
