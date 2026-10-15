using AnimationEditor.Core.ViewModels;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests;

/// <summary>Which keyframe lanes the timeline shows for a selection (#1489): shape lanes replace the Sprite lanes.</summary>
public class TimelineLanePlanTests
{
    private static readonly double[] Widths = { 20, 30, 40 };

    // Offset, Color and the rectangle's Offset and Size all in use.
    private static (AnimationChainSave Chain, AARectSave[] Rects) Chain()
    {
        var chain = new AnimationChainSave { Name = "c" };
        var rects = new AARectSave[3];
        for (int i = 0; i < 3; i++)
        {
            var frame = new AnimationFrameSave { TextureName = "t.png", FrameLength = 0.1f, ShapesSave = new ShapesSave(), RelativeX = i * 5, Red = i * 10 };
            rects[i] = new AARectSave { Name = "Hit", X = i * 10, ScaleX = 4 + i, ScaleY = 4 };
            frame.ShapesSave.Add(rects[i]);
            chain.Frames.Add(frame);
        }
        return (chain, rects);
    }

    [Fact]
    public void NoShapeSelected_ShowsTheSpriteOffsetAndColorLanes()
    {
        var (chain, _) = Chain();

        var plan = TimelineLanePlanner.Build(chain, new object[0], Widths);

        Assert.NotNull(plan.First);
        Assert.NotNull(plan.Second);
        Assert.Equal("Offset", plan.FirstLabel);
        Assert.Equal("Color", plan.SecondLabel);
    }

    [Fact]
    public void OneShapeSelected_ShowsThatShapesOffsetAndSizeLanes_LabeledWithItsName_InPlaceOfTheSpriteLanes()
    {
        var (chain, rects) = Chain();

        var plan = TimelineLanePlanner.Build(chain, new object[] { rects[1] }, Widths);

        Assert.Equal("Hit Offset", plan.FirstLabel);
        Assert.Equal("Hit Size", plan.SecondLabel);
        Assert.Equal(new double[] { 0, 20, 50 }, plan.First!.KeyframeXs);
        Assert.Equal(new double[] { 0, 20, 50 }, plan.Second!.KeyframeXs);
    }

    [Fact]
    public void OneShapeSelected_WithOnlyItsOffsetInUse_HidesTheSizeLane()
    {
        var (chain, rects) = Chain();
        foreach (var r in rects) { r.ScaleX = 4; r.ScaleY = 4; }

        var plan = TimelineLanePlanner.Build(chain, new object[] { rects[0] }, Widths);

        Assert.NotNull(plan.First);
        Assert.Null(plan.Second);
    }

    [Fact]
    public void OneShapeSelected_WhoseLanesAreNotInUse_ShowsNothing_NotTheSpriteLanes()
    {
        var (chain, rects) = Chain();
        foreach (var r in rects) { r.X = 0; r.ScaleX = 4; }

        var plan = TimelineLanePlanner.Build(chain, new object[] { rects[0] }, Widths);

        Assert.Null(plan.First);
        Assert.Null(plan.Second);
    }

    [Fact]
    public void UnnamedShapeSelected_ShowsNoLanes()
    {
        var (chain, rects) = Chain();
        foreach (var r in rects) r.Name = "";

        var plan = TimelineLanePlanner.Build(chain, new object[] { rects[0] }, Widths);

        Assert.Null(plan.First);
        Assert.Null(plan.Second);
    }

    [Fact]
    public void PolygonSelected_ShowsOnlyItsOffsetLane()
    {
        var (chain, _) = Chain();
        var polys = new PolygonSave[3];
        for (int i = 0; i < 3; i++)
        {
            chain.Frames[i].ShapesSave = new ShapesSave();
            polys[i] = new PolygonSave { Name = "P", X = i * 3 };
            chain.Frames[i].ShapesSave!.Add(polys[i]);
        }

        var plan = TimelineLanePlanner.Build(chain, new object[] { polys[0] }, Widths);

        Assert.NotNull(plan.First);
        Assert.Equal("P Offset", plan.FirstLabel);
        Assert.Null(plan.Second);
    }

    [Fact]
    public void SeveralShapesSelected_ShowNoLanes()
    {
        var (chain, rects) = Chain();

        var plan = TimelineLanePlanner.Build(chain, new object[] { rects[0], rects[1] }, Widths);

        Assert.Null(plan.First);
        Assert.Null(plan.Second);
    }

    [Fact]
    public void NoChain_ShowsNoLanes()
    {
        var plan = TimelineLanePlanner.Build(null, new object[0], Widths);

        Assert.Null(plan.First);
        Assert.Null(plan.Second);
    }
}
