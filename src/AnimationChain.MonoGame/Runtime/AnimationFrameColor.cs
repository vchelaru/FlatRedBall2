using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall.AnimationChain;

/// <summary>
/// Applies an <see cref="AnimationFrame"/>'s authored color channels to a base draw
/// <see cref="Color"/>. Used by <see cref="SpriteBatchExtensions.DrawAnimation"/>.
/// </summary>
public static class AnimationFrameColor
{
    /// <summary>
    /// Combines <paramref name="frame"/>'s color channels with <paramref name="baseColor"/> and
    /// returns the resulting <see cref="Color"/> to pass to <see cref="SpriteBatch"/>'s draw call.
    /// <para>
    /// <see cref="ColorOperation.Multiply"/> scales R/G/B by <see cref="AnimationFrameBase.Red"/>/
    /// <see cref="AnimationFrameBase.Green"/>/<see cref="AnimationFrameBase.Blue"/> (unset channels
    /// default to 255, the identity). <see cref="ColorOperation.Add"/> leaves RGB unchanged here —
    /// it is applied by a pixel shader instead, since <see cref="SpriteBatch"/> can only multiply,
    /// not offset, texture color. See <see cref="GetAddOffset"/> and
    /// <see cref="SpriteBatchExtensions.DrawAnimation"/>.
    /// </para>
    /// <para>
    /// <see cref="AnimationFrameBase.Alpha"/> is independent of <see cref="AnimationFrameBase.ColorOperation"/>
    /// and always scales the base color's alpha when set.
    /// </para>
    /// </summary>
    public static Color Apply(AnimationFrame frame, Color baseColor)
    {
        var result = baseColor;

        if (frame.ColorOperation == ColorOperation.Multiply)
        {
            int r = System.Math.Clamp(frame.Red ?? 255, 0, 255);
            int g = System.Math.Clamp(frame.Green ?? 255, 0, 255);
            int b = System.Math.Clamp(frame.Blue ?? 255, 0, 255);
            result.R = (byte)(result.R * r / 255);
            result.G = (byte)(result.G * g / 255);
            result.B = (byte)(result.B * b / 255);
        }

        if (frame.Alpha.HasValue)
        {
            int a = System.Math.Clamp(frame.Alpha.Value, 0, 255);
            result.A = (byte)(result.A * a / 255);
        }

        return result;
    }

    /// <summary>
    /// Computes the per-channel offset (range -1..1) that <see cref="SpriteBatchExtensions.DrawAnimation"/>
    /// passes to the add-color pixel shader for a frame whose <see cref="AnimationFrameBase.ColorOperation"/>
    /// is <see cref="ColorOperation.Add"/>. Unset channels default to 0 (the identity for Add). Values
    /// are clamped to the AnimationEditor's authored range (-255..255) before scaling.
    /// </summary>
    public static Vector3 GetAddOffset(AnimationFrame frame)
    {
        int r = System.Math.Clamp(frame.Red ?? 0, -255, 255);
        int g = System.Math.Clamp(frame.Green ?? 0, -255, 255);
        int b = System.Math.Clamp(frame.Blue ?? 0, -255, 255);
        return new Vector3(r / 255f, g / 255f, b / 255f);
    }
}
