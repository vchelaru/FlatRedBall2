using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SpaceInvadersSample.Entities;

public class Player : Entity
{
    public AxisAlignedRectangle Rectangle { get; private set; } = null!;

    private IKeyboard _keyboard = null!;
    private KeyboardPressableInput _shootInput = null!;
    private float _shootCooldown;

    public override void CustomInitialize()
    {
        Rectangle = new AxisAlignedRectangle
        {
            Width = 32f,
            Height = 20f,
            IsVisible = true,
            Color = new XnaColor(80, 220, 80, 255),
        };
        Add(Rectangle);

        _keyboard = Engine.InputManager.Keyboard;
        _shootInput = new KeyboardPressableInput(_keyboard, Keys.Space);
    }

    public override void CustomActivity(FrameTime time)
    {
        float vx = 0f;
        if (_keyboard.IsKeyDown(Keys.Left) || _keyboard.IsKeyDown(Keys.A))
            vx -= 250f;
        if (_keyboard.IsKeyDown(Keys.Right) || _keyboard.IsKeyDown(Keys.D))
            vx += 250f;
        VelocityX = vx;

        // Clamp after physics already moved us
        X = Math.Clamp(X, -584f, 584f);

        _shootCooldown -= time.DeltaSeconds;
        if (_shootInput.WasJustPressed && _shootCooldown <= 0f)
        {
            _shootCooldown = 0.35f;
            var bullet = Engine.GetFactory<PlayerBullet>().Create();
            bullet.X = X;
            bullet.Y = Y + 20f;
        }
    }
}
