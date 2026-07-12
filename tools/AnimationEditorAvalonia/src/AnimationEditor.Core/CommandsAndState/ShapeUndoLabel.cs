using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState.Commands;

/// <summary>
/// Shared undo-label fragment for a collision shape — matches
/// <c>Move Rect 'Hitbox'</c> / <c>Delete Circle 'X'</c> naming.
/// </summary>
internal static class ShapeUndoLabel
{
    public static string Format(object shape) => shape switch
    {
        AARectSave r => string.IsNullOrEmpty(r.Name) ? "Rect" : $"Rect '{r.Name}'",
        CircleSave c => string.IsNullOrEmpty(c.Name) ? "Circle" : $"Circle '{c.Name}'",
        _ => "Shape",
    };

    /// <summary>Add/Delete wording uses "Rectangle" rather than the shorter "Rect".</summary>
    public static string FormatForAddDelete(object shape) => shape switch
    {
        AARectSave r => string.IsNullOrEmpty(r.Name) ? "Rectangle" : $"Rectangle '{r.Name}'",
        CircleSave c => string.IsNullOrEmpty(c.Name) ? "Circle" : $"Circle '{c.Name}'",
        _ => "Shape",
    };
}
