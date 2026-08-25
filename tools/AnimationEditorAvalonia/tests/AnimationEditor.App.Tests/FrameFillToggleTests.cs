using System;
using System.IO;
using AnimationEditor.App.Controls;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #976: a toolbar toggle controls whether frame region rectangles are drawn with a
/// semi-transparent fill or as an outline only.
/// </summary>
public class FrameFillToggleTests
{
    private static TestServices ResetSingletons()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;
        ctx.SelectedState.SelectedFrame = null;
        ctx.SelectedState.SelectedNodes = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        ctx.AppState.OffsetMultiplier = 1f;
        return ctx;
    }

    private static string WriteSolidPng(string dir, SKColor color, int w = 64, int h = 64)
    {
        var path = Path.Combine(dir, $"{Guid.NewGuid():N}.png");
        using var bm = new SKBitmap(w, h);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    /// <summary>
    /// Loads a black texture, creates a single selected frame covering (16,16)-(48,48) on a
    /// 64×64 sheet, and fixes the camera at pan=(0,0) zoom=1 so texture pixels map 1:1 to screen.
    /// </summary>
    private static (WireframeControl ctrl, string dir) BuildCtrlWithSelectedFrame(TestServices ctx)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir, SKColors.Black);

        var chain = new AnimationChainSave { Name = "C" };
        var frame = new AnimationFrameSave
        {
            TextureName      = Path.GetFileName(png),
            LeftCoordinate   = 16f / 64f,
            TopCoordinate    = 16f / 64f,
            RightCoordinate  = 48f / 64f,
            BottomCoordinate = 48f / 64f,
        };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0, 0, 1);
        return (ctrl, dir);
    }

    [AvaloniaFact]
    public void FillFrames_DefaultsToTrue()
    {
        var ctx = ResetSingletons();
        var ctrl = ctx.CreateWireframeControl();
        Assert.True(ctrl.FillFrames);
    }

    /// <summary>
    /// The interior of a selected frame's rectangle (away from the 1px stroke border) must
    /// render as the plain black texture when FillFrames is off, but as a blue-tinted fill
    /// when FillFrames is on.
    /// </summary>
    [AvaloniaFact]
    public void FillFrames_False_InteriorMatchesRawTexture_NotBlueFill()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            ctrl.FillFrames = true;
            using var bmFilled = ctrl.RenderToBitmap(64, 64);

            ctrl.FillFrames = false;
            using var bmOutline = ctrl.RenderToBitmap(64, 64);

            // Sample inside the frame at (24,24) — off both the stroke border and the diagonal
            // from frame-center (32,32), where the origin crosshair's horizontal/vertical arms
            // would otherwise contaminate the sample.
            var filledPixel   = bmFilled.GetPixel(24, 24);
            var outlinePixel  = bmOutline.GetPixel(24, 24);

            Assert.NotEqual(filledPixel, outlinePixel);
            Assert.Equal(SKColors.Black, outlinePixel);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Disabling the fill must not remove the stroke outline — the frame's border pixels
    /// should still differ from the surrounding unselected texture either way.
    /// </summary>
    [AvaloniaFact]
    public void FillFrames_False_StrokeOutlineStillRenders()
    {
        var ctx = ResetSingletons();
        var (ctrl, dir) = BuildCtrlWithSelectedFrame(ctx);
        try
        {
            ctrl.FillFrames = false;
            using var bm = ctrl.RenderToBitmap(64, 64);

            // The stroke is drawn along the frame's edge (x=16..48, y=16..48); sample the top
            // edge at (32,16), which should differ from the plain black background.
            var edgePixel = bm.GetPixel(32, 16);
            Assert.NotEqual(SKColors.Black, edgePixel);
        }
        finally { Directory.Delete(dir, true); }
    }
}
