using AnimationEditor.Core.Keyframes;
using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>
/// The keyframe lane drawn for backward playback (#1538): showing frame N glides toward the previous keyframe, so each
/// span covers the cells between them shifted one cell right, keyframes land on their cell's right edge, and frame 0
/// holds unless a looping chain glides across the loop point.
/// </summary>
public class BackwardKeyframeLaneBuilderTests
{
    private static readonly double[] Widths = { 10, 20, 30 };

    // Three keyframes with distinct offsets; frame i's offset curve is Linear.
    private static AnimationChainSave Chain(bool loop, bool pingPong = false)
    {
        var chain = new AnimationChainSave { Name = "c", Loop = loop, PingPong = pingPong };
        for (int i = 0; i < 3; i++)
            chain.Frames.Add(new AnimationFrameSave { FrameLength = 0.1f, RelativeX = i * 10, OffsetInterpolation = "Linear" });
        return chain;
    }

    [Fact]
    public void Build_Backward_SpansShiftOneCellRight_DiamondsOnRightEdges_FirstCellHolds()
    {
        var lane = KeyframeLaneBuilder.Build(Chain(loop: false), KeyframeGroup.Offset, Widths, backward: true)!;

        Assert.Equal(new double[] { 10, 30, 60 }, lane.KeyframeXs);
        Assert.Equal(new[] { (10.0, 30.0), (30.0, 60.0) }, lane.Spans.Select(s => (s.StartX, s.EndX)));
        Assert.All(lane.Spans, s => Assert.True(s.Backward));
        Assert.Equal(new[] { new KeyframeLaneHold(0, 10) }, lane.Holds);
    }

    [Fact]
    public void Build_BackwardLooping_FirstCellGlidesAcrossTheLoopPoint()
    {
        var lane = KeyframeLaneBuilder.Build(Chain(loop: true), KeyframeGroup.Offset, Widths, backward: true)!;

        var wrap = lane.Spans.Single(s => s.StartX == 0);
        Assert.Equal(10, wrap.EndX);
        Assert.True(wrap.Backward);
        Assert.Empty(lane.Holds);
    }

    [Fact]
    public void Build_LoopingPingPong_ForwardLaneDoesNotWrap()
    {
        var lane = KeyframeLaneBuilder.Build(Chain(loop: true, pingPong: true), KeyframeGroup.Offset, Widths)!;

        Assert.Equal(2, lane.Spans.Count);
        Assert.Equal(new[] { new KeyframeLaneHold(30, 60) }, lane.Holds);
    }

    [Fact]
    public void CurvePoints_BackwardSnap_JumpsAtTheLeftEdge()
    {
        var points = KeyframeLaneBuilder.CurvePoints(KeyframeCurve.Snap, 8, backward: true);

        Assert.Equal(new[] { (0.0, 0.0), (0.0, 1.0), (1.0, 1.0) }, points);
    }

    [Fact]
    public void BuildShape_Backward_SpansShiftOneCellRight()
    {
        var chain = new AnimationChainSave { Name = "c", Loop = false };
        AARectSave? first = null;
        for (int i = 0; i < 2; i++)
        {
            var rect = new AARectSave { Name = "Box", X = i * 10, ScaleX = 8, ScaleY = 8, OffsetInterpolation = "Linear" };
            first ??= rect;
            var frame = new AnimationFrameSave { FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            frame.ShapesSave.Add(rect);
            chain.Frames.Add(frame);
        }

        var lane = KeyframeLaneBuilder.BuildShape(chain, first, ShapeKeyframeGroup.Offset, new double[] { 10, 20 }, backward: true)!;

        Assert.Equal(new[] { (10.0, 30.0) }, lane.Spans.Select(s => (s.StartX, s.EndX)));
        Assert.Equal(new double[] { 10, 30 }, lane.KeyframeXs);
        Assert.Equal(new[] { new KeyframeLaneHold(0, 10) }, lane.Holds);
    }
}
