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

    /// <summary>When <c>true</c>, the source region is transposed (flipped along the diagonal).
    /// Authored and round-tripped by the Animation Editor; <see cref="SpriteBatchExtensions.DrawAnimation"/>
    /// does <b>not</b> apply it — MonoGame's <see cref="SpriteEffects"/> has no diagonal option.</summary>
    public bool FlipDiagonal;

    /// <summary>
    /// Per-frame X offset applied while this frame is displayed. Unscaled source pixels from
    /// the .achx; positive shifts right. <see cref="SpriteBatchExtensions.DrawAnimation"/>
    /// applies this as <c>RelativeX * scale</c>.
    /// </summary>
    public float RelativeX;

    /// <summary>
    /// Per-frame Y offset applied while this frame is displayed. Unscaled source pixels from
    /// the .achx; positive shifts down (standard MonoGame screen-space convention).
    /// <see cref="SpriteBatchExtensions.DrawAnimation"/> applies this as <c>RelativeY * scale</c>.
    /// </summary>
    public float RelativeY;

    /// <summary>
    /// Per-frame red color channel (0-255), already resolved for the "sticky" .achx semantic (an
    /// omitted channel inherits the last frame that set it — see <see cref="AchxLoader"/>).
    /// <c>null</c> means no frame in this chain ever set it. Applied automatically by
    /// <see cref="SpriteBatchExtensions.DrawAnimation"/> when <see cref="ColorOperation"/> is
    /// <see cref="FlatRedBall.AnimationChain.ColorOperation.Multiply"/> — see <see cref="AnimationFrameColor.Apply"/>.
    /// </summary>
    public int? Red;

    /// <summary>Per-frame green color channel (0-255), sticky-resolved. See <see cref="Red"/>.</summary>
    public int? Green;

    /// <summary>Per-frame blue color channel (0-255), sticky-resolved. See <see cref="Red"/>.</summary>
    public int? Blue;

    /// <summary>
    /// Per-frame alpha/transparency channel (0-255), sticky-resolved. <c>null</c> means no frame in
    /// this chain ever set it. Independent of <see cref="ColorOperation"/> — always applied by
    /// <see cref="SpriteBatchExtensions.DrawAnimation"/> when set. See <see cref="Red"/>.
    /// </summary>
    public int? Alpha;

    /// <summary>
    /// How <see cref="Red"/>/<see cref="Green"/>/<see cref="Blue"/> combine with the texture,
    /// sticky-resolved. <c>null</c> means none. <see cref="FlatRedBall.AnimationChain.ColorOperation.Multiply"/>
    /// is applied automatically by <see cref="SpriteBatchExtensions.DrawAnimation"/>;
    /// <see cref="FlatRedBall.AnimationChain.ColorOperation.Add"/> is also applied, via a pixel
    /// shader that ends and restarts the <see cref="Microsoft.Xna.Framework.Graphics.SpriteBatch"/>
    /// — see the remarks on <see cref="SpriteBatchExtensions.DrawAnimation"/>.
    /// </summary>
    public ColorOperation? ColorOperation;

    /// <summary>
    /// Per-frame shape definitions. Present as data only — the caller is responsible for
    /// applying these to collision shapes. See <see cref="AnimationShapeFrame"/>.
    /// </summary>
    public List<AnimationShapeFrame> Shapes { get; } = new();
}
