using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for drag-to-offset a whole animation chain (issue #912): dragging the Preview panel's
/// currently-playing sprite when a chain is selected but no single frame is pinned shifts every
/// frame's <see cref="AnimationFrameSave.RelativeX"/>/<see cref="AnimationFrameSave.RelativeY"/> by
/// the same delta, preserving each frame's own starting offset. Mirrors
/// <see cref="PreviewFrameDragTests"/>: most cases use <see cref="PreviewControl.SimulateChainDrag"/>;
/// the routing test drives real pointer input to prove the whole-chain branch is actually reached.
/// </summary>
public class PreviewChainDragTests
{
    private static AnimationFrameSave MakeFrame(float relativeX, float relativeY)
    {
        return new AnimationFrameSave
        {
            FrameLength = 0.1f,
            RelativeX   = relativeX,
            RelativeY   = relativeY,
            ShapesSave  = new ShapesSave()
        };
    }

    // ── Whole-chain drag ──────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateChainDrag_ChainSelectedNoSingleFrame_ShiftsEveryFrameByDelta_PreservingOwnOffset()
    {
        var ctx    = TestHelpers.BuildServices();
        var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
        var frameB = MakeFrame(relativeX: 10f, relativeY: -5f);
        var chain  = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.SelectedState.SelectedChain = chain;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateChainDrag(3f, 2f);

        Assert.Equal(3f, frameA.RelativeX, precision: 3);
        Assert.Equal(2f, frameA.RelativeY, precision: 3);
        Assert.Equal(13f, frameB.RelativeX, precision: 3);
        Assert.Equal(-3f, frameB.RelativeY, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateChainDrag_AfterRelease_SingleUndoRestoresEveryFramesOwnOffset()
    {
        var ctx    = TestHelpers.BuildServices();
        var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
        var frameB = MakeFrame(relativeX: 10f, relativeY: -5f);
        var chain  = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.SelectedState.SelectedChain = chain;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateChainDrag(3f, 2f);

        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.Equal(0f, frameA.RelativeX, precision: 3);
        Assert.Equal(0f, frameA.RelativeY, precision: 3);
        Assert.Equal(10f, frameB.RelativeX, precision: 3);
        Assert.Equal(-5f, frameB.RelativeY, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateChainDrag_AfterUndo_RedoReappliesShiftToEveryFrame()
    {
        var ctx    = TestHelpers.BuildServices();
        var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
        var frameB = MakeFrame(relativeX: 10f, relativeY: -5f);
        var chain  = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.SelectedState.SelectedChain = chain;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateChainDrag(3f, 2f);
        ctx.UndoManager.Undo();
        ctx.UndoManager.Redo();

        Assert.Equal(3f, frameA.RelativeX, precision: 3);
        Assert.Equal(2f, frameA.RelativeY, precision: 3);
        Assert.Equal(13f, frameB.RelativeX, precision: 3);
        Assert.Equal(-3f, frameB.RelativeY, precision: 3);
    }

    // ── Gating: a single pinned frame must NOT trigger whole-chain drag ────────

    [AvaloniaFact]
    public void SimulateChainDrag_SingleFramePinned_IsNoOp()
    {
        var ctx    = TestHelpers.BuildServices();
        var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
        var frameB = MakeFrame(relativeX: 10f, relativeY: -5f);
        var chain  = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frameA;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateChainDrag(3f, 2f);

        Assert.Equal(0f, frameA.RelativeX, precision: 3);
        Assert.Equal(10f, frameB.RelativeX, precision: 3);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    [AvaloniaFact]
    public void SimulateChainDrag_NoChainSelected_IsNoOp()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateChainDrag(3f, 2f); // must not throw

        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── Priority / real pointer routing ─────────────────────────────────────

    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame            = null;
        ctx.SelectedState.SelectedNodes           = new List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        return ctx;
    }

    private static string WriteSolidPng(string dir, int size = 64, string name = "sprite.png")
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(SKColors.CornflowerBlue);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [AvaloniaFact]
    public void RealDrag_ChainSelectedNoSingleFrame_MovesEveryFrameTogether()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir);
            var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
            var frameB = MakeFrame(relativeX: 10f, relativeY: -5f);
            frameA.TextureName = texPath;
            frameB.TextureName = texPath;

            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frameA);
            chain.Frames.Add(frameB);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            // Pre-warm the bitmap cache the way a real render pass would, so the frame-drag
            // hit-test (which requires a cached bitmap) is actually eligible to fire.
            ctx.ThumbnailService.GetBitmap(texPath);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            float centerX = (float)((preview.Bounds.Width - 20) / 2 + 20);
            float centerY = (float)((preview.Bounds.Height - 20) / 2 + 20);
            var localPoint  = new Point(centerX, centerY); // frame 0 sits at world (0,0) == canvas center
            var windowPoint = preview.TranslatePoint(localPoint, window)!.Value;

            // Hover affordance before any drag starts, same SizeAll cursor the single-frame
            // path already shows over its draggable sprite (issue #912 follow-up).
            Assert.Equal(StandardCursorType.SizeAll, preview.GetHoverCursorTypeForTest(centerX, centerY));

            window.MouseDown(windowPoint, MouseButton.Left);
            var movedPoint = windowPoint + new Point(5, 5);
            window.MouseMove(movedPoint);
            Dispatcher.UIThread.RunJobs();

            // Still mid-drag (no mouse-up yet) — cursor stays SizeAll everywhere while dragging,
            // not just over the sprite footprint (queried well away from it, near the ruler
            // corner, so this actually exercises the drag-in-progress branch).
            Assert.Equal(StandardCursorType.SizeAll, preview.GetHoverCursorTypeForTest(22f, 22f));

            window.MouseUp(movedPoint, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(0f, frameA.RelativeX);
            Assert.NotEqual(10f, frameB.RelativeX); // the un-pinned second frame shifted too

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// The reported multi-chain gap (#1032 follow-up): chain A (locked) is the pinned
    /// SelectedChain, but chain B (unlocked) is also selected and shown at the same screen
    /// position (group preview, #576). Dragging at that shared position must move only B, not
    /// silently no-op just because the pinned chain happens to be locked.
    /// </summary>
    [AvaloniaFact]
    public void RealDrag_TwoChainsSelectedOverlapping_PinnedChainLocked_DragsOnlyUnlockedChain()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir);
            // RelativeY offsets both frames up off the canvas's vertical center: group-preview
            // mode overlays a taller scrub-track dock over the bottom of the Preview canvas
            // (RefreshTimelineStrip's GroupTimelineScrubHost), which would otherwise swallow a
            // click at world (0, 0).
            var lockedFrame = MakeFrame(relativeX: 0f, relativeY: 40f);
            lockedFrame.TextureName = texPath;
            var lockedChain = new AnimationChainSave { Name = "A", IsLocked = true };
            lockedChain.Frames.Add(lockedFrame);

            // Same RelativeX/Y as the locked chain's frame — their sprites coincide on screen.
            var unlockedFrame = MakeFrame(relativeX: 0f, relativeY: 40f);
            unlockedFrame.TextureName = texPath;
            var unlockedChain = new AnimationChainSave { Name = "B" };
            unlockedChain.Frames.Add(unlockedFrame);

            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(lockedChain);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(unlockedChain);
            ctx.ThumbnailService.GetBitmap(texPath);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            // Both chains multi-selected (group preview) with the locked chain pinned as
            // SelectedChain -- e.g. A was the last one clicked in the tree.
            ctx.SelectedState.SelectedNodes = new List<object> { unlockedChain, lockedChain };
            ctx.SelectedState.SelectedChain = lockedChain;
            Dispatcher.UIThread.RunJobs();

            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            float centerX = (float)((preview.Bounds.Width - 20) / 2 + 20);
            float centerY = (float)((preview.Bounds.Height - 20) / 2 + 20) - 40f;
            var localPoint  = new Point(centerX, centerY);
            var windowPoint = preview.TranslatePoint(localPoint, window)!.Value;

            Assert.Equal(Avalonia.Input.StandardCursorType.SizeAll, preview.GetHoverCursorTypeForTest(centerX, centerY));

            window.MouseDown(windowPoint, MouseButton.Left);
            var movedPoint = windowPoint + new Point(5, 5);
            window.MouseMove(movedPoint);
            window.MouseUp(movedPoint, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0f, lockedFrame.RelativeX, precision: 3);
            Assert.Equal(40f, lockedFrame.RelativeY, precision: 3);
            Assert.NotEqual(0f, unlockedFrame.RelativeX);
            Assert.NotEqual(40f, unlockedFrame.RelativeY);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
