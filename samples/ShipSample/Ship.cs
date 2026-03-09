using FlatRedBall2;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace ShipSample;

public class Ship : Entity
{
    private Sprite _sprite = null!;

    private const float RotateSpeed = 150f; // degrees per second
    private const float ThrustForce = 400f;
    private const float ShotSpeed = 450f;
    private const float ShotCooldown = 0.25f;

    private float _shotTimer;

    public override void CustomInitialize()
    {
        var texture = Engine.Content.Load<Texture2D>("ship_0001");
        _sprite = new Sprite
        {
            Texture = texture,
            TextureScale = 1.5f,
            IsVisible = true,
        };
        Add(_sprite);

        Drag = 3f;
    }

    public override void CustomActivity(FrameTime time)
    {
        var kb = Engine.Input.Keyboard;
        float dt = time.DeltaSeconds;

        // Rotate — left/right arrow keys (positive = clockwise on screen)
        if (kb.IsKeyDown(Keys.Left))
            Rotation = Rotation - Angle.FromDegrees(RotateSpeed * dt);
        if (kb.IsKeyDown(Keys.Right))
            Rotation = Rotation + Angle.FromDegrees(RotateSpeed * dt);

        var forward = Rotation.ToVector2();

        // Thrust — up arrow key; drag decelerates when released
        if (kb.IsKeyDown(Keys.Up))
        {
            AccelerationX = forward.X * ThrustForce;
            AccelerationY = forward.Y * ThrustForce;
        }
        else
        {
            AccelerationX = 0f;
            AccelerationY = 0f;
        }

        // Shoot — space bar
        _shotTimer -= dt;
        if (_shotTimer <= 0f && kb.IsKeyDown(Keys.Space))
        {
            var shot = Engine.GetFactory<Shot>().Create();
            shot.X = X;
            shot.Y = Y;
            shot.Rotation = Rotation;
            shot.VelocityX = forward.X * ShotSpeed;
            shot.VelocityY = forward.Y * ShotSpeed;
            _shotTimer = ShotCooldown;
        }
    }

    public override void CustomDestroy() => _sprite.Destroy();
}
