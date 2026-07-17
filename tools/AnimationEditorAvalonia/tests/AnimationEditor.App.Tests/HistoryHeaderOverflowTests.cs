using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The History tab's Undo/Redo header (#541) must reflow instead of overflow when the sidebar
/// column is dragged narrower than the buttons' combined width. Before the fix the header was a
/// fixed-height <c>StackPanel</c> with no <c>ClipToBounds</c>, so a narrow column let the Redo
/// button's right edge render past the header's own right edge — bleeding into the wireframe pane
/// beside it. The fix swaps in a <c>WrapPanel</c> (the same pattern already used for the .achx
/// toolbars, #510) so overflow buttons wrap to a second row and the header grows to fit instead.
/// </summary>
public class HistoryHeaderOverflowTests
{
    [AvaloniaFact]
    public void HistoryHeader_NarrowSidebar_ButtonsStayWithinHeaderBounds()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Width = 1400;
        window.Height = 900;
        window.Show();
        Dispatcher.UIThread.RunJobs();

        try
        {
            var tabs = window.FindControl<TabControl>("SidebarTabs")
                ?? throw new InvalidOperationException("SidebarTabs not found");
            var historyTab = window.FindControl<TabItem>("HistoryTab")
                ?? throw new InvalidOperationException("HistoryTab not found");
            tabs.SelectedItem = historyTab;
            Dispatcher.UIThread.RunJobs();

            // Simulate dragging the left|middle splitter to a width narrower than the two
            // 26px buttons' combined footprint (there is no MinWidth guard on this column).
            var mainGrid = window.FindControl<Grid>("MainContentGrid")
                ?? throw new InvalidOperationException("MainContentGrid not found");
            mainGrid.ColumnDefinitions[0].Width = new GridLength(40);
            Dispatcher.UIThread.RunJobs();
            Dispatcher.UIThread.RunJobs();

            var undo = window.FindControl<Button>("HistoryUndoButton")
                ?? throw new InvalidOperationException("HistoryUndoButton not found");
            var redo = window.FindControl<Button>("HistoryRedoButton")
                ?? throw new InvalidOperationException("HistoryRedoButton not found");
            var header = undo.GetVisualAncestors().OfType<Border>().First();

            double headerRight = header.TranslatePoint(new Point(header.Bounds.Width, 0), window)!.Value.X;
            double undoRight = undo.TranslatePoint(new Point(undo.Bounds.Width, 0), window)!.Value.X;
            double redoRight = redo.TranslatePoint(new Point(redo.Bounds.Width, 0), window)!.Value.X;

            const double eps = 0.5;
            Assert.True(undoRight <= headerRight + eps,
                $"HistoryUndoButton right edge {undoRight} overflows header right edge {headerRight}");
            Assert.True(redoRight <= headerRight + eps,
                $"HistoryRedoButton right edge {redoRight} overflows header right edge {headerRight}");

            // The header must be the one that grows (wrapping to a second row), not the buttons
            // that overflow a fixed-height band — this is what distinguishes "reflowed" from
            // "silently clipped" (a ClipToBounds fix would pass the assertions above too, but
            // would hide a button instead of showing it on a second row).
            Assert.True(header.Bounds.Height > 26,
                $"Header should have grown to fit a wrapped second row; height={header.Bounds.Height}");
        }
        finally { window.Close(); }
    }
}
