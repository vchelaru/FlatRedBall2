using AnimationEditor.Core.CommandsAndState;
using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.Core.Tests;

public class GroupPlaybackSyncTests
{
    [Fact]
    public void ComputeDiff_ChainDroppedAndChainAdded_ReturnsBothInOneDiff()
    {
        var a = new AnimationChainSave { Name = "A" };
        var b = new AnimationChainSave { Name = "B" };
        var c = new AnimationChainSave { Name = "C" };

        var (toAdd, toRemove) = GroupPlaybackSync.ComputeDiff(
            existingKeys: new[] { a, b },
            desiredChains: new List<AnimationChainSave> { b, c });

        Assert.Equal(new[] { c }, toAdd);
        Assert.Equal(new[] { a }, toRemove);
    }

    [Fact]
    public void ComputeDiff_SameChains_ReturnsEmptyDiff()
    {
        var a = new AnimationChainSave { Name = "A" };
        var b = new AnimationChainSave { Name = "B" };

        var (toAdd, toRemove) = GroupPlaybackSync.ComputeDiff(
            existingKeys: new[] { a, b },
            desiredChains: new List<AnimationChainSave> { a, b });

        Assert.Empty(toAdd);
        Assert.Empty(toRemove);
    }
}
