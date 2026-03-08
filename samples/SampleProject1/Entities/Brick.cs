using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace SampleProject1.Entities;

public class Brick : Entity
{
    public const float BrickWidth = 80f;
    public const float BrickHeight = 20f;

    // Color indexed by hits remaining (index 0 unused; brick is destroyed at 0)
    private static readonly Color[] ColorsByHits =
    {
        Color.Transparent,
        new Color(70, 200, 70),    // 1 hit — green (easy)
        new Color(70, 120, 220),   // 2 hits — blue
        new Color(220, 140, 40),   // 3 hits — orange
        new Color(220, 55, 55),    // 4 hits — red (toughest)
    };

    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    /// <summary>Hit points remaining. Set after Create(), then call UpdateColor().</summary>
    public int HitsRemaining { get; set; } = 1;

    /// <summary>Score value for each hit, before multipliers.</summary>
    public int HitValue => HitsRemaining;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = BrickWidth,
            Height = BrickHeight,
            IsVisible = true,
        };
        Add(Rectangle);
        UpdateColor();
    }

    /// <summary>Updates the brick's color to reflect current HitsRemaining.</summary>
    public void UpdateColor()
    {
        int index = Math.Clamp(HitsRemaining, 0, ColorsByHits.Length - 1);
        Rectangle.Color = ColorsByHits[index];
    }

    /// <summary>Decrements hits. Destroys the brick when hits reach zero.</summary>
    public void TakeHit()
    {
        HitsRemaining--;
        if (HitsRemaining <= 0)
            Destroy();
        else
            UpdateColor();
    }
}
