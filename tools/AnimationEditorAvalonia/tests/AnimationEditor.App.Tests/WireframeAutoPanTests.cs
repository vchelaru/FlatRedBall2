using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Integration tests for auto-pan while dragging past the wireframe viewport edge (#540).
/// The value under test is specifically that <see cref="WireframeControl.StepAutoPan"/>, called
/// repeatedly with the pointer held at a FIXED screen position past the edge, keeps moving the
/// camera tick over tick — a single <c>ApplyHandleDrag</c>/<c>ApplyChainDrag</c> call is a pure
/// function of (pointer, camera) and can't produce that on its own; only the timer-driven nudge
/// can, which is what proves auto-pan actually engages rather than just being a no-op wrapper.
/// </summary>
public class WireframeAutoPanTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        ctx.AppState.OffsetMultiplier             = 1f;
        return ctx;
    }

    private static string WriteSolidPng(string dir, SKColor color, int size, string name = "sprite.png")
    {
        var path = System.IO.Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// A 512×512 texture (bigger than the 400×300 viewport, so there's room to pan) with one
    /// full-UV frame selected, camera fixed at pan(0,0) zoom 1 so texture pixels == screen pixels.
    /// </summary>
    private static (WireframeControl ctrl, AnimationFrameSave frame, string dir)
        BuildCtrlWithSelectedFrame(TestServices ctx)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.DarkGray, size: 512);

        var frame = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0f, TopCoordinate    = 0f,
            RightCoordinate  = 1f, BottomCoordinate = 1f,
            ShapesSave = new ShapesSave(),
        };
        var chain = new AnimationChainSave { Name = "Test" };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();

        return (ctrl, frame, dir);
    }

    [AvaloniaFact]
    public void StepAutoPan_HandleDragHeldPastRightEdge_PansCameraLeftEachTick()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            // Grab the BotRight handle at its starting corner (512,512) and drag to (390,150) —
            // inside the 400-wide viewport but within the 32px right-edge margin (400-32=368).
            ctrl.SimulateHandleDragBegin(HandleKind.BotRight, startScreenX: 512f, startScreenY: 512f);
            ctrl.SimulateDragPointerMove(390f, 150f);

            float rightAfterMove = frame.RightCoordinate;
            float panXAfterMove = ctrl.CameraState.PanX;

            // Hold the pointer at the SAME screen position and step the auto-pan tick five times.
            float prevPanX = panXAfterMove;
            float prevRight = rightAfterMove;
            for (int i = 0; i < 5; i++)
            {
                ctrl.StepAutoPan(1f / 60f);

                float panX = ctrl.CameraState.PanX;
                float right = frame.RightCoordinate;

                // Camera keeps panning further left (more negative) tick over tick...
                Assert.True(panX < prevPanX, $"tick {i}: expected panX ({panX}) < previous ({prevPanX})");
                // ...and the dragged edge keeps tracking further right in texture space as a result —
                // impossible from a single ApplyHandleDrag call with an unchanged pointer position.
                Assert.True(right > prevRight, $"tick {i}: expected right UV ({right}) > previous ({prevRight})");

                prevPanX = panX;
                prevRight = right;
            }

            ctrl.SimulateDragEnd();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void StepAutoPan_ChainDragHeldPastBottomEdge_PansCameraUpEachTick()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            // Grab the chain at its top-left origin and drag down to (150, 290) — inside the
            // 300-tall viewport but within the 32px bottom-edge margin (300-32=268).
            ctrl.SimulateChainDragBegin(startScreenX: 0f, startScreenY: 0f);
            ctrl.SimulateDragPointerMove(150f, 290f);

            float topAfterMove = frame.TopCoordinate;
            float panYAfterMove = ctrl.CameraState.PanY;

            float prevPanY = panYAfterMove;
            float prevTop = topAfterMove;
            for (int i = 0; i < 5; i++)
            {
                ctrl.StepAutoPan(1f / 60f);

                float panY = ctrl.CameraState.PanY;
                float top = frame.TopCoordinate;

                Assert.True(panY < prevPanY, $"tick {i}: expected panY ({panY}) < previous ({prevPanY})");
                Assert.True(top > prevTop, $"tick {i}: expected top UV ({top}) > previous ({prevTop})");

                prevPanY = panY;
                prevTop = top;
            }

            ctrl.SimulateDragEnd();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void StepAutoPan_PointerWellInsideViewport_DoesNotMoveCamera()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            ctrl.SimulateHandleDragBegin(HandleKind.BotRight, startScreenX: 512f, startScreenY: 512f);
            ctrl.SimulateDragPointerMove(200f, 150f); // dead centre of the 400×300 viewport

            var (panX0, panY0, _) = ctrl.CameraState;
            ctrl.StepAutoPan(1f / 60f);
            var (panX1, panY1, _) = ctrl.CameraState;

            Assert.Equal(panX0, panX1, 4);
            Assert.Equal(panY0, panY1, 4);

            ctrl.SimulateDragEnd();
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void StepAutoPan_NoActiveDrag_DoesNothing()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            var (panX0, panY0, _) = ctrl.CameraState;
            ctrl.StepAutoPan(1f / 60f); // no drag in progress — must no-op even at the edge
            var (panX1, panY1, _) = ctrl.CameraState;

            Assert.Equal(panX0, panX1, 4);
            Assert.Equal(panY0, panY1, 4);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
