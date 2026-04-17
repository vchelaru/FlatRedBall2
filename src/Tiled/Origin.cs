namespace FlatRedBall2.Tiled;

/// <summary>
/// Specifies which point on a tile object becomes the spawned entity's position.
/// Tile objects in Tiled occupy a rectangular area; the origin determines which
/// corner or edge-center maps to the entity's (X, Y).
/// </summary>
public enum Origin
{
    Center,
    BottomCenter,
    TopCenter,
    BottomLeft,
    TopLeft,
    BottomRight,
    TopRight,
}
