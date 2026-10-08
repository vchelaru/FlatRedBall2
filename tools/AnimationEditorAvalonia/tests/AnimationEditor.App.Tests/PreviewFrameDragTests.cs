using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
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
/// Tests for drag-to-reposition the previewed sprite (writes
/// <see cref="AnimationFrameSave.RelativeX"/>/<see cref="AnimationFrameSave.RelativeY"/>) in the
/// PreviewControl. Mirrors <see cref="PreviewShapeDragTests"/>: most cases use
/// <see cref="PreviewControl.SimulateFrameDrag"/>, which applies a world-space delta and commits
/// exactly as the live pointer path does. The priority test drives real pointer input through a
/// full window, since it verifies OnPointerPressed's branch order, not just the drag math.
/// </summary>
public class PreviewFrameDragTests
{
    private static AnimationFrameSave MakeFrame(float relativeX = 0f, float relativeY = 0f)
    {
        return new AnimationFrameSave
        {
            FrameLength = 0.1f,
            RelativeX   = relativeX,
            RelativeY   = relativeY,
            ShapesSave  = new ShapesSave()
        };
    }

    // ── Single-frame drag ─────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateFrameDrag_SingleFrameSelected_UpdatesAndSnapsRelativeXY()
    {
        var ctx   = TestHelpers.BuildServices();
        var frame = MakeFrame();
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateFrameDrag(10.6f, -4.2f);

        Assert.Equal(11f, frame.RelativeX, precision: 3);
        Assert.Equal(-4f, frame.RelativeY, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateFrameDrag_AfterRelease_UndoRestoresPosition()
    {
        var ctx   = TestHelpers.BuildServices();
        var frame = MakeFrame(relativeX: 2f, relativeY: 3f);
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateFrameDrag(5f, 5f);

        Assert.Equal(7f, frame.RelativeX, precision: 3);
        Assert.True(ctx.UndoManager.CanUndo);

        ctx.UndoManager.Undo();

        Assert.Equal(2f, frame.RelativeX, precision: 3);
        Assert.Equal(3f, frame.RelativeY, precision: 3);
    }

    // ── Multi-select gating ────────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateFrameDrag_MultipleFramesSelected_IsNoOp()
    {
        var ctx    = TestHelpers.BuildServices();
        var frame  = MakeFrame();
        var other  = MakeFrame();
        ctx.SelectedState.SelectedFrame = frame;
        ctx.SelectedState.SelectedNodes = new List<object> { frame, other };

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateFrameDrag(10f, 10f);

        Assert.Equal(0f, frame.RelativeX, precision: 3);
        Assert.Equal(0f, frame.RelativeY, precision: 3);
        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── No-op when no frame ───────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateFrameDrag_NoFrameSelected_IsNoOp()
    {
        var ctx  = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateFrameDrag(10f, 10f); // must not throw

        Assert.False(ctx.UndoManager.CanUndo);
    }

    // ── Priority: an existing shape must still win over frame-drag ─────────────

    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
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
    public void RealDrag_OnExistingShape_MovesShapeNotFrame()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir);
            var circle  = new CircleSave { X = 0f, Y = 0f, Radius = 10f };
            var frame   = MakeFrame();
            frame.TextureName = texPath;
            frame.ShapesSave!.Add(circle);

            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain  = chain;
            ctx.SelectedState.SelectedFrame  = frame;
            ctx.SelectedState.SelectedCircle = circle;
            // Pre-warm the bitmap cache the way a real render pass would, so the frame-drag
            // hit-test (which requires a cached bitmap) is actually eligible to fire.
            ctx.ThumbnailService.GetBitmap(texPath);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            float centerX = (float)((preview.Bounds.Width - 20) / 2 + 20);
            float centerY = (float)((preview.Bounds.Height - 20) / 2 + 20);
            var localPoint  = new Point(centerX, centerY); // circle sits at world (0,0) == canvas center
            var windowPoint = preview.TranslatePoint(localPoint, window)!.Value;

            window.MouseDown(windowPoint, MouseButton.Left);
            var movedPoint = windowPoint + new Point(5, 5);
            window.MouseMove(movedPoint);
            window.MouseUp(movedPoint, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(0f, circle.X);
            Assert.Equal(0f, frame.RelativeX, precision: 3);
            Assert.Equal(0f, frame.RelativeY, precision: 3);

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Live update while dragging (not just on release) ───────────────────────

    [AvaloniaFact]
    public void RealDrag_OnFrameSprite_FiresFrameLiveUpdatedBeforeRelease()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var texPath = WriteSolidPng(dir);
            var frame   = MakeFrame();
            frame.TextureName = texPath;

            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;
            // Pre-warm the bitmap cache the way a real render pass would, so the frame-drag
            // hit-test (which requires a cached bitmap) is actually eligible to fire.
            ctx.ThumbnailService.GetBitmap(texPath);

            var window = ctx.CreateMainWindow();
            window.Show();
            Dispatcher.UIThread.RunJobs();

            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            float centerX = (float)((preview.Bounds.Width - 20) / 2 + 20);
            float centerY = (float)((preview.Bounds.Height - 20) / 2 + 20);
            var localPoint  = new Point(centerX, centerY); // frame sits at world (0,0) == canvas center
            var windowPoint = preview.TranslatePoint(localPoint, window)!.Value;

            AnimationFrameSave? liveUpdatedFrame = null;
            preview.FrameLiveUpdated += f => liveUpdatedFrame = f;

            window.MouseDown(windowPoint, MouseButton.Left);
            var movedPoint = windowPoint + new Point(5, 5);
            window.MouseMove(movedPoint);
            Dispatcher.UIThread.RunJobs();

            // Still mid-drag (no mouse-up yet) — the live event, and the property-panel field it
            // drives, must already reflect the move; not just after release (#900 follow-up).
            Assert.Same(frame, liveUpdatedFrame);
            Assert.NotEqual(0f, frame.RelativeX);

            var propRelX = window.FindControl<NumericUpDown>("PropRelX")!;
            Assert.Equal((decimal)frame.RelativeX, propRelX.Value);

            window.MouseUp(movedPoint, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
