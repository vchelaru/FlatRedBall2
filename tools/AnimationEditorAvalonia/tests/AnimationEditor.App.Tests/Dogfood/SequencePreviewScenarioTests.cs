using AnimationEditor.App.Controls;
using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// A <c>.seqj</c> tab's composited preview and playback: the sprites shown at the playhead, the transport, ruler
/// scrubbing, the Space hotkey and the sequence's Loop flag.
/// </summary>
public class SequencePreviewScenarioTests
{
    // hero.achx on hero.png: Walk, five 0.1 s frames (0.5 s); Run, two (0.2 s). fx.achx on fx.png: Spark, two (0.2 s).
    private static string WriteFixture(AnimationEditorHarness editor, bool loop, params SequenceTrackSave[] tracks)
    {
        editor.WritePng("hero.png", 64, 64);
        editor.WritePng("fx.png", 32, 32);
        editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("Walk", "hero.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16), (48, 0, 16, 16), (0, 16, 16, 16)),
            AnimationEditorHarness.Chain("Run", "hero.png", (0, 32, 16, 16), (16, 32, 16, 16)));
        editor.WriteAchx("fx.achx", AnimationEditorHarness.Chain("Spark", "fx.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var save = new SequenceListSave { Sources = { "hero.achx", "fx.achx" } };
        var sequence = new SequenceSave { Name = "Attack", Loop = loop };
        sequence.Tracks.AddRange(tracks);
        save.Sequences.Add(sequence);
        save.Save(path);
        return path;
    }

    private static SequenceTrackSave Track(string name, params (string Source, string Chain, double Start, float Loops)[] chains)
    {
        var track = new SequenceTrackSave { Name = name };
        foreach (var (source, chain, start, loops) in chains)
            track.Chains.Add(new SequenceChainReferenceSave { Source = source, Chain = chain, Start = start, Loops = loops });
        return track;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static PreviewControl SequencePreview(AnimationEditorHarness editor) =>
        editor.Control<PreviewControl>("SequencePreview");

    private static bool IsPlaying(AnimationEditorHarness editor) =>
        editor.Control<Avalonia.Svg.Skia.Svg>("SequencePlayIcon").Path!.Contains("IconPause");

    /// <summary>A window point on the timeline ruler at <paramref name="time"/>.</summary>
    private static Point RulerAt(AnimationEditorHarness editor, double time)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, timeline.Viewport.XAt(time), TimelineViewport.RulerHeight / 2);
    }

    private static List<string> Shown(AnimationEditorHarness editor) =>
        SequencePreview(editor).Composite!.Select(sprite => $"{sprite.Chain.Name}:{sprite.FrameIndex}").ToList();

    [AvaloniaFact]
    public async Task Scrubbing_ShowsBothTracksComposited_EachFromItsOwnTexture_TopLaneInFront()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: false,
            Track("Fx", ("fx.achx", "Spark", 0, 1)),
            Track("Body", ("hero.achx", "Walk", 0, 1)));
        await editor.OpenAsync(boss);

        editor.ClickAt(RulerAt(editor, 0.15));

        Timeline(editor).Playhead.ShouldBe(0.15, 0.01);
        Shown(editor).ShouldBe(new[] { "Walk:1", "Spark:1" });
        SequencePreview(editor).Composite!.Select(sprite => Path.GetFileName(sprite.TexturePath))
            .ShouldBe(new[] { "hero.png", "fx.png" });
        editor.Control<TextBlock>("SequenceTimeText").Text.ShouldBe("0.15 s / 0.50 s");
    }

    [AvaloniaFact]
    public async Task DraggingOnTheRuler_MovesThePlayheadAndThePreview()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: false, Track("Body", ("hero.achx", "Walk", 0, 1)));
        await editor.OpenAsync(boss);

        editor.Drag(RulerAt(editor, 0.05), RulerAt(editor, 0.35));

        Timeline(editor).Playhead.ShouldBe(0.35, 0.01);
        Shown(editor).ShouldBe(new[] { "Walk:3" });
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task OutsideItsChain_ATracksSpriteIsHidden()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: false,
            Track("Body", ("hero.achx", "Walk", 0, 1)),
            Track("Fx", ("fx.achx", "Spark", 0.3, 1)));
        await editor.OpenAsync(boss);

        editor.ClickAt(RulerAt(editor, 0.1));
        Shown(editor).ShouldBe(new[] { "Walk:1" });
        editor.ClickAt(RulerAt(editor, 0.35));
        Shown(editor).ShouldBe(new[] { "Spark:0", "Walk:3" });
    }

    [AvaloniaFact]
    public async Task BeforeItsChainStarts_TheTrackIsHiddenAtTimeZero()
    {
        // Hidden before the chain on purpose: this is how a sprite appears partway through a sequence.
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: false, Track("Body", ("hero.achx", "Walk", 0.4, 1)));
        await editor.OpenAsync(boss);

        Timeline(editor).Playhead.ShouldBe(0, 1e-6);
        SequencePreview(editor).Composite.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task ACutChain_SwitchesToTheNextChainAtItsStart()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        // Walk asks for 1 s (two cycles) but Run starts at 0.6 s on the same track.
        string boss = WriteFixture(editor, loop: false, Track("Body", ("hero.achx", "Walk", 0, 2), ("hero.achx", "Run", 0.6, 1)));
        await editor.OpenAsync(boss);

        editor.ClickAt(RulerAt(editor, 0.55));
        Shown(editor).ShouldBe(new[] { "Walk:0" });
        editor.ClickAt(RulerAt(editor, 0.65));
        Shown(editor).ShouldBe(new[] { "Run:0" });
    }

    [AvaloniaFact]
    public async Task Play_WithoutLoop_StopsAtTheEnd()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: false, Track("Body", ("hero.achx", "Run", 0, 1)));
        await editor.OpenAsync(boss);

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        IsPlaying(editor).ShouldBeTrue();

        (await editor.WaitUntilAsync(() => !IsPlaying(editor), TimeSpan.FromSeconds(3))).ShouldBeTrue();
        Timeline(editor).Playhead.ShouldBe(0.2, 1e-6);
        // The sequence stops at its end and holds its final pose (Run's last frame), not a blank preview.
        SequencePreview(editor).Composite!.Select(sprite => sprite.FrameIndex).ShouldBe(new[] { 1 });
    }

    [AvaloniaFact]
    public async Task Play_WithLoop_WrapsBackToTheStartAndKeepsPlaying()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: true, Track("Body", ("hero.achx", "Run", 0, 1)));
        await editor.OpenAsync(boss);

        editor.Click(editor.Control<Button>("SequencePlayButton"));
        (await editor.WaitUntilAsync(() => Timeline(editor).Playhead > 0.12, TimeSpan.FromSeconds(3))).ShouldBeTrue();
        (await editor.WaitUntilAsync(() => Timeline(editor).Playhead < 0.08, TimeSpan.FromSeconds(3))).ShouldBeTrue();

        IsPlaying(editor).ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task Stop_PausesAndRewindsToZero()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: true, Track("Body", ("hero.achx", "Walk", 0, 1)));
        await editor.OpenAsync(boss);
        editor.ClickAt(RulerAt(editor, 0.3));

        editor.Click(editor.Control<Button>("SequenceStopButton"));

        Timeline(editor).Playhead.ShouldBe(0);
        IsPlaying(editor).ShouldBeFalse();
        Shown(editor).ShouldBe(new[] { "Walk:0" });
    }

    [AvaloniaFact]
    public async Task Space_PlaysAndPausesTheSequence_AndStillPlaysThePreviewOnAnAchxTab()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: true, Track("Body", ("hero.achx", "Walk", 0, 1)));
        await editor.OpenAsync(Path.Combine(editor.ProjectFolder, "hero.achx"));
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Preview.IsPlaying.ShouldBeTrue();
        await editor.OpenAsync(boss);
        editor.Click(Timeline(editor));

        editor.Press(Key.Space);
        IsPlaying(editor).ShouldBeTrue();
        // The achx preview behind the tab is left alone.
        editor.Preview.IsPlaying.ShouldBeTrue();
        editor.Press(Key.Space);
        IsPlaying(editor).ShouldBeFalse();
        editor.Preview.IsPlaying.ShouldBeTrue();

        editor.ClickTab("hero.achx");
        editor.ClickRow(editor.ChainNamed("Walk"));
        editor.Press(Key.Space);
        editor.Preview.IsPlaying.ShouldBeFalse();
        editor.Press(Key.Space);
        editor.Preview.IsPlaying.ShouldBeTrue();
    }

    [AvaloniaFact]
    public async Task LoopToggle_SetsTheSequencesLoopFlagAsOneUndoStep()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: false, Track("Body", ("hero.achx", "Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var toggle = editor.Control<ToggleButton>("SequenceLoopToggle");
        toggle.IsChecked.ShouldBe(false);

        editor.Click(toggle);

        SequenceListSave.FromFile(boss).Sequences[0].Loop.ShouldBeTrue();
        editor.UndoLabels.ShouldBe(new[] { "Turn Loop On for 'Attack'" });
        editor.Click(Timeline(editor));
        editor.Press(Key.Z, RawInputModifiers.Control);
        SequenceListSave.FromFile(boss).Sequences[0].Loop.ShouldBeFalse();
        toggle.IsChecked.ShouldBe(false);
    }

    [AvaloniaFact]
    public async Task TimelineZoomControl_ZoomsTheTimeline()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        string boss = WriteFixture(editor, loop: false, Track("Body", ("hero.achx", "Walk", 0, 1)));
        await editor.OpenAsync(boss);
        var zoom = editor.Control<ZoomControl>("SequenceTimelineZoom");
        zoom.Text.ShouldBe("100%");

        zoom.StepUp();

        Timeline(editor).Viewport.PixelsPerSecond.ShouldBe(TimelineViewport.DefaultPixelsPerSecond * 1.5, 1e-9);
        zoom.Text.ShouldBe("150%");
    }

    [AvaloniaFact]
    public async Task ActiveChainShapes_DrawAtTheirTrack_MirroredOnAFlippedTrack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("hero.png", 64, 64);
        var guard = AnimationEditorHarness.Chain("Guard", "hero.png", (0, 0, 16, 16));
        var hitbox = new AARectSave { Name = "Hit", X = 5, Y = 2, ScaleX = 4, ScaleY = 3 };
        guard.Frames[0].ShapesSave = new ShapesSave();
        guard.Frames[0].ShapesSave!.Add(hitbox);
        editor.WriteAchx("hero.achx", guard);
        string boss = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var track = Track("Body", ("hero.achx", "Guard", 0, 1));
        track.X = 20;
        track.FlipHorizontal = true;
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        save.Sequences.Add(new SequenceSave { Name = "Attack", Tracks = { track } });
        save.Save(boss);

        await editor.OpenAsync(boss);

        var shape = SequencePreview(editor).GetShapeInfosForTest().Single();
        (shape.X, shape.Y, shape.Param1, shape.Param2).ShouldBe((15f, 2f, 4f, 3f));
    }
}
