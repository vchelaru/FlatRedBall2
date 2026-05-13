using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for multi-chain selection in the wireframe:
///   1. All selected chains' frames render when SelectedNodes contains multiple chains.
///   2. Bulk handle-drag applies the same delta to every visible frame.
///   3. Chain drag moves all chains' frames together.
///
/// Camera fixed at pan=(0,0) zoom=1 so texture pixels == screen pixels.
/// Texture size: 64 × 64.
/// </summary>
public class WireframeMultiChainTests
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

    private static string WriteSolidPng(string dir, SKColor color, int size = 64,
                                         string name = "sprite.png")
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// Builds two chains on the same 64×64 texture.
    /// chain1.Frames[0]: left half  (0→0.5, full height)
    /// chain2.Frames[0]: right half (0.5→1, full height)
    /// SelectedNodes = [chain1, chain2], SelectedChain = chain1, SelectedFrame = null.
    /// </summary>
    private static (WireframeControl ctrl, AnimationChainSave c1, AnimationChainSave c2,
                    AnimationFrameSave f1, AnimationFrameSave f2, string dir)
        BuildMultiChainCtrl(TestServices ctx)
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.DarkGray, name: "sprite.png");

        var f1 = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            LeftCoordinate   = 0f,  TopCoordinate    = 0f,
            RightCoordinate  = 0.5f, BottomCoordinate = 1f,
            FrameLength      = 0.1f, ShapesSave = new ShapesSave()
        };
        var f2 = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            LeftCoordinate   = 0.5f, TopCoordinate    = 0f,
            RightCoordinate  = 1f,   BottomCoordinate = 1f,
            FrameLength      = 0.1f, ShapesSave = new ShapesSave()
        };

        var c1 = new AnimationChainSave { Name = "Run" };
        c1.Frames.Add(f1);
        var c2 = new AnimationChainSave { Name = "Idle" };
        c2.Frames.Add(f2);

        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(c1);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(c2);
        ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedNodes = new List<object> { c1, c2 };
        ctx.SelectedState.SelectedChain = c1;
        // SelectedFrame remains null → multi-chain mode

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();

        return (ctrl, c1, c2, f1, f2, dir);
    }

    // ── Rendering ─────────────────────────────────────────────────────────────

    /// <summary>
    /// When SelectedNodes contains two chains, both chains' frames must appear
    /// in the wireframe (FrameRectCount == 2).
    /// </summary>
    [AvaloniaFact]
    public void MultiChain_SelectedNodes_RendersFramesFromAllChains()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, _, _, _, dir) = BuildMultiChainCtrl(ctx);
        try
        {
            Assert.Equal(2, ctrl.FrameRectCount);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// When only a single chain is in SelectedNodes, only that chain's frames render.
    /// </summary>
    [AvaloniaFact]
    public void SingleChain_SelectedNodes_RendersOnlyThatChainsFrames()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, SKColors.DarkGray, name: "sprite.png");
            var f = new AnimationFrameSave
            {
                TextureName = "sprite.png",
                LeftCoordinate = 0f, TopCoordinate = 0f,
                RightCoordinate = 1f, BottomCoordinate = 1f,
                FrameLength = 0.1f, ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Run" };
            chain.Frames.Add(f);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

            ctx.SelectedState.SelectedNodes = new List<object> { chain };
            ctx.SelectedState.SelectedChain = chain;

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.SetCamera(0f, 0f, 1f);
            ctrl.RefreshFrames();

            Assert.Equal(1, ctrl.FrameRectCount);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Bulk handle drag ───────────────────────────────────────────────────────

    /// <summary>
    /// Bulk Right-handle drag: drag the right edge of f1 inward by 8px.
    /// At zoom=1, 8px = 8/64 = 0.125 UV units.
    /// Both f1 and f2 must have their right coordinate decreased by 0.125.
    /// </summary>
    [AvaloniaFact]
    public void BulkHandleDrag_Right_AppliesSameDeltaToBothFrames()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, _, f1, f2, dir) = BuildMultiChainCtrl(ctx);
        try
        {
            // f1 right edge in screen space at zoom=1: 0.5 * 64 = 32
            // Drag from screen x=32 to x=24 → dx = -8 → right UV decreases by 8/64 = 0.125
            ctrl.SimulateBulkHandleDrag(f1, HandleKind.MidRight,
                startScreenX: 32f, startScreenY: 32f,
                endScreenX:   24f, endScreenY:   32f);

            Assert.Equal(0.5f  - 0.125f, f1.RightCoordinate, precision: 4);
            Assert.Equal(1f    - 0.125f, f2.RightCoordinate, precision: 4);
            // Left edges must be unchanged
            Assert.Equal(0f,   f1.LeftCoordinate, precision: 4);
            Assert.Equal(0.5f, f2.LeftCoordinate, precision: 4);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Bulk TopLeft handle drag: both frames shrink by the same amount.
    /// </summary>
    [AvaloniaFact]
    public void BulkHandleDrag_TopLeft_AppliesSameDeltaToBothFrames()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, _, f1, f2, dir) = BuildMultiChainCtrl(ctx);
        try
        {
            // f1 top-left corner in screen space: (0,0). Drag to (8,8) → left+8, top+8.
            ctrl.SimulateBulkHandleDrag(f1, HandleKind.TopLeft,
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   8f, endScreenY:   8f);

            float delta = 8f / 64f; // 0.125

            Assert.Equal(0f   + delta, f1.LeftCoordinate,   precision: 4);
            Assert.Equal(0.5f + delta, f2.LeftCoordinate,   precision: 4);
            Assert.Equal(0f   + delta, f1.TopCoordinate,    precision: 4);
            Assert.Equal(0f   + delta, f2.TopCoordinate,    precision: 4);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Bulk undo ─────────────────────────────────────────────────────────────

    /// <summary>
    /// After a bulk drag, a single undo should revert BOTH frames to their
    /// pre-drag UV coordinates.
    /// </summary>
    [AvaloniaFact]
    public void BulkHandleDrag_Undo_RevertsAllFrames()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, _, f1, f2, dir) = BuildMultiChainCtrl(ctx);
        try
        {
            float f1RightBefore = f1.RightCoordinate;
            float f2RightBefore = f2.RightCoordinate;

            ctrl.SimulateBulkHandleDrag(f1, HandleKind.MidRight,
                startScreenX: 32f, startScreenY: 32f,
                endScreenX:   24f, endScreenY:   32f);

            ctx.UndoManager.Undo();

            Assert.Equal(f1RightBefore, f1.RightCoordinate, precision: 4);
            Assert.Equal(f2RightBefore, f2.RightCoordinate, precision: 4);
        }
        finally { Directory.Delete(dir, true); }
    }

    // ── Chain drag across multiple chains ─────────────────────────────────────

    /// <summary>
    /// Chain drag must translate frames from ALL selected chains.
    /// Both f1 and f2 should move by the same (dx, dy).
    /// </summary>
    [AvaloniaFact]
    public void ChainDrag_MultiChain_MovesAllChainFrames()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, _, f1, f2, dir) = BuildMultiChainCtrl(ctx);
        try
        {
            float f1L = f1.LeftCoordinate, f1R = f1.RightCoordinate;
            float f2L = f2.LeftCoordinate, f2R = f2.RightCoordinate;

            // Drag 8px to the right → all UV left/right coords shift by 8/64 = 0.125
            ctrl.SimulateChainDrag(
                startScreenX: 0f, startScreenY: 0f,
                endScreenX:   8f, endScreenY:   0f);

            float delta = 8f / 64f;
            Assert.Equal(f1L + delta, f1.LeftCoordinate,  precision: 4);
            Assert.Equal(f1R + delta, f1.RightCoordinate, precision: 4);
            Assert.Equal(f2L + delta, f2.LeftCoordinate,  precision: 4);
            Assert.Equal(f2R + delta, f2.RightCoordinate, precision: 4);
        }
        finally { Directory.Delete(dir, true); }
    }
}
