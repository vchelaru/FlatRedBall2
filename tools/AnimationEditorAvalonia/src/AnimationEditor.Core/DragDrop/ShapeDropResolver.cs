using FlatRedBall2.AnimationEditorCommon;
using System;

namespace AnimationEditor.Core.DragDrop;

/// <summary>Which half of a shape row the pointer is over during a shape drag.</summary>
public enum ShapeRowHalf { Upper, Lower }

/// <summary>
/// The resolved landing slot for a shape drag within <see cref="Frame"/>: <see cref="InsertIndex"/>
/// is interpreted against the frame's shape list before the dragged shape is removed.
/// <see cref="IsValid"/> is false when the drop must be rejected (or would change nothing) and no
/// indicator line should be shown.
/// </summary>
public readonly record struct ShapeDropTarget(AnimationFrameSave? Frame, int InsertIndex, bool IsValid)
{
    public static readonly ShapeDropTarget None = new(null, -1, false);
}

/// <summary>
/// Pure drag-and-drop logic for reordering a shape within its frame in the tree. Shapes only
/// reorder inside their own frame; side-effect free so it can be unit-tested without Avalonia.
/// </summary>
public static class ShapeDropResolver
{
    /// <summary>
    /// A sibling shape resolves to upper-half-before / lower-half-after that shape; the shape's own
    /// frame row appends to the end; anything else (another frame's shapes, chains, nothing) is no
    /// drop. A slot directly before or after the dragged shape is reported invalid because dropping
    /// there changes nothing.
    /// </summary>
    public static ShapeDropTarget Resolve(
        object? nodeData,
        ShapeRowHalf half,
        object draggedShape,
        AnimationFrameSave sourceFrame,
        Func<object, AnimationFrameSave?> getFrameContainingShape)
    {
        var shapes = sourceFrame.ShapesSave?.Shapes;
        if (shapes is null) return ShapeDropTarget.None;
        int draggedIndex = shapes.IndexOf(draggedShape);
        if (draggedIndex < 0) return ShapeDropTarget.None;

        int insertIndex;
        switch (nodeData)
        {
            case AnimationFrameSave frame when ReferenceEquals(frame, sourceFrame):
                insertIndex = shapes.Count;
                break;

            case AARectSave or CircleSave or PolygonSave:
                if (!ReferenceEquals(getFrameContainingShape(nodeData), sourceFrame)) return ShapeDropTarget.None;
                int targetIndex = shapes.IndexOf(nodeData);
                if (targetIndex < 0) return ShapeDropTarget.None;
                insertIndex = half == ShapeRowHalf.Upper ? targetIndex : targetIndex + 1;
                break;

            default:
                return ShapeDropTarget.None;
        }

        bool changesOrder = insertIndex != draggedIndex && insertIndex != draggedIndex + 1;
        return new ShapeDropTarget(sourceFrame, insertIndex, changesOrder);
    }
}
