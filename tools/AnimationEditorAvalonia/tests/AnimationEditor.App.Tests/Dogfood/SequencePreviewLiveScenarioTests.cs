using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Sequences;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;
using System.Text.Json;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// The <c>.seqj</c> preview always shows <see cref="SequenceScene.At"/> at the playhead: right after a file opens, and
/// right after every edit, undo, redo, tab switch and drag step, with no play or scrub in between. Each scenario
/// compares the preview's sprites with the scene a document freshly read from disk gives at the same playhead.
/// </summary>
public class SequencePreviewLiveScenarioTests
{
    // hero.achx on sheet.png: Walk, five 0.1 s frames (0.5 s); Run, two (0.2 s).
    private static string WriteFixture(AnimationEditorHarness editor, params SequenceTrackSave[] tracks) =>
        WriteSequences(editor, new[] { tracks });

    private static string WriteSequences(AnimationEditorHarness editor, params SequenceTrackSave[][] sequences)
    {
        editor.WritePng("sheet.png", 64, 64);
        WriteHero(editor);
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        for (int i = 0; i < sequences.Length; i++)
        {
            var sequence = new SequenceSave { Name = i == 0 ? "Attack" : $"Other {i}" };
            sequence.Tracks.AddRange(sequences[i]);
            save.Sequences.Add(sequence);
        }
        save.Save(path);
        return path;
    }

    private static void WriteHero(AnimationEditorHarness editor) =>
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "sheet.png", (0, 32, 16, 16), (16, 32, 16, 16)));

    private static SequenceTrackSave Track(string name, params (string Chain, double Start)[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        foreach (var (chain, start) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Start = start, Loops = 1 });
        return track;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static PreviewControl Preview(AnimationEditorHarness editor) =>
        editor.Control<PreviewControl>("SequencePreview");

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static ListBox List(AnimationEditorHarness editor) => editor.Control<ListBox>("SequenceList");

    private static LibraryChipView Chip(AnimationEditorHarness editor, string chain) =>
        editor.Control<SequenceLibraryControl>("SequenceLibrary").ChipFor("hero.achx", chain)!;

    private static Point At(AnimationEditorHarness editor, int lane, double time, double dx = 0)
    {
        var timeline = Timeline(editor);
        double y = timeline.Viewport.LaneTop(lane) + (TimelineLayout.BodyTop + TimelineLayout.BodyBottom) / 2;
        return editor.PointIn(timeline, timeline.Viewport.XAt(time) + dx, y);
    }

    private static Point Header(AnimationEditorHarness editor, int lane)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(lane) + 10);
    }

    private static Point RulerAt(AnimationEditorHarness editor, double time)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), TimelineViewport.RulerHeight / 2);
    }

    private static TextBox BoxOf(FlankerNumericField field) => field.GetVisualDescendants().OfType<TextBox>().First();

    private static void TypeInto(AnimationEditorHarness editor, FlankerNumericField field, string text) =>
        editor.TypeAndEnter(BoxOf(field), text);

    private static void TypeInto(AnimationEditorHarness editor, NumericUpDown field, string text) =>
        editor.TypeAndEnter(field.GetVisualDescendants().OfType<TextBox>().First(), text);

    private static void Undo(AnimationEditorHarness editor) => editor.Press(Key.Z, RawInputModifiers.Control);

    private static void Redo(AnimationEditorHarness editor) => editor.Press(Key.Y, RawInputModifiers.Control);

    private static string Describe(IEnumerable<SequenceSprite>? sprites) =>
        sprites is null
            ? "<null>"
            : string.Join(", ", sprites.Select(s =>
                $"{s.Track}:{s.Chain.Name}#{s.FrameIndex} {Path.GetFileName(s.TexturePath)} at ({s.Sample().OffsetX},{s.Sample().OffsetY}){(s.FlipHorizontal ? " flipped" : "")}"));

    /// <summary>What <see cref="SequenceScene.At"/> gives for the saved file at the playhead, for the selected sequence.</summary>
    private static string Expected(AnimationEditorHarness editor, string path) =>
        Describe(SequenceScene.At(SequenceDocument.Load(path), List(editor).SelectedIndex, Timeline(editor).Playhead));

    private static string Shown(AnimationEditorHarness editor) => Describe(Preview(editor).Composite);

    private static List<string> ShownChains(AnimationEditorHarness editor) =>
        Preview(editor).Composite!.Select(sprite => $"{sprite.Chain.Name}:{sprite.FrameIndex}").ToList();

    /// <summary>Notes each step whose preview is not the scene, so one run lists every stale edit rather than the first.</summary>
    private sealed class Steps(AnimationEditorHarness editor, string path)
    {
        private readonly List<string> _stale = new();

        public void Check(string label)
        {
            string expected = Expected(editor, path);
            string shown = Shown(editor);
            if (expected != shown) _stale.Add($"{label}: preview [{shown}] but the scene is [{expected}]");
        }

        public void ShouldAllBeFresh() => _stale.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task OpeningAFile_ShowsTheSequenceAtZeroAtOnce_WithNoPlayOrScrub()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0)));

        await editor.OpenAsync(boss);

        Timeline(editor).Playhead.ShouldBe(0);
        ShownChains(editor).ShouldBe(new[] { "Walk:0" });
        Shown(editor).ShouldBe(Expected(editor, boss));
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public void RestoringASession_ShowsTheActiveSequenceAtZero()
    {
        string folder = Path.Combine(Path.GetTempPath(), "AnimationEditorDogfood", Guid.NewGuid().ToString("N"));
        string settingsRoot = Path.Combine(folder, "settings");
        using AnimationEditorHarness writer = new AnimationEditorHarness();
        string boss = WriteFixture(writer, Track("Body", ("Walk", 0)));
        var settingsFile = AppSettingsLocation.ForApplicationDataRoot(settingsRoot);
        Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
        File.WriteAllText(settingsFile.FullPath, JsonSerializer.Serialize(new AppSettingsModel
        {
            OpenTabPaths = new List<string> { boss },
            ActiveTabPath = boss,
        }));

        try
        {
            using AnimationEditorHarness editor = new AnimationEditorHarness(settingsRoot);
            editor.WaitUntil(() => editor.TabLabels.Count == 1, TimeSpan.FromSeconds(5));
            editor.Layout();

            ShownChains(editor).ShouldBe(new[] { "Walk:0" });
            Shown(editor).ShouldBe(Expected(editor, boss));
        }
        finally
        {
            try { Directory.Delete(folder, recursive: true); } catch { /* a watcher may still hold it */ }
        }
    }

    [AvaloniaFact]
    public async Task SwitchingSequences_ShowsTheOtherSequenceAtZero()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteSequences(editor, new[] { Track("Body", ("Walk", 0)) }, new[] { Track("Legs", ("Run", 0)) }, Array.Empty<SequenceTrackSave>());
        await editor.OpenAsync(boss);
        var steps = new Steps(editor, boss);

        editor.ClickAt(RulerAt(editor, 0.15));
        editor.Click((Control)List(editor).ContainerFromIndex(1)!);
        ShownChains(editor).ShouldBe(new[] { "Run:0" });
        steps.Check("second sequence");
        editor.Click((Control)List(editor).ContainerFromIndex(2)!);
        ShownChains(editor).ShouldBeEmpty();
        steps.Check("empty sequence");
        editor.Click((Control)List(editor).ContainerFromIndex(0)!);
        ShownChains(editor).ShouldBe(new[] { "Walk:0" });
        steps.Check("back to the first");

        steps.ShouldAllBeFresh();
    }

    [AvaloniaFact]
    public async Task SwitchingSequences_ShowsTheOtherSequencesLoopFlag()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteSequences(editor, new[] { Track("Body", ("Walk", 0)) }, new[] { Track("Legs", ("Run", 0)) });
        var file = SequenceListSave.FromFile(boss);
        file.Sequences[1].Loop = true;
        file.Save(boss);
        await editor.OpenAsync(boss);
        var toggle = editor.Control<Avalonia.Controls.Primitives.ToggleButton>("SequenceLoopToggle");
        toggle.IsChecked.ShouldBe(false);

        editor.Click((Control)List(editor).ContainerFromIndex(1)!);
        toggle.IsChecked.ShouldBe(true);
        editor.Click((Control)List(editor).ContainerFromIndex(0)!);
        toggle.IsChecked.ShouldBe(false);
    }

    [AvaloniaFact]
    public async Task SwitchingTabsAndBack_ShowsTheSequenceAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0)));
        await editor.OpenAsync(boss);
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));

        editor.ClickTab("boss.seqj");

        ShownChains(editor).ShouldBe(new[] { "Walk:0" });
        Shown(editor).ShouldBe(Expected(editor, boss));
    }

    [AvaloniaFact]
    public async Task ANewEmptySequence_ThenItsFirstChipAtZero_ShowsTheSpriteAtOnce()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor);
        await editor.OpenAsync(boss);
        ShownChains(editor).ShouldBeEmpty();

        editor.Drag(editor.CenterOf(Chip(editor, "Walk")), At(editor, TimelineViewport.NewTrackLane, 0));

        ShownChains(editor).ShouldBe(new[] { "Walk:0" });
        Shown(editor).ShouldBe(Expected(editor, boss));
        editor.Click(editor.Control<Button>("AddSequenceButton"));
        ShownChains(editor).ShouldBeEmpty();
        editor.Drag(editor.CenterOf(Chip(editor, "Run")), At(editor, TimelineViewport.NewTrackLane, 0));
        ShownChains(editor).ShouldBe(new[] { "Run:0" });
    }

    [AvaloniaFact]
    public async Task EveryPausedEdit_AndItsUndoAndRedo_LeavesThePreviewOnTheScene()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0)));
        await editor.OpenAsync(boss);
        var steps = new Steps(editor, boss);
        var timeline = Timeline(editor);
        var inspector = Inspector(editor);
        steps.Check("open");

        editor.ClickAt(editor.PointIn(timeline, 30, timeline.Viewport.LaneTop(TimelineViewport.NewTrackLane) + TimelineViewport.LaneHeight / 2));
        editor.Press(Key.Escape);
        steps.Check("+ new track");
        Undo(editor);
        steps.Check("undo + new track");
        Redo(editor);
        steps.Check("redo + new track");

        editor.Drag(editor.CenterOf(Chip(editor, "Run")), At(editor, 0, 0));
        steps.Check("chip onto a lane");
        editor.Drag(editor.CenterOf(Chip(editor, "Run")), At(editor, TimelineViewport.NewTrackLane, 0));
        steps.Check("chip onto the new-track row");
        Undo(editor);
        steps.Check("undo chip drop");
        Redo(editor);
        steps.Check("redo chip drop");

        editor.Drag(At(editor, 1, 0.1), At(editor, 1, 0.1, 400));
        steps.Check("move a chain");
        Undo(editor);
        steps.Check("undo move");
        Redo(editor);
        steps.Check("redo move");
        editor.Drag(At(editor, 2, 0.5), At(editor, 2, 0.2));
        steps.Check("resize a chain");
        Undo(editor);
        steps.Check("undo resize");

        editor.ClickAt(Header(editor, 2));
        editor.Click(inspector.TrackFlipBox);
        steps.Check("track flip");
        Undo(editor);
        steps.Check("undo flip");
        Redo(editor);
        steps.Check("redo flip");

        editor.ClickAt(At(editor, 2, 0.1));
        TypeInto(editor, inspector.ChainStartField, "0.3");
        steps.Check("chain start");
        TypeInto(editor, inspector.ChainStartField, "0");
        TypeInto(editor, inspector.ChainLoopsField, "2");
        steps.Check("chain loops");
        Undo(editor);
        steps.Check("undo loops");

        editor.ClickAt(At(editor, 2, 0.6));
        editor.Press(Key.Delete);
        steps.Check("Delete key on a chain");
        Undo(editor);
        steps.Check("undo delete chain");

        editor.Drag(Header(editor, 2), Header(editor, 0));
        steps.Check("reorder tracks");
        Undo(editor);
        steps.Check("undo reorder");
        editor.RightClickAt(Header(editor, 1));
        editor.PickMenuItem(timeline.Menu, "Rename Track…");
        editor.TypeIntoOpenSequenceRename("Legs");
        steps.Check("rename a track");
        editor.RightClickAt(Header(editor, 0));
        editor.PickMenuItem(timeline.Menu, "Delete Track");
        steps.Check("delete a track");
        Undo(editor);
        steps.Check("undo delete track");

        steps.ShouldAllBeFresh();
    }

    [AvaloniaFact]
    public async Task EditingWhilePlaying_KeepsPlayingFromWhereItWas_AndShowsTheEditAtOnce()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0)), Track("Fx"));
        await editor.OpenAsync(boss);
        var steps = new Steps(editor, boss);
        editor.Click(editor.Control<Button>("SequencePlayButton"));
        (await editor.WaitUntilAsync(() => Timeline(editor).Playhead > 0.1, TimeSpan.FromSeconds(3))).ShouldBeTrue();
        double before = Timeline(editor).Playhead;

        editor.Drag(editor.CenterOf(Chip(editor, "Run")), At(editor, 1, 0));

        editor.Control<Avalonia.Svg.Skia.Svg>("SequencePlayIcon").Path!.ShouldContain("IconPause");
        Timeline(editor).Playhead.ShouldBeGreaterThanOrEqualTo(before);
        steps.Check("chip dropped while playing");
        steps.ShouldAllBeFresh();
    }

    [AvaloniaFact]
    public async Task ShorteningTheSequenceBelowThePlayhead_ClampsThePlayheadAndRedrawsThePreview()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0)));
        await editor.OpenAsync(boss);
        editor.ClickAt(RulerAt(editor, 0.45));
        ShownChains(editor).ShouldBe(new[] { "Walk:4" });

        editor.Drag(At(editor, 0, 0.5), At(editor, 0, 0.2));

        Timeline(editor).SequenceLength.ShouldBe(0.2, 0.011);
        Timeline(editor).Playhead.ShouldBeLessThanOrEqualTo(Timeline(editor).SequenceLength + 1e-9);
        Shown(editor).ShouldBe(Expected(editor, boss));
    }

    [AvaloniaFact]
    public async Task MovingAChainAwayFromThePlayhead_RemovesItFromThePreviewWhileDragging()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0)));
        await editor.OpenAsync(boss);
        ShownChains(editor).ShouldBe(new[] { "Walk:0" });

        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 0.25, 400));

        Timeline(editor).Blocks.Single().Start.ShouldBeGreaterThan(0.5);
        ShownChains(editor).ShouldBeEmpty();
        editor.DragRelease(At(editor, 0, 0.25, 400));
        ShownChains(editor).ShouldBeEmpty();
        Shown(editor).ShouldBe(Expected(editor, boss));
    }

    [AvaloniaFact]
    public async Task ResizingAChainBelowThePlayhead_RemovesItFromThePreviewWhileDragging_AndEscapeBringsItBack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0)));
        await editor.OpenAsync(boss);
        editor.ClickAt(RulerAt(editor, 0.4));
        ShownChains(editor).ShouldBe(new[] { "Walk:4" });

        editor.DragHold(At(editor, 0, 0.5), At(editor, 0, 0.2));

        Timeline(editor).Blocks.Single().Full.ShouldBeLessThan(0.3);
        ShownChains(editor).ShouldBeEmpty();
        editor.Press(Key.Escape);
        ShownChains(editor).ShouldBe(new[] { "Walk:4" });
        Shown(editor).ShouldBe(Expected(editor, boss));
    }

    [AvaloniaFact]
    public async Task AChainThatStartsAfterThePlayhead_LeavesThePreviewPlainlyEmpty_UntilTheyScrubIntoIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0.4)));
        await editor.OpenAsync(boss);

        ShownChains(editor).ShouldBeEmpty();

        editor.ClickAt(RulerAt(editor, 0.45));
        ShownChains(editor).ShouldBe(new[] { "Walk:0" });
    }

    [AvaloniaFact]
    public async Task ChangingAChainsStart_FillsOrEmptiesThePreviewAtOnce_AndSoDoUndoAndRedo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0.4)));
        await editor.OpenAsync(boss);
        editor.ClickAt(At(editor, 0, 0.6));

        TypeInto(editor, Inspector(editor).ChainStartField, "0");
        ShownChains(editor).ShouldBe(new[] { "Walk:0" });
        Undo(editor);
        ShownChains(editor).ShouldBeEmpty();
        Redo(editor);
        ShownChains(editor).ShouldBe(new[] { "Walk:0" });
    }

    [AvaloniaFact]
    public async Task HidingAndShowingATrack_UpdatesThePreviewAtOnce_AndSoDoUndoAndRedo()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0)), Track("Fx", ("Run", 0)));
        await editor.OpenAsync(boss);
        var steps = new Steps(editor, boss);

        Timeline(editor).ToggleTrackVisibility(0);
        ShownChains(editor).ShouldBe(new[] { "Run:0" });
        steps.Check("hide");
        Undo(editor);
        ShownChains(editor).ShouldBe(new[] { "Run:0", "Walk:0" });
        steps.Check("undo hide");
        Redo(editor);
        ShownChains(editor).ShouldBe(new[] { "Run:0" });
        steps.Check("redo hide");

        steps.ShouldAllBeFresh();
    }

    [AvaloniaFact]
    public async Task AnEdit_LeavesThePreviewsZoomAndPanAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0)));
        await editor.OpenAsync(boss);
        var preview = Preview(editor);
        preview.SetZoomPercent(300);
        preview.SetPan(40, -25);
        float zoom = preview.Zoom;
        var pan = preview.PanOffset;

        editor.Drag(editor.CenterOf(Chip(editor, "Run")), At(editor, 0, 0));
        Undo(editor);
        editor.ClickAt(Header(editor, 0));
        editor.Click(Inspector(editor).TrackFlipBox);
        editor.DragHold(At(editor, 0, 0.25), At(editor, 0, 0.25, 100));
        editor.DragRelease(At(editor, 0, 0.25, 100));

        preview.Zoom.ShouldBe(zoom);
        preview.PanOffset.ShouldBe(pan);
    }

    [AvaloniaFact]
    public async Task AMissingSourceOrChain_DrawsNothingAndRaisesNoError_WhileTheOtherTracksStillShow()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var broken = new SequenceTrackSave { Name = "Broken" };
        broken.Chains.Add(new SequenceChainReferenceSave { Source = "gone.achx", Chain = "Walk", Start = 0 });
        broken.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Nope", Start = 0 });
        string boss = WriteFixture(editor, broken, Track("Body", ("Run", 0)));

        await editor.OpenAsync(boss);

        ShownChains(editor).ShouldBe(new[] { "Run:0" });
        editor.ProblemText.ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task ASourceEditedWhileItsTabWasOpen_ShowsItsNewChainsWhenTheSequenceTabIsShownAgain()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, Track("Body", ("Walk", 0)));
        await editor.OpenAsync(boss);
        editor.ClickAt(RulerAt(editor, 0.35));
        ShownChains(editor).ShouldBe(new[] { "Walk:3" });
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));

        // Walk is now two frames of 0.1 s; the file changes under the open sequence.
        string hero = Path.Combine(editor.ProjectFolder, "hero.achx");
        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        list.AnimationChains.Add(AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        list.Save(hero);
        File.SetLastWriteTimeUtc(hero, DateTime.UtcNow.AddMinutes(1));
        editor.ClickTab("boss.seqj");

        Shown(editor).ShouldBe(Expected(editor, boss));
        ShownChains(editor).ShouldBe(new[] { "Walk:0" });
        Timeline(editor).SequenceLength.ShouldBe(0.2, 1e-6);
    }
}
