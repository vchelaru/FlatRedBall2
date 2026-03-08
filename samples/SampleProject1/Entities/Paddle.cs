using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SampleProject1.Entities;

public class Paddle : Entity
{
    public const float PaddleWidth = 120f;
    public const float PaddleHeight = 14f;

    // Paddle X is clamped to this range so it stays within the walled play area.
    private const float HalfPlayWidth = 580f;
    private const float Speed = 600f;

    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    private IKeyboard _keyboard = null!;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = PaddleWidth,
            Height = PaddleHeight,
            Color = new Color(180, 220, 255),
            IsVisible = true,
        };
        Add(Rectangle);

        _keyboard = Engine!.InputManager.Keyboard;
    }

    public override void CustomActivity(FrameTime time)
    {
        float input = 0f;
        if (_keyboard.IsKeyDown(Keys.Left) || _keyboard.IsKeyDown(Keys.A)) input -= 1f;
        if (_keyboard.IsKeyDown(Keys.Right) || _keyboard.IsKeyDown(Keys.D)) input += 1f;

        // Direct position control — bypasses physics intentionally for snappy feel
        X += input * Speed * time.DeltaSeconds;
        X = Math.Clamp(X, -HalfPlayWidth, HalfPlayWidth);

        // Keep velocity at zero so PhysicsUpdate doesn't double-move the paddle
        VelocityX = 0f;
    }
}
