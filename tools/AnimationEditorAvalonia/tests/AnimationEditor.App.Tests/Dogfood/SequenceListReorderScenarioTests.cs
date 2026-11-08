using AnimationEditor.App.Models;
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
/// Dragging a row of the sequence list reorders the file's sequences: one row, or the whole selection as a block.
/// The drop gap is the half of the row under the pointer, shown as an insertion line; Escape cancels; one undo entry.
/// </summary>
public class SequenceListReorderScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static Control Row(AnimationEditorHarness editor, int row) => (Control)List(editor).ContainerFromIndex(row)!;

    // Upper quarter of a row points at the gap above it; lower quarter at the gap below it.
    private static Point AboveRow(AnimationEditorHarness editor, int row) =>
        editor.PointIn(Row(editor, row), Row(editor, row).Bounds.Width / 2, Row(editor, row).Bounds.Height * 0.25);

    private static Point BelowRow(AnimationEditorHarness editor, int row) =>
        editor.PointIn(Row(editor, row), Row(editor, row).Bounds.Width / 2, Row(editor, row).Bounds.Height * 0.75);

    private static List<string> Selected(AnimationEditorHarness editor) =>
        List(editor).SelectedItems!.OfType<SequenceListItem>().Select(item => item.Name).ToList();

    private static List<string> SavedNames(string path) =>
        SequenceListSave.FromFile(path).Sequences.Select(sequence => sequence.Name).ToList();

    private static string[] Abcde = { "A", "B", "C", "D", "E" };

    private static void Undo(AnimationEditorHarness editor) => editor.Press(Key.Z, RawInputModifiers.Control);

    private static void Redo(AnimationEditorHarness editor) => editor.Press(Key.Y, RawInputModifiers.Control);

    /// <summary>The insertion lines the drag shows: 2 px bars in the window's drag overlay.</summary>
    private static List<Border> DropLines(AnimationEditorHarness editor) =>
        editor.Control<Canvas>("DragOverlayCanvas").Children.OfType<Border>().Where(border => border.Height == 2).ToList();

    private static double LineY(AnimationEditorHarness editor, Border line) => Canvas.GetTop(line) + 1;

    /// <summary>The y, in the drag overlay, of the top edge of <paramref name="row"/> (or the bottom edge of the last one).</summary>
    private static double GapY(AnimationEditorHarness editor, int gap)
    {
        var overlay = editor.Control<Canvas>("DragOverlayCanvas");
        editor.Layout();
        var row = gap < List(editor).ItemCount ? Row(editor, gap) : Row(editor, gap - 1);
        double top = row.TranslatePoint(new Point(0, gap < List(editor).ItemCount ? 0 : row.Bounds.Height), overlay)!.Value.Y;
        return top;
    }

    [AvaloniaFact]
    public async Task DraggingARowBelowAnother_MovesIt_SavesTheOrder_AndUndoRedoRestoreIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);

        editor.Drag(editor.CenterOf(Row(editor, 0)), BelowRow(editor, 2));

        editor.SequenceNames.ShouldBe(new[] { "B", "C", "A", "D", "E" });
        SavedNames(boss).ShouldBe(new[] { "B", "C", "A", "D", "E" });
        editor.UndoLabels.ShouldBe(new[] { "Move Sequence 'A'" });
        Selected(editor).ShouldBe(new[] { "A" });
        List(editor).SelectedIndex.ShouldBe(2);
        DropLines(editor).ShouldBeEmpty();

        Undo(editor);
        editor.SequenceNames.ShouldBe(Abcde);
        SavedNames(boss).ShouldBe(Abcde);
        Selected(editor).ShouldBe(new[] { "A" });
        List(editor).SelectedIndex.ShouldBe(0);

        Redo(editor);
        editor.SequenceNames.ShouldBe(new[] { "B", "C", "A", "D", "E" });
        List(editor).SelectedIndex.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task DraggingARowAboveAnother_DropsOnTheUpperHalf_AndToTheTop()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);

        editor.Drag(editor.CenterOf(Row(editor, 4)), AboveRow(editor, 1));
        editor.SequenceNames.ShouldBe(new[] { "A", "E", "B", "C", "D" });

        editor.Drag(editor.CenterOf(Row(editor, 3)), AboveRow(editor, 0));
        editor.SequenceNames.ShouldBe(new[] { "C", "A", "E", "B", "D" });
        List(editor).SelectedIndex.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task WhileDragging_AnInsertionLineMarksTheGap_AndItGoesAwayOnRelease()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);

        editor.DragHold(editor.CenterOf(Row(editor, 0)), BelowRow(editor, 2));

        var line = DropLines(editor).ShouldHaveSingleItem();
        LineY(editor, line).ShouldBe(GapY(editor, 3), 1.5);

        editor.DragMove(AboveRow(editor, 4));
        LineY(editor, DropLines(editor).ShouldHaveSingleItem()).ShouldBe(GapY(editor, 4), 1.5);

        // The gap next to the dragged row itself moves nothing, so it shows no line.
        editor.DragMove(BelowRow(editor, 0));
        DropLines(editor).ShouldBeEmpty();

        editor.DragMove(BelowRow(editor, 4));
        LineY(editor, DropLines(editor).ShouldHaveSingleItem()).ShouldBe(GapY(editor, 5), 1.5);

        editor.DragRelease(BelowRow(editor, 4));
        DropLines(editor).ShouldBeEmpty();
        editor.SequenceNames.ShouldBe(new[] { "B", "C", "D", "E", "A" });
    }

    [AvaloniaFact]
    public async Task DraggingOneOfSeveralSelectedRows_MovesTheWholeSelectionAsABlock_KeepingSelectionAndPrimary()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);
        editor.Click(Row(editor, 1));
        editor.Click(Row(editor, 3), RawInputModifiers.Control);
        editor.Click(Row(editor, 4), RawInputModifiers.Control);
        Selected(editor).ShouldBe(new[] { "B", "D", "E" });

        editor.Drag(editor.CenterOf(Row(editor, 3)), AboveRow(editor, 0));

        editor.SequenceNames.ShouldBe(new[] { "B", "D", "E", "A", "C" });
        SavedNames(boss).ShouldBe(new[] { "B", "D", "E", "A", "C" });
        editor.UndoLabels.ShouldBe(new[] { "Move 3 Sequences" });
        Selected(editor).ShouldBe(new[] { "B", "D", "E" });
        List(editor).SelectedIndex.ShouldBe(0);
        editor.Control<TextBox>("SequenceNameBox").Text.ShouldBe("B");

        Undo(editor);
        editor.SequenceNames.ShouldBe(Abcde);
        Selected(editor).ShouldBe(new[] { "B", "D", "E" });
        editor.Control<TextBox>("SequenceNameBox").Text.ShouldBe("B");
    }

    [AvaloniaFact]
    public async Task DraggingASelectedRowOtherThanThePrimary_StillMovesTheBlock_AndThePrimaryStaysTheSame()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);
        editor.Click(Row(editor, 0));
        editor.Click(Row(editor, 2), RawInputModifiers.Control);

        // Grabbed away from the spot just clicked, which would read as the second press of a double-click.
        editor.Drag(AboveRow(editor, 2), BelowRow(editor, 4));

        editor.SequenceNames.ShouldBe(new[] { "B", "D", "E", "A", "C" });
        Selected(editor).ShouldBe(new[] { "A", "C" });
        List(editor).SelectedIndex.ShouldBe(3);
        editor.Control<TextBox>("SequenceNameBox").Text.ShouldBe("A");
    }

    [AvaloniaFact]
    public async Task DraggingAnUnselectedRow_SelectsItAlone_ThenMovesOnlyIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);
        editor.Click(Row(editor, 0));
        editor.Click(Row(editor, 1), RawInputModifiers.Control);

        editor.Drag(editor.CenterOf(Row(editor, 3)), AboveRow(editor, 0));

        editor.SequenceNames.ShouldBe(new[] { "D", "A", "B", "C", "E" });
        Selected(editor).ShouldBe(new[] { "D" });
        editor.UndoLabels.ShouldBe(new[] { "Move Sequence 'D'" });
    }

    [AvaloniaFact]
    public async Task DroppingOnItsOwnGap_OrNextToItself_ChangesNothing_AndRecordsNoUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);

        editor.Drag(editor.CenterOf(Row(editor, 2)), editor.CenterOf(Row(editor, 2)) + new Point(0, 2));
        editor.Drag(editor.CenterOf(Row(editor, 2)), AboveRow(editor, 2));
        editor.Drag(editor.CenterOf(Row(editor, 2)), BelowRow(editor, 2));
        editor.Drag(editor.CenterOf(Row(editor, 2)), BelowRow(editor, 1));
        editor.Drag(editor.CenterOf(Row(editor, 2)), AboveRow(editor, 3));

        editor.SequenceNames.ShouldBe(Abcde);
        editor.UndoLabels.ShouldBeEmpty();
        SavedNames(boss).ShouldBe(Abcde);
        Selected(editor).ShouldBe(new[] { "C" });
    }

    [AvaloniaFact]
    public async Task DroppingABlockInsideItsOwnSpan_ChangesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);
        editor.Click(Row(editor, 0));
        editor.Click(Row(editor, 2), RawInputModifiers.Shift);
        Selected(editor).Count.ShouldBe(3);

        editor.Drag(editor.CenterOf(Row(editor, 1)), BelowRow(editor, 1));

        editor.SequenceNames.ShouldBe(Abcde);
        editor.UndoLabels.ShouldBeEmpty();
        Selected(editor).Count.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task EscapeDuringTheDrag_CancelsIt_NothingMovesAndNoLineIsLeft()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);
        editor.Click(Row(editor, 0));
        editor.Click(Row(editor, 1), RawInputModifiers.Control);

        editor.DragHold(editor.CenterOf(Row(editor, 0)), BelowRow(editor, 3));
        DropLines(editor).ShouldHaveSingleItem();
        editor.Press(Key.Escape);
        DropLines(editor).ShouldBeEmpty();
        editor.DragMove(BelowRow(editor, 4));
        editor.DragRelease(BelowRow(editor, 4));

        editor.SequenceNames.ShouldBe(Abcde);
        editor.UndoLabels.ShouldBeEmpty();
        SavedNames(boss).ShouldBe(Abcde);
        Selected(editor).ShouldBe(new[] { "A", "B" });
        DropLines(editor).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task PlainClickOnASelectedRowOfAMultiSelection_StillNarrowsToThatRow_AndCtrlShiftClicksStillExtend()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);

        editor.Click(Row(editor, 0));
        editor.Click(Row(editor, 2), RawInputModifiers.Control);
        editor.Click(Row(editor, 4), RawInputModifiers.Control);
        Selected(editor).ShouldBe(new[] { "A", "C", "E" });
        editor.Click(Row(editor, 2), RawInputModifiers.Control);
        Selected(editor).ShouldBe(new[] { "A", "E" });
        editor.Click(Row(editor, 1), RawInputModifiers.Shift);
        Selected(editor).Count.ShouldBeGreaterThan(1);

        editor.Click(Row(editor, 4));
        Selected(editor).ShouldBe(new[] { "E" });
        editor.UndoLabels.ShouldBeEmpty();
        editor.SequenceNames.ShouldBe(Abcde);
    }

    [AvaloniaFact]
    public async Task DoubleClickingARowThatWasInAMultiSelection_StillStartsTheRename()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);
        editor.Click(Row(editor, 0));
        editor.Click(Row(editor, 2), RawInputModifiers.Control);

        editor.DoubleClickAt(editor.CenterOf(Row(editor, 2)));

        Row(editor, 2).GetVisualDescendants().OfType<InlineRenameBox>().Single().IsEditing.ShouldBeTrue();
        editor.SequenceNames.ShouldBe(Abcde);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ARenameInProgress_CommitsBeforeAnotherRowIsDragged()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);
        editor.DoubleClickAt(editor.CenterOf(Row(editor, 1)));
        var box = Row(editor, 1).GetVisualDescendants().OfType<InlineRenameBox>().Single();
        box.Focus();
        editor.Layout();
        box.SelectAll();
        editor.Type("Bee");

        editor.Drag(editor.CenterOf(Row(editor, 3)), AboveRow(editor, 0));

        editor.SequenceNames.ShouldBe(new[] { "D", "A", "Bee", "C", "E" });
        SavedNames(boss).ShouldBe(new[] { "D", "A", "Bee", "C", "E" });
        editor.UndoLabels.ShouldBe(new[] { "Rename Sequence 'B' to 'Bee'", "Move Sequence 'D'" });
        editor.ProblemText.ShouldBeNull();
        Selected(editor).ShouldBe(new[] { "D" });
        List(editor).GetVisualDescendants().OfType<InlineRenameBox>().ShouldAllBe(inline => !inline.IsEditing);
    }

    [AvaloniaFact]
    public async Task APressInTheRenameTextBox_DoesNotStartADrag()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);
        editor.DoubleClickAt(editor.CenterOf(Row(editor, 1)));
        var box = Row(editor, 1).GetVisualDescendants().OfType<InlineRenameBox>().Single();
        box.IsEditing.ShouldBeTrue();

        editor.DragHold(editor.CenterOf(box), BelowRow(editor, 3));

        DropLines(editor).ShouldBeEmpty();
        editor.DragRelease(BelowRow(editor, 3));
        editor.SequenceNames.ShouldBe(Abcde);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AfterAMove_ThePrimarySequenceIsStillTheOneShown_ByNameNotByOldIndex()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        foreach (string name in new[] { "A", "B", "C" })
        {
            var sequence = new SequenceSave { Name = name };
            var track = new SequenceTrackSave { Name = name + "Track" };
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
            sequence.Tracks.Add(track);
            save.Sequences.Add(sequence);
        }
        save.Save(boss);
        await editor.OpenAsync(boss);
        editor.Click(Row(editor, 0));

        editor.Drag(editor.CenterOf(Row(editor, 0)), BelowRow(editor, 2));

        editor.SequenceNames.ShouldBe(new[] { "B", "C", "A" });
        List(editor).SelectedIndex.ShouldBe(2);
        editor.Control<TextBox>("SequenceNameBox").Text.ShouldBe("A");
        editor.Control<AnimationEditor.App.Controls.SequenceTimelineControl>("SequenceTimeline").Blocks.ShouldNotBeEmpty();        editor.Control<AnimationEditor.App.Controls.PreviewControl>("SequencePreview").Composite!.ShouldNotBeEmpty();
    }

    [AvaloniaFact]
    public async Task AMove_IsAnEditNotAPlaceToReturnTo_SoBackHasNothingToGoBackTo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);
        editor.Control<MenuItem>("MenuNavBack").IsEnabled.ShouldBeFalse();

        // The selected row moves from index 0 to 4; recorded by index, Back would land on a different sequence.
        editor.Drag(editor.CenterOf(Row(editor, 0)), BelowRow(editor, 4));

        editor.SequenceNames.ShouldBe(new[] { "B", "C", "D", "E", "A" });
        editor.Control<MenuItem>("MenuNavBack").IsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task TheMoveSavesLikeAnyEdit_SoTheLiveReloadDoesNotReactToItsOwnWrite()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = editor.WriteSeqj("boss.seqj", Abcde);
        await editor.OpenAsync(boss);

        editor.Drag(editor.CenterOf(Row(editor, 0)), BelowRow(editor, 4));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(1500));

        editor.InfoText.ShouldBeNull();
        editor.ProblemText.ShouldBeNull();
        editor.SequenceNames.ShouldBe(new[] { "B", "C", "D", "E", "A" });
        SavedNames(boss).ShouldBe(new[] { "B", "C", "D", "E", "A" });
        editor.UndoLabels.ShouldBe(new[] { "Move Sequence 'A'" });
    }
}
