using System.Collections.Generic;

namespace AnimationEditor.Core.Rendering;

/// <summary>Ctrl/Cmd+click in the preview: toggles a shape in or out of the shape selection.</summary>
public static class ShapeSelectionToggle
{
    /// <summary>
    /// The shape selection after Ctrl/Cmd+clicking <paramref name="clicked"/>: removed when it is in
    /// <paramref name="selected"/>, appended otherwise. Order is kept, so the first entry stays the primary
    /// shape until it is deselected.
    /// </summary>
    public static List<object> Toggle(IReadOnlyList<object> selected, object clicked)
    {
        var result = new List<object>(selected.Count + 1);
        bool found = false;
        foreach (var shape in selected)
        {
            if (ReferenceEquals(shape, clicked)) { found = true; continue; }
            if (!result.Contains(shape)) result.Add(shape);
        }
        if (!found) result.Add(clicked);
        return result;
    }
}
