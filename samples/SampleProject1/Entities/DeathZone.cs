using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace SampleProject1.Entities;

/// <summary>
/// Invisible trigger region below the paddle. Ball-vs-DeathZone fires the life-lost logic.
/// </summary>
public class DeathZone : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 2000f,
            Height = 80f,
            Visible = false,
        };
        Add(Rectangle);
    }
}
