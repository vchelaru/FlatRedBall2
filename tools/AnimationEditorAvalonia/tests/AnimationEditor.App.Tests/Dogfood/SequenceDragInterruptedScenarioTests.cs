using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Something else changes the sequence while a block is held on the timeline: the file is rewritten outside, an undo runs,
/// or another tab comes to the front. Fixture: Body: Walk 0..0.5, Walk 1..1.5, Run 2..2.2. Head: Run 0.5..0.7, Walk 1.5..2.
/// </summary>
public class SequenceDragInterruptedScenarioTests
{
    private static async Task RewriteOutsideAsync(AnimationEditorHarness editor, string seqj)
    {
        var outside = SequenceListSave.FromFile(seqj);
        outside.Sequences.Add(new SequenceSave { Name = "Outside" });
        outside.Save(seqj);
        (await editor.WaitUntilAsync(() => editor.SequenceNames.Contains("Outside"), TimeSpan.FromSeconds(8)))
            .ShouldBeTrue("the live reload should have read the outside change");
    }

    private static bool HasOutside(string seqj) => SequenceListSave.FromFile(seqj).Sequences.Any(s => s.Name == "Outside");

    [AvaloniaFact]
    public async Task FileRewrittenOutsideDuringADrag_Escape_KeepsTheOutsideChange()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        await editor.OpenAsync(seqj);
        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 0.25, dx: 30), RawInputModifiers.Alt);

        await RewriteOutsideAsync(editor, seqj);
        editor.Press(Key.Escape);

        HasOutside(seqj).ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task FileRewrittenOutsideDuringADrag_ContinuingAndReleasing_DoesNotThrow_AndKeepsTheOutsideChange()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        await editor.OpenAsync(seqj);
        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 0.25, dx: 30), RawInputModifiers.Alt);

        await RewriteOutsideAsync(editor, seqj);
        Should.NotThrow(() =>
        {
            editor.DragMove(At(editor, 0, 0.25, dx: 60), RawInputModifiers.Alt);
            editor.DragRelease(At(editor, 0, 0.25, dx: 60), RawInputModifiers.Alt);
        });

        HasOutside(seqj).ShouldBeTrue();
        while (editor.UndoLabels.Count > 0) editor.Press(Key.Z, RawInputModifiers.Control);
        HasOutside(seqj).ShouldBeTrue("undo must not reach back past the outside change");
    }

    [AvaloniaFact]
    public async Task EdgeDrag_FileRewrittenOutside_UndoAfterRelease_KeepsTheOutsideChange()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        await editor.OpenAsync(seqj);
        editor.DragHold(At(editor, 0, 0.5, dx: -1), At(editor, 0, 0.5, dx: 30), RawInputModifiers.Alt);

        await RewriteOutsideAsync(editor, seqj);
        editor.DragRelease(At(editor, 0, 0.5, dx: 30), RawInputModifiers.Alt);

        HasOutside(seqj).ShouldBeTrue();
        while (editor.UndoLabels.Count > 0) editor.Press(Key.Z, RawInputModifiers.Control);
        HasOutside(seqj).ShouldBeTrue("undo must not reach back past the outside change");
    }

    [AvaloniaFact]
    public async Task UndoDuringADrag_ThenRelease_DoesNotThrow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        await editor.OpenAsync(seqj);
        editor.Drag(At(editor, 0, 2.1), At(editor, 0, 2.1, dx: 60), RawInputModifiers.Alt);
        editor.UndoLabels.Count.ShouldBe(1);
        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 0.25, dx: 30), RawInputModifiers.Alt);

        editor.Press(Key.Z, RawInputModifiers.Control);

        Should.NotThrow(() =>
        {
            editor.DragMove(At(editor, 0, 0.25, dx: 60), RawInputModifiers.Alt);
            editor.DragRelease(At(editor, 0, 0.25, dx: 60), RawInputModifiers.Alt);
        });
    }

    [AvaloniaFact]
    public async Task DeleteDuringADrag_ThenRelease_DoesNotThrow_AndOneUndoRestoresTheBlock()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        await editor.OpenAsync(seqj);
        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 0.25, dx: 30), RawInputModifiers.Alt);

        editor.Press(Key.Delete);

        Should.NotThrow(() =>
        {
            editor.DragMove(At(editor, 0, 0.25, dx: 60), RawInputModifiers.Alt);
            editor.DragRelease(At(editor, 0, 0.25, dx: 60), RawInputModifiers.Alt);
        });
        while (editor.UndoLabels.Count > 0) editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(seqj).Sequences[0].Tracks[0].Chains[0].Start.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task AnotherTabComesToTheFrontDuringADrag_TheMoveIsEitherSavedOrGone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        await editor.OpenAsync(seqj);
        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 0.25, dx: 60), RawInputModifiers.Alt);

        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));
        editor.DragRelease(At(editor, 0, 0.25, dx: 60), RawInputModifiers.Alt);
        editor.ClickTab("boss.seqj");
        editor.Layout();

        double saved = SequenceListSave.FromFile(seqj).Sequences[0].Tracks[0].Chains[0].Start;
        Timeline(editor).Blocks.First(b => b.Track == 0 && b.Chain == 0).Start.ShouldBe(saved);
        editor.UndoLabels.Count.ShouldBe(saved == 0 ? 0 : 1);
    }

    [AvaloniaFact]
    public async Task ReadOnlyFile_EscapeDuringADrag_DoesNotReportAFailedSave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        await editor.OpenAsync(seqj);
        UnusualProjectScenarioTests.MakeUnwritable(seqj);
        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 0.25, dx: 30), RawInputModifiers.Alt);

        editor.Press(Key.Escape);

        editor.ProblemText.ShouldBeNull("nothing changed, so nothing needed saving");
    }
}
