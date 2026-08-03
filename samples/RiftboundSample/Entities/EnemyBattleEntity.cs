using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using RiftboundSample.Models;

namespace RiftboundSample.Entities;

public class EnemyBattleEntity : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;
    public CombatantState State { get; set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 28,
            Height = 28,
            Color = new Color(200, 60, 60),
            IsVisible = true,
        };
        Add(Rectangle);
    }
}
