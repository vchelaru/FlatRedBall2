using System.Numerics;
using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SampleProject1.Entities;

public class LineSegmentEntity : Entity
{
    private Line _line = null!;

    public static readonly XnaColor DefaultColor  = new XnaColor(100, 180, 255, 220);
    public static readonly XnaColor CollisionColor = new XnaColor(255, 70, 50, 255);

    /// <summary>
    /// Tint used when not colliding. Defaults to <see cref="DefaultColor"/>.
    /// Override after <see cref="Entity.CustomInitialize"/> to give this entity a distinct idle look.
    /// </summary>
    public XnaColor IdleColor { get; set; } = DefaultColor;

    /// <summary>
    /// Set to <c>true</c> by a collision event; cleared at the start of the next
    /// <see cref="CustomActivity"/> after the color has been applied.
    /// </summary>
    public bool CollidingThisFrame { get; set; }

    /// <summary>
    /// Offset from the entity's origin to the second endpoint of the line segment.
    /// Since <see cref="Entity.X"/>/<see cref="Entity.Y"/> is the first endpoint,
    /// the segment in world space runs from <c>(AbsoluteX, AbsoluteY)</c>
    /// to <c>(AbsoluteX + EndPoint.X, AbsoluteY + EndPoint.Y)</c>.
    /// </summary>
    public Vector2 EndPoint
    {
        get => _line.EndPoint;
        set => _line.EndPoint = value;
    }

    public override void CustomInitialize()
    {
        _line = new Line
        {
            Visible = true,
            Color = DefaultColor,
            LineThickness = 1f,
        };
        Add(_line);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Apply collision color first, then reset the flag so it is clear for the next frame.
        _line.Color = CollidingThisFrame ? CollisionColor : IdleColor;
        CollidingThisFrame = false;
    }

    public override void CustomDestroy() => _line.Destroy();
}
