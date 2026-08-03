using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using RiftboundSample.Models;

namespace RiftboundSample.Entities;

public class CharacterBattleEntity : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;
    public CombatantState State { get; set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 24,
            Height = 32,
            Color = new Color(60, 100, 220),
            IsVisible = true,
        };
        Add(Rectangle);
    }

    /// <summary>Call after setting State to update the visual color based on row position.</summary>
    public void ApplyRowColor()
    {
        if (State == null) return;
        Rectangle.Color = State.Row == RowPosition.Back
            ? new Color(80, 200, 220)
            : new Color(60, 100, 220);
    }
}
