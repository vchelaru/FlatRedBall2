using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.Linq;
using Xunit;
using FilePath = AnimationEditor.Core.Paths.FilePath;

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

    // ── No undo entry when click has no movement ──────────────────────────────

    /// <summary>
    /// Releasing a handle without dragging (start == end) must not push an undo
    /// entry, because no frame coordinates changed.
    /// Regression guard for: https://github.com/vchelaru/FlatRedBall2/issues/362
    /// </summary>
    [AvaloniaFact]
    public void HandleDrag_NoMovement_DoesNotRecordUndo()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            ctrl.SimulateHandleDrag(HandleKind.TopLeft,
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   0f, endScreenY:   0f);

            Assert.False(ctx.UndoManager.CanUndo,
                "Clicking a handle without dragging must not create an undo entry");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Same as <see cref="HandleDrag_NoMovement_DoesNotRecordUndo"/> for the bulk
    /// (multi-chain) handle-drag path.
    /// </summary>
    [AvaloniaFact]
    public void BulkHandleDrag_NoMovement_DoesNotRecordUndo()
    {
        var ctx = ResetSingletons();
        var (ctrl, frame, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            // Add a second chain so bulk mode is active
            var chain2 = new AnimationChainSave { Name = "Walk2" };
            var frame2 = new AnimationFrameSave
            {
                TextureName      = "sprite.png",
                FrameLength      = 0.1f,
                LeftCoordinate   = 0.5f, TopCoordinate    = 0f,
                RightCoordinate  = 1.0f, BottomCoordinate = 0.5f,
                ShapesSave       = new ShapesSave(),
            };
            chain2.Frames.Add(frame2);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain2);
            ctx.SelectedState.SelectedNodes = new System.Collections.Generic.List<object>
            {
                ctx.SelectedState.SelectedChain!, chain2,
            };
            ctrl.RefreshFrames();

            ctrl.SimulateBulkHandleDrag(frame, HandleKind.TopLeft,
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   0f, endScreenY:   0f);

            Assert.False(ctx.UndoManager.CanUndo,
                "Bulk handle click without dragging must not create an undo entry");
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Native tsx: stretching one frame propagates its new size to siblings ─────────────────
    // A multi-tile .tsx tile animation (MultiTileToTiledAnimationMapper) requires every frame in
    // a chain to share the exact same whole-tile footprint. Stretching just one frame to span
    // more tiles is the only way to author that in the wireframe, so the resize must propagate
    // to every other frame in the chain -- but only for a native tsx project; an ordinary achx
    // project's frames are independent.

    private const string NativeTsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    /// <summary>
    /// Loads the fixture above as a native tsx project: one chain ("ID:0") with two 16px-tile
    /// frames -- frame 0 at pixel (0,0)-(16,16), frame 1 at (16,0)-(32,16) -- against a real
    /// 64x64 "Heroes.png" so <see cref="WireframeControl.SimulateHandleDrag"/>'s texture-space
    /// math has a bitmap to work against.
    /// </summary>
    private static (WireframeControl ctrl, AnimationChainSave chain, string dir) BuildNativeTsxCtrl(TestServices ctx)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var tsxPath = System.IO.Path.Combine(dir, "Heroes.tsx");
        System.IO.File.WriteAllText(tsxPath, NativeTsxFixtureXml);
        WriteSolidPng(dir, SKColors.DarkGray, size: 64, name: "Heroes.png");

        ctx.ProjectManager.LoadTsxProject(new FilePath(tsxPath));

        var chain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = chain.Frames[0];

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(System.IO.Path.Combine(dir, "Heroes.png"));
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();

        return (ctrl, chain, dir);
    }

    [AvaloniaFact]
    public void HandleDrag_StretchingFrameInNativeTsxProject_PropagatesNewSizeToSiblingFrames()
    {
        var ctx = ResetSingletons();
        var (ctrl, chain, dir) = BuildNativeTsxCtrl(ctx);
        try
        {
            var frame0 = chain.Frames[0];
            var frame1 = chain.Frames[1];

            // Stretch frame 0's right edge from pixel 16 to pixel 32, doubling its width to span
            // two 16px tiles instead of one.
            ctrl.SimulateHandleDrag(HandleKind.BotRight,
                startScreenX: 16f, startScreenY: 16f,
                endScreenX:   32f, endScreenY:   16f);

            Assert.Equal(0.5f, frame0.RightCoordinate, precision: 4);

            // Frame 1 keeps its own Left/Top (its position in the sheet) but must now match
            // frame 0's new 32px width -- otherwise MultiTileToTiledAnimationMapper drops the
            // whole chain's tile animation on save (footprint mismatch).
            Assert.Equal(0.25f, frame1.LeftCoordinate,   precision: 4);
            Assert.Equal(0f,    frame1.TopCoordinate,    precision: 4);
            Assert.Equal(0.75f, frame1.RightCoordinate,  precision: 4);
            Assert.Equal(0.25f, frame1.BottomCoordinate, precision: 4);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void HandleDrag_StretchingFrameInNativeTsxProject_UndoRestoresSiblingFrameToo()
    {
        var ctx = ResetSingletons();
        var (ctrl, chain, dir) = BuildNativeTsxCtrl(ctx);
        try
        {
            var frame1 = chain.Frames[1];
            var beforeR = frame1.RightCoordinate;

            ctrl.SimulateHandleDrag(HandleKind.BotRight,
                startScreenX: 16f, startScreenY: 16f,
                endScreenX:   32f, endScreenY:   16f);

            Assert.NotEqual(beforeR, frame1.RightCoordinate);

            ctx.UndoManager.Undo();

            Assert.Equal(beforeR, frame1.RightCoordinate, precision: 4);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // Two chains at rows 0 and 2, frame 0 of each selected as individual frame nodes. A bulk
    // handle drag resizes only those two visible frames; each chain's other frame must follow
    // its own chain's new footprint, in the same undo command.
    private const string TwoChainNativeTsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
         <tile id="8">
          <animation>
           <frame tileid="8" duration="200"/>
           <frame tileid="9" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    [AvaloniaFact]
    public void BulkHandleDrag_StretchingOneFrameOfEachChainInNativeTsxProject_PropagatesToEachChainsSiblings()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var tsxPath = System.IO.Path.Combine(dir, "Heroes.tsx");
            System.IO.File.WriteAllText(tsxPath, TwoChainNativeTsxFixtureXml);
            WriteSolidPng(dir, SKColors.DarkGray, size: 64, name: "Heroes.png");
            ctx.ProjectManager.LoadTsxProject(new FilePath(tsxPath));
            var chains = ctx.ProjectManager.AnimationChainListSave!.AnimationChains;
            var walk0 = chains[0].Frames[0];
            var walk1 = chains[0].Frames[1];
            var run0 = chains[1].Frames[0];
            var run1 = chains[1].Frames[1];
            ctx.SelectedState.SelectedNodes = new System.Collections.Generic.List<object> { walk0, run0 };

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(System.IO.Path.Combine(dir, "Heroes.png"));
            ctrl.SetCamera(0f, 0f, 1f);
            ctrl.RefreshFrames();

            // Drag walk0's right edge from pixel 16 to 32 (one tile wider); run0 gets the same delta.
            ctrl.SimulateBulkHandleDrag(walk0, HandleKind.BotRight,
                startScreenX: 16f, startScreenY: 16f,
                endScreenX:   32f, endScreenY:   16f);

            Assert.Equal(0.5f, walk0.RightCoordinate, precision: 4);
            Assert.Equal(0.5f, run0.RightCoordinate, precision: 4);
            Assert.Equal(0.75f, walk1.RightCoordinate, precision: 4); // tile 1: left 0.25 + 0.5
            Assert.Equal(0.75f, run1.RightCoordinate, precision: 4);  // tile 9: left 0.25 + 0.5

            ctx.UndoManager.Undo();

            Assert.Equal(0.5f, walk1.RightCoordinate, precision: 4);
            Assert.Equal(0.5f, run1.RightCoordinate, precision: 4);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void HandleDrag_StretchingFrame_PlainAchxProject_DoesNotPropagateToSiblingFrames()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            var chain = ctx.SelectedState.SelectedChain!;
            var frame0 = chain.Frames[0]; // full-UV (0,0,1,1) from the helper
            var frame1 = new AnimationFrameSave
            {
                TextureName      = "sprite.png",
                FrameLength      = 0.1f,
                LeftCoordinate   = 0f, TopCoordinate    = 0f,
                RightCoordinate  = 0.25f, BottomCoordinate = 0.25f,
                ShapesSave = new ShapesSave(),
            };
            chain.Frames.Add(frame1);
            ctrl.RefreshFrames();

            var beforeR = frame1.RightCoordinate;

            ctrl.SimulateHandleDrag(HandleKind.BotRight,
                startScreenX: 64f, startScreenY: 64f,
                endScreenX:   32f, endScreenY:   64f);

            Assert.NotEqual(beforeR, frame0.RightCoordinate); // frame 0 really did resize
            Assert.Equal(beforeR, frame1.RightCoordinate, precision: 4); // sibling untouched
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
