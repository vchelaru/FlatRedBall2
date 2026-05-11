using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using SkiaSharp;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Integration tests for the chain-drag feature: when an AnimationChain (not an individual frame)
/// is selected in the tree view, dragging the wireframe bounding rect moves all frames of the
/// chain together as one unit.
/// </summary>
public class WireframeChainDragTests
{
    private static TestServices ResetSingletons() {
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
    /// Builds a WireframeControl with a 64×64 texture, a chain of two frames, and the chain
    /// selected (no individual frame selected). Camera is at (0,0,1) so texture pixels == screen pixels.
    ///
    /// Frame A: top-left quarter  — UV (0.00, 0.00, 0.50, 0.50) → pixels (0, 0, 32, 32)
    /// Frame B: top-right quarter — UV (0.50, 0.00, 1.00, 0.50) → pixels (32, 0, 64, 32)
    /// </summary>
    private static (WireframeControl ctrl, AnimationChainSave chain,
                    AnimationFrameSave frameA, AnimationFrameSave frameB, string dir)
        BuildCtrlWithChainSelected(TestServices ctx)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.DarkGray, name: "sprite.png");

        var frameA = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0f,   TopCoordinate    = 0f,
            RightCoordinate  = 0.5f, BottomCoordinate = 0.5f,
            ShapeCollectionSave = new ShapeCollectionSave(),
        };
        var frameB = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.5f, TopCoordinate    = 0f,
            RightCoordinate  = 1.0f, BottomCoordinate = 0.5f,
            ShapeCollectionSave = new ShapeCollectionSave(),
        };

        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        // Select the chain — no individual frame selected
        ctx.SelectedState.SelectedChain = chain;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.InitializeServices(ctx.SelectedState, ctx.AppState, ctx.AppCommands, ctx.ApplicationEvents, ctx.ProjectManager, ctx.UndoManager);
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();

        return (ctrl, chain, frameA, frameB, dir);
    }

    // ── All frames translate together ─────────────────────────────────────────

    /// <summary>
    /// Dragging the chain +16px right and +16px down shifts every frame's UV coords
    /// by the same pixel delta. Frame sizes (UV width / height) must not change.
    ///
    /// Before: frameA pixel bounds (0,0,32,32), frameB (32,0,64,32).
    /// After +16px right, +16px down:
    ///   frameA → (16,16,48,48) → UV L=0.25, T=0.25, R=0.75, B=0.75
    ///   frameB → (48,16,80,48) → UV L=0.75, T=0.25, R=1.25, B=0.75 (values may exceed 1 — no clamping)
    /// </summary>
    [AvaloniaFact]
    public void SimulateChainDrag_WithTwoFrames_AllFramesTranslatedByDelta()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, frameA, frameB, dir) = BuildCtrlWithChainSelected(ctx);
        try
        {
            // Pre-drag frame sizes
            float widthA  = frameA.RightCoordinate  - frameA.LeftCoordinate;   // 0.5
            float heightA = frameA.BottomCoordinate - frameA.TopCoordinate;    // 0.5
            float widthB  = frameB.RightCoordinate  - frameB.LeftCoordinate;   // 0.5
            float heightB = frameB.BottomCoordinate - frameB.TopCoordinate;    // 0.5

            // Drag start inside the composite bounding rect; +16px right, +16px down
            ctrl.SimulateChainDrag(startScreenX: 16f, startScreenY: 8f,
                                   endScreenX:   32f, endScreenY:   24f);

            // Both frames must have moved +16px in each axis (UV delta = 16/64 = 0.25)
            Assert.Equal(0.25f, frameA.LeftCoordinate,   precision: 4);
            Assert.Equal(0.25f, frameA.TopCoordinate,    precision: 4);
            Assert.Equal(0.75f, frameB.LeftCoordinate,   precision: 4);
            Assert.Equal(0.25f, frameB.TopCoordinate,    precision: 4);

            // Frame sizes must be preserved
            Assert.Equal(widthA,  frameA.RightCoordinate  - frameA.LeftCoordinate,  precision: 4);
            Assert.Equal(heightA, frameA.BottomCoordinate - frameA.TopCoordinate,   precision: 4);
            Assert.Equal(widthB,  frameB.RightCoordinate  - frameB.LeftCoordinate,  precision: 4);
            Assert.Equal(heightB, frameB.BottomCoordinate - frameB.TopCoordinate,   precision: 4);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── ChainRegionChanged event fires ────────────────────────────────────────

    /// <summary>
    /// SimulateChainDrag must fire ChainRegionChanged with the moved chain
    /// so that MainWindow can save and raise AnimationChainsChanged.
    /// </summary>
    [AvaloniaFact]
    public void SimulateChainDrag_FiresChainRegionChangedWithCorrectChain()
    {
        var ctx = ResetSingletons();
        var (ctrl, chain, _, _, dir) = BuildCtrlWithChainSelected(ctx);
        try
        {
            AnimationChainSave? notified = null;
            ctrl.ChainRegionChanged += c => notified = c;

            ctrl.SimulateChainDrag(startScreenX: 0f, startScreenY: 0f,
                                   endScreenX:   8f, endScreenY:   0f);

            Assert.NotNull(notified);
            Assert.Same(chain, notified);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    // ── Safety: no chain selected ─────────────────────────────────────────────

    /// <summary>
    /// SimulateChainDrag must be a safe no-op when no chain is selected.
    /// </summary>
    [AvaloniaFact]
    public void SimulateChainDrag_NoChainSelected_NoException()
    {
        var ctx = ResetSingletons();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black);
        try
        {
            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.SetCamera(0f, 0f, 1f);
            // No chain selected → must not throw
            ctrl.SimulateChainDrag(0f, 0f, 8f, 8f);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
