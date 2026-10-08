using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.DragDrop;

/// <summary>
/// Whether a shape drop is a same-type reorder (<see cref="Valid"/>), lands on another shape type
/// (<see cref="CrossType"/>, which the file format cannot keep so the drop shows a toast), or lands
/// anywhere else in the tree (<see cref="None"/>, which cancels silently).
/// </summary>
public enum ShapeDropOutcome { Valid, CrossType, None }

/// <summary>
/// The resolved drop for a shape drag. <see cref="InsertIndex"/> is a position in the frame's current
/// shape list, before the dragged shape is removed.
/// </summary>
public readonly record struct ShapeDropTarget(ShapeDropOutcome Outcome, int InsertIndex)
{
    public static readonly ShapeDropTarget None = new(ShapeDropOutcome.None, -1);
    public bool IsValid => Outcome == ShapeDropOutcome.Valid;
}

/// <summary>Pure logic for dragging a shape row to a new position among shapes of its own type.</summary>
public static class ShapeDropResolver
{
    /// <summary>Resolves a drop of <paramref name="dragged"/> onto <paramref name="hoveredData"/> in the same frame's <paramref name="shapes"/>.</summary>
    public static ShapeDropTarget Resolve(object? hoveredData, FrameRowHalf half, object dragged, ShapesSave shapes)
    {
        if (hoveredData is null || shapes.IndexOf(dragged) < 0) return ShapeDropTarget.None;
        int hoveredIndex = shapes.IndexOf(hoveredData);
        if (hoveredIndex < 0) return ShapeDropTarget.None;
        if (hoveredData.GetType() != dragged.GetType())
            return new ShapeDropTarget(ShapeDropOutcome.CrossType, -1);
        return new ShapeDropTarget(ShapeDropOutcome.Valid, half == FrameRowHalf.Upper ? hoveredIndex : hoveredIndex + 1);
    }

    /// <summary>
    /// The shape order after moving <paramref name="dragged"/> to <paramref name="insertIndex"/> (a position in the
    /// current list, before removal), clamped to the shape's type group. Equal to the current order for a no-op drop.
    /// </summary>
    public static List<object> ApplyDrop(ShapesSave shapes, object dragged, int insertIndex)
    {
        var order = shapes.Shapes.ToList();
        int from = order.IndexOf(dragged);
        if (from < 0) return order;
        var group = Enumerable.Range(0, order.Count).Where(i => order[i].GetType() == dragged.GetType()).ToList();
        int clamped = System.Math.Clamp(insertIndex, group[0], group[^1] + 1);
        order.RemoveAt(from);
        order.Insert(clamped > from ? clamped - 1 : clamped, dragged);
        return order;
    }
}
