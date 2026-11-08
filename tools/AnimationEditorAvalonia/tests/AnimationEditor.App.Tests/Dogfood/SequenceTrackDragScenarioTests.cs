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
/// Placing a track from the sequence preview: pressing its sprite selects it and dragging moves its offset live, as one undo
/// entry, with Escape cancelling; the arrow keys nudge the selected tracks, a held key being one undo entry.
/// </summary>
public class SequenceTrackDragScenarioTests
{
    // Body (track 0, the top lane) at (-20, 0) and Arm (track 1) at (20, 0), both a 16 by 16 Walk frame. At 200% they sit 80 px apart.
    private static async Task<(string Path, PreviewControl Preview)> OpenAsync(AnimationEditorHarness editor,
        Action<SequenceTrackSave, SequenceTrackSave>? shape = null, int zoomPercent = 200)
    {
        editor.WritePng("sheet.png", 64, 64);
        editor.WriteAchx("hero.achx", AnimationEditorHarness.Chain("Walk", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16)));
        string path = Path.Combine(editor.ProjectFolder, "boss.seqj");
        var body = Track("Body", -20, 0);
        var arm = Track("Arm", 20, 0);
        shape?.Invoke(body, arm);
        var save = new SequenceListSave { Sources = { "hero.achx" } };
        var sequence = new SequenceSave { Name = "Attack" };
        sequence.Tracks.Add(body);
        sequence.Tracks.Add(arm);
        save.Sequences.Add(sequence);
        save.Save(path);
        await editor.OpenAsync(path);
        var preview = editor.Control<PreviewControl>("SequencePreview");
        preview.SetZoomPercent(zoomPercent);
        editor.Layout();
        editor.WaitUntil(() => preview.TrackSpriteCenterForTest(0) is not null || preview.TrackSpriteCenterForTest(1) is not null, TimeSpan.FromSeconds(5));
        return (path, preview);
    }

    private static SequenceTrackSave Track(string name, float x, float y)
    {
        var track = new SequenceTrackSave { Name = name, X = x, Y = y };
        track.Chains.Add(new SequenceChainReferenceSave { Source = "hero.achx", Chain = "Walk", Start = 0, Loops = 1 });
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

    private static (float X, float Y) Offset(string path, int track)
    {
        var saved = SequenceListSave.FromFile(path).Sequences[0].Tracks[track];
        return (saved.X, saved.Y);
    }

    private static Point OnHeader(AnimationEditorHarness editor, int lane)
    {
        var timeline = Timeline(editor);
        return editor.PointIn(timeline, 20, timeline.Viewport.LaneTop(lane) + 10);
    }

    [AvaloniaFact]
    public async Task DraggingASprite_MovesItsTrack_LiveInTheInspector_AndIsOneUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        var from = Sprite(editor, preview, 0);
        double centerBefore = preview.TrackSpriteCenterForTest(0)!.Value.X;

        // 20 px right and 12 px down at 200%: 10 units right, 6 down (Y is up).
        editor.DragHold(from, from + new Vector(20, 12));

        Timeline(editor).SelectedTrack.ShouldBe(0);
        Inspector(editor).TrackXField.Value.ShouldBe(-10m);
        Inspector(editor).TrackYField.Value.ShouldBe(-6m);
        Offset(path, 0).ShouldBe((-20f, 0f));   // nothing is written until the button comes up
        editor.UndoLabels.ShouldBeEmpty();

        editor.DragRelease(from + new Vector(20, 12));

        editor.UndoLabels.ShouldBe(new[] { "Move Track 'Body'" });
        Offset(path, 0).ShouldBe((-10f, -6f));
        Offset(path, 1).ShouldBe((20f, 0f));
        preview.TrackSpriteCenterForTest(0)!.Value.X.ShouldBe(centerBefore + 20, 0.5);
        editor.Press(Key.Z, RawInputModifiers.Control);
        Offset(path, 0).ShouldBe((-20f, 0f));
    }

    [AvaloniaFact]
    public async Task TheDragFollowsTheZoom_AWorldUnitIsMoreScreenPixelsWhenZoomedIn()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor, zoomPercent: 50);
        var from = Sprite(editor, preview, 0);

        editor.Drag(from, from + new Vector(10, 0));

        // 10 px at 50% is 20 units.
        Offset(path, 0).ShouldBe((0f, 0f));
    }

    [AvaloniaFact]
    public async Task APressThatNeverTravelsThreePixels_OnlySelectsTheTrack()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        var from = Sprite(editor, preview, 1);

        editor.Drag(from, from + new Vector(2, 2));

        Timeline(editor).SelectedTrack.ShouldBe(1);
        editor.UndoLabels.ShouldBeEmpty();
        Offset(path, 1).ShouldBe((20f, 0f));
    }

    [AvaloniaFact]
    public async Task APressOnEmptySpace_LeavesTheSelectionAlone()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor);
        editor.ClickAt(OnHeader(editor, 1));
        Timeline(editor).SelectedTrack.ShouldBe(1);

        editor.Drag(Empty(editor, preview), Empty(editor, preview) + new Vector(-30, -20));

        Timeline(editor).SelectedTrack.ShouldBe(1);
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task Escape_MidDrag_PutsTheTrackBack_WithNoUndoEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        var from = Sprite(editor, preview, 1);
        editor.DragHold(from, from + new Vector(30, 30));
        Inspector(editor).TrackXField.Value.ShouldBe(35m);

        editor.Press(Key.Escape);
        editor.DragRelease(from + new Vector(30, 30));

        Inspector(editor).TrackXField.Value.ShouldBe(20m);
        Offset(path, 1).ShouldBe((20f, 0f));
        editor.UndoLabels.ShouldBeEmpty();
    }

    [AvaloniaFact]
    public async Task AFlippedTrack_FollowsThePointerExactlyLikeAnUnflippedOne()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor, (_, arm) => arm.FlipHorizontal = true);
        var from = Sprite(editor, preview, 1);

        editor.Drag(from, from + new Vector(20, -8));

        Offset(path, 1).ShouldBe((30f, 4f));
        preview.TrackSpriteCenterForTest(1)!.Value.X.ShouldBe(preview.TrackSpriteCenterForTest(0)!.Value.X + 80 + 20, 0.5);
    }

    [AvaloniaFact]
    public async Task SeveralSelectedTracks_MoveTogetherByTheSameDelta_AsOneEntry()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        editor.ClickAt(OnHeader(editor, 0));
        editor.ClickAt(OnHeader(editor, 1), RawInputModifiers.Control);
        Timeline(editor).SelectedTracks.ShouldBe(new[] { 0, 1 });
        var from = Sprite(editor, preview, 1);

        editor.Drag(from, from + new Vector(-16, 10));

        Timeline(editor).SelectedTracks.ShouldBe(new[] { 0, 1 });
        Offset(path, 0).ShouldBe((-28f, -5f));
        Offset(path, 1).ShouldBe((12f, -5f));
        editor.UndoLabels.ShouldBe(new[] { "Move 2 tracks" });
        editor.Press(Key.Z, RawInputModifiers.Control);
        (Offset(path, 0), Offset(path, 1)).ShouldBe(((-20f, 0f), (20f, 0f)));
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
    public async Task ShiftLocksTheDragToTheLargerAxis()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        var from = Sprite(editor, preview, 1);

        editor.Drag(from, from + new Vector(30, 8), RawInputModifiers.Shift);

        Offset(path, 1).ShouldBe((35f, 0f));
    }

    [AvaloniaFact]
    public async Task OverlappingSprites_ThePressGrabsTheTopLane()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor, (_, arm) => arm.X = -20);

        editor.ClickAt(Sprite(editor, preview, 1));

        Timeline(editor).SelectedTrack.ShouldBe(0);
    }

    [AvaloniaFact]
    public async Task AHiddenTrack_IsNotHitTestable()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor, (body, arm) =>
        {
            arm.X = -20;
            body.Visible = false;
        });

        editor.ClickAt(Sprite(editor, preview, 1));

        Timeline(editor).SelectedTrack.ShouldBe(1);
        preview.TrackSpriteCenterForTest(0).ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task HoveringASprite_ShowsTheMoveCursor_AndEmptySpaceDoesNot()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (_, preview) = await OpenAsync(editor);
        var onSprite = preview.TrackSpriteCenterForTest(0)!.Value;

        preview.GetHoverCursorTypeForTest((float)onSprite.X, (float)onSprite.Y).ShouldBe(StandardCursorType.SizeAll);
        preview.GetHoverCursorTypeForTest((float)preview.Bounds.Width - 40, (float)preview.Bounds.Height - 40).ShouldBeNull();
    }

    [AvaloniaFact]
    public async Task DraggingAfterPanningTheView_StillGrabsTheSprite()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        preview.PanBy(30, -14);
        editor.Layout();
        var from = Sprite(editor, preview, 1);

        editor.Drag(from, from + new Vector(10, 0));

        Offset(path, 1).ShouldBe((25f, 0f));
    }

    // ── Arrow keys ───────────────────────────────────────────────────────────

    [AvaloniaFact]
    public async Task ArrowKeys_NudgeTheSelectedTrack_OneUnit_TenWithShift_UpIsPositiveY()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        editor.ClickAt(Sprite(editor, preview, 1));   // selects the track and focuses the preview

        editor.Press(Key.Right);
        Offset(path, 1).ShouldBe((21f, 0f));
        editor.Press(Key.Up);
        Offset(path, 1).ShouldBe((21f, 1f));
        editor.Press(Key.Left, RawInputModifiers.Shift);
        Offset(path, 1).ShouldBe((11f, 1f));
        editor.Press(Key.Down, RawInputModifiers.Shift);
        Offset(path, 1).ShouldBe((11f, -9f));
        Inspector(editor).TrackXField.Value.ShouldBe(11m);
    }

    [AvaloniaFact]
    public async Task HoldingAnArrow_IsOneUndoEntry_AndSeparatePressesAreSeparateEntries()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        editor.ClickAt(Sprite(editor, preview, 1));

        editor.HoldKey(Key.Right, 40);
        editor.UndoLabels.ShouldBe(new[] { "Move Track 'Arm'" });
        Offset(path, 1).ShouldBe((60f, 0f));

        editor.Press(Key.Right);
        editor.Press(Key.Right);
        editor.UndoLabels.Count.ShouldBe(3);

        editor.Press(Key.Z, RawInputModifiers.Control);
        Offset(path, 1).ShouldBe((61f, 0f));
        editor.Press(Key.Z, RawInputModifiers.Control);
        editor.Press(Key.Z, RawInputModifiers.Control);
        Offset(path, 1).ShouldBe((20f, 0f));
    }

    [AvaloniaFact]
    public async Task ArrowKeys_OnATrackSelectedOnTheTimeline_NudgeAllSelectedTracks()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, _) = await OpenAsync(editor);
        editor.ClickAt(OnHeader(editor, 0));
        editor.ClickAt(OnHeader(editor, 1), RawInputModifiers.Control);

        editor.Press(Key.Right);

        Offset(path, 0).ShouldBe((-19f, 0f));
        Offset(path, 1).ShouldBe((21f, 0f));
        editor.UndoLabels.ShouldBe(new[] { "Move 2 tracks" });
    }

    [AvaloniaFact]
    public async Task ArrowKeys_WithNoTrackSelected_DoNothing()
    {
        using AnimationEditorHarness editor = new AnimationEditorHarness();
        var (path, preview) = await OpenAsync(editor);
        editor.ClickAt(Empty(editor, preview));

        editor.Press(Key.Right);

        Offset(path, 0).ShouldBe((-20f, 0f));
        editor.UndoLabels.ShouldBeEmpty();
    }
}
