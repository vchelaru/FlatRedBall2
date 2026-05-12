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
/// Tests for Ctrl+click frame creation in plain mode (no grid, no magic-wand).
/// Covers <see cref="WireframeControl.SimulatePlainCtrlClick"/>.
/// </summary>
public class CtrlClickPlainModeTests
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

    private static string WriteSolidPng(string dir, int w = 64, int h = 64)
    {
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}.png");
        using var bm = new SKBitmap(w, h);
        bm.Erase(SKColors.Black);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static (WireframeControl ctrl, string dir) BuildCtrl(TestServices ctx, int w = 64, int h = 64)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, w, h);
        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);
        return (ctrl, dir);
    }

    // ── No-bitmap guard ───────────────────────────────────────────────────────

    [AvaloniaFact]
    public void CtrlClick_NoBitmap_DoesNotFire()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreateWireframeControl();  // no texture loaded
        bool fired = false;
        ctrl.FrameCreatedFromRegion += (_, _, _, _) => fired = true;

        ctrl.SimulatePlainCtrlClick(32, 32);

        Assert.False(fired, "FrameCreatedFromRegion must not fire when no bitmap is loaded.");
    }

    // ── Frame creation fires ──────────────────────────────────────────────────

    [AvaloniaFact]
    public void CtrlClick_NoGrid_NoWand_FiresFrameCreatedFromRegion()
    {
        var ctx = ResetSingletons();
        var (ctrl, _) = BuildCtrl(ctx);
        (int x0, int y0, int x1, int y1)? received = null;
        ctrl.FrameCreatedFromRegion += (x0, y0, x1, y1) => received = (x0, y0, x1, y1);

        ctrl.SimulatePlainCtrlClick(32, 32);

        Assert.NotNull(received);
    }

    [AvaloniaFact]
    public void CtrlClick_NoGrid_NoWand_FrameCenteredAtClick_DefaultSize16()
    {
        var ctx = ResetSingletons();
        var (ctrl, _) = BuildCtrl(ctx);
        (int x0, int y0, int x1, int y1)? received = null;
        ctrl.FrameCreatedFromRegion += (x0, y0, x1, y1) => received = (x0, y0, x1, y1);

        ctrl.SimulatePlainCtrlClick(32, 32);

        Assert.NotNull(received);
        var (rx0, ry0, rx1, ry1) = received!.Value;
        // Click at (32,32) with default 16×16 size: center at 32, half=8 → [24, 40]
        Assert.Equal(24, rx0);
        Assert.Equal(24, ry0);
        Assert.Equal(40, rx1);
        Assert.Equal(40, ry1);
    }

    // ── Last-frame size used ──────────────────────────────────────────────────

    [AvaloniaFact]
    public void CtrlClick_UsesLastFrameSize_WhenLargerThan16()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir);

        ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

        var chain = new AnimationChainSave { Name = "Chain" };
        // Last frame occupies the left half of the 64×64 texture → 32×64 pixels
        var lastFrame = new AnimationFrameSave
        {
            TextureName         = Path.GetFileName(png),
            FrameLength         = 0.1f,
            LeftCoordinate      = 0f,
            TopCoordinate       = 0f,
            RightCoordinate     = 0.5f,   // 32px wide
            BottomCoordinate    = 1.0f,   // 64px tall
            ShapesSave = new ShapesSave(),
        };
        chain.Frames.Add(lastFrame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);

        (int x0, int y0, int x1, int y1)? received = null;
        ctrl.FrameCreatedFromRegion += (x0, y0, x1, y1) => received = (x0, y0, x1, y1);

        ctrl.SimulatePlainCtrlClick(32, 32);

        Assert.NotNull(received);
        var (rx0, ry0, rx1, ry1) = received!.Value;
        // Last frame: 32px wide, 64px tall; click at (32,32)
        // w=max(16,32)=32, h=max(16,64)=64
        // minX=max(0,32-16)=16; maxX=min(64,16+32)=48 → width=32
        // minY=max(0,32-32)=0;  maxY=min(64,0+64)=64  → height=64
        Assert.Equal(32, rx1 - rx0);
        Assert.Equal(64, ry1 - ry0);
    }

    // ── Grid guard ────────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void CtrlClick_WithGridActive_SimulatePlainCtrlClickIsNoOp()
    {
        var ctx = ResetSingletons();
        var (ctrl, _) = BuildCtrl(ctx);
        ctrl.SetGrid(true, 16);
        bool fired = false;
        ctrl.FrameCreatedFromRegion += (_, _, _, _) => fired = true;

        ctrl.SimulatePlainCtrlClick(32, 32);

        Assert.False(fired, "SimulatePlainCtrlClick must be a no-op when grid is active.");
    }
}
