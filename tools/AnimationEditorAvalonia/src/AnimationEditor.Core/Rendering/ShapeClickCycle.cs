using System.Collections.Generic;

namespace AnimationEditor.Core.Rendering;

/// <summary>Click-to-cycle order for overlapping collision shapes in the preview.</summary>
public static class ShapeClickCycle
{
    /// <summary>
    /// The shape a click selects, given every shape under the cursor <paramref name="stackTopFirst"/>
    /// (topmost first). Clicking the already-selected shape advances to the next one beneath it,
    /// wrapping to the top; otherwise the topmost shape wins. Null when nothing is under the cursor.
    /// </summary>
    public static object? NextTarget(IReadOnlyList<object> stackTopFirst, object? selected)
    {
        if (stackTopFirst.Count == 0) return null;
        for (int i = 0; i < stackTopFirst.Count; i++)
            if (ReferenceEquals(stackTopFirst[i], selected))
                return stackTopFirst[(i + 1) % stackTopFirst.Count];
        return stackTopFirst[0];
    }
}
