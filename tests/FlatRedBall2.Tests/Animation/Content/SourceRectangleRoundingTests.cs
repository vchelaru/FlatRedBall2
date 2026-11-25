using FlatRedBall2.AnimationEditorCommon;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation.Content;

[Collection(GraphicsDeviceCollection.Name)]
public class SourceRectangleRoundingTests
{
    private readonly GraphicsDeviceFixture _fixture;

    public SourceRectangleRoundingTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    [Fact]
    public void ToAnimationChainList_UvRegionOfWholePixels_KeepsItsSize()
    {
        if (!_fixture.IsAvailable) return;
        var content = new ContentLoader { TextureLoader = _ => new Texture2D(_fixture.GraphicsDevice!, 160, 96) };
        var chain = new AnimationChainSave { Name = "Walk" };
        chain.Frames.Add(new AnimationFrameSave
        {
            TextureName = "a.png",
            LeftCoordinate = 48 / 160f, RightCoordinate = 64 / 160f,
            TopCoordinate = 64 / 96f, BottomCoordinate = 80 / 96f,
        });

        var list = new AnimationChainListSave { CoordinateType = TextureCoordinateType.UV, AnimationChains = { chain } }.ToAnimationChainList(content);

        list[0][0].SourceRectangle.ShouldBe(new Rectangle(48, 64, 16, 16));
    }
}
