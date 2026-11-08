using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Where a track's sprite sits on screen: its centre and half size, in the preview's pixels. Built from the sprite as the
/// preview draws it, so a flipped track is boxed where its mirrored frame lands.
/// </summary>
public readonly record struct SpriteBox(int Sequence, int Track, double CenterX, double CenterY, double HalfWidth, double HalfHeight)
{
    public bool Contains(double x, double y) =>
        x >= CenterX - HalfWidth && x <= CenterX + HalfWidth && y >= CenterY - HalfHeight && y <= CenterY + HalfHeight;

    /// <summary>
    /// The box of <paramref name="sprite"/> for a <paramref name="sourceWidth"/> by <paramref name="sourceHeight"/> frame, with the
    /// entity origin at (<paramref name="originX"/>, <paramref name="originY"/>) on screen. Offsets draw scaled by
    /// <paramref name="offsetMultiplier"/> and <paramref name="zoom"/>, the frame's pixels by <paramref name="zoom"/> alone; world Y is up, screen Y down.
    /// </summary>
    public static SpriteBox Of(SequenceSprite sprite, int sourceWidth, int sourceHeight, double zoom, double offsetMultiplier,
        double originX, double originY)
    {
        var sample = sprite.Sample();
        double scale = offsetMultiplier * zoom;
        return new SpriteBox(sprite.Sequence, sprite.Track, originX + sample.OffsetX * scale, originY - sample.OffsetY * scale,
            sourceWidth * zoom / 2, sourceHeight * zoom / 2);
    }
}

/// <summary>Which track's sprite is under the pointer in the sequence preview, so a click can select that track.</summary>
public static class TrackHitTest
{
    /// <summary>
    /// The sprite under (<paramref name="x"/>, <paramref name="y"/>) that a click selects: the frontmost box of
    /// <paramref name="sequence"/>, given back to front (the order <see cref="SequenceScene"/> returns). Other sequences'
    /// sprites do not block or take the click. Null when nothing is hit.
    /// </summary>
    public static SpriteBox? TopmostAt(IReadOnlyList<SpriteBox> backToFront, double x, double y, int sequence)
    {
        for (int i = backToFront.Count - 1; i >= 0; i--)
            if (backToFront[i].Sequence == sequence && backToFront[i].Contains(x, y)) return backToFront[i];
        return null;
    }
}
