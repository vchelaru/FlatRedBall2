using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace ZombieSample.Entities;

/// <summary>
/// The destination the player must reach to win. Rendered as a bright gold rectangle.
/// </summary>
public class GoalZone : Entity
{
    private static readonly Color GoalColor = new(255, 200, 0, 200);

    private AxisAlignedRectangle _rect = null!;

    public override void CustomInitialize()
    {
        _rect = new AxisAlignedRectangle
        {
            Width   = 80f,
            Height  = 80f,
            Color   = GoalColor,
            IsFilled = true,
            IsVisible  = true,
        };
        Add(_rect);
    }

    public override void CustomDestroy()
    {
        _rect.Destroy();
    }
}
