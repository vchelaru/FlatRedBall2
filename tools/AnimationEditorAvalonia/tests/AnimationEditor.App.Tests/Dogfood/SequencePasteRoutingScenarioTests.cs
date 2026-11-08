using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Ctrl+V picks what to paste from what the clipboard holds (sequences, chains or tracks), whichever pane has the focus, and
/// a paste that cannot apply says why. Fixture: Attack has tracks Body (3 chains), Head (2), Tail (none).
/// </summary>
public class SequencePasteRoutingScenarioTests
{
    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static Avalonia.Point Header(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 70, Timeline(editor).HeaderCenters(lane).Name);

    private static async Task CtrlAsync(AnimationEditorHarness editor, Key key)
    {
        editor.Press(key, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Layout();
    }

    private static List<(string Name, string[] Chains)> Tracks(string path, int sequence) =>
        SequenceListSave.FromFile(path).Sequences[sequence].Tracks
            .Select(t => (t.Name, t.Chains.Select(c => c.Chain).ToArray())).ToList();

    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenWithTwoTracksCopiedAsync()
    {
        var editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);
        Timeline(editor).SelectedTracks.Count.ShouldBe(2);
        await CtrlAsync(editor, Key.C);
        return (editor, path);
    }

    private static async Task AddSequenceAsync(AnimationEditorHarness editor)
    {
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Layout();
    }

    private static void AssertPastedIntoNew(AnimationEditorHarness editor, string path)
    {
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Sequence" });
        var pasted = Tracks(path, 1);
        pasted.Select(t => t.Name).ShouldBe(new[] { "Body", "Head" });
        pasted[0].Chains.ShouldBe(new[] { "Walk", "Walk", "Run" });
        pasted[1].Chains.ShouldBe(new[] { "Run", "Walk" });
        Timeline(editor).SelectedTracks.ShouldBe(new[] { 0, 1 });
        editor.UndoLabels.Last().ShouldBe("Paste 2 tracks");
    }

    [AvaloniaFact]
    public async Task CopyTracks_AddSequence_Escape_CtrlV_PastesBothTracksIntoTheNewSequence()
    {
        var (editor, path) = await OpenWithTwoTracksCopiedAsync();
        using var _ = editor;
        await AddSequenceAsync(editor);
        editor.Press(Key.Escape);   // keep the default name; the sequence list keeps the focus

        await CtrlAsync(editor, Key.V);

        AssertPastedIntoNew(editor, path);
    }

    [AvaloniaFact]
    public async Task CopyTracks_AddSequence_Enter_CtrlV_PastesBothTracksIntoTheNewSequence()
    {
        var (editor, path) = await OpenWithTwoTracksCopiedAsync();
        using var _ = editor;
        await AddSequenceAsync(editor);
        editor.Press(Key.Enter);

        await CtrlAsync(editor, Key.V);

        AssertPastedIntoNew(editor, path);
    }

    [AvaloniaFact]
    public async Task CopyTracks_ClickTheNewRow_CtrlV_PastesBothTracksIntoTheNewSequence()
    {
        var (editor, path) = await OpenWithTwoTracksCopiedAsync();
        using var _ = editor;
        await AddSequenceAsync(editor);
        editor.Press(Key.Escape);
        editor.Click((Control)List(editor).ContainerFromIndex(1)!);

        await CtrlAsync(editor, Key.V);

        AssertPastedIntoNew(editor, path);
    }

    [AvaloniaFact]
    public async Task CopyTracks_NewSequence_TimelineFocused_CtrlV_PastesBothTracksIntoTheNewSequence()
    {
        var (editor, path) = await OpenWithTwoTracksCopiedAsync();
        using var _ = editor;
        await AddSequenceAsync(editor);
        editor.Press(Key.Escape);
        Timeline(editor).Focus();
        editor.Layout();

        await CtrlAsync(editor, Key.V);

        AssertPastedIntoNew(editor, path);
    }

    [AvaloniaFact]
    public async Task CopyTracks_PasteWithTheListFocused_MovesTheFocusToTheTimeline_SoDeleteCannotRemoveTheSequence()
    {
        var (editor, path) = await OpenWithTwoTracksCopiedAsync();
        using var _ = editor;
        await AddSequenceAsync(editor);
        editor.Press(Key.Escape);

        await CtrlAsync(editor, Key.V);

        Timeline(editor).IsFocused.ShouldBeTrue();
        editor.Press(Key.Delete);
        editor.SequenceNames.ShouldBe(new[] { "Attack", "Sequence" });
        Tracks(path, 1).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CopyTracks_ThenDeleteEverySequence_CtrlV_SaysThereIsNothingToPasteInto()
    {
        var (editor, path) = await OpenWithTwoTracksCopiedAsync();
        using var _ = editor;
        editor.Click((Control)List(editor).ContainerFromIndex(0)!);
        editor.Press(Key.Delete);
        editor.SequenceNames.ShouldBeEmpty();

        await CtrlAsync(editor, Key.V);

        editor.ProblemText.ShouldNotBeNull().ShouldContain("no sequence");
    }

    [AvaloniaFact]
    public async Task CopyChains_NewEmptySequence_CtrlV_PastesTheChainsOntoNewTracks()
    {
        var editor = new AnimationEditorHarness();
        using var _ = editor;
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 0.6), RawInputModifiers.Control);
        await CtrlAsync(editor, Key.C);
        await AddSequenceAsync(editor);
        editor.Press(Key.Escape);

        await CtrlAsync(editor, Key.V);

        var pasted = Tracks(path, 1);
        pasted.Select(t => t.Name).ShouldBe(new[] { "Body", "Head" });
        pasted[0].Chains.ShouldBe(new[] { "Walk" });
        pasted[1].Chains.ShouldBe(new[] { "Run" });
    }

    [AvaloniaFact]
    public async Task CopyTracks_PasteIntoTheSameSequence_AddsCopiesOnTop()
    {
        var (editor, path) = await OpenWithTwoTracksCopiedAsync();
        using var _ = editor;
        editor.ClickAt(At(editor, 2, 5.0));   // nothing selected, the timeline keeps the focus

        await CtrlAsync(editor, Key.V);

        Tracks(path, 0).Select(t => t.Name).ShouldBe(new[] { "Body 2", "Head 2", "Body", "Head", "Tail" });
    }

    [AvaloniaFact]
    public async Task CopySequence_CtrlVOnTheTimeline_PastesTheSequence()
    {
        var editor = new AnimationEditorHarness();
        using var _ = editor;
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        editor.Click((Control)List(editor).ContainerFromIndex(0)!);
        await CtrlAsync(editor, Key.C);
        Timeline(editor).Focus();
        editor.Layout();

        await CtrlAsync(editor, Key.V);

        editor.SequenceNames.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task CtrlV_WithOrdinaryTextOnTheClipboard_SaysNothingWasPasted()
    {
        var editor = new AnimationEditorHarness();
        using var _ = editor;
        await editor.OpenAsync(WriteFixture(editor));
        await editor.Window.Clipboard!.SetTextAsync("just some words");
        editor.Click((Control)List(editor).ContainerFromIndex(0)!);

        await CtrlAsync(editor, Key.V);

        editor.ProblemText.ShouldNotBeNull();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CtrlV_WithNothingOnTheClipboard_SaysNothingWasPasted()
    {
        var editor = new AnimationEditorHarness();
        using var _ = editor;
        await editor.OpenAsync(WriteFixture(editor));
        await editor.Window.Clipboard!.ClearAsync();
        editor.Click((Control)List(editor).ContainerFromIndex(0)!);

        await CtrlAsync(editor, Key.V);

        editor.ProblemText.ShouldNotBeNull();
    }
}
