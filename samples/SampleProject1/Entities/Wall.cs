using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace SampleProject1.Entities;

/// <summary>
/// Invisible boundary wall used for ball bounce collision.
/// Configure Rectangle.Width/Height and X/Y after Create().
/// </summary>
public class Wall : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 80f,
            Height = 1000f,
            Color = Color.Transparent,
            Visible = false,
        };
        AddChild(Rectangle);
    }
}
