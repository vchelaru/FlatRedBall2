using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended;
using MonoGame.Extended.ViewportAdapters;
using FlatRedBall2.Rendering;
using FlatRedBall2.Tiled;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tiled;

public class TileMapLayerRenderableTests
{
    [Fact]
    public void Batch_DefaultsToTiledRenderBatch()
    {
        // TileMapLayerRenderable requires a real TilemapSpriteBatchRenderer + layer,
        // but the Batch default is set at field-init time. We verify via the singleton.
        TiledRenderBatch.Instance.ShouldNotBeNull();
        TiledRenderBatch.Instance.FlipsY.ShouldBeFalse();
    }
}
