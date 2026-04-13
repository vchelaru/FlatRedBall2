using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SampleProject1.Entities;

public class PlatformerPlayer : Entity
{
    private readonly PlatformerBehavior _platformer = new();
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 28,
            Height = 36,
            Color = new Color(80, 180, 255, 220),
            IsVisible = true,
        };
        Add(Rectangle);

        var groundValues = new PlatformerValues
        {
            MaxSpeedX = 220f,
            Gravity = 900f,
            MaxFallSpeed = 600f,
            JumpVelocity = 420f,
            JumpApplyLength = TimeSpan.FromSeconds(0.18),
            JumpApplyByButtonHold = true,
        };

        _platformer.GroundMovement = groundValues;
        _platformer.AirMovement = groundValues;

        var keyboard = Engine.Input.Keyboard;
        _platformer.JumpInput = new KeyboardPressableInput(keyboard, Keys.Space)
            .Or(new KeyboardPressableInput(keyboard, Keys.Up));
        _platformer.MovementInput = new KeyboardInput2D(
            keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down);
    }

    public override void CustomActivity(FrameTime time)
    {
        _platformer.Update(this, time);
    }
}
