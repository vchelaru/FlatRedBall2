using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// Extension methods on <see cref="SpriteBatch"/> for drawing an <see cref="AnimationPlayer"/>.
/// </summary>
public static class SpriteBatchExtensions
{
    /// <summary>
    /// Draws the current frame of <paramref name="player"/> at <paramref name="position"/>.
    /// Per-frame <see cref="AnimationFrame.RelativeX"/> and <see cref="AnimationFrame.RelativeY"/>
    /// are added to <paramref name="position"/> automatically — these represent authoring-time
    /// offsets baked into the .achx (e.g. a kick frame that shifts the character forward).
    /// <para>
    /// <b>Coordinate convention:</b> <c>RelativeY</c> is in screen-down space (positive = down),
    /// matching standard MonoGame <see cref="SpriteBatch"/> coordinates. If your .achx was
    /// authored in a Y-up world, negate <c>RelativeY</c> manually or flip the Y axis in your
    /// camera transform.
    /// </para>
    /// </summary>
    /// <param name="spriteBatch">Must be between <see cref="SpriteBatch.Begin"/> and <see cref="SpriteBatch.End"/>.</param>
    /// <param name="player">The player whose <see cref="AnimationPlayer.CurrentFrame"/> will be drawn.</param>
    /// <param name="position">Top-left draw position in screen pixels (before per-frame offset).</param>
    /// <param name="color">Tint color. Use <see cref="Color.White"/> for no tint.</param>
    /// <param name="origin">
    /// Pivot point within the source rectangle, in pixels. Defaults to <see cref="Vector2.Zero"/>
    /// (top-left). Pass <c>new Vector2(width/2, height/2)</c> for center-origin drawing.
    /// </param>
    /// <param name="scale">Uniform scale factor. 1.0 = original size.</param>
    /// <param name="layerDepth">Depth value for layered sprites (0 = front, 1 = back).</param>
    public static void DrawAnimation(
        this SpriteBatch spriteBatch,
        AnimationPlayer player,
        Vector2 position,
        Color color,
        Vector2 origin = default,
        float scale = 1f,
        float layerDepth = 0f)
    {
        var frame = player.CurrentFrame;
        if (frame?.Texture == null) return;

        var effects = SpriteEffects.None;
        if (frame.FlipHorizontal) effects |= SpriteEffects.FlipHorizontally;
        if (frame.FlipVertical)   effects |= SpriteEffects.FlipVertically;

        var drawPos = new Vector2(
            position.X + frame.RelativeX * scale,
            position.Y + frame.RelativeY * scale);

        spriteBatch.Draw(
            frame.Texture,
            drawPos,
            frame.SourceRectangle,
            color,
            0f,
            origin,
            scale,
            effects,
            layerDepth);
    }
}
