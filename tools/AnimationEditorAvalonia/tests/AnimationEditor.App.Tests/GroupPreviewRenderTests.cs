using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Canvas compositing for the multi-select group preview (#576 scope items 2–3): every selected
/// chain draws in the same canvas at the shared entity origin, back-to-front in selection (click)
/// order — the most recently selected chain draws on top.
/// </summary>
public class GroupPreviewRenderTests
{
    private static string WritePng(string dir, string name, SKColor color, int size = 64)
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(size, size);
        bm.Erase(color);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static AnimationChainSave MakeSingleFrameChain(string name, string texturePath)
    {
        var chain = new AnimationChainSave { Name = name };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = texturePath,
            FrameLength = 1f,
            LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 1f, BottomCoordinate = 1f,
            ShapesSave = new ShapesSave(),
        });
        return chain;
    }

    [AvaloniaFact]
    public void RenderToBitmap_TwoOverlappingChainsSelected_MostRecentlySelectedDrawsOnTop()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var redPath  = WritePng(dir, "red.png", SKColors.Red);
            var bluePath = WritePng(dir, "blue.png", SKColors.Blue);
            var redChain  = MakeSingleFrameChain("Red", redPath);
            var blueChain = MakeSingleFrameChain("Blue", bluePath);

            var ctx = TestHelpers.BuildServices();
            var ctrl = ctx.CreatePreviewControl();
            ctrl.PauseAutoPlayback();

            // Click order [Red, Blue]: Red selected first (back), Blue selected last (front).
            ctx.SelectedState.SelectedNodes = new List<object> { redChain, blueChain };
            Dispatcher.UIThread.RunJobs();

            using var bitmap = ctrl.RenderToBitmap(200, 200);
            var center = bitmap.GetPixel(100, 100);

            Assert.Equal(SKColors.Blue.Red,  center.Red);
            Assert.Equal(SKColors.Blue.Blue, center.Blue);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }

    [AvaloniaFact]
    public void RenderToBitmap_ReversedClickOrder_ReversesWhichChainDrawsOnTop()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var redPath  = WritePng(dir, "red.png", SKColors.Red);
            var bluePath = WritePng(dir, "blue.png", SKColors.Blue);
            var redChain  = MakeSingleFrameChain("Red", redPath);
            var blueChain = MakeSingleFrameChain("Blue", bluePath);

            var ctx = TestHelpers.BuildServices();
            var ctrl = ctx.CreatePreviewControl();
            ctrl.PauseAutoPlayback();

            // Click order [Blue, Red]: Blue selected first (back), Red selected last (front).
            ctx.SelectedState.SelectedNodes = new List<object> { blueChain, redChain };
            Dispatcher.UIThread.RunJobs();

            using var bitmap = ctrl.RenderToBitmap(200, 200);
            var center = bitmap.GetPixel(100, 100);

            Assert.Equal(SKColors.Red.Red,  center.Red);
            Assert.Equal(SKColors.Red.Blue, center.Blue);
        }
        finally { Directory.Delete(dir, recursive: true); }
    }
}
