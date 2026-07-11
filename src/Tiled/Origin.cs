namespace FlatRedBall2.Tiled;

/// <summary>
/// Specifies which point on a tile object becomes the spawned entity's position.
/// Tile objects in Tiled occupy a rectangular area; the origin determines which
/// corner or edge-center maps to the entity's (X, Y).
/// </summary>
/// <remarks>
/// This only controls the entity's <c>X</c>/<c>Y</c> — it has no effect on how any child
/// <c>Sprite</c> renders. A <c>Sprite</c> always draws centered on its entity, so
/// <see cref="Center"/> is the only value where the entity position and the sprite's visual
/// center coincide. For any other origin, the sprite's on-screen bounds are offset by half the
/// tile's width/height from what the entity position alone suggests. To make a non-<see
/// cref="Center"/> origin match the sprite's visual bounds (e.g. so <see cref="BottomLeft"/>
/// puts the sprite's visible bottom-left at the tile object's bottom-left), offset the sprite
/// in the entity's <c>CustomInitialize</c> once its texture is known — for
/// <see cref="BottomLeft"/>: <c>sprite.X = texture.Width / 2f; sprite.Y = texture.Height / 2f;</c>.
/// </remarks>
public enum Origin
{
    /// <summary>The center of the tile rectangle.</summary>
    Center,
    /// <summary>The middle of the bottom edge.</summary>
    BottomCenter,
    /// <summary>The middle of the top edge.</summary>
    TopCenter,
    /// <summary>The bottom-left corner.</summary>
    BottomLeft,
    /// <summary>The top-left corner.</summary>
    TopLeft,
    /// <summary>The bottom-right corner.</summary>
    BottomRight,
    /// <summary>The top-right corner.</summary>
    TopRight,
}
