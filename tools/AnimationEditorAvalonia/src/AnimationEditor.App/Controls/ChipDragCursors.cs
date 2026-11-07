using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Input;
using System;
using System.Runtime.InteropServices;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Sets the mouse cursor while a library chip is dragged. On Windows it calls the Win32 <c>SetCursor</c> directly with
/// a stock cursor (hand over a drop target, "no" elsewhere): the chip holds the pointer capture, and during that
/// capture Avalonia's <see cref="InputElement.Cursor"/> never reached the OS cursor, whichever element, window or
/// overlay it was set on (probed on Windows 11 with a per-move <c>GetCursorInfo</c> log; only the raw call changed it).
/// Other platforms use the chip's own <see cref="InputElement.Cursor"/>, which is Avalonia's documented way.
/// </summary>
internal static class ChipDragCursors
{
    private const int IdcArrow = 32512;
    private const int IdcNo = 32648;
    private const int IdcHand = 32649;

    /// <summary>The stock Win32 cursor (<c>IDC_*</c>) for <paramref name="cursor"/>.</summary>
    internal static int StockCursorId(ChipDragCursor cursor) => cursor == ChipDragCursor.Copy ? IdcHand : IdcNo;

    private static readonly Cursor CopyCursor = new(StandardCursorType.DragCopy);
    private static readonly Cursor NotAllowedCursor = new(StandardCursorType.No);

    /// <summary>Shows <paramref name="cursor"/>. Call on every pointer move: Windows resets the cursor between moves.</summary>
    public static void Apply(Control chip, ChipDragCursor cursor)
    {
        if (OperatingSystem.IsWindows()) SetCursor(LoadCursor(IntPtr.Zero, StockCursorId(cursor)));
        else chip.Cursor = cursor == ChipDragCursor.Copy ? CopyCursor : NotAllowedCursor;
    }

    /// <summary>Ends the drag's cursor. The next pointer move lets Avalonia set the cursor of whatever is under it.</summary>
    public static void Restore(Control chip, Cursor? resting)
    {
        if (OperatingSystem.IsWindows()) SetCursor(LoadCursor(IntPtr.Zero, IdcArrow));
        else chip.Cursor = resting;
    }

    [DllImport("user32.dll")]
    private static extern IntPtr SetCursor(IntPtr cursor);

    [DllImport("user32.dll", EntryPoint = "LoadCursorW")]
    private static extern IntPtr LoadCursor(IntPtr instance, int name);
}
