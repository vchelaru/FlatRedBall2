using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.ViewModels;

/// <summary>The "(mixed)" rule shared by every Inspector that edits several items at once.</summary>
public static class MixedState
{
    /// <summary>The value every item has, or null when they differ (or there are none).</summary>
    public static T? Common<T>(IEnumerable<T> values) where T : struct
    {
        var list = values.Distinct().Take(2).ToList();
        return list.Count == 1 ? list[0] : null;
    }

    /// <summary>The string every item has, or null when they differ (or there are none).</summary>
    public static string? Common(IEnumerable<string> values)
    {
        var list = values.Distinct().Take(2).ToList();
        return list.Count == 1 ? list[0] : null;
    }
}
