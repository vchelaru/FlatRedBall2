using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Rendering;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Regression tests for issue #587: with multiple frames visible (a chain selected,
/// no single frame selected), the resize/move cursor must only appear over an actual
/// frame rect, not merely inside the union bounding box of all visible frames.
/// </summary>
public class WireframeCursorHitTestTests
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

    private static string WriteSolidPng(string dir, SKColor color, int size = 100,
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
    /// Builds a WireframeControl with a 100x100 texture, a chain of two frames that
    /// don't tile (leaving a gap between them), and the chain selected (no individual
    /// frame selected). Camera is at (0,0,1) so texture pixels == screen pixels.
    ///
    /// Frame A: pixels (0, 0, 20, 20)   — UV (0.00, 0.00, 0.20, 0.20)
    /// Frame B: pixels (60, 60, 80, 80) — UV (0.60, 0.60, 0.80, 0.80)
    /// Union bounding box: (0, 0, 80, 80). Point (40, 40) is inside that bounding box
    /// but outside both individual frame rects.
    /// </summary>
    private static (WireframeControl ctrl, string dir) BuildCtrlWithGappedFrames(TestServices ctx)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.DarkGray, name: "sprite.png");

        var frameA = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.0f, TopCoordinate    = 0.0f,
            RightCoordinate  = 0.2f, BottomCoordinate = 0.2f,
            ShapesSave = new ShapesSave(),
        };
        var frameB = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.6f, TopCoordinate    = 0.6f,
            RightCoordinate  = 0.8f, BottomCoordinate = 0.8f,
            ShapesSave = new ShapesSave(),
        };

        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        // Select the chain — no individual frame selected
        ctx.SelectedState.SelectedChain = chain;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();

        return (ctrl, dir);
    }

    [AvaloniaFact]
    public void GapBetweenSelectedFrames_HitTestReturnsNone()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithGappedFrames(ctx);
        try
        {
            // (40, 40) is inside the union bounding box (0,0,80,80) but outside both
            // frame A (0,0,20,20) and frame B (60,60,80,80).
            var handle = ctrl.HitTestHandleKindAt(40f, 40f);
            Assert.Equal(HandleKind.None, handle);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void OverAnIndividualSelectedFrame_HitTestReturnsMove()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithGappedFrames(ctx);
        try
        {
            // (10, 10) is inside frame A's body (0,0,20,20).
            var handle = ctrl.HitTestHandleKindAt(10f, 10f);
            Assert.Equal(HandleKind.Move, handle);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Builds a WireframeControl with two vertically-stacked frames separated by a
    /// narrow (10px) gap, the parent chain selected, no individual frame selected.
    /// Frame A (top):    pixels (0, 0, 40, 40)  — UV (0.00, 0.00, 0.40, 0.40)
    /// Frame B (bottom):  pixels (0, 50, 40, 90) — UV (0.00, 0.50, 0.40, 0.90)
    /// </summary>
    private static (WireframeControl ctrl, string dir) BuildCtrlWithNarrowGapFrames(TestServices ctx)
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.DarkGray, name: "sprite.png");

        var frameA = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.0f, TopCoordinate    = 0.0f,
            RightCoordinate  = 0.4f, BottomCoordinate = 0.4f,
            ShapesSave = new ShapesSave(),
        };
        var frameB = new AnimationFrameSave
        {
            TextureName      = "sprite.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.0f, TopCoordinate    = 0.5f,
            RightCoordinate  = 0.4f, BottomCoordinate = 0.9f,
            ShapesSave = new ShapesSave(),
        };

        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(frameA);
        chain.Frames.Add(frameB);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = System.IO.Path.Combine(dir, "test.achx");

        // Select the chain — no individual frame selected
        ctx.SelectedState.SelectedChain = chain;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();

        return (ctrl, dir);
    }

    /// <summary>
    /// Regression guard: with a narrow (10px) gap between two selected frames, the
    /// combined reach of each frame's resize-handle hit zone (5px handle offset + 7px
    /// hit radius = 12px) previously bled into the gap between them, so the cursor at
    /// the gap's midpoint still resolved to a hit. Only actual frame bodies should hit.
    /// </summary>
    [AvaloniaFact]
    public void NarrowGapBetweenStackedSelectedFrames_HitTestReturnsNone()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithNarrowGapFrames(ctx);
        try
        {
            // Midpoint of the 10px gap between frame A's bottom (y=40) and frame B's top (y=50).
            var handle = ctrl.HitTestHandleKindAt(20f, 45f);
            Assert.Equal(HandleKind.None, handle);
        }
        finally { System.IO.Directory.Delete(dir, true); }
    }
}
