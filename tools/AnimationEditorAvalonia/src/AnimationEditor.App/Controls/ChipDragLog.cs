using System;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.VisualTree;

namespace AnimationEditor.App.Controls;

// TEMPORARY diagnostic: records what Avalonia reports under the pointer during a chip drag. Remove before merging.
internal static class ChipDragLog
{
    private const string Path = @"F:\GitHub\FlatRedBall2\.claude\worktrees\agent-a23a831c2e1b47582\tools\AnimationEditorAvalonia\tests\_out\chip-drag-cursor\cursor-log.txt";
    private static int _count;
    private static int _drags;

    /// <summary>1..4, rotates per drag: 1 sheet only, 2 + window.Cursor, 3 + chip.Cursor, 4 + direct platform SetCursor.</summary>
    public static int Strategy => _drags % 4 + 1;

    public static void DragEnded(Control chip)
    {
        Write($"=== drag {_drags + 1} (strategy {Strategy}) ended ===");
        _drags++;
        if (TopLevel.GetTopLevel(chip) is { } top) top.Cursor = null;
    }

    public static void Apply(Control chip, ChipDragCursor kind)
    {
        var cursor = ChipDragGhost.CursorFor(kind);
        var top = TopLevel.GetTopLevel(chip);
        if (top is null) return;
        int strategy = Strategy;
        if (strategy == 2) top.Cursor = cursor;
        if (strategy == 3) { top.Cursor = cursor; chip.Cursor = cursor; }
        if (strategy == 4) top.PlatformImpl?.SetCursor(cursor.PlatformImpl);
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct CursorInfo { public int Size; public int Flags; public IntPtr Handle; public int X; public int Y; }

    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool GetCursorInfo(ref CursorInfo info);
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr instance, IntPtr name);

    private static string OsCursor()
    {
        try
        {
            var info = new CursorInfo { Size = System.Runtime.InteropServices.Marshal.SizeOf<CursorInfo>() };
            if (!GetCursorInfo(ref info)) return "GetCursorInfo failed";
            foreach (var (name, id) in new[] { ("ARROW", 32512), ("IBEAM", 32513), ("WAIT", 32514), ("CROSS", 32515), ("SIZEALL", 32646), ("NO", 32648), ("HAND", 32649) })
                if (LoadCursor(IntPtr.Zero, (IntPtr)id) == info.Handle) return $"OS cursor={name}";
            return $"OS cursor=0x{info.Handle:X} (not a stock arrow/hand/no/ibeam/cross/sizeall)";
        }
        catch (Exception ex) { return "OS cursor unavailable: " + ex.Message; }
    }

    private static string Describe(object? o) => o switch
    {
        null => "null",
        InputElement ie => $"{o.GetType().Name}#{ie.Name} cursor={ie.Cursor?.ToString() ?? "null"} hitTestVisible={ie.IsHitTestVisible}",
        _ => o.GetType().Name,
    };

    private static object? PointerOver(TopLevel? top) =>
        top?.GetType().GetProperty("PointerOverElement", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.FlattenHierarchy)?.GetValue(top)
        ?? typeof(IInputRoot).GetProperty("PointerOverElement")?.GetValue(top);

    public static void Write(string line)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
        }
        catch { }
    }

    public static void Move(Control chip, PointerEventArgs e, string drag, string lastSet, Panel? layer, Control? sheet)
    {
        var top = TopLevel.GetTopLevel(chip);
        var pos = top is null ? default : e.GetPosition(top);
        string line = $"#{++_count} strategy={Strategy} {OsCursor()} pos=({pos.X:0},{pos.Y:0}) pointerType={e.Pointer.Type} drag=[{drag}] lastSet={lastSet}";
        line += $"\n    captured: {Describe(e.Pointer.Captured)}";
        line += $"\n    topLevel: {top?.GetType().Name} cursor={top?.Cursor?.ToString() ?? "null"} platform={top?.PlatformImpl?.GetType().Name}";
        line += $"\n    pointerOver: {Describe(PointerOver(top))}";
        var hit = top?.InputHitTest(pos);
        line += $"\n    hitTest: {Describe(hit)}";
        if (hit is Visual v)
            line += $"\n    hitChain: {string.Join(" > ", v.GetSelfAndVisualAncestors().OfType<InputElement>().Take(6).Select(x => $"{x.GetType().Name}({x.Cursor?.ToString() ?? "-"})"))}";
        line += $"\n    layer: {(layer is null ? "null" : $"{layer.GetType().Name} bounds={layer.Bounds} children={layer.Children.Count} hitTestVisible={layer.IsHitTestVisible}")}";
        line += $"\n    sheet: {(sheet is null ? "null" : $"bounds={sheet.Bounds} attached={sheet.Parent is not null} zIndex={sheet.ZIndex} cursor={(sheet as InputElement)?.Cursor?.ToString() ?? "null"}")}";
        Write(line);
    }
}
