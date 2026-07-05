using Avalonia.Input;
using System.Diagnostics;
using System.Linq;

namespace AnimationEditor.App.Services;

/// <summary>
/// Extracts the first dropped file's local path from a <see cref="DragEventArgs"/>. Shared by
/// every OS/Files-panel PNG drop target (ANIMATIONS tree, wireframe canvas) so they all resolve
/// the dropped file the same way instead of each re-deriving it from <c>DataTransfer</c>.
/// </summary>
public static class DragDropFileResolver
{
    public static string? GetFirstDroppedFilePath(DragEventArgs e)
    {
        // Log item formats so we can see exactly what the OS provides
        var itemFormats = e.DataTransfer.Items?
            .Select(i => "[" + string.Join(",", i.Formats) + "]")
            .ToList();
        Trace.WriteLine($"[DragDrop] Items and their formats: {(itemFormats == null ? "(null)" : string.Join(" ", itemFormats))}");
        Trace.WriteLine($"[DragDrop] Contains(DataFormat.File)={e.DataTransfer.Contains(DataFormat.File)}");

        // Correct Avalonia 12 API for OS file drops
        var files = e.DataTransfer.TryGetFiles()?.ToList();
        Trace.WriteLine($"[DragDrop] TryGetFiles() count={files?.Count ?? -1}");
        if (files?.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            Trace.WriteLine($"[DragDrop] resolved path={path}");
            return path;
        }

        // Fallback: per-item TryGetFile()
        var items = e.DataTransfer.Items?.ToList();
        Trace.WriteLine($"[DragDrop] Items count={items?.Count ?? -1}");
        foreach (var item in items ?? new())
            Trace.WriteLine($"[DragDrop] Item: Formats=[{string.Join(",", item.Formats)}] TryGetFile={item.TryGetFile()?.Path?.LocalPath ?? "(null)"}");

        var fallback = items?
            .Select(item => item.TryGetFile())
            .FirstOrDefault(f => f is not null);
        Trace.WriteLine($"[DragDrop] Items fallback resolved={fallback?.Path.LocalPath ?? "(null)"}");
        return fallback?.Path.LocalPath;
    }
}
