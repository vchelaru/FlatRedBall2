using AnimationEditor.App.Controls;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System.Collections.Generic;
using System.IO;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Selection-outline reveal (#542): one-shot <see cref="AnimationEditor.Core.Rendering.RevealAnimation"/>
/// shrink-to-rest when the selected frame set changes — same curve as the PNG diff boxes (#606).
/// Pure easing is covered in <c>RevealAnimationTests</c>; these cover the WireframeControl wiring.
/// </summary>
public class WireframeSelectionRevealTests
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
        return ctx;
    }

    private static string WriteSolidPng(string dir, string name = "sprite.png")
    {
        var path = Path.Combine(dir, name);
        using var bm = new SKBitmap(64, 64);
        bm.Erase(SKColors.DarkGray);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    private static (WireframeControl ctrl, AnimationFrameSave f0, AnimationFrameSave f1, string dir)
        BuildTwoFrameCtrl(TestServices ctx)
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var png = WriteSolidPng(dir);

        var f0 = new AnimationFrameSave
        {
            TextureName = "sprite.png",
            LeftCoordinate = 0f, TopCoordinate = 0f,
            RightCoordinate = 0.5f, BottomCoordinate = 1f,
            FrameLength = 0.1f, ShapesSave = new ShapesSave()
        };
        var f1 = new AnimationFrameSave
        {
            TextureName = "sprite.png",
            LeftCoordinate = 0.5f, TopCoordinate = 0f,
            RightCoordinate = 1f, BottomCoordinate = 1f,
            FrameLength = 0.1f, ShapesSave = new ShapesSave()
        };

        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.AddRange(new[] { f0, f1 });
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.ProjectManager.FileName = Path.Combine(dir, "test.achx");

        ctx.SelectedState.SelectedFrame = f0;

        var ctrl = ctx.CreateWireframeControl();
        ctrl.Measure(new Size(400, 300));
        ctrl.Arrange(new Rect(0, 0, 400, 300));
        ctrl.LoadTexture(png);
        ctrl.SetCamera(0f, 0f, 1f);
        ctrl.RefreshFrames();
        Dispatcher.UIThread.RunJobs();
        ctrl.SettleSelectionReveal();

        return (ctrl, f0, f1, dir);
    }

    [AvaloniaFact]
    public void RefreshFrames_WithoutSelectionChange_DoesNotRestartReveal()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, _, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            Assert.False(ctrl.IsSelectionRevealAnimating);
            Assert.Equal(1f, ctrl.SelectionRevealProgress);

            ctrl.RefreshFrames();

            Assert.False(ctrl.IsSelectionRevealAnimating);
            Assert.Equal(1f, ctrl.SelectionRevealProgress);
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void SelectedFrame_Change_StartsSelectionReveal()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            Assert.False(ctrl.IsSelectionRevealAnimating);

            ctx.SelectedState.SelectedFrame = f1;
            Dispatcher.UIThread.RunJobs();

            Assert.True(ctrl.IsSelectionRevealAnimating);
            Assert.Equal(0f, ctrl.SelectionRevealProgress);
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void SettleSelectionReveal_LandsAtRestAndStops()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            ctx.SelectedState.SelectedFrame = f1;
            Dispatcher.UIThread.RunJobs();
            Assert.True(ctrl.IsSelectionRevealAnimating);

            ctrl.SettleSelectionReveal();

            Assert.False(ctrl.IsSelectionRevealAnimating);
            Assert.Equal(1f, ctrl.SelectionRevealProgress);
        }
        finally { Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public void StepSelectionReveal_OneTick_MovesTowardRestWithoutFinishing()
    {
        var ctx = ResetSingletons();
        var (ctrl, _, f1, dir) = BuildTwoFrameCtrl(ctx);
        try
        {
            ctx.SelectedState.SelectedFrame = f1;
            Dispatcher.UIThread.RunJobs();

            float before = ctrl.SelectionRevealProgress;
            ctrl.StepSelectionReveal(0.016f);
            float after = ctrl.SelectionRevealProgress;

            Assert.True(ctrl.IsSelectionRevealAnimating);
            Assert.True(after > before && after < 1f,
                $"expected progress in ({before}, 1) after one tick, got {after}");
        }
        finally { Directory.Delete(dir, true); }
    }
}
