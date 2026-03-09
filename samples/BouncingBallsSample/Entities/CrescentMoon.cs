using FlatRedBall2;
using FlatRedBall2.Collision;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Vec2 = System.Numerics.Vector2;

namespace BouncingBallsSample.Entities;

public class CrescentMoon : Entity
{
    public Polygon Polygon { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Polygon = Polygon.FromPoints(BuildCrescentPoints(outerRadius: 100f, innerCenterX: -60f, arcSteps: 16));
        Polygon.IsVisible = true;
        Polygon.Color = new Color(255, 220, 80, 210);
        Polygon.IsFilled = true;
        Add(Polygon);
    }

    // Builds a C-shaped crescent (concave polygon) ~200px tall.
    //
    // Outer arc: right semicircle of a circle with radius outerRadius — goes from
    //   (0, -outerRadius) → (outerRadius, 0) → (0, outerRadius), the convex "front".
    //
    // Inner arc: the concave "bite" — a shorter arc on a larger circle whose center is to
    //   the LEFT (innerCenterX < 0). This arc also connects the two tips but peaks at a
    //   smaller x than the outer arc, creating the crescent hollow.
    //   With innerCenterX = -60 the inner arc peaks at x ≈ 57 vs the outer peak at x = 100.
    //
    // Both arcs share endpoints at (0, ±outerRadius). The resulting polygon is concave:
    // at each tip the interior angle exceeds 180°, which exercises the Hertel-Mehlhorn
    // convex decomposition used by the collision system.
    private static IEnumerable<Vec2> BuildCrescentPoints(float outerRadius, float innerCenterX, int arcSteps)
    {
        // Outer arc: angles -π/2 → +π/2 (CCW in Y-up space, sweeping through x = outerRadius)
        for (int i = 0; i <= arcSteps; i++)
        {
            float t = i / (float)arcSteps;
            float angle = MathF.PI * (t - 0.5f); // -π/2 to +π/2
            yield return new Vec2(outerRadius * MathF.Cos(angle), outerRadius * MathF.Sin(angle));
        }

        // Inner arc: CW from top tip to bottom tip, passing through the inner-circle's rightmost point.
        // The inner circle is centered at (innerCenterX, 0) with radius chosen so it passes through the tips.
        float r = MathF.Sqrt(innerCenterX * innerCenterX + outerRadius * outerRadius);
        float startAngle = MathF.Atan2(outerRadius, -innerCenterX);   // angle from inner center to top tip
        float endAngle   = MathF.Atan2(-outerRadius, -innerCenterX);  // angle from inner center to bottom tip
        // startAngle > endAngle (e.g. ~+59° → ~−59°) so interpolation goes CW through 0°.
        for (int i = 1; i < arcSteps; i++) // skip i=0 and i=arcSteps to avoid duplicating the tips
        {
            float t = i / (float)arcSteps;
            float angle = startAngle + (endAngle - startAngle) * t;
            yield return new Vec2(innerCenterX + r * MathF.Cos(angle), r * MathF.Sin(angle));
        }
    }
}
