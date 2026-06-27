using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.ViewModels;

/// <summary>Result of hit-testing a timeline x-position: which frame cell, where within it.</summary>
public readonly record struct TimelineScrubResult(int FrameIndex, double LocalX, double Fraction);

/// <summary>
/// Pure geometry for timeline scrubbing: maps a content-space x-position (pixels from the strip's
/// left edge) onto the frame cell under it. Clicks left of the first cell clamp to its start and
/// clicks past the last cell clamp to its end, so a scrub always lands on a real frame.
/// </summary>
public static class TimelineScrubMapper
{
    public static TimelineScrubResult Resolve(double contentX, IReadOnlyList<double> cellWidths)
    {
        if (cellWidths is null || cellWidths.Count == 0)
            return new TimelineScrubResult(0, 0, 0);

        double left = 0;
        for (int i = 0; i < cellWidths.Count; i++)
        {
            double w = cellWidths[i];
            // The last cell absorbs everything to its right so off-the-end clicks clamp here.
            bool isLast = i == cellWidths.Count - 1;
            if (contentX < left + w || isLast)
            {
                double localX   = Math.Clamp(contentX - left, 0, w);
                double fraction = w > 0 ? Math.Clamp((contentX - left) / w, 0, 1) : 0;
                return new TimelineScrubResult(i, localX, fraction);
            }
            left += w;
        }

        // Unreachable: the isLast branch above always returns on the final cell.
        return new TimelineScrubResult(cellWidths.Count - 1, 0, 1);
    }
}
