using System.Numerics;
using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SampleProject1.Entities;

/// <summary>
/// Draws a line from the player to the mouse cursor, stopping at the first tile it hits.
/// A circle is shown at the impact point when a tile is struck.
/// </summary>
public class SightLine : Entity
{
    private Line _line = null!;
    private Circle _hitCircle = null!;

    public TopDownPlayer Player { get; set; } = null!;
    public TileShapeCollection Tiles { get; set; } = null!;

    public override void CustomInitialize()
    {
        _line = new Line
        {
            Color = new XnaColor(255, 220, 60, 200),
            LineThickness = 2f,
            Visible = true,
        };
        Add(_line);

        _hitCircle = new Circle
        {
            Radius = 6f,
            Color = new XnaColor(255, 80, 80, 220),
            IsFilled = true,
            Visible = false,
        };
        Add(_hitCircle);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Player == null || Tiles == null) return;

        var mouseWorld = Engine.InputManager.Cursor.WorldPosition;
        var playerPos = new Vector2(Player.AbsoluteX, Player.AbsoluteY);

        // AbsolutePoint1 = (entity.X + line.X, entity.Y + line.Y) = (0 + playerPos.X, 0 + playerPos.Y)
        _line.X = playerPos.X;
        _line.Y = playerPos.Y;

        if (Tiles.Raycast(playerPos, mouseWorld, out Vector2 hitPoint, out _))
        {
            _line.SetAbsoluteEndpoint(hitPoint);
            _hitCircle.X = hitPoint.X;
            _hitCircle.Y = hitPoint.Y;
            _hitCircle.Visible = true;
        }
        else
        {
            _line.SetAbsoluteEndpoint(mouseWorld);
            _hitCircle.Visible = false;
        }
    }
}
