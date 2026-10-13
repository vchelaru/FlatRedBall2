using System;
using Microsoft.Xna.Framework;

namespace FlatRedBall2.GoldenImage;

public enum ComparisonOutcome
{
    Match,
    PixelMismatch,
    SizeMismatch,
}

/// <param name="MismatchedPixelCount">Pixels with at least one channel off by more than the tolerance.</param>
/// <param name="MaxChannelDelta">Largest single-channel difference anywhere, including pixels within tolerance.</param>
/// <param name="Diff">Null for a size mismatch. See <see cref="GoldenComparer.Compare"/>.</param>
public sealed record ComparisonResult(
    ComparisonOutcome Outcome,
    int MismatchedPixelCount,
    int MaxChannelDelta,
    PixelImage? Diff,
    string Summary)
{
    public bool IsMatch => Outcome == ComparisonOutcome.Match;
}

public static class GoldenComparer
{
    /// <summary>
    /// Compares two images channel by channel (R, G, B and A). A pixel matches when every channel
    /// differs by at most <paramref name="tolerance"/>.
    /// </summary>
    /// <returns>
    /// A result whose <c>Diff</c> image has the actual picture dimmed to gray where pixels match
    /// and solid magenta where they do not. Images of different sizes are not compared pixelwise.
    /// </returns>
    public static ComparisonResult Compare(PixelImage expected, PixelImage actual, int tolerance)
    {
        if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));

        if (expected.Width != actual.Width || expected.Height != actual.Height)
        {
            return new ComparisonResult(
                ComparisonOutcome.SizeMismatch, 0, 0, null,
                $"size differs: expected {expected.Width}x{expected.Height}, actual {actual.Width}x{actual.Height}");
        }

        var diff = new Color[actual.Pixels.Length];
        int mismatched = 0;
        int maxDelta = 0;

        for (int i = 0; i < diff.Length; i++)
        {
            var e = expected.Pixels[i];
            var a = actual.Pixels[i];
            int delta = System.Math.Max(
                System.Math.Max(System.Math.Abs(e.R - a.R), System.Math.Abs(e.G - a.G)),
                System.Math.Max(System.Math.Abs(e.B - a.B), System.Math.Abs(e.A - a.A)));

            if (delta > maxDelta) maxDelta = delta;

            if (delta > tolerance)
            {
                mismatched++;
                diff[i] = new Color(255, 0, 255, 255);
            }
            else
            {
                byte gray = (byte)((a.R + a.G + a.B) / 3 / 4);
                diff[i] = new Color(gray, gray, gray, (byte)255);
            }
        }

        var image = new PixelImage(actual.Width, actual.Height, diff);
        if (mismatched == 0)
        {
            return new ComparisonResult(
                ComparisonOutcome.Match, 0, maxDelta, image,
                $"match (max channel delta {maxDelta}, tolerance {tolerance})");
        }

        return new ComparisonResult(
            ComparisonOutcome.PixelMismatch, mismatched, maxDelta, image,
            $"{mismatched} of {diff.Length} pixels differ by more than {tolerance} per channel (max channel delta {maxDelta})");
    }
}
