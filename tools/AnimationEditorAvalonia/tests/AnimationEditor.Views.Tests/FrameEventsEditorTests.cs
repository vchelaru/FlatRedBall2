using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.Animation;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Views.Tests;

// #1121: the inspector's per-frame event rows. The control only raises requests; the host routes
// them through undoable AppCommands.
public class FrameEventsEditorTests
{
    private static FrameEventsEditor Show(params AnimationFrameEvent[] events)
    {
        var editor = new FrameEventsEditor();
        new Window { Content = editor }.Show();
        editor.ShowEvents(events);
        Dispatcher.UIThread.RunJobs();
        return editor;
    }

    [AvaloniaFact]
    public void EditName_FocusMovesToData_RaisesEditCommitted()
    {
        var editor = Show(new AnimationFrameEvent { Name = "Step", Data = "left" });
        (int, string, string?)? committed = null;
        editor.EditCommitted += (i, n, d) => committed = (i, n, d);
        var row = editor.Rows[0];

        row.Name.Focus();
        row.Name.Text = "Footstep";
        row.Data.Focus();

        Assert.Equal((0, "Footstep", "left"), committed);
    }

    [AvaloniaFact]
    public void EditName_Blank_RestoresNameWithoutCommitting()
    {
        var editor = Show(new AnimationFrameEvent { Name = "Step" });
        bool committed = false;
        editor.EditCommitted += (_, _, _) => committed = true;
        var row = editor.Rows[0];

        row.Name.Focus();
        row.Name.Text = "  ";
        row.Data.Focus();

        Assert.False(committed);
        Assert.Equal("Step", row.Name.Text);
    }

    [AvaloniaFact]
    public void RemoveButton_RaisesRemoveRequestedWithRowIndex()
    {
        var editor = Show(new AnimationFrameEvent { Name = "A" }, new AnimationFrameEvent { Name = "B" });
        var removed = new List<int>();
        editor.RemoveRequested += removed.Add;

        editor.Rows[1].Remove.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

        Assert.Equal(new[] { 1 }, removed);
    }

    [AvaloniaFact]
    public void ShowEvents_NullForMultiSelection_HidesAddButtonAndShowsHint()
    {
        var editor = Show(new AnimationFrameEvent { Name = "A" });

        editor.ShowEvents(null);

        Assert.False(editor.AddButton.IsVisible);
        Assert.False(editor.RowsPanel.IsVisible);
        Assert.True(editor.HintText.IsVisible);
    }
}
