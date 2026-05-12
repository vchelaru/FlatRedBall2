using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.Core.Tests;

// In FRB1 this class tested XmlSerializer ShouldSerialize* gates on AnimationFrameSave.
// In FRB2 the .achx writer is hand-rolled (AnimationChainListSave.Save), so those gates
// no longer exist as members — the equivalent "omit when default" behavior is covered by
// AchxSerializationTests against the new writer. Only the default-value invariants on
// the data type itself remain useful here.
public class AnimationFrameSaveConditionalSerializationTests
{
    [Fact]
    public void DefaultFrame_BottomCoordinateIsOne()
    {
        var frame = new AnimationFrameSave();

        Assert.Equal(1f, frame.BottomCoordinate);
    }

    [Fact]
    public void DefaultFrame_RightCoordinateIsOne()
    {
        var frame = new AnimationFrameSave();

        Assert.Equal(1f, frame.RightCoordinate);
    }
}
