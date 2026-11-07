using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>Where duplicated chains land.</summary>
public static class TimelineDuplicate
{
    /// <summary>
    /// Seconds to shift every copy: the latest full end of <paramref name="selected"/> minus the earliest start, so the
    /// earliest copy starts exactly where the selection ends and the copies keep their distances from each other.
    /// </summary>
    public static double Shift(IReadOnlyList<TimelineBlock> selected) =>
        selected.Count == 0 ? 0 : selected.Max(block => block.FullEnd) - selected.Min(block => block.Start);
}
