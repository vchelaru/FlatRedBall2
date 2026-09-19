using AnimationEditor.Core.Tiled;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.Core.Tests.Tiled;

public class AchjSyncSnapshotTests
{
    [Fact]
    public void Clone_CopiesChainNamesAndFrameCoordinates()
    {
        var source = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "Heroes.png", FrameLength = 0.1f,
            LeftCoordinate = 0, TopCoordinate = 0, RightCoordinate = 16, BottomCoordinate = 16,
        });
        source.AnimationChains.Add(chain);

        var clone = AchjSyncSnapshot.Clone(source);

        Assert.Equal("Walk", clone.AnimationChains[0].Name);
        Assert.Equal(16f, clone.AnimationChains[0].Frames[0].RightCoordinate);
        Assert.Equal(TextureCoordinateType.Pixel, clone.CoordinateType);
    }

    [Fact]
    public void Clone_MutatingSourceAfterClone_DoesNotAffectClone()
    {
        var source = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave { TextureName = "Heroes.png" });
        source.AnimationChains.Add(chain);

        var clone = AchjSyncSnapshot.Clone(source);
        source.AnimationChains[0].Frames.Add(new AnimationFrameSave { TextureName = "Extra.png" });
        source.AnimationChains.Add(new AnimationChainSave { Name = "Run" });

        Assert.Single(clone.AnimationChains);
        Assert.Single(clone.AnimationChains[0].Frames);
    }
}
