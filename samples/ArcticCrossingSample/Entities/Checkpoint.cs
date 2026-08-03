using FlatRedBall2;
using FlatRedBall2.Collision;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Entities;

public class Checkpoint : Entity
{
    public AxisAlignedRectangle TriggerZone { get; private set; } = null!;

    // Visual parts (non-collision)
    private AxisAlignedRectangle _pole = null!;
    private AxisAlignedRectangle _flag = null!;

    public bool IsActivated { get; private set; }
    public int Index { get; set; }

    private static readonly XnaColor InactiveFlag = new(180, 180, 180, 255);
    private static readonly XnaColor ActiveFlag = new(50, 255, 100, 255);
    private static readonly XnaColor PoleColor = new(140, 100, 70, 255);

    public override void CustomInitialize()
    {
        // Invisible trigger zone for collision
        TriggerZone = new AxisAlignedRectangle
        {
            Width = 40f,
            Height = 60f,
            IsVisible = false,
        };
        Add(TriggerZone);

        // Pole (visual only)
        _pole = new AxisAlignedRectangle
        {
            Width = 4f,
            Height = 50f,
            IsVisible = true,
            IsFilled = true,
            Color = PoleColor,
            Y = 25f,
        };
        Add(_pole, isDefaultCollision: false);

        // Flag (visual only)
        _flag = new AxisAlignedRectangle
        {
            Width = 20f,
            Height = 14f,
            IsVisible = true,
            IsFilled = true,
            Color = InactiveFlag,
            X = 12f,
            Y = 42f,
        };
        Add(_flag, isDefaultCollision: false);
    }

    public void Activate()
    {
        if (IsActivated) return;
        IsActivated = true;
        _flag.Color = ActiveFlag;
    }

    public void Reset()
    {
        IsActivated = false;
        _flag.Color = InactiveFlag;
    }
}
