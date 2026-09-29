using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// Extension methods on <see cref="SpriteBatch"/> for drawing an <see cref="AnimationPlayer{TFrame}"/>.
/// </summary>
public static class SpriteBatchExtensions
{
    /// <summary>
    /// Draws the current frame of <paramref name="player"/> at <paramref name="position"/>.
    /// <paramref name="position"/> is the <b>top-left</b> of the frame's source rectangle
    /// in screen pixels (Y down), before per-frame offset.
    /// Per-frame <see cref="AnimationFrameBase.RelativeX"/> and <see cref="AnimationFrameBase.RelativeY"/>
    /// are unscaled source pixels from the .achx; they are added as <c>offset * scale</c>
    /// (e.g. a kick frame that shifts the character forward).
    /// <para>
    /// <b>Coordinate convention:</b> positive <c>RelativeY</c> moves the sprite down,
    /// matching standard MonoGame <see cref="SpriteBatch"/> coordinates. If your .achx was
    /// authored in a Y-up world, negate <c>RelativeY</c> manually or flip the Y axis in your
    /// camera transform. Empty pixels inside <see cref="AnimationFrameBase.SourceRectangle"/> still
    /// count toward height — offset those frames in the editor if shoes do not sit on the
    /// last row of the cell.
    /// </para>
    /// </summary>
    /// <param name="spriteBatch">Must be between <see cref="SpriteBatch.Begin"/> and <see cref="SpriteBatch.End"/>.</param>
    /// <param name="player">The player whose <see cref="AnimationPlayer{TFrame}.CurrentFrame"/> will be drawn.</param>
    /// <param name="position">Top-left draw position in screen pixels (before per-frame offset).</param>
    /// <param name="color">
    /// Base tint color. Defaults to <see cref="Color.White"/> (no tint) when omitted. The frame's authored
    /// <see cref="AnimationFrameBase.Red"/>/<see cref="AnimationFrameBase.Green"/>/<see cref="AnimationFrameBase.Blue"/>
    /// are multiplied into this when <see cref="AnimationFrameBase.ColorOperation"/> is
    /// <see cref="ColorOperation.Multiply"/>, and <see cref="AnimationFrameBase.Alpha"/> is always
    /// multiplied into this color's alpha when set. See <see cref="AnimationFrameColor.Apply"/>.
    /// <see cref="ColorOperation.Add"/> is applied via a pixel shader instead — see the remarks below.
    /// </param>
    /// <param name="origin">
    /// Pivot point within the source rectangle, in pixels. Defaults to <c>null</c>, which centers
    /// on the frame (matching the AnimationEditor's default preview alignment and classic
    /// FlatRedBall's centered <c>Sprite</c>). Pass <see cref="Vector2.Zero"/> explicitly for
    /// top-left-origin drawing.
    /// </param>
    /// <param name="scale">Uniform scale factor. 1.0 = original size.</param>
    /// <param name="layerDepth">Depth value for layered sprites (0 = front, 1 = back).</param>
    /// <remarks>
    /// <b>Add frames break the batch.</b> <see cref="ColorOperation.Add"/> needs a custom
    /// <see cref="Effect"/> (<see cref="SpriteBatch"/> can only multiply, not offset, texture color),
    /// and an effect can only be set at <c>SpriteBatch.Begin</c> time. So for an
    /// <c>Add</c> frame this method ends whatever batch is currently open, draws this one sprite in
    /// its own effect-scoped batch, then resumes with a plain <c>spriteBatch.Begin()</c> — losing any
    /// custom transform/sampler/blend state the caller's original <c>Begin</c> set. If you rely on a
    /// custom transform (e.g. a camera matrix) and play <c>Add</c> frames, re-<c>Begin</c> with your
    /// own settings immediately after this call returns.
    /// </remarks>
    public static void DrawAnimation(
        this SpriteBatch spriteBatch,
        AnimationPlayer<AnimationFrame> player,
        Vector2 position,
        Color? color = null,
        Vector2? origin = null,
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

        var drawColor = AnimationFrameColor.Apply(frame, ResolveBaseColor(color));
        Rectangle? sourceRectangle = frame.SourceRectangle?.ToXnaRectangle();
        var resolvedOrigin = ResolveOrigin(
            origin,
            width: frame.SourceRectangle?.Width ?? frame.Texture.Width,
            height: frame.SourceRectangle?.Height ?? frame.Texture.Height);

        if (frame.ColorOperation == ColorOperation.Add)
        {
            var effect = AddColorEffect.Get(spriteBatch.GraphicsDevice);
            effect.Parameters["ColorOffset"].SetValue(AnimationFrameColor.GetAddOffset(frame));

            spriteBatch.End();
            spriteBatch.Begin(effect: effect);
            spriteBatch.Draw(frame.Texture, drawPos, sourceRectangle, drawColor, 0f, resolvedOrigin, scale, effects, layerDepth);
            spriteBatch.End();
            spriteBatch.Begin();
            return;
        }

        spriteBatch.Draw(
            frame.Texture,
            drawPos,
            sourceRectangle,
            drawColor,
            0f,
            resolvedOrigin,
            scale,
            effects,
            layerDepth);
    }

    /// <summary>Resolves the base tint <see cref="DrawAnimation"/> uses when <c>color</c> is omitted.</summary>
    internal static Color ResolveBaseColor(Color? color) => color ?? Color.White;

    /// <summary>
    /// Resolves the pivot point <see cref="DrawAnimation"/> uses when <c>origin</c> is omitted:
    /// the center of a <paramref name="width"/> x <paramref name="height"/> frame.
    /// </summary>
    internal static Vector2 ResolveOrigin(Vector2? origin, int width, int height) =>
        origin ?? new Vector2(width / 2f, height / 2f);
}
