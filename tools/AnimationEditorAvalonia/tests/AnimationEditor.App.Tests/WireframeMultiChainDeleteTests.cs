using AnimationEditor.Core.IO;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Deleting a multi-selection of animations (chains) must clear the wireframe's
/// frame-rectangle overlay for the chains that were just removed. See issue #1172:
/// the sheet kept drawing the previous multi-select's frame rects after the delete.
/// </summary>
public class WireframeMultiChainDeleteTests
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

    [AvaloniaFact]
    public void DeleteAnimationChains_MultiSelect_ClearsWireframeFrameRects()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, SKColors.DarkGray, name: "sprite.png");

            var f1 = new AnimationFrameSave
            {
                TextureName      = "sprite.png",
                LeftCoordinate   = 0f,   TopCoordinate    = 0f,
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

            Assert.Equal(2, ctrl.FrameRectCount); // baseline: both chains' frames drawn

            ctx.AppCommands.DeleteAnimationChains(ctx.SelectedState.SelectedChains);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, ctrl.FrameRectCount);
        }
        finally { Directory.Delete(dir, true); }
    }

    /// <summary>
    /// Same starting overlay (two chains' frames drawn from a multi-chain selection),
    /// but here the user deletes the individual frames spanning both chains (as if every
    /// drawn frame rect were selected and deleted at once) rather than deleting the chains
    /// themselves. The overlay must still clear.
    /// </summary>
    [AvaloniaFact]
    public void DeleteFrames_AcrossMultipleChains_ClearsWireframeFrameRects()
    {
        var ctx = ResetSingletons();
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var png = WriteSolidPng(dir, SKColors.DarkGray, name: "sprite.png");

            var f1 = new AnimationFrameSave
            {
                TextureName      = "sprite.png",
                LeftCoordinate   = 0f,   TopCoordinate    = 0f,
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

            // Start from the same multi-chain overlay as the delete-chains test.
            ctx.SelectedState.SelectedNodes = new List<object> { c1, c2 };
            ctx.SelectedState.SelectedChain = c1;

            var ctrl = ctx.CreateWireframeControl();
            ctrl.LoadTexture(png);
            ctrl.SetCamera(0f, 0f, 1f);
            ctrl.RefreshFrames();

            Assert.Equal(2, ctrl.FrameRectCount); // baseline: both chains' frames drawn

            // Now the multi-selection narrows to the individual frames (both drawn rects
            // picked directly), spanning both chains, and the user deletes them.
            ctx.SelectedState.SelectedNodes = new List<object> { f1, f2 };
            ctx.AppCommands.DeleteFrames(new List<AnimationFrameSave> { f1, f2 });
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, ctrl.FrameRectCount);
        }
        finally { Directory.Delete(dir, true); }
    }
}
