using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// An open in-place rename box is a normal text box for Ctrl+Z, Ctrl+Y, Ctrl+Shift+Z and Ctrl+A: they act on the typed
/// text, the box stays open with the focus, and the document's undo stack is not touched. Escape and Enter keep their
/// meaning. A live reload keeps the box when the renamed sequence or track is still in the file.
/// </summary>
public class SequenceRenameBoxKeysScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static string WriteFixture(AnimationEditorHarness editor)
    {
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave();
        var attack = new SequenceSave { Name = "Attack" };
        attack.Tracks.Add(new SequenceTrackSave { Name = "Body" });
        attack.Tracks.Add(new SequenceTrackSave { Name = "Arms" });
        save.Sequences.Add(attack);
        save.Sequences.Add(new SequenceSave { Name = "Idle" });
        save.Save(path);
        return path;
    }

    private static Point OnHeader(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 20, Timeline(editor).Viewport.LaneTop(lane) + 10);

    private static InlineRenameBox OpenSequenceBox(AnimationEditorHarness editor, int row, string typed)
    {
        editor.DoubleClickAt(editor.CenterOf((Control)List(editor).ContainerFromIndex(row)!));
        var box = List(editor).ContainerFromIndex(row)!.GetVisualDescendants().OfType<InlineRenameBox>().Single();
        box.IsEditing.ShouldBeTrue();
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type(typed);
        return box;
    }

    private static InlineRenameBox OpenTrackBox(AnimationEditorHarness editor, int lane, string typed)
    {
        editor.DoubleClickAt(OnHeader(editor, lane));
        var box = Timeline(editor).GetVisualChildren().OfType<InlineRenameBox>().Single();
        box.IsEditing.ShouldBeTrue();
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type(typed);
        return box;
    }

    private static void ShouldKeepEditing(AnimationEditorHarness editor, InlineRenameBox box)
    {
        box.IsEditing.ShouldBeTrue("the box stays open");
        editor.Window.FocusManager!.GetFocusedElement().ShouldBe(box, "the focus stays in the box");
        editor.UndoLabels.ShouldBeEmpty("the document's undo stack is untouched");
        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task CtrlZ_InASequenceRenameBox_UndoesTheTypedText_AndKeepsTheBoxOpen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        var box = OpenSequenceBox(editor, 1, "Zed");
        box.Text.ShouldBe("Zed");
        editor.Press(Key.Z, RawInputModifiers.Control);

        ShouldKeepEditing(editor, box);
        box.Text.ShouldNotBe("Zed");
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });

        editor.Press(Key.Y, RawInputModifiers.Control);
        ShouldKeepEditing(editor, box);
        box.Text.ShouldBe("Zed");

        editor.Press(Key.Z, RawInputModifiers.Control);
        box.Text.ShouldNotBe("Zed");
        editor.Press(Key.Z, RawInputModifiers.Control | RawInputModifiers.Shift);
        ShouldKeepEditing(editor, box);
        box.Text.ShouldBe("Zed");
    }

    [AvaloniaFact]
    public async Task CtrlZ_InATrackRenameBox_UndoesTheTypedText_AndKeepsTheBoxOpen()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));

        var box = OpenTrackBox(editor, 1, "Legs");
        box.Text.ShouldBe("Legs");
        editor.Press(Key.Z, RawInputModifiers.Control);

        ShouldKeepEditing(editor, box);
        box.Text.ShouldNotBe("Legs");

        editor.Press(Key.Y, RawInputModifiers.Control);
        ShouldKeepEditing(editor, box);
        box.Text.ShouldBe("Legs");
    }

    [AvaloniaFact]
    public async Task CtrlA_InARenameBox_SelectsItsText_AndEscapeAndEnterStillMean_Cancel_AndCommit()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);

        var box = OpenSequenceBox(editor, 1, "Zed");
        box.SelectionStart = box.SelectionEnd = 1;
        editor.Press(Key.A, RawInputModifiers.Control);
        ShouldKeepEditing(editor, box);
        Math.Abs(box.SelectionEnd - box.SelectionStart).ShouldBe(3);

        editor.Press(Key.Escape);
        box.IsEditing.ShouldBeFalse();
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Idle" });

        box = OpenSequenceBox(editor, 1, "Rest");
        editor.Press(Key.Enter);
        box.IsEditing.ShouldBeFalse();
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Rest" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'Idle' to 'Rest'" });
    }
}
