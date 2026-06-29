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
}
