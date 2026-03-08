using System.Collections.Generic;
using System.Numerics;
using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SampleProject1.Entities;

/// <summary>
/// Draws a line from the player to the mouse cursor, stopping at the first obstacle hit.
/// Supports both <see cref="TileShapeCollection"/> tiles and a list of <see cref="PolygonObstacle"/>
/// entities — set whichever source applies. When both are set, the closest hit wins.
/// A circle is shown at the impact point when any obstacle is struck.
/// </summary>
public class SightLine : Entity
{
    private Line _line = null!;
    private Circle _hitCircle = null!;

    public TopDownPlayer Player { get; set; } = null!;

    /// <summary>Tile-based collision source. Leave null when using polygon obstacles.</summary>
    public TileShapeCollection? Tiles { get; set; }

    /// <summary>Polygon obstacle source. Leave null when using tiles.</summary>
    public IEnumerable<PolygonObstacle>? Obstacles { get; set; }

    public override void CustomInitialize()
    {
        _line = new Line
        {
            Color = new XnaColor(255, 220, 60, 200),
            LineThickness = 2f,
            IsVisible = true,
        };
        Add(_line);

        _hitCircle = new Circle
        {
            Radius = 6f,
            Color = new XnaColor(255, 80, 80, 220),
            IsFilled = true,
            IsVisible = false,
        };
        Add(_hitCircle);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Player == null) return;

        var mouseWorld = Engine.InputManager.Cursor.WorldPosition;
        var playerPos = new Vector2(Player.AbsoluteX, Player.AbsoluteY);

        _line.X = playerPos.X;
        _line.Y = playerPos.Y;

        Vector2? hitPoint = FindClosestHit(playerPos, mouseWorld);

        if (hitPoint.HasValue)
        {
            _line.SetAbsoluteEndpoint(hitPoint.Value);
            _hitCircle.X = hitPoint.Value.X;
            _hitCircle.Y = hitPoint.Value.Y;
            _hitCircle.IsVisible = true;
        }
        else
        {
            _line.SetAbsoluteEndpoint(mouseWorld);
            _hitCircle.IsVisible = false;
        }
    }

    private Vector2? FindClosestHit(Vector2 start, Vector2 end)
    {
        Vector2? closest = null;
        float closestDistSq = float.MaxValue;

        if (Tiles != null && Tiles.Raycast(start, end, out Vector2 tileHit, out _))
        {
            closest = tileHit;
            closestDistSq = (tileHit - start).LengthSquared();
        }

        if (Obstacles != null)
        {
            foreach (var obstacle in Obstacles)
            {
                if (obstacle.Polygon.Raycast(start, end, out Vector2 polyHit, out _))
                {
                    float distSq = (polyHit - start).LengthSquared();
                    if (distSq < closestDistSq)
                    {
                        closestDistSq = distSq;
                        closest = polyHit;
                    }
                }
            }
        }

        return closest;
    }
}
