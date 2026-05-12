using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Integration tests for the wireframe handle-drag workflow:
/// <see cref="WireframeControl.SimulateHandleDrag"/> drives the same
/// <c>ApplyHandleDrag</c> path as pointer events and writes UV coords
/// back to the <see cref="AnimationFrameSave"/>.
///
/// Tutorial doc step: "You will now see a white square with 8 circle handles.
/// You can push on the circles and drag to resize the frame." and
/// "You can move the mouse over the region … Push the mouse button to move the frame."
///
/// Camera fixed at pan=(0,0) zoom=1 so texture pixels == screen pixels.
/// Texture size: 64 × 64. Full-texture frame UV: 0→1 on both axes.
/// Pixel bounds at camera(0,0,1): left=0, top=0, right=64, bottom=64.
/// </summary>
public class WireframeHandleDragTests
{
    private static TestServices ResetSingletons() {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread              = a => a();
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        ctx.AppState.OffsetMultiplier             = 1f;
        return ctx;
    }

    private static string WriteSolidPng(string dir, SKColor color, int size = 64,
                                         string name = "sprite.png")
    {
        var path = System.IO.Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// Builds a WireframeControl with a 64×64 texture, a full-UV frame selected,
    /// and camera at (0,0,1) so screen ≡ texture coords.
    ///
    /// Uses a relative <c>TextureName</c> ("sprite.png") so that the filter in
    /// <c>RefreshFramesInternal</c> (achxFolder + TextureName == loadedTexturePath)
    /// passes correctly.  Returns (ctrl, frame, dir).
    /// </summary>
    private static (WireframeControl ctrl, AnimationFrameSave frame, string dir) BuildCtrlWithSelectedFrame(TestServices ctx)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.DarkGray, name: "sprite.png");

        var frame = new AnimationFrameSave
        {
            TextureName      = "sprite.png",   // relative — achxFolder + "sprite.png" == png
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
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();

        return (ctrl, frame, dir);
    }

    // ── TopLeft handle resize ─────────────────────────────────────────────────

    /// <summary>
    /// Drag the TopLeft handle from screen (0,0) to (8,8).
    /// At pan=(0,0) zoom=1 the top-left corner maps to texture (0,0).
    /// After drag: texture left = 8, top = 8  →  UV left = 8/64 = 0.125, top = 0.125.
    /// Right and bottom UV must remain 1.0 (unchanged).
    /// </summary>
    [AvaloniaFact]
    public void HandleDrag_TopLeft_MovedInward8px_UpdatesLeftAndTopUV()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            ctrl.SimulateHandleDrag(HandleKind.TopLeft,
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   8f, endScreenY:   8f);

            Assert.Equal(0.125f, frame.LeftCoordinate,   precision: 4);
            Assert.Equal(0.125f, frame.TopCoordinate,    precision: 4);
            Assert.Equal(1f,     frame.RightCoordinate,  precision: 4);
            Assert.Equal(1f,     frame.BottomCoordinate, precision: 4);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── BotRight handle resize ────────────────────────────────────────────────

    /// <summary>
    /// Drag the BotRight handle from screen (64,64) to (56,56).
    /// After drag: texture right = 56, bottom = 56  →  UV right = 56/64 = 0.875, bottom = 0.875.
    /// Left and top UV must remain 0.
    /// </summary>
    [AvaloniaFact]
    public void HandleDrag_BotRight_MovedInward8px_UpdatesRightAndBottomUV()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            ctrl.SimulateHandleDrag(HandleKind.BotRight,
                startScreenX: 64f, startScreenY: 64f,
                endScreenX:   56f, endScreenY:   56f);

            Assert.Equal(0f,     frame.LeftCoordinate,   precision: 4);
            Assert.Equal(0f,     frame.TopCoordinate,    precision: 4);
            Assert.Equal(0.875f, frame.RightCoordinate,  precision: 4);
            Assert.Equal(0.875f, frame.BottomCoordinate, precision: 4);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── TopCenter handle resize ───────────────────────────────────────────────

    /// <summary>
    /// Drag the TopCenter handle down by 16 px.
    /// Only the top coordinate changes; left/right/bottom are unaffected.
    /// </summary>
    [AvaloniaFact]
    public void HandleDrag_TopCenter_MovedDown16px_OnlyTopUVChanges()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            ctrl.SimulateHandleDrag(HandleKind.TopCenter,
                startScreenX: 32f, startScreenY: 0f,
                endScreenX:   32f, endScreenY:   16f);

            Assert.Equal(0.25f, frame.TopCoordinate,    precision: 4);   // 16/64
            Assert.Equal(0f,    frame.LeftCoordinate,   precision: 4);
            Assert.Equal(1f,    frame.RightCoordinate,  precision: 4);
            Assert.Equal(1f,    frame.BottomCoordinate, precision: 4);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Move handle ───────────────────────────────────────────────────────────

    /// <summary>
    /// Drag the Move handle 8 px right and 8 px down.
    ///
    /// Uses a 32×32-pixel frame (UV 0.25→0.75) in the centre of a 64×64 texture
    /// so there is room to translate.
    ///
    /// Before: pixel bounds (16,16,48,48). After +8px: (24,24,56,56).
    /// UV after: left=24/64=0.375, right=56/64=0.875.
    /// Size (0.5) must be preserved; frame must have moved right (left > 0.25).
    /// </summary>
    [AvaloniaFact]
    public void HandleDrag_Move_Translate8px_PreservesFrameSize()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, dir) = BuildCtrlWithSelectedFrame(ctx);   // full-UV frame from helper (will replace)
        try
        {
            // Replace the frame with a 32×32 centre frame so there's room to Move
            var chain = ctx.SelectedState.SelectedChain!;
            chain.Frames.Clear();
            var frame = new AnimationFrameSave
            {
                TextureName      = "sprite.png",
                FrameLength      = 0.1f,
                LeftCoordinate   = 0.25f, TopCoordinate    = 0.25f,
                RightCoordinate  = 0.75f, BottomCoordinate = 0.75f,
                ShapesSave = new ShapesSave(),
            };
            chain.Frames.Add(frame);
            ctx.SelectedState.SelectedFrame = frame;
            ctrl.RefreshFrames();

            float preDx = frame.RightCoordinate  - frame.LeftCoordinate;  // 0.5
            float preDy = frame.BottomCoordinate - frame.TopCoordinate;   // 0.5

            // Centre of frame at screen (32,32); drag +8px right, +8px down
            ctrl.SimulateHandleDrag(HandleKind.Move,
                startScreenX: 32f, startScreenY: 32f,
                endScreenX:   40f, endScreenY:   40f);

            float postDx = frame.RightCoordinate  - frame.LeftCoordinate;
            float postDy = frame.BottomCoordinate - frame.TopCoordinate;

            // Width and height in UV space must remain the same after a pure translation
            Assert.Equal(preDx, postDx, precision: 3);
            Assert.Equal(preDy, postDy, precision: 3);

            // Frame must have moved right (left increased from 0.25)
            Assert.True(frame.LeftCoordinate > 0.25f,
                $"Frame should have moved right; LeftCoordinate={frame.LeftCoordinate:F4}");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── FrameRegionChanged fires ──────────────────────────────────────────────

    /// <summary>
    /// SimulateHandleDrag must fire the <see cref="WireframeControl.FrameRegionChanged"/>
    /// event with the mutated frame after the drag completes.
    /// This is the event that MainWindow uses to refresh the tree view and raise
    /// AnimationChainsChanged (which updates PreviewControl).
    /// </summary>
    [AvaloniaFact]
    public void HandleDrag_TopLeft_FiresFrameRegionChangedWithCorrectFrame()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            AnimationFrameSave? notifiedFrame = null;
            ctrl.FrameRegionChanged += f => notifiedFrame = f;

            ctrl.SimulateHandleDrag(HandleKind.TopLeft,
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   4f, endScreenY:   4f);

            Assert.NotNull(notifiedFrame);
            Assert.Same(frame, notifiedFrame);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Handle drag updates rendered wireframe bitmap ─────────────────────────

    /// <summary>
    /// After dragging the TopLeft handle inward, the wireframe render must change:
    /// the frame border rect shrinks, so the dark background is now visible in the
    /// top-left region that the handle was dragged to.
    /// </summary>
    [AvaloniaFact]
    public void HandleDrag_TopLeft_RenderedBitmapChangesAfterDrag()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            using var beforeDrag = ctrl.RenderToBitmap(64, 64);

            ctrl.SimulateHandleDrag(HandleKind.TopLeft,
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:  16f, endScreenY:  16f);

            using var afterDrag = ctrl.RenderToBitmap(64, 64);

            bool anyDiff = false;
            for (int x = 0; x < 64 && !anyDiff; x++)
                for (int y = 0; y < 64 && !anyDiff; y++)
                    anyDiff = beforeDrag.GetPixel(x, y) != afterDrag.GetPixel(x, y);

            Assert.True(anyDiff, "Wireframe render should change after dragging a handle");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── No-op when no frame selected ─────────────────────────────────────────

    /// <summary>
    /// SimulateHandleDrag must be a safe no-op when no frame is selected.
    /// </summary>
    [AvaloniaFact]
    public void HandleDrag_NoFrameSelected_NoException()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black);
        try
        {
            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.SetCamera(0f, 0f, 1f);
            // No frame selected → should not throw
            ctrl.SimulateHandleDrag(HandleKind.TopLeft, 0f, 0f, 8f, 8f);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
