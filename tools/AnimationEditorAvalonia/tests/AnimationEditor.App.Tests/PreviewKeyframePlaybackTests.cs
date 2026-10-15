using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The preview plays the curves stored on the chain's frames (#1478): offset and color glide between
/// keyframes, a snap chain holds each frame's own values, and a frame whose keyframe is off shows the
/// value in effect where it starts.
/// </summary>
public class PreviewKeyframePlaybackTests
{
    private static AnimationChainSave MakeChain(string name)
    {
        var chain = new AnimationChainSave { Name = name };
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f, Alpha = 0 });
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f, RelativeX = 10f, Alpha = 200 });
        return chain;
    }

    private static (TestServices Ctx, AnimationChainSave Chain, PreviewControl Ctrl)
        Open(AnimationChainSave chain)
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;
        Dispatcher.UIThread.RunJobs();
        return (ctx, chain, ctrl);
    }

    [AvaloniaFact]
    public void ScrubToFrame_StoredLinearCurve_ShowsTheGlidingOffsetAndColor()
    {
        var (_, chain, ctrl) = Open(MakeChain("A"));
        chain.Frames[0].OffsetInterpolation = "Linear";
        chain.Frames[0].ColorInterpolation = "Linear";

        ctrl.ScrubToFrame(0, 0.5);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(5f, ctrl.GetFrameOffsetForTest().X, 0.01f);
        Assert.Equal(100, ctrl.GetFrameColorForTest().Alpha);
    }

    [AvaloniaFact]
    public void ScrubToFrame_NoStoredCurve_HoldsTheFramesOwnOffsetAndColor()
    {
        var (_, _, ctrl) = Open(MakeChain("A"));

        ctrl.ScrubToFrame(0, 0.5);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(0f, ctrl.GetFrameOffsetForTest().X, 0.01f);
        Assert.Equal(0, ctrl.GetFrameColorForTest().Alpha);
    }

    [AvaloniaFact]
    public void ScrubToFrame_StoredEaseIn_IsNotForcedLinear()
    {
        var (_, chain, ctrl) = Open(MakeChain("A"));
        chain.Frames[0].OffsetInterpolation = "Quadratic";
        chain.Frames[0].OffsetEasing = "In";

        ctrl.ScrubToFrame(0, 0.5);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2.5f, ctrl.GetFrameOffsetForTest().X, 0.01f);
    }

    [AvaloniaFact]
    public void SelectedFrameWithKeyframeOff_ShowsTheValueInEffectAtItsStart()
    {
        var chain = MakeChain("A");
        chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f, RelativeX = 20f, Alpha = 255 });
        chain.Frames[0].OffsetInterpolation = "Linear";
        chain.Frames[0].ColorInterpolation = "Linear";
        chain.Frames[1].SkipOffsetKeyframe = true;
        chain.Frames[1].SkipColorKeyframe = true;
        var (ctx, _, ctrl) = Open(chain);

        ctx.SelectedState.SelectedFrame = chain.Frames[1];
        Dispatcher.UIThread.RunJobs();

        // Frame 0 glides to frame 2 across 0.2s; frame 1 starts halfway.
        Assert.Equal(10f, ctrl.GetFrameOffsetForTest().X, 0.01f);
        Assert.Equal(128, ctrl.GetFrameColorForTest().Alpha);
    }

    [AvaloniaFact]
    public void GroupLayers_StoredColorCurve_GlidesOnlyThatTrack()
    {
        var ctx = TestHelpers.BuildServices();
        var a = MakeChain("A");
        var b = MakeChain("B");
        a.Frames[0].ColorInterpolation = "Linear";
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(a);
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(b);
        var ctrl = ctx.CreatePreviewControl();
        ctrl.PauseAutoPlayback();
        ctx.SelectedState.SelectedNodes = new List<object> { a, b };
        Dispatcher.UIThread.RunJobs();

        ctrl.ScrubGroupTrack(a, 0, 0.5);
        ctrl.ScrubGroupTrack(b, 0, 0.5);

        var colors = ctrl.GetGroupLayerColorsForTest();
        Assert.Equal(100, colors[0].Alpha);
        Assert.Equal(0, colors[1].Alpha);
    }
}
