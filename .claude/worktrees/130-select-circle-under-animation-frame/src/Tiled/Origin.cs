namespace FlatRedBall2.Tiled;

/// <summary>
/// Specifies which point on a tile object becomes the spawned entity's position.
/// Tile objects in Tiled occupy a rectangular area; the origin determines which
/// corner or edge-center maps to the entity's (X, Y).
/// </summary>
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
