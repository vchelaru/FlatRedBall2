using Microsoft.Xna.Framework.Graphics;

namespace FlatRedBall2.Rendering;

/// <summary>
/// Anything the engine can draw each frame: sprites, shape visuals, and custom renderables.
/// The engine sorts renderables by <see cref="Layer"/> then by <see cref="Z"/> (see
/// <see cref="SortMode"/>) and groups consecutive renderables that share the same
/// <see cref="Batch"/> into a single <c>SpriteBatch.Begin</c>/<c>End</c> pair.
/// </summary>
public interface IRenderable
{
    /// <summary>
    /// Per-renderable draw order within its <see cref="Layer"/>. Lower draws first (behind);
    /// higher draws last (in front). Ties preserve insertion order. Layer wins over Z when
    /// the two are on different layers.
    /// </summary>
    float Z { get; }

    /// <summary>
    /// The layer this renderable belongs to, or <c>null</c> for the screen's default layer.
    /// Setting this moves the renderable between layers; the engine re-sorts on the next frame.
    /// </summary>
    Layer? Layer { get; set; }

    /// <summary>
    /// The batch responsible for calling <c>SpriteBatch.Begin</c>/<c>End</c> for this
    /// renderable. Renderables sharing the same batch reference are drawn together;
    /// switching batch references forces a flush. Common batches:
    /// <c>WorldSpaceBatch</c> (camera transform, Y-flip), <c>ScreenSpaceBatch</c> (no transform),
    /// <c>ShapesBatch</c> (debug-shape rendering).
    /// </summary>
    IRenderBatch Batch { get; }

    /// <summary>Optional diagnostic name shown in tooling. Not used by the renderer.</summary>
    string? Name { get; }

    /// <summary>
    /// When <c>false</c>, the engine skips this renderable entirely — no <see cref="Draw"/> call,
    /// and critically, it does not participate in batch-transition detection either. Default
    /// <c>true</c>. Collision-only shapes (<c>AARect</c>, <c>Circle</c>, <c>Line</c>, <c>Polygon</c>)
    /// default their own <c>IsVisible</c> to <c>false</c>, and every existing <see cref="Draw"/>
    /// implementation already early-returns on it — so before this member existed, an invisible
    /// shape sitting between two same-batch renderables still forced a real (wasted) batch
    /// Begin/End pair, because the engine only checked visibility inside <see cref="Draw"/>,
    /// after the batch-transition decision had already been made.
    /// </summary>
    bool IsVisible => true;

    /// <summary>
    /// Issues draw calls into <paramref name="spriteBatch"/>. Called by the engine inside
    /// the <see cref="Batch"/>'s <c>Begin</c>/<c>End</c> pair — implementations must not
    /// call <c>Begin</c>/<c>End</c> themselves.
    /// </summary>
    void Draw(SpriteBatch spriteBatch, Camera camera);
}
