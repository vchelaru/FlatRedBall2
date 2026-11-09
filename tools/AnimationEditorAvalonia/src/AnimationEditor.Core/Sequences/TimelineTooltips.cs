using System;

namespace AnimationEditor.Core.Sequences;

/// <summary>Timing and decisions shared by the timeline's hover tooltips (track names, chain blocks).</summary>
public static class TimelineTooltips
{
    /// <summary>How long the pointer rests before a timeline tooltip shows: quicker than the ~1 s default, so a cut-off name reads as soon as it is looked at.</summary>
    public static readonly TimeSpan ShowDelay = TimeSpan.FromMilliseconds(200);

    /// <summary>The gap kept between a header name and the column's right edge.</summary>
    public const double NameRightPadding = 8;

    /// <summary>The width a track name may use in a name column <paramref name="columnWidth"/> wide.</summary>
    public static double NameRoom(double columnWidth) => columnWidth - TimelineLayout.HeaderNameLeft - NameRightPadding;

    /// <summary>True when text <paramref name="textWidth"/> wide does not fit in <paramref name="availableWidth"/>, so it is drawn with an ellipsis.</summary>
    public static bool IsTruncated(double textWidth, double availableWidth) => textWidth > availableWidth + 0.5;

    /// <summary>Where a tooltip pill's top-left goes for a pointer at (<paramref name="anchorX"/>, <paramref name="anchorY"/>): below and right of it, kept inside the surface. The one position rule for every timeline tooltip.</summary>
    public static (double X, double Y) PillOrigin(double anchorX, double anchorY, double width, double height, double surfaceWidth, double surfaceHeight) =>
        (Math.Clamp(anchorX + 12, 0, Math.Max(0, surfaceWidth - width)), Math.Clamp(anchorY + 16, 0, Math.Max(0, surfaceHeight - height)));
}
