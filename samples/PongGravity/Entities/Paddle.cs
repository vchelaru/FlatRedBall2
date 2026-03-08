using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace PongGravity.Entities;

public class Paddle : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    private Keys _upKey;
    private Keys _downKey;

    private const float HalfHeight = 60f;
    private const float FieldHalfHeight = 320f;

    public void SetKeys(Keys up, Keys down)
    {
        _upKey = up;
        _downKey = down;
    }

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 18f,
            Height = HalfHeight * 2f,
            IsVisible = true,
            Color = new Color(100, 200, 255, 255),
        };
        Add(Rectangle);
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.InputManager.Keyboard;
        const float Speed = 350f;

        if (kb.IsKeyDown(_upKey))
            VelocityY = Speed;
        else if (kb.IsKeyDown(_downKey))
            VelocityY = -Speed;
        else
            VelocityY = 0f;

        // Clamp to field
        float maxY = FieldHalfHeight - HalfHeight;
        if (Y > maxY) { Y = maxY; VelocityY = 0f; }
        if (Y < -maxY) { Y = -maxY; VelocityY = 0f; }
    }
}
