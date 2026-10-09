using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.VisualTree;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using Shouldly;

namespace AnimationEditor.App.Tests.Dogfood;

/// <summary>
/// Dragging a sprite in the preview when the selection mixes whole animations and individual
/// frames (a group preview where one track is playing and another is pinned to a frame). Every
/// selected, unlocked item moves together; locked ones stay put. Two animations, A on the left
/// (RelativeX -30) and B on the right (+30), so each sprite can be grabbed on its own.
/// </summary>
public class MixedSelectionPreviewDragScenarioTests
{
    private const float AX = -30f;
    private const float BX = 30f;
    // Raised so the sprites clear the group timeline, which docks over the preview's lower half.
    private const float Y = 40f;

    private static async Task<(AnimationEditorHarness Editor, AnimationChainSave A, AnimationChainSave B)> OpenTwoChainsAsync()
    {
        AnimationEditorHarness editor = new AnimationEditorHarness();
        editor.WritePng("sheet.png", 64, 64);
        string path = editor.WriteAchx("hero.achx",
            AnimationEditorHarness.Chain("A", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)),
            AnimationEditorHarness.Chain("B", "sheet.png", (0, 0, 16, 16), (16, 0, 16, 16), (32, 0, 16, 16)));
        await editor.OpenAsync(path);
        AnimationChainSave a = editor.ChainNamed("A");
        AnimationChainSave b = editor.ChainNamed("B");
        foreach (AnimationFrameSave frame in a.Frames) { frame.RelativeX = AX; frame.RelativeY = Y; }
        foreach (AnimationFrameSave frame in b.Frames) { frame.RelativeX = BX; frame.RelativeY = Y; }
        editor.ClickRow(a);
        editor.ClickRow(b, RawInputModifiers.Control);
        return (editor, a, b);
    }

    /// <summary>Clicks <paramref name="chain"/>'s group-timeline cell for <paramref name="frameIndex"/>, pinning that track to the frame.</summary>
    private static void PinTrackToFrame(AnimationEditorHarness editor, AnimationChainSave chain, int frameIndex)
    {
        editor.Layout();
        ItemsControl tracks = editor.Control<ItemsControl>("GroupTimelineTracks");
        ItemsControl list = tracks.GetVisualDescendants().OfType<ItemsControl>()
            .First(ic => ic.Name == "TrackFramesList" && ReferenceEquals(((ChainTimelineTrackVm)ic.DataContext!).Chain, chain));
        Grid cell = list.GetVisualDescendants().OfType<Grid>()
            .First(g => g.DataContext is TimelineFrameVm vm && vm.Index == frameIndex);
        editor.ClickAt(editor.PointIn(cell, cell.Bounds.Width / 2, cell.Bounds.Height / 2));
    }

    private static void Lock(AnimationEditorHarness editor, AnimationChainSave chain) =>
        editor.Click(editor.RowButton(chain, "Lock Animation"));

    private static void DragSprite(AnimationEditorHarness editor, float spriteX) =>
        editor.Drag(editor.PreviewPointAt(spriteX, Y), editor.PreviewPointAt(spriteX + 10, Y));

    private static float[] Xs(AnimationChainSave chain) => chain.Frames.Select(f => f.RelativeX).ToArray();

    [AvaloniaFact]
    public async Task LockedAnimationPinnedToAFrame_DoesNotStopTheUnlockedPlayingOneFromMoving()
    {
        var (editor, a, b) = await OpenTwoChainsAsync();
        using var _ = editor;
        Lock(editor, b);
        PinTrackToFrame(editor, b, 1);

        DragSprite(editor, AX);

        Xs(a).ShouldBe(new[] { AX + 10, AX + 10, AX + 10 });
        Xs(b).ShouldBe(new[] { BX, BX, BX });
    }

    [AvaloniaFact]
    public async Task UnlockedAnimationPinnedToAFrame_DoesNotStopTheOtherPlayingOneFromMoving()
    {
        var (editor, a, b) = await OpenTwoChainsAsync();
        using var _ = editor;
        PinTrackToFrame(editor, b, 1);

        DragSprite(editor, AX);

        Xs(a).ShouldBe(new[] { AX + 10, AX + 10, AX + 10 });
        // Only the pinned frame of B is selected, so only it moves with A.
        Xs(b).ShouldBe(new[] { BX, BX + 10, BX });
    }

    [AvaloniaFact]
    public async Task DraggingThePinnedUnlockedFrame_MovesItAndTheOtherSelectedAnimation()
    {
        var (editor, a, b) = await OpenTwoChainsAsync();
        using var _ = editor;
        PinTrackToFrame(editor, b, 1);

        DragSprite(editor, BX);

        Xs(a).ShouldBe(new[] { AX + 10, AX + 10, AX + 10 });
        Xs(b).ShouldBe(new[] { BX, BX + 10, BX });
        editor.UndoLabels.Count(label => label.Contains("Move")).ShouldBe(1, "one gesture is one undo step");
    }

    [AvaloniaFact]
    public async Task LockedPlayingAnimation_StaysPut_WhenTheUnlockedPinnedFrameIsDragged()
    {
        var (editor, a, b) = await OpenTwoChainsAsync();
        using var _ = editor;
        Lock(editor, a);
        PinTrackToFrame(editor, b, 1);

        DragSprite(editor, BX);

        Xs(a).ShouldBe(new[] { AX, AX, AX });
        Xs(b).ShouldBe(new[] { BX, BX + 10, BX });
    }

    [AvaloniaFact]
    public async Task EverySelectedItemLocked_NothingMoves_AndNothingIsRecorded()
    {
        var (editor, a, b) = await OpenTwoChainsAsync();
        using var _ = editor;
        Lock(editor, a);
        Lock(editor, b);
        PinTrackToFrame(editor, b, 1);
        int undoCount = editor.UndoLabels.Count;

        DragSprite(editor, AX);
        DragSprite(editor, BX);

        Xs(a).ShouldBe(new[] { AX, AX, AX });
        Xs(b).ShouldBe(new[] { BX, BX, BX });
        editor.UndoLabels.Count.ShouldBe(undoCount);
    }

    [AvaloniaFact]
    public async Task BothPinnedToFrames_WithOneLocked_MovesOnlyTheUnlockedFrame()
    {
        var (editor, a, b) = await OpenTwoChainsAsync();
        using var _ = editor;
        Lock(editor, b);
        PinTrackToFrame(editor, a, 0);
        PinTrackToFrame(editor, b, 1);

        DragSprite(editor, AX);

        Xs(a).ShouldBe(new[] { AX + 10, AX, AX });
        Xs(b).ShouldBe(new[] { BX, BX, BX });
    }

    [AvaloniaFact]
    public async Task FrameCtrlClickedInTheTree_WithAnotherAnimationSelected_StillDragsTheUnlockedAnimation()
    {
        var (editor, a, b) = await OpenTwoChainsAsync();
        using var _ = editor;
        Lock(editor, b);
        editor.Expand(b);
        editor.ClickRow(b.Frames[1], RawInputModifiers.Control);

        DragSprite(editor, AX);

        Xs(a).ShouldBe(new[] { AX + 10, AX + 10, AX + 10 });
        Xs(b).ShouldBe(new[] { BX, BX, BX });
    }

    [AvaloniaFact]
    public async Task HoverCursor_OverTheUnlockedSprite_ShowsMove_ButNotOverTheLockedOne()
    {
        var (editor, a, b) = await OpenTwoChainsAsync();
        using var _ = editor;
        Lock(editor, b);
        PinTrackToFrame(editor, b, 1);
        Point origin = editor.PointIn(editor.Preview, 0, 0);
        Point onA = editor.PreviewPointAt(AX, Y) - origin;
        Point onB = editor.PreviewPointAt(BX, Y) - origin;

        editor.Preview.GetHoverCursorTypeForTest((float)onA.X, (float)onA.Y).ShouldBe(StandardCursorType.SizeAll);
        editor.Preview.GetHoverCursorTypeForTest((float)onB.X, (float)onB.Y).ShouldBeNull();
    }
}
