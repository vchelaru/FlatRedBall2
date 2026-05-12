using Microsoft.Xna.Framework.Graphics;
using FlatRedBall2.Rendering;

namespace FlatRedBall2.Tiled;

/// <summary>
/// No-op render batch for Tiled tile layers. <see cref="TileMapLayerRenderable"/> delegates
/// to MonoGame.Extended's <c>TilemapSpriteBatchRenderer</c>, which manages its own
/// <see cref="SpriteBatch"/> Begin/End calls. This batch exists solely to participate in
/// the rendering pipeline's batch-transition logic, ensuring the <see cref="SpriteBatch"/>
/// is not in an active state when <c>DrawLayer</c> is called.
/// </summary>
internal class TiledRenderBatch : IRenderBatch
{
    public static readonly TiledRenderBatch Instance = new();

    /// <inheritdoc/>
    public bool FlipsY => false;
    /// <inheritdoc/>
    public void Begin(SpriteBatch spriteBatch, Camera camera) { }
    /// <inheritdoc/>
    public void End(SpriteBatch spriteBatch) { }
}
