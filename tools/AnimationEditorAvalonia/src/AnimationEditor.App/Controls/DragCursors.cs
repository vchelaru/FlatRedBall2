using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Runtime.InteropServices;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Sets the mouse cursor while a drag holds the pointer capture (a library chip, a chain's resize edge). On Windows it
/// calls the Win32 <c>SetCursor</c> directly with a stock cursor: during that capture Avalonia's
/// <see cref="InputElement.Cursor"/> never reached the OS cursor, whichever element, window or overlay it was set on
/// (probed on Windows 11 with a per-move <c>GetCursorInfo</c> log; only the raw call changed it). Other platforms use
/// the control's own <see cref="InputElement.Cursor"/>, which is Avalonia's documented way. Without a capture, plain
/// hover cursors still go through <see cref="InputElement.Cursor"/>.
/// </summary>
internal static class DragCursors
{
    private const int IdcArrow = 32512;
    private const int IdcSizeWE = 32644;
    private const int IdcSizeNS = 32645;
    private const int IdcSizeAll = 32646;
    private const int IdcNo = 32648;
    private const int IdcHand = 32649;

    /// <summary>The stock Win32 cursor (<c>IDC_*</c>) for <paramref name="cursor"/>.</summary>
    internal static int StockCursorId(ChipDragCursor cursor) => cursor == ChipDragCursor.Copy ? IdcHand : IdcNo;

    /// <summary>The stock Win32 cursor (<c>IDC_*</c>) for <paramref name="cursor"/>.</summary>
    internal static int StockCursorId(TimelineCursor cursor) => cursor switch
    {
        TimelineCursor.ResizeHorizontal => IdcSizeWE,
        TimelineCursor.Reorder => IdcSizeNS,
        TimelineCursor.Pan => IdcSizeAll,
        _ => IdcArrow,
    };

    private static readonly Cursor CopyCursor = new(StandardCursorType.DragCopy);
    private static readonly Cursor NotAllowedCursor = new(StandardCursorType.No);

    /// <summary>The Avalonia cursor for a timeline hover, or null (inherit) for the default arrow.</summary>
    internal static Cursor? ForHover(TimelineCursor cursor) =>
        cursor switch
        {
            TimelineCursor.ResizeHorizontal => ResizeCursor,
            TimelineCursor.Reorder => ReorderCursor,
            TimelineCursor.Pan => PanCursor,
            _ => null,
        };

    private static readonly Cursor ResizeCursor = new(StandardCursorType.SizeWestEast);
    private static readonly Cursor ReorderCursor = new(StandardCursorType.SizeNorthSouth);
    private static readonly Cursor PanCursor = new(StandardCursorType.SizeAll);

    /// <summary>Shows <paramref name="cursor"/> during a chip drag. Call on every pointer move: Windows resets the cursor between moves.</summary>
    public static void Apply(Control control, ChipDragCursor cursor)
    {
        if (OperatingSystem.IsWindows()) SetOsCursor(StockCursorId(cursor));
        else control.Cursor = cursor == ChipDragCursor.Copy ? CopyCursor : NotAllowedCursor;
    }

    /// <summary>Shows <paramref name="cursor"/> during a timeline drag. Call on every pointer move: Windows resets the cursor between moves.</summary>
    public static void Apply(Control control, TimelineCursor cursor)
    {
        if (OperatingSystem.IsWindows()) SetOsCursor(StockCursorId(cursor));
        else control.Cursor = ForHover(cursor);
    }

    /// <summary>Ends the drag's cursor. The next pointer move lets Avalonia set the cursor of whatever is under it.</summary>
    public static void Restore(Control control, Cursor? resting)
    {
        if (OperatingSystem.IsWindows()) SetOsCursor(IdcArrow);
        else control.Cursor = resting;
    }

    private static void SetOsCursor(int stockId) => SetCursor(LoadCursor(IntPtr.Zero, stockId));

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr cursor);

    [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static extern IntPtr LoadCursor(IntPtr instance, int name);
}
