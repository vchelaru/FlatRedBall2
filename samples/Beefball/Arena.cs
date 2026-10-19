namespace Beefball;

/// <summary>Fixed arena geometry in world units (Y+ up, centered on the origin).</summary>
public static class Arena
{
    public const float HalfWidth = 400f;
    public const float HalfHeight = 300f;
    public const float GoalHalfHeight = 100f;
    public const float PocketDepth = 60f;
    /// <summary>Thickness of the invisible collision slabs. Thick enough that nothing tunnels through.</summary>
    public const float WallThickness = 40f;

    /// <summary>Design resolution. Extra headroom above the arena holds the HUD.</summary>
    public const int ResolutionWidth = 1000;
    public const int ResolutionHeight = 740;

    /// <summary>Solid slabs (center x, center y, width, height) that bounce players and puck.</summary>
    public static IEnumerable<(float X, float Y, float W, float H)> SolidWalls()
    {
        const float t = WallThickness;
        float fullWidth = 2f * (HalfWidth + PocketDepth + t);
        yield return (0f, HalfHeight + t / 2f, fullWidth, t);
        yield return (0f, -HalfHeight - t / 2f, fullWidth, t);

        foreach (float side in new[] { -1f, 1f })
        {
            float sideLength = HalfHeight + t - GoalHalfHeight;
            float sideCenter = GoalHalfHeight + sideLength / 2f;
            // Wall above and below the goal mouth.
            yield return (side * (HalfWidth + t / 2f), sideCenter, t, sideLength);
            yield return (side * (HalfWidth + t / 2f), -sideCenter, t, sideLength);
            // Pocket roof and floor, and its back wall.
            float pocketWidth = PocketDepth + t;
            float pocketCenterX = side * (HalfWidth + pocketWidth / 2f);
            yield return (pocketCenterX, GoalHalfHeight + t / 2f, pocketWidth, t);
            yield return (pocketCenterX, -GoalHalfHeight - t / 2f, pocketWidth, t);
            yield return (side * (HalfWidth + PocketDepth + t / 2f), 0f, t, 2f * (GoalHalfHeight + t));
        }
    }

    /// <summary>Slabs that keep players (not the puck) out of the goal pockets.</summary>
    public static IEnumerable<(float X, float Y, float W, float H)> GoalBlockers()
    {
        const float thickness = 20f;
        foreach (float side in new[] { -1f, 1f })
            yield return (side * (HalfWidth + thickness / 2f), 0f, thickness, 2f * GoalHalfHeight);
    }

    /// <summary>
    /// The visible neon wall segments (x1, y1, x2, y2), horizontal or vertical: top and bottom runs,
    /// the wall pieces either side of each goal mouth, and the three sides of each goal pocket.
    /// </summary>
    public static IEnumerable<(float X1, float Y1, float X2, float Y2)> NeonLines()
    {
        yield return (-HalfWidth, HalfHeight, HalfWidth, HalfHeight);
        yield return (-HalfWidth, -HalfHeight, HalfWidth, -HalfHeight);
        foreach (float side in new[] { -1f, 1f })
        {
            float w = side * HalfWidth;
            float back = side * (HalfWidth + PocketDepth);
            yield return (w, GoalHalfHeight, w, HalfHeight);
            yield return (w, -HalfHeight, w, -GoalHalfHeight);
            yield return (w, GoalHalfHeight, back, GoalHalfHeight);
            yield return (w, -GoalHalfHeight, back, -GoalHalfHeight);
            yield return (back, -GoalHalfHeight, back, GoalHalfHeight);
        }
    }
}
