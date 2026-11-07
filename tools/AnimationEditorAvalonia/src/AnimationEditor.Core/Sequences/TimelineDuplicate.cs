using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>Where duplicated chains land.</summary>
public static class TimelineDuplicate
{
    /// <summary>
    /// Seconds to shift every copy: the latest full end of <paramref name="selected"/> minus the earliest start, so the
    /// earliest copy starts exactly where the selection ends and the copies keep their distances from each other. Rounded to a
    /// microsecond: frame lengths are floats, and their noise should not end up as digits in the saved file.
    /// </summary>
    public static double Shift(IReadOnlyList<TimelineBlock> selected) =>
        selected.Count == 0 ? 0 : Math.Round(selected.Max(block => block.FullEnd) - selected.Min(block => block.Start), 6);
}
