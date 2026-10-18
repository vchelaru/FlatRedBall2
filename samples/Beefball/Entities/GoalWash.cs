using FlatRedBall2;
using Gum.GueDeriving;
using Gum.Managers;
using Microsoft.Xna.Framework;
using RenderingLibrary.Graphics;

namespace Beefball.Entities;

/// <summary>
/// Soft radial light that spills out of a goal when it scores. One Gum circle with a radial gradient
/// (a single smooth shape; stacking many large translucent engine shapes left scanline streaks).
/// </summary>
public class GoalWash : Entity
{
    public const float Radius = 330f;

    private CircleRuntime _circle = null!;

    public override void CustomInitialize()
    {
        Z = -80f;
        _circle = new CircleRuntime
        {
            Width = Radius * 2f,
            Height = Radius * 2f,
            XOrigin = HorizontalAlignment.Center,
            YOrigin = VerticalAlignment.Center,
            IsFilled = true,
            StrokeWidth = 0f,
            UseGradient = true,
            GradientType = GradientType.Radial,
            GradientX1 = Radius,
            GradientY1 = Radius,
            GradientInnerRadius = 0f,
            GradientOuterRadius = Radius,
            Visible = false,
        };
        Add(_circle);
    }

    /// <summary>Peak alpha 0..1 at the center, fading to nothing at the rim. 0 hides it.</summary>
    public void Set(Color color, float peakAlpha)
    {
        _circle.Visible = peakAlpha > 0.001f;
        _circle.FillColor = Visuals.WithAlpha(color, peakAlpha);
        _circle.Color2 = Visuals.WithAlpha(color, 0f);
    }
}
