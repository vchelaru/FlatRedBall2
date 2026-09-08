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
/// Tests for Shift-axis-locking a frame/animation offset drag (issue #1022): holding Shift
/// constrains the drag to whichever axis (horizontal or vertical) has the larger delta, same
/// convention as Photoshop/Figma constrained drags. The axis-lock math itself is covered by
/// <c>AxisLockTests</c> in AnimationEditor.Core.Tests; these tests prove it is actually wired
/// into the per-frame drag (<see cref="PreviewControl.SimulateFrameDrag"/>), the whole-chain
/// drag (<see cref="PreviewControl.SimulateChainDrag"/>), and the multi-frame drag
/// (<see cref="PreviewControl.SimulateMultiFrameDrag"/>), plus that live pointer input reads
/// the real Shift key state.
/// </summary>
public class PreviewAxisLockDragTests
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

    // ── Per-frame offset drag ──────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateFrameDrag_ShiftHeld_HorizontalDeltaLarger_LocksToHorizontal()
    {
        var ctx   = TestHelpers.BuildServices();
        var frame = MakeFrame();
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateFrameDrag(10f, 4f, shiftHeld: true);

        Assert.Equal(10f, frame.RelativeX, precision: 3);
        Assert.Equal(0f, frame.RelativeY, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateFrameDrag_ShiftHeld_VerticalDeltaLarger_LocksToVertical()
    {
        var ctx   = TestHelpers.BuildServices();
        var frame = MakeFrame();
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateFrameDrag(4f, 10f, shiftHeld: true);

        Assert.Equal(0f, frame.RelativeX, precision: 3);
        Assert.Equal(10f, frame.RelativeY, precision: 3);
    }

    [AvaloniaFact]
    public void SimulateFrameDrag_ShiftNotHeld_MovesFreely()
    {
        var ctx   = TestHelpers.BuildServices();
        var frame = MakeFrame();
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateFrameDrag(10f, 4f, shiftHeld: false);

        Assert.Equal(10f, frame.RelativeX, precision: 3);
        Assert.Equal(4f, frame.RelativeY, precision: 3);
    }

    // ── Whole-animation offset drag ────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateChainDrag_ShiftHeld_LocksEveryFrameToTheSameAxis()
    {
        var ctx    = TestHelpers.BuildServices();
        var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
        var frameB = MakeFrame(relativeX: 10f, relativeY: -5f);
        var chain  = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.SelectedState.SelectedChain = chain;

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateChainDrag(3f, 9f, shiftHeld: true);

        Assert.Equal(0f, frameA.RelativeX, precision: 3);
        Assert.Equal(9f, frameA.RelativeY, precision: 3);
        Assert.Equal(10f, frameB.RelativeX, precision: 3);
        Assert.Equal(4f, frameB.RelativeY, precision: 3);
    }

    // ── Multi-frame offset drag ─────────────────────────────────────────────────

    [AvaloniaFact]
    public void SimulateMultiFrameDrag_ShiftHeld_LocksSelectedFramesToTheSameAxis()
    {
        var ctx    = TestHelpers.BuildServices();
        var frameA = MakeFrame(relativeX: 0f, relativeY: 0f);
        var frameB = MakeFrame(relativeX: 10f, relativeY: -5f);
        var chain  = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedNodes = new List<object> { frameA, frameB };

        var ctrl = ctx.CreatePreviewControl();
        ctrl.SimulateMultiFrameDrag(9f, 3f, shiftHeld: true);

        Assert.Equal(9f, frameA.RelativeX, precision: 3);
        Assert.Equal(0f, frameA.RelativeY, precision: 3);
        Assert.Equal(19f, frameB.RelativeX, precision: 3);
        Assert.Equal(-5f, frameB.RelativeY, precision: 3);
    }

    // ── Real pointer routing: live Shift key actually reaches the drag ─────────

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
    public void RealDrag_ShiftHeldWithLargerHorizontalMovement_LocksVerticalToStartingOffset()
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

            window.MouseDown(windowPoint, MouseButton.Left);
            // Larger horizontal than vertical movement, with Shift held throughout.
            var movedPoint = windowPoint + new Point(20, 6);
            window.MouseMove(movedPoint, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();

            Assert.NotEqual(0f, frame.RelativeX);
            Assert.Equal(0f, frame.RelativeY, precision: 3);

            window.MouseUp(movedPoint, MouseButton.Left, RawInputModifiers.Shift);
            Dispatcher.UIThread.RunJobs();

            window.Close();
        }
        finally { Directory.Delete(dir, true); }
    }
}
