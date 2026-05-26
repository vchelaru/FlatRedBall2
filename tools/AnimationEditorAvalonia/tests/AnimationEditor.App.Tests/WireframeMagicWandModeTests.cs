using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for Magic Wand edit mode:
///
/// 1. Handles (resize handles + origin crosshair) are suppressed when
///    <see cref="WireframeControl.IsMagicWandMode"/> is true, even when a frame is selected.
///
/// 2. A double-click via <see cref="WireframeControl.SimulateWandDoubleClick"/> while a
///    hover preview is active applies the preview rect to the selected frame and fires
///    <see cref="WireframeControl.FrameRegionChanged"/>.
/// </summary>
public class WireframeMagicWandModeTests
{
    private static TestServices ResetSingletons()
    {
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

    /// <summary>
    /// Writes a 64×64 solid opaque black PNG (no transparency) for use in handle tests.
    /// </summary>
    private static string WriteSolidBlackPng(string dir)
    {
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}.png");
        using var bm = new SKBitmap(64, 64);
        bm.Erase(SKColors.Black);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// Writes a 64×64 PNG where pixels in [minPx, maxPx) × [minPy, maxPy) are opaque white,
    /// and the remainder are fully transparent — useful for a well-defined wand flood-fill target.
    /// </summary>
    private static string WriteRegionPng(string dir, int minPx, int minPy, int maxPx, int maxPy)
    {
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}.png");
        using var bm = new SKBitmap(64, 64, SKColorType.Rgba8888, SKAlphaType.Premul);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                bm.SetPixel(x, y, x >= minPx && x < maxPx && y >= minPy && y < maxPy
                    ? new SKColor(255, 255, 255, 255)
                    : new SKColor(0, 0, 0, 0));
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    // ── Test 1: handles suppressed in wand mode ───────────────────────────────

    /// <summary>
    /// When a frame is selected in Move mode, the top-left resize handle renders at pixel (3,3).
    /// In Magic Wand mode, handles must be suppressed: pixel (3,3) must NOT be white.
    ///
    /// Frame at UV 0.125→0.875 on a 64×64 texture → screen rect (8,8,56,56) at camera(0,0,1).
    /// TopLeft handle centre = (8−5, 8−5) = (3, 3) — white in Move mode, absent in Wand mode.
    /// </summary>
    [AvaloniaFact]
    public void WandMode_HandlesHidden_WhenFrameSelected()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidBlackPng(dir);

            var frame = new AnimationFrameSave
            {
                TextureName = png, FrameLength = 0.1f,
                LeftCoordinate = 0.125f, TopCoordinate = 0.125f,
                RightCoordinate = 0.875f, BottomCoordinate = 0.875f,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.RefreshFrames();
            ctrl.SetCamera(0, 0, 1);
            ctrl.IsMagicWandMode = true;

            using var bm = ctrl.RenderToBitmap(64, 64);

            // In Move mode pixel (3,3) would be white (handle fill).
            // In Wand mode handles must be suppressed → pixel should remain dark.
            var px = bm.GetPixel(3, 3);
            Assert.True(px.Red < 200 || px.Green < 200 || px.Blue < 200,
                $"Top-left handle pixel (3,3) should be suppressed in wand mode; " +
                $"R={px.Red} G={px.Green} B={px.Blue}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    // ── Test 2: double-click applies preview rect ─────────────────────────────

    /// <summary>
    /// In Magic Wand mode, <see cref="WireframeControl.SimulateWandDoubleClick"/> at a position
    /// inside an opaque pixel island should:
    /// <list type="bullet">
    ///   <item>Fire <see cref="WireframeControl.FrameRegionChanged"/>.</item>
    ///   <item>Update the selected frame's UV coords to the island's bounding box.</item>
    /// </list>
    ///
    /// Texture: 64×64 with opaque pixels in [20,40) × [20,40) and transparent everywhere else.
    /// Camera: pan=(0,0), zoom=1 → texture pixel (30,30) = screen pixel (30,30).
    /// Expected frame UV after double-click: left≈20/64, top≈20/64, right≈40/64, bottom≈40/64.
    /// </summary>
    [AvaloniaFact]
    public void WandMode_DoubleClick_AppliesPreviewRectToSelectedFrame()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            const int IslandMin = 20, IslandMax = 40;
            var png = WriteRegionPng(dir, IslandMin, IslandMin, IslandMax, IslandMax);

            // Start with frame covering the full texture.
            var frame = new AnimationFrameSave
            {
                TextureName = png, FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f,
                RightCoordinate = 1f, BottomCoordinate = 1f,
                ShapesSave = new ShapesSave()
            };
            var chain = new AnimationChainSave { Name = "Test" };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.RefreshFrames();
            ctrl.SetCamera(0, 0, 1);
            ctrl.IsMagicWandMode = true;

            AnimationFrameSave? changedFrame = null;
            ctrl.FrameRegionChanged += f => changedFrame = f;

            // Double-click at screen (30,30) → texture pixel (30,30) which is inside the island.
            ctrl.SimulateWandDoubleClick(30f, 30f);

            Assert.NotNull(changedFrame);
            Assert.Same(frame, changedFrame);

            const float Eps = 1.5f / 64f;   // 1.5 pixel tolerance in UV space
            Assert.True(Math.Abs(frame.LeftCoordinate   - IslandMin / 64f) < Eps,
                $"LeftCoordinate expected ≈{IslandMin}/64 but was {frame.LeftCoordinate}");
            Assert.True(Math.Abs(frame.TopCoordinate    - IslandMin / 64f) < Eps,
                $"TopCoordinate expected ≈{IslandMin}/64 but was {frame.TopCoordinate}");
            Assert.True(Math.Abs(frame.RightCoordinate  - IslandMax / 64f) < Eps,
                $"RightCoordinate expected ≈{IslandMax}/64 but was {frame.RightCoordinate}");
            Assert.True(Math.Abs(frame.BottomCoordinate - IslandMax / 64f) < Eps,
                $"BottomCoordinate expected ≈{IslandMax}/64 but was {frame.BottomCoordinate}");
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
