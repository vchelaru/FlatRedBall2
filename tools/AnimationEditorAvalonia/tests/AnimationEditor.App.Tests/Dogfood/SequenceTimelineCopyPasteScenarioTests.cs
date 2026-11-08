using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Text.Json.Nodes;
using Xunit;
using static AnimationEditor.App.Tests.Dogfood.SequenceTimelineMultiSelectScenarioTests;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Ctrl+C, Ctrl+X and Ctrl+V (and Edit &gt; Copy / Cut / Paste) on timeline chains and tracks while the timeline is the
/// focused surface. The sequence list keeps sequence copy and paste; text boxes and the other tab kinds keep their own meaning of the keys.
/// Fixture: Body: Walk 0..0.5, Walk 1..1.5, Run 2..2.2. Head: Run 0.5..0.7, Walk 1.5..2. Tail: empty.
/// </summary>
public class SequenceTimelineCopyPasteScenarioTests
{
    private static (string Chain, double Start)[] Chains(string path, int track) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks[track].Chains.Select(c => (c.Chain, c.Start)).ToArray();

    private static async Task SetClipboardAsync(AnimationEditorHarness editor, string text)
    {
        await editor.Window.Clipboard!.SetTextAsync(text);
        editor.Layout();
    }

    private static async Task<string?> ClipboardAsync(AnimationEditorHarness editor)
    {
        editor.Layout();
        return await editor.Window.Clipboard!.TryGetTextAsync();
    }

    /// <summary>Presses Ctrl+key, then lets the fire-and-forget clipboard work finish.</summary>
    private static async Task CtrlAsync(AnimationEditorHarness editor, Key key)
    {
        editor.Press(key, RawInputModifiers.Control);
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));
        editor.Layout();
    }

    private static Avalonia.Point Header(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 70, Timeline(editor).HeaderCenters(lane).Name);

    private static void Scrub(AnimationEditorHarness editor, double time) =>
        editor.ClickAt(editor.PointIn(Timeline(editor), Timeline(editor).Viewport.XAt(time), 8));

    private static async Task<(AnimationEditorHarness Editor, string Path)> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        string path = WriteFixture(editor);
        await editor.OpenAsync(path);
        return (editor, path);
    }

    [AvaloniaFact]
    public async Task CtrlC_OnTheTimeline_CopiesTheSelectedChains_AsAChainsEnvelope_AndChangesNothing()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        editor.ClickAt(At(editor, 0, 1.25));
        editor.ClickAt(At(editor, 0, 0.25), RawInputModifiers.Control);

        await CtrlAsync(editor, Key.C);

        var json = JsonNode.Parse((await ClipboardAsync(editor))!)!.AsObject();
        json["kind"]!.GetValue<string>().ShouldBe("frb2-animation-editor-chains");
        json["track"]!.GetValue<string>().ShouldBe("Body");
        var items = json["chains"]!.AsArray();
        items.Select(i => i!["chain"]!.GetValue<string>()).ShouldBe(new[] { "Walk", "Walk" });
        items.Select(i => i!["start"]!.GetValue<double>()).ShouldBe(new[] { 0.0, 1.0 });
        editor.UndoLabels.ShouldBeEmpty();
        Timeline(editor).SelectedChains.Count.ShouldBe(2);
    }

    [AvaloniaFact]
    public async Task CtrlV_OntoTheSelectedTrack_PutsTheEarliestChainAtThePlayhead_AndSelectsWhatWasPasted()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 0, 1.25), RawInputModifiers.Control);
        await CtrlAsync(editor, Key.C);
        editor.ClickAt(Header(editor, 2));
        Scrub(editor, 2.0);   // the playhead stops at the end of the sequence, 2.2 s
        timeline.Playhead.ShouldBe(2.0, 1e-6);

        await CtrlAsync(editor, Key.V);

        Chains(boss, 2).ShouldBe(new[] { ("Walk", 2.0), ("Walk", 3.0) });
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(2, 0), new TimelineChainId(2, 1) });
        editor.UndoLabels.ShouldBe(new[] { "Paste 2 chains" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        Chains(boss, 2).ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CtrlV_WithNoTrackSelected_GoesOntoTheTrackOfTheFirstCopiedChain()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        editor.ClickAt(At(editor, 1, 1.75));
        await CtrlAsync(editor, Key.C);
        editor.Press(Key.Escape);   // nothing selected, the timeline keeps the focus, the playhead stays at 0

        await CtrlAsync(editor, Key.V);

        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5), ("Walk", 1.5), ("Walk", 0.0) });
    }

    [AvaloniaFact]
    public async Task CtrlV_WithNoTrackSelected_PutsEachChainBackOnItsOwnTrack()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        // One chain from Body and two from Head.
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 0.6), RawInputModifiers.Control);
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);
        await CtrlAsync(editor, Key.C);
        editor.ClickAt(At(editor, 2, 5.0));   // empty lane space: nothing selected
        Scrub(editor, 2.0);

        await CtrlAsync(editor, Key.V);

        // Walk (0) is the earliest copied chain, so it lands at the playhead; Head's two keep their distances from it.
        Chains(boss, 0).ShouldBe(new[] { ("Walk", 0.0), ("Walk", 1.0), ("Run", 2.0), ("Walk", 2.0) });
        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5), ("Walk", 1.5), ("Run", 2.5), ("Walk", 3.5) });
        Chains(boss, 2).ShouldBeEmpty();
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 3), new TimelineChainId(1, 2), new TimelineChainId(1, 3) });
        editor.UndoLabels.ShouldBe(new[] { "Paste 3 chains" });
    }

    [AvaloniaFact]
    public async Task CtrlV_WithATrackSelected_KeepsTheLaneOffsetsBetweenTheCopiedTracks()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);
        await CtrlAsync(editor, Key.C);
        editor.ClickAt(Header(editor, 1));
        Scrub(editor, 2.0);

        await CtrlAsync(editor, Key.V);

        // Body (lane 0) maps to the selected Head (lane 1); Head's chain is one lane further, on Tail.
        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5), ("Walk", 1.5), ("Walk", 2.0) });
        Chains(boss, 2).ShouldBe(new[] { ("Walk", 3.5) });
    }

    [AvaloniaFact]
    public async Task CtrlV_IntoAnotherSeqjTab_MakesTheTrack_AddsTheSourceRelativeToIt_InOneUndoEntry()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        string otherFolder = Path.Combine(editor.ProjectFolder, "other");
        Directory.CreateDirectory(otherFolder);
        string other = Path.Combine(otherFolder, "other.seqj");
        var save = new SequenceListSave();
        var sequence = new SequenceSave { Name = "Empty" };
        sequence.Tracks.Add(new SequenceTrackSave { Name = "Lane" });
        save.Sequences.Add(sequence);
        save.Save(other);
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 0, 1.25), RawInputModifiers.Control);
        await CtrlAsync(editor, Key.C);

        await editor.OpenAsync(other);
        editor.Tabs.ActiveTab!.DisplayName.ShouldBe("other.seqj");
        editor.ClickAt(editor.PointIn(Timeline(editor), 500, Timeline(editor).Viewport.LaneTop(3)));
        editor.UndoLabels.ShouldBeEmpty();
        await CtrlAsync(editor, Key.V);

        var saved = SequenceListSave.FromFile(other);
        saved.Sources.ShouldBe(new[] { "../hero.achx" });
        saved.Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Lane", "Body" });
        saved.Sequences[0].Tracks[1].Chains.Select(c => (c.Source, c.Start)).ShouldBe(new[] { ("../hero.achx", 0.0), ("../hero.achx", 1.0) });
        editor.UndoLabels.ShouldBe(new[] { "Paste 2 chains" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(other).Sources.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task CopyingAndPastingTracks_InsertsCopiesAfterTheSelectedTrack_WithFreeNames_AndSelectsThem()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        var timeline = Timeline(editor);
        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(Header(editor, 1), RawInputModifiers.Control);
        await CtrlAsync(editor, Key.C);
        JsonNode.Parse((await ClipboardAsync(editor))!)!["kind"]!.GetValue<string>().ShouldBe("frb2-animation-editor-tracks");
        // A few pixels off the earlier click on this header, so it is not a double-click (which renames).
        editor.ClickAt(editor.PointIn(timeline, 100, timeline.HeaderCenters(1).Name));

        await CtrlAsync(editor, Key.V);

        SequenceListSave.FromFile(boss).Sequences[0].Tracks.Select(t => t.Name).ShouldBe(new[] { "Body", "Head", "Body 2", "Head 2", "Tail" });
        Chains(boss, 2).ShouldBe(new[] { ("Walk", 0.0), ("Walk", 1.0), ("Run", 2.0) });
        timeline.SelectedTracks.ShouldBe(new[] { 2, 3 });
        editor.UndoLabels.ShouldBe(new[] { "Paste 2 tracks" });
    }

    [AvaloniaFact]
    public async Task CtrlX_OnTheTimeline_CopiesThenRemovesTheChains_AsOneUndoEntry()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);

        await CtrlAsync(editor, Key.X);

        JsonNode.Parse((await ClipboardAsync(editor))!)!["chains"]!.AsArray().Count.ShouldBe(2);
        Chains(boss, 0).ShouldBe(new[] { ("Walk", 1.0), ("Run", 2.0) });
        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5) });
        editor.UndoLabels.ShouldBe(new[] { "Remove 2 chains" });
    }

    [AvaloniaFact]
    public async Task TextThatIsNotAnEnvelope_PastesNothing_AndShowsNoBanner_ADamagedOneShowsTheError()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        editor.ClickAt(Header(editor, 2));

        await SetClipboardAsync(editor, "just some text");
        await CtrlAsync(editor, Key.V);
        editor.UndoLabels.ShouldBeEmpty();
        editor.ProblemText.ShouldBeNull();

        await SetClipboardAsync(editor, "{ \"kind\": \"frb2-animation-editor-chains\", \"version\": 1 }");
        await CtrlAsync(editor, Key.V);
        editor.UndoLabels.ShouldBeEmpty();
        editor.ProblemText.ShouldNotBeNull().ShouldContain("damaged");
    }

    [AvaloniaFact]
    public async Task WithTheSequenceListFocused_CopyIsStillTheSequence_AndAChainsEnvelopePastesIntoTheTimeline()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        editor.ClickAt(At(editor, 0, 0.25));
        await CtrlAsync(editor, Key.C);
        string chains = (await ClipboardAsync(editor))!;
        var list = editor.Control<ListBox>("SequenceList");

        editor.Click((Control)list.ContainerFromIndex(0)!);
        await CtrlAsync(editor, Key.V);

        editor.SequenceNames.Count.ShouldBe(1);
        editor.UndoLabels.ShouldBe(new[] { "Paste 'Walk' on 'Body'" });
        editor.ProblemText.ShouldBeNull();
        Chains(boss, 0).Length.ShouldBe(4);
        Timeline(editor).IsFocused.ShouldBeTrue();
        editor.Click((Control)list.ContainerFromIndex(0)!);
        await CtrlAsync(editor, Key.C);
        JsonNode.Parse((await ClipboardAsync(editor))!)!["kind"]!.GetValue<string>().ShouldBe("frb2-animation-editor-sequence");
        chains.ShouldContain("frb2-animation-editor-chains");
    }

    [AvaloniaFact]
    public async Task TypingInATextBox_KeepsItsOwnCopyAndPaste_TheTimelineIsNotTouched()
    {
        var (editor, boss) = await OpenAsync();
        using var _ = editor;
        editor.ClickAt(At(editor, 0, 0.25));
        await CtrlAsync(editor, Key.C);
        editor.ClickAt(Header(editor, 2));
        var nameBox = editor.Control<SequenceInspectorControl>("SequenceInspector").TrackNameBox;
        nameBox.Focus();
        editor.Layout();

        await CtrlAsync(editor, Key.V);

        Chains(boss, 2).ShouldBeEmpty();
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task EditMenuCopyPaste_FollowTheFocusedSurface()
    {
        var (editor, _) = await OpenAsync();
        using var __ = editor;
        var copy = editor.Control<MenuItem>("MenuCopy");
        var paste = editor.Control<MenuItem>("MenuPaste");
        var cut = editor.Control<MenuItem>("MenuCut");

        editor.ClickAt(At(editor, 0, 0.25));
        editor.Layout();
        (copy.IsEnabled, paste.IsEnabled, cut.IsEnabled).ShouldBe((true, true, true));

        // A button in the sequence list's footer is neither the list nor the timeline: the timeline stays the last pane touched.
        editor.Control<Button>("AddSequenceButton").Focus();
        editor.Layout();
        (copy.IsEnabled, paste.IsEnabled, cut.IsEnabled).ShouldBe((true, true, true));
    }

    [AvaloniaFact]
    public async Task EditMenuCopy_WithTheTimelineFocused_PutsTheSelectionOnTheClipboard()
    {
        var (editor, _) = await OpenAsync();
        using var __ = editor;
        editor.ClickAt(At(editor, 0, 0.25));

        editor.ClickMenu("MenuCopy");
        await editor.WaitAsync(TimeSpan.FromMilliseconds(150));

        JsonNode.Parse((await ClipboardAsync(editor))!)!["kind"]!.GetValue<string>().ShouldBe("frb2-animation-editor-chains");
    }
}
