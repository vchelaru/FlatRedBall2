using AnimationEditor.App.Controls;
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
/// A track has no offset, so the sequence preview only selects: a click on a sprite selects its track, and dragging it or
/// pressing an arrow key changes nothing. Sprites sit at their frames' offsets.
/// </summary>
public class SequenceTrackClickScenarioTests
{
    // Body (track 0, the top lane) uses Left, whose frame offset is (-20, 0); Arm (track 1) uses Right, at (+20, 0). At 200% they sit 80 px apart.
    private static async Task<(string Path, PreviewControl Preview)> OpenAsync(AnimationEditorHarness editor,
        Action<SequenceTrackSave, SequenceTrackSave>? shape = null, string armChain = "Right")
    {
        editor.WritePng("sheet.png", 64, 64);
        var left = AnimationEditorHarness.Chain("Left", "sheet.png", (0, 0, 16, 16));
        left.Frames[0].RelativeX = -20;
        var right = AnimationEditorHarness.Chain("Right", "sheet.png", (0, 0, 16, 16));
        right.Frames[0].RelativeX = 20;
        editor.WriteAchx("hero.achx", left, right);
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var body = Track("Body", "Left");
        var arm = Track("Arm", armChain);
        shape?.Invoke(body, arm);
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(body);
        sequence.Tracks.Add(arm);
        save.Sequences.Add(sequence);
        save.Save(path);
        await editor.OpenAsync(path);
        var preview = editor.Control<PreviewControl>("SequencePreview");
        preview.SetZoomPercent(200);
        editor.Layout();
        editor.WaitUntil(() => preview.TrackSpriteCenterForTest(0) is not null || preview.TrackSpriteCenterForTest(1) is not null, TimeSpan.FromSeconds(5));
        return (path, preview);
    }

    private static SequenceTrackSave Track(string name, string chain)
    {
        var track = new SequenceTrackSave { Name = name };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = chain, Start = 0, Loops = 1 });
        return track;
    }

    private static SequenceTimelineControl Timeline(AnimationEditorHarness editor) =>
        editor.Control<SequenceTimelineControl>("SequenceTimeline");

    private static SequenceInspectorControl Inspector(AnimationEditorHarness editor) =>
        editor.Control<SequenceInspectorControl>("SequenceInspector");

    private static Point Sprite(AnimationEditorHarness editor, PreviewControl preview, int track)
    {
        var center = preview.TrackSpriteCenterForTest(track) ?? throw new InvalidOperationException($"Track {track} is not drawn.");
        return editor.PointIn(preview, center.X, center.Y);
    }

    private static Point Empty(AnimationEditorHarness editor, PreviewControl preview) =>
        editor.PointIn(preview, preview.Bounds.Width - 40, preview.Bounds.Height - 40);

    private static Point OnHeader(AnimationEditorHarness editor, int lane)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(lane) + 10);
    }

    [AvaloniaFact]
    public async Task ClickingASprite_SelectsItsTrack_InTheTimelineAndTheInspector()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor);

        editor.ClickAt(Sprite(editor, preview, 1));

        Timeline(editor).SelectedTrack.ShouldBe(1);
        Inspector(editor).TrackNameBox.Text.ShouldBe("Arm");
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task APressOnEmptySpace_LeavesTheSelectionAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor);
        editor.ClickAt(OnHeader(editor, 1));

        editor.ClickAt(Empty(editor, preview));

        Timeline(editor).SelectedTrack.ShouldBe(1);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AClickOnOneSpriteOfAMultiSelection_NarrowsToThatTrack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor);
        editor.ClickAt(OnHeader(editor, 0));
        editor.ClickAt(OnHeader(editor, 1), RawInputModifiers.Control);

        editor.ClickAt(Sprite(editor, preview, 1));

        Timeline(editor).SelectedTracks.ShouldBe(new[] { 1 });
    }

    [AvaloniaFact]
    public async Task OverlappingSprites_TheClickSelectsTheTopLane()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor, armChain: "Left");

        editor.ClickAt(Sprite(editor, preview, 1));

        Timeline(editor).SelectedTrack.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task AHiddenTrack_IsNotHitTestable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor, (body, _) => body.Visible = false, armChain: "Left");

        editor.ClickAt(Sprite(editor, preview, 1));

        Timeline(editor).SelectedTrack.ShouldBe(1);
        preview.TrackSpriteCenterForTest(0).ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task AfterPanningTheView_ClickingASprite_StillSelectsIt()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor);
        preview.PanBy(30, -14);
        editor.Layout();

        editor.ClickAt(Sprite(editor, preview, 1));

        Timeline(editor).SelectedTrack.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task HoveringASprite_KeepsTheNormalCursor_NothingDragsHere()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor);
        var onSprite = preview.TrackSpriteCenterForTest(0)!.Value;

        preview.GetHoverCursorTypeForTest((float)onSprite.X, (float)onSprite.Y).ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task DraggingASprite_DoesNothing_NoMoveNoUndoEntryNoSave()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        string before = File.ReadAllText(path);
        var from = Sprite(editor, preview, 1);
        double centerBefore = preview.TrackSpriteCenterForTest(1)!.Value.X;

        editor.Drag(from, from + new Vector(40, 24));

        preview.TrackSpriteCenterForTest(1)!.Value.X.ShouldBe(centerBefore, 0.5);
        editor.UndoLabels.ShouldBeEmpty();
        File.ReadAllText(path).ShouldBe(before);
        Timeline(editor).SelectedTrack.ShouldBe(1);
    }

    [AvaloniaFact]
    public async Task ArrowKeys_DoNotMoveTracks_WhateverIsSelected()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        string before = File.ReadAllText(path);
        editor.ClickAt(Sprite(editor, preview, 1));
        double centerBefore = preview.TrackSpriteCenterForTest(1)!.Value.X;

        editor.Press(Key.Right);
        editor.Press(Key.Up, RawInputModifiers.Shift);
        editor.ClickAt(OnHeader(editor, 0));
        editor.Press(Key.Left);

        preview.TrackSpriteCenterForTest(1)!.Value.X.ShouldBe(centerBefore, 0.5);
        editor.UndoLabels.ShouldBeEmpty();
        File.ReadAllText(path).ShouldBe(before);
    }

    [AvaloniaFact]
    public async Task TheInspectorTrackPanel_ShowsNameVisibleAndFlipOnly()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        await OpenAsync(editor);
        editor.ClickAt(OnHeader(editor, 0));

        var inspector = Inspector(editor);
        inspector.TrackNameBox.IsEffectivelyVisible.ShouldBeTrue();
        inspector.TrackVisibleBox.IsEffectivelyVisible.ShouldBeTrue();
        inspector.TrackFlipBox.IsEffectivelyVisible.ShouldBeTrue();
        inspector.GetVisualDescendants().OfType<NumericUpDown>().Where(f => f.IsEffectivelyVisible).ShouldBeEmpty();
    }
}
