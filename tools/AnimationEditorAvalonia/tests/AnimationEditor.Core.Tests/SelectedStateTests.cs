using AnimationEditor.Core;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class SelectedStateTests
{
    [Fact]
    public void SelectedNodes_SameContent_DoesNotRaiseSelectionChanged()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var chain = TestHelpers.MakeChain(ctx.Acls, "Walk", 2);
        var f0 = chain.Frames[0];
        var f1 = chain.Frames[1];
        var nodes = new List<object> { f0, f1 };

        int changes = 0;
        ctx.SelectedState.SelectionChanged += () => changes++;

        ctx.SelectedState.SelectedNodes = nodes;
        Assert.Equal(1, changes);

        ctx.SelectedState.SelectedNodes = new List<object> { f0, f1 };
        Assert.Equal(1, changes);
    }

    [Fact]
    public void SelectedRectangles_MultipleRectsInSelectedNodes_ReturnsAll()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var rectA = new AARectSave { Name = "A" };
        var rectB = new AARectSave { Name = "B" };
        ctx.SelectedState.SelectedNodes = new List<object> { rectA, rectB };

        var result = ctx.SelectedState.SelectedRectangles;

        Assert.Equal(2, result.Count);
        Assert.Contains(rectA, result);
        Assert.Contains(rectB, result);
    }

    [Fact]
    public void SelectedRectangles_SelectedNodesEmpty_FallsBackToSelectedRectangle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var rect = new AARectSave { Name = "A" };

        // Simulates selecting a shape via a drag in the preview panel, which sets
        // SelectedRectangle directly without touching the tree's multi-select bag.
        ctx.SelectedState.SelectedRectangle = rect;

        var result = ctx.SelectedState.SelectedRectangles;

        Assert.Single(result);
        Assert.Same(rect, result[0]);
    }

    [Fact]
    public void SelectedCircles_SelectedNodesEmpty_FallsBackToSelectedCircle()
    {
        var ctx = TestHelpers.SetupFreshAcls();
        var circle = new CircleSave { Name = "A" };

        ctx.SelectedState.SelectedCircle = circle;

        var result = ctx.SelectedState.SelectedCircles;

        Assert.Single(result);
        Assert.Same(circle, result[0]);
    }
}
