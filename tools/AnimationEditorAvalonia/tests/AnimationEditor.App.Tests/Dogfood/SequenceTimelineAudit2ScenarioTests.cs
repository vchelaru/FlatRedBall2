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
/// Second audit: the clipboard wiring the first scenario tests left unpinned (where a paste is anchored, which key goes
/// to which surface). Same fixture as the multi-select scenarios: Body: Walk 0..0.5, Walk 1..1.5, Run 2..2.2.
/// Head: Run 0.5..0.7, Walk 1.5..2. Tail: empty.
/// </summary>
public class SequenceTimelineAudit2ScenarioTests
{
    private static (string Chain, double Start)[] Chains(string path, int track) =>
        SequenceListSave.FromFile(path).Sequences[0].Tracks[track].Chains.Select(c => (c.Chain, c.Start)).ToArray();

    private static string[] TrackNames(string path) => SequenceListSave.FromFile(path).Sequences[0].Tracks.Select(t => t.Name).ToArray();

    private static Avalonia.Point Header(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 70, Timeline(editor).HeaderCenters(lane).Name);

    // A few pixels off Header(lane), so two clicks on one header are not a double-click (which renames).
    private static Avalonia.Point HeaderOffset(AnimationEditorHarness editor, int lane) =>
        editor.PointIn(Timeline(editor), 100, Timeline(editor).HeaderCenters(lane).Name);

    /// <summary>Presses Ctrl+key, then waits until <paramref name="done"/> holds: the clipboard work is fire-and-forget.</summary>
    private static async Task CtrlAsync(AnimationEditorHarness editor, Key key, Func<bool> done)
    {
        editor.Press(key, RawInputModifiers.Control);
        (await editor.WaitUntilAsync(done, TimeSpan.FromSeconds(5))).ShouldBeTrue("the clipboard work finished");
        editor.Layout();
    }

    /// <summary>Ctrl+C, then waits until the copy is on the clipboard.</summary>
    private static async Task CopyAsync(AnimationEditorHarness editor)
    {
        await editor.Window.Clipboard!.SetTextAsync("");
        editor.Press(Key.C, RawInputModifiers.Control);
        for (int tries = 0; tries < 500; tries++)
        {
            if (await editor.Window.Clipboard!.TryGetTextAsync() is { Length: > 0 }) return;
            await editor.WaitAsync(TimeSpan.FromMilliseconds(10));
        }
        throw new TimeoutException("Ctrl+C put nothing on the clipboard.");
    }

    private static async Task<AnimationEditorHarness> OpenAsync()
    {
        var editor = new AnimationEditorHarness();
        await editor.OpenAsync(WriteFixture(editor));
        return editor;
    }

    [AvaloniaFact]
    public async Task CtrlV_WithAChainSelectedOnAnotherTrack_PastesOntoThatChainsTrack()
    {
        using var editor = await OpenAsync();
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        editor.ClickAt(At(editor, 0, 0.25));
        await CopyAsync(editor);
        // A Head chain is the selection now, not a header: the copy goes where that chain is, not back to Body by name.
        editor.ClickAt(At(editor, 1, 1.75));

        await CtrlAsync(editor, Key.V, () => editor.UndoLabels.Count == 1);

        Chains(boss, 1).ShouldBe(new[] { ("Run", 0.5), ("Walk", 1.5), ("Walk", 0.0) });
        Chains(boss, 0).Length.ShouldBe(3);
    }

    [AvaloniaFact]
    public async Task CtrlV_OfACopiedTrack_WithHeadersSelected_InsertsBelowTheLowestSelectedHeader()
    {
        using var editor = await OpenAsync();
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        editor.ClickAt(Header(editor, 2));
        await CopyAsync(editor);
        editor.ClickAt(Header(editor, 0));
        editor.ClickAt(HeaderOffset(editor, 1), RawInputModifiers.Control);

        await CtrlAsync(editor, Key.V, () => editor.UndoLabels.Count == 1);

        TrackNames(boss).ShouldBe(new[] { "Body", "Head", "Tail 2", "Tail" });
    }

    [AvaloniaFact]
    public async Task CtrlV_OfACopiedTrack_WithChainsSelected_InsertsBelowTheLowestSelectedChainsTrack()
    {
        using var editor = await OpenAsync();
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        editor.ClickAt(Header(editor, 2));
        await CopyAsync(editor);
        // The first chain selected is on the upper track; the lowest one is on Head.
        editor.ClickAt(At(editor, 0, 0.25));
        editor.ClickAt(At(editor, 1, 1.75), RawInputModifiers.Control);

        await CtrlAsync(editor, Key.V, () => editor.UndoLabels.Count == 1);

        TrackNames(boss).ShouldBe(new[] { "Body", "Head", "Tail 2", "Tail" });
    }

    [AvaloniaFact]
    public async Task CtrlV_OfChainsFromASourceThatCannotBeOne_ShowsTheRefusalOnTheBanner()
    {
        using var editor = await OpenAsync();
        editor.ClickAt(Header(editor, 2));
        await editor.Window.Clipboard!.SetTextAsync(
            "{\"kind\":\"frb2-animation-editor-chains\",\"version\":1,\"sources\":[],\"chains\":[{\"source\":\"sheet.png\",\"chain\":\"Walk\",\"start\":0,\"loops\":1}]}");

        await CtrlAsync(editor, Key.V, () => editor.ProblemText is not null);

        editor.ProblemText!.ShouldContain("sheet.png");
    }

    [AvaloniaFact]
    public async Task CtrlX_WithTheFocusOnAButton_StillCutsFromTheLastTouchedPane()
    {
        using var editor = await OpenAsync();
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        editor.ClickAt(At(editor, 0, 0.25));
        // A button in the sequence list's footer takes the focus but not the ownership: the timeline was touched last, so Cut acts on its chain.
        editor.Control<Button>("AddSequenceButton").Focus();
        editor.Layout();

        editor.Press(Key.X, RawInputModifiers.Control);
        editor.Layout();

        Chains(boss, 0).Length.ShouldBe(2);
        editor.UndoLabels.Count.ShouldBe(1);
    }
    [AvaloniaFact]
    public async Task EditMenuCut_WithTheTimelineFocused_RemovesTheSelectedChain()
    {
        using var editor = await OpenAsync();
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        editor.ClickAt(At(editor, 0, 0.25));

        editor.ClickMenu("MenuCut");

        Chains(boss, 0).ShouldBe(new[] { ("Walk", 1.0), ("Run", 2.0) });
        editor.UndoLabels.ShouldBe(new[] { "Remove 'Walk' from 'Body'" });
    }

    [AvaloniaFact]
    public async Task EditMenuDuplicate_WithTheTimelineFocused_DuplicatesTheSelectedChain()
    {
        using var editor = await OpenAsync();
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        editor.ClickAt(At(editor, 0, 0.25));

        editor.ClickMenu("MenuDuplicate");

        Chains(boss, 0).Length.ShouldBe(4);
        editor.UndoLabels.Count.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task DeleteCutAndDuplicate_InTheTrackRenameBox_EditTheText_NotTheTimeline()
    {
        using var editor = await OpenAsync();
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        editor.DoubleClickAt(Header(editor, 2));
        Timeline(editor).IsRenamingTrack.ShouldBeTrue();

        editor.Press(Key.Delete);
        editor.Press(Key.X, RawInputModifiers.Control);
        editor.Press(Key.D, RawInputModifiers.Control);

        TrackNames(boss).ShouldBe(new[] { "Body", "Head", "Tail" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task F3_TogglesTheTimelineDiagnosticsWithTheOtherPanels()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);
        timeline.DiagnosticsEnabled.ShouldBeFalse();

        editor.Press(Key.F3);
        timeline.DiagnosticsEnabled.ShouldBeTrue();

        editor.Press(Key.F3);
        timeline.DiagnosticsEnabled.ShouldBeFalse();
    }

    [AvaloniaFact]
    public async Task DraggingAChipOntoALane_WithAltHeld_FollowsThePointerFreely()
    {
        using var editor = await OpenAsync();
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var timeline = Timeline(editor);
        var chip = editor.CenterOf(editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", "Run")!);
        // 3 px past the end of Body's first Walk, which a plain drop would snap to.
        var target = At(editor, 2, 0.5, dx: 3);

        editor.DragHold(chip, target, RawInputModifiers.Alt);

        timeline.DropPreview!.Value.Start.ShouldBe(0.5 + 3 / 120.0, 1e-6);
        timeline.SnapGuideTime.ShouldBeNull();
        editor.DragRelease(target, RawInputModifiers.Alt);
        SequenceListSave.FromFile(boss).Sequences[0].Tracks[2].Chains.Single().Start.ShouldBe(0.5 + 3 / 120.0, 1e-6);
    }

    [AvaloniaFact]
    public async Task CtrlWheelOverTheTrackHeaders_ZoomsAroundTheLeftEdgeOfTheLanes()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);
        timeline.Viewport.SetPixelsPerSecond(600);
        timeline.Viewport.SetScrollSeconds(3);
        double atLanesEdge = timeline.Viewport.TimeAt(TimelineViewport.HeaderWidth);
        var onHeader = editor.PointIn(timeline, 40, timeline.Viewport.LaneTop(0) + 10);

        editor.Wheel(onHeader, 1, RawInputModifiers.Control);
        timeline.SettleZoomAnimation();

        timeline.Viewport.PixelsPerSecond.ShouldBeGreaterThan(600);
        timeline.Viewport.TimeAt(TimelineViewport.HeaderWidth).ShouldBe(atLanesEdge, 1e-6);
    }

    [AvaloniaFact]
    public async Task PressingTheRulerCornerAboveTheHeaders_AndDraggingOverChains_SelectsNothing()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);
        var corner = editor.PointIn(timeline, 30, TimelineViewport.RulerHeight / 2);

        editor.DragHold(corner, At(editor, 1, 1.75));

        timeline.IsMarqueeActive.ShouldBeFalse();
        editor.DragRelease(At(editor, 1, 1.75));
        timeline.SelectedChains.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Escape_DuringACtrlRubberBand_PutsTheSelectionBackToWhatItWasBeforeTheBand()
    {
        using var editor = await OpenAsync();
        var timeline = Timeline(editor);
        editor.ClickAt(At(editor, 0, 2.1));
        editor.DragHold(At(editor, 1, 0.2), At(editor, 1, 0.6), RawInputModifiers.Control);
        timeline.IsMarqueeActive.ShouldBeTrue();
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 2), new TimelineChainId(1, 0) });

        editor.Press(Key.Escape);

        timeline.IsMarqueeActive.ShouldBeFalse();
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 2) });
        editor.DragRelease(At(editor, 1, 0.6), RawInputModifiers.Control);
        timeline.SelectedChains.ShouldBe(new[] { new TimelineChainId(0, 2) });
    }
}
