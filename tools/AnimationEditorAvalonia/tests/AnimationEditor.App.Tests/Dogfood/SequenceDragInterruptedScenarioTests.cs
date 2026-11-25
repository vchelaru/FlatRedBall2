using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using Xunit;
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

    // Main: one track that plays Earth (1 s) from 0, then Body: Walk at 0. Earth: Run for 1 s.
    private static string WriteReferenceFixture(AnimationEditorHarness editor)
    {
        WriteFixture(editor);
        string path = Path.Combine(editor.ProjectFolder, "refs.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var main = new SequenceSave { Name = "Main" };
        main.Tracks.Add(new SequenceTrackSave { Name = "Link", References = [new SequenceReferenceSave { Sequence = "Earth" }] });
        main.Tracks.Add(Track("Body", ("Walk", 0, null)));
        var earth = new SequenceSave { Name = "Earth" };
        earth.Tracks.Add(Track("Ring", ("Run", 0, 1.0)));
        save.Sequences.Add(main);
        save.Sequences.Add(earth);
        save.Save(path);
        return path;
    }

    private static void HoldReference(AnimationEditorHarness editor, bool edge) =>
        editor.DragHold(At(editor, 0, edge ? 1 : 0.4, dx: edge ? -1 : 0), At(editor, 0, edge ? 1 : 0.4, dx: 60), RawInputModifiers.Alt);

    private static void ContinueAndReleaseReference(AnimationEditorHarness editor)
    {
        editor.DragMove(At(editor, 0, 0.4, dx: 90), RawInputModifiers.Alt);
        editor.DragRelease(At(editor, 0, 0.4, dx: 90), RawInputModifiers.Alt);
    }

    private static SequenceReferenceSave SavedReference(string seqj) => SequenceListSave.FromFile(seqj).Sequences[0].Tracks[0].References![0];

    [AvaloniaTheory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task ReferenceClip_FileRewrittenOutsideDuringADrag_KeepsTheOutsideChange(bool edge, bool escape)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteReferenceFixture(editor);
        await editor.OpenAsync(seqj);
        HoldReference(editor, edge);

        await RewriteOutsideAsync(editor, seqj);
        if (escape) editor.Press(Key.Escape);
        ContinueAndReleaseReference(editor);

        HasOutside(seqj).ShouldBeTrue();
        SavedReference(seqj).Start.ShouldBe(0);
        SavedReference(seqj).Length.ShouldBeNull();
        Timeline(editor).References.Single().Start.ShouldBe(0);
        while (editor.UndoLabels.Count > 0) editor.Press(Key.Z, RawInputModifiers.Control);
        HasOutside(seqj).ShouldBeTrue("undo must not reach back past the outside change");
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReferenceClip_UndoDuringADrag_TakesBackTheEarlierMove_AndDropsTheDrag(bool edge)
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteReferenceFixture(editor);
        await editor.OpenAsync(seqj);
        editor.Drag(At(editor, 1, 0.25), At(editor, 1, 0.25, dx: 60), RawInputModifiers.Alt);
        editor.UndoLabels.Count.ShouldBe(1);
        HoldReference(editor, edge);

        editor.Press(Key.Z, RawInputModifiers.Control);
        ContinueAndReleaseReference(editor);

        editor.UndoLabels.ShouldBeEmpty();
        SavedReference(seqj).Start.ShouldBe(0);
        SavedReference(seqj).Length.ShouldBeNull();
        SequenceListSave.FromFile(seqj).Sequences[0].Tracks[1].Chains[0].Start.ShouldBe(0);
        Timeline(editor).References.Single().Start.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task ReferenceClip_AnotherTabComesToTheFrontDuringADrag_PutsTheClipBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteReferenceFixture(editor);
        await editor.OpenAsync(seqj);
        HoldReference(editor, edge: false);

        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));
        ContinueAndReleaseReference(editor);
        editor.ClickTab("refs.seqj");
        editor.Layout();

        SavedReference(seqj).Start.ShouldBe(0);
        Timeline(editor).References.Single().Start.ShouldBe(0);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AnotherSequenceTabComesToTheFrontDuringADrag_ReleasingChangesNeitherFile()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        string other = WriteReferenceFixture(editor);
        await editor.OpenAsync(seqj);
        string before = File.ReadAllText(seqj), otherBefore = File.ReadAllText(other);
        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 0.25, dx: 60), RawInputModifiers.Alt);

        await editor.OpenAsync(other);
        editor.DragMove(At(editor, 1, 0.25, dx: 90), RawInputModifiers.Alt);
        editor.DragRelease(At(editor, 1, 0.25, dx: 90), RawInputModifiers.Alt);
        editor.ClickTab("boss.seqj");
        editor.Layout();

        Timeline(editor).Blocks.First(b => b.Track == 0 && b.Chain == 0).Start.ShouldBe(0);
        File.ReadAllText(seqj).ShouldBe(before);
        File.ReadAllText(other).ShouldBe(otherBefore);
    }

    [AvaloniaFact]
    public async Task TrackHeaderDrag_TracksReorderedOutside_ReleaseMovesNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        await editor.OpenAsync(seqj);
        Point header = editor.PointIn(Timeline(editor), 20, Timeline(editor).Viewport.LaneTop(0) + 10);
        Point below = editor.PointIn(Timeline(editor), 20, Timeline(editor).Viewport.LaneTop(3) - 3);
        editor.DragHold(header, below);
        Timeline(editor).DraggedTrack.ShouldBe(0);

        var outside = SequenceListSave.FromFile(seqj);
        outside.Sequences[0].Tracks.Reverse();
        outside.Save(seqj);
        (await editor.WaitUntilAsync(() => Timeline(editor).Blocks.All(b => b.Track != 0), TimeSpan.FromSeconds(8))).ShouldBeTrue();
        editor.DragRelease(below);

        SequenceListSave.FromFile(seqj).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Tail", "Head", "Body" });
    }

    [AvaloniaFact]
    public async Task RubberBand_SelectedBlocksRemovedOutside_DoesNotSelectWhatIsGone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        await editor.OpenAsync(seqj);
        editor.ClickAt(At(editor, 0, 2.1));
        editor.DragHold(At(editor, 2, 0.1), At(editor, 2, 0.1, dx: 30), RawInputModifiers.Control);

        var outside = SequenceListSave.FromFile(seqj);
        outside.Sequences[0].Tracks[0].Chains.Clear();
        outside.Save(seqj);
        (await editor.WaitUntilAsync(() => Timeline(editor).Blocks.All(b => b.Track != 0), TimeSpan.FromSeconds(8))).ShouldBeTrue();
        editor.DragMove(At(editor, 2, 0.1, dx: 60), RawInputModifiers.Control);
        editor.DragRelease(At(editor, 2, 0.1, dx: 60), RawInputModifiers.Control);

        Timeline(editor).SelectedChains.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task FileTouchedWithTheSameContentDuringADrag_TheDragGoesOn()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string seqj = WriteFixture(editor);
        await editor.OpenAsync(seqj);
        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 0.25, dx: 30), RawInputModifiers.Alt);

        File.WriteAllText(seqj, File.ReadAllText(seqj));
        await editor.WaitAsync(TimeSpan.FromSeconds(1.5));
        editor.DragRelease(At(editor, 0, 0.25, dx: 30), RawInputModifiers.Alt);

        editor.UndoLabels.Count.ShouldBe(1);
        SequenceListSave.FromFile(seqj).Sequences[0].Tracks[0].Chains[0].Start.ShouldBeGreaterThan(0);
    }
}
