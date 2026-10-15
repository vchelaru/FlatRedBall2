using AnimationEditor.App.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The preview draws shapes where the stored shape curves put them (#1478): offset and size glide between
/// keyframes, a snap shape holds its own values, and a shape whose keyframe is off shows the value in effect
/// where its frame starts.
/// </summary>
public class PreviewShapeKeyframePlaybackTests
{
    private static AnimationChainSave MakeChain(int frames = 2)
    {
        var chain = new AnimationChainSave { Name = "A" };
        for (int i = 0; i < frames; i++)
        {
            var frame = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            frame.ShapesSave.Add(new AARectSave { Name = "Hit", X = i * 10f, ScaleX = 4f + i * 4f, ScaleY = 4f });
            frame.ShapesSave.Add(new CircleSave { Name = "Aura", X = i * 10f, Radius = 8f + i * 8f });
            chain.Frames.Add(frame);
        }
        return chain;
    }

    private static (TestServices Ctx, PreviewControl Ctrl) Open(AnimationChainSave chain)
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;
        Dispatcher.UIThread.RunJobs();
        return (ctx, ctrl);
    }

    [AvaloniaFact]
    public void ScrubToFrame_StoredLinearCurves_ShapesGlideOffsetAndSize()
    {
        var chain = MakeChain();
        var rect = chain.Frames[0].ShapesSave!.AARectSaves.Single();
        var circle = chain.Frames[0].ShapesSave!.CircleSaves.Single();
        rect.OffsetInterpolation = "Linear";
        rect.SizeInterpolation = "Linear";
        circle.SizeInterpolation = "Linear";
        var (_, ctrl) = Open(chain);

        ctrl.ScrubToFrame(0, 0.5);
        Dispatcher.UIThread.RunJobs();

        var shapes = ctrl.GetShapeInfosForTest();
        var rectInfo = shapes.Single(s => s.Kind == PreviewControl.PreviewShapeKind.Rect);
        var circleInfo = shapes.Single(s => s.Kind == PreviewControl.PreviewShapeKind.Circle);
        Assert.Equal(5f, rectInfo.X, 0.01f);
        Assert.Equal(6f, rectInfo.Param1, 0.01f);
        Assert.Equal(4f, rectInfo.Param2, 0.01f);
        Assert.Equal(0f, circleInfo.X, 0.01f);          // offset curve is snap
        Assert.Equal(12f, circleInfo.Param1, 0.01f);    // size curve is linear
    }

    [AvaloniaFact]
    public void ScrubToFrame_NoStoredCurve_ShapesHoldTheirOwnValues()
    {
        var (_, ctrl) = Open(MakeChain());

        ctrl.ScrubToFrame(0, 0.5);
        Dispatcher.UIThread.RunJobs();

        var rectInfo = ctrl.GetShapeInfosForTest().Single(s => s.Kind == PreviewControl.PreviewShapeKind.Rect);
        Assert.Equal(0f, rectInfo.X, 0.01f);
        Assert.Equal(4f, rectInfo.Param1, 0.01f);
    }

    [AvaloniaFact]
    public void SelectedFrameWithShapeKeyframeOff_ShowsTheValueInEffectAtItsStart()
    {
        var chain = MakeChain(3);
        chain.Frames[0].ShapesSave!.AARectSaves.Single().OffsetInterpolation = "Linear";
        chain.Frames[1].ShapesSave!.AARectSaves.Single().SkipOffsetKeyframe = true;
        chain.Frames[2].ShapesSave!.AARectSaves.Single().X = 40f;
        var (ctx, ctrl) = Open(chain);

        ctx.SelectedState.SelectedFrame = chain.Frames[1];
        Dispatcher.UIThread.RunJobs();

        // Frame 0 glides 0 -> 40 across 0.2s; frame 1 starts halfway.
        var rectInfo = ctrl.GetShapeInfosForTest().Single(s => s.Kind == PreviewControl.PreviewShapeKind.Rect);
        Assert.Equal(20f, rectInfo.X, 0.01f);
    }
}
