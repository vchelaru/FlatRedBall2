using Microsoft.Xna.Framework;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;

namespace FlatRedBall2.Rendering;

/// <summary>
/// Applies an <see cref="AnimationFrame"/>'s authored color channels to a base draw
/// <see cref="Color"/>. Used by <see cref="Sprite.Draw"/>.
/// </summary>
public static class SpriteFrameColor
{
    /// <summary>
    /// Combines <paramref name="frame"/>'s color channels with <paramref name="baseColor"/> and
    /// returns the resulting <see cref="Color"/> to pass to <see cref="Microsoft.Xna.Framework.Graphics.SpriteBatch"/>.
    /// <para>
    /// <see cref="ColorOperation.Multiply"/> scales R/G/B by <see cref="AnimationFrame.Red"/>/
    /// <see cref="AnimationFrame.Green"/>/<see cref="AnimationFrame.Blue"/> (unset channels default
    /// to 255, the identity). <see cref="ColorOperation.Add"/> leaves RGB unchanged here — it is
    /// applied by the pixel shader in <see cref="Batches.WorldSpaceAddColorBatch"/>/
    /// <see cref="Batches.ScreenSpaceAddColorBatch"/> instead, via <see cref="Sprite.AddColor"/>; see <see cref="GetAddOffset(AnimationFrame)"/>.
    /// </para>
    /// <para>
    /// <see cref="AnimationFrame.Alpha"/> is independent of <see cref="AnimationFrame.ColorOperation"/>
    /// and always scales the base color's alpha when set.
    /// </para>
    /// </summary>
    public static Color Apply(AnimationFrame frame, Color baseColor)
        => Apply(frame.ColorOperation, new KeyframeColor(frame.Red, frame.Green, frame.Blue, frame.Alpha), baseColor);

    /// <summary>
    /// Same as <see cref="Apply(AnimationFrame, Color)"/> for explicit channels, which is how
    /// <see cref="Sprite"/> applies the keyframe-interpolated color between two frames.
    /// </summary>
    public static Color Apply(ColorOperation? operation, KeyframeColor channels, Color baseColor)
    {
        var result = baseColor;

        if (operation == ColorOperation.Multiply)
        {
            int r = System.Math.Clamp(channels.Red ?? 255, 0, 255);
            int g = System.Math.Clamp(channels.Green ?? 255, 0, 255);
            int b = System.Math.Clamp(channels.Blue ?? 255, 0, 255);
            result.R = (byte)(result.R * r / 255);
            result.G = (byte)(result.G * g / 255);
            result.B = (byte)(result.B * b / 255);
        }

        if (channels.Alpha.HasValue)
        {
            int a = System.Math.Clamp(channels.Alpha.Value, 0, 255);
            result.A = (byte)(result.A * a / 255);
        }

        return result;
    }

    /// <summary>
    /// Computes the per-channel offset (range -1..1) that <see cref="Batches.WorldSpaceAddColorBatch"/>/
    /// <see cref="Batches.ScreenSpaceAddColorBatch"/> pass to the add-color pixel shader for a frame
    /// whose <see cref="AnimationFrame.ColorOperation"/> is <see cref="ColorOperation.Add"/>. Unset
    /// channels default to 0 (the identity for Add). Values are clamped to the AnimationEditor's
    /// authored range (-255..255) before scaling.
    /// </summary>
    public static Vector3 GetAddOffset(AnimationFrame frame)
        => GetAddOffset(new KeyframeColor(frame.Red, frame.Green, frame.Blue, frame.Alpha));

    /// <summary>Same as <see cref="GetAddOffset(AnimationFrame)"/> for explicit (for example interpolated) channels.</summary>
    public static Vector3 GetAddOffset(KeyframeColor channels)
    {
        int r = System.Math.Clamp(channels.Red ?? 0, -255, 255);
        int g = System.Math.Clamp(channels.Green ?? 0, -255, 255);
        int b = System.Math.Clamp(channels.Blue ?? 0, -255, 255);
        return new Vector3(r / 255f, g / 255f, b / 255f);
    }
}
