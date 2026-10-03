using AnimationEditor.Core.Paths;

namespace AnimationEditor.Core.DragDrop;

/// <summary>
/// Wording for the toast shown when the user drags a shape row to reorder it. Shape order cannot
/// be reordered by drag: the animation file formats save shapes grouped by type (rectangles,
/// polygons, circles), so a cross-type order would not survive a save.
/// </summary>
public static class ShapeReorderNotice
{
    /// <summary>The toast text; names the extension of <paramref name="openFilePath"/> when it has one.</summary>
    public static string Message(string? openFilePath)
    {
        string extension = string.IsNullOrEmpty(openFilePath) ? "" : "." + new FilePath(openFilePath).Extension;
        string format = extension.Length > 1 ? extension : ".achx/.achj";
        return $"Can't reorder shapes: {format} saves them grouped by type.";
    }
}
