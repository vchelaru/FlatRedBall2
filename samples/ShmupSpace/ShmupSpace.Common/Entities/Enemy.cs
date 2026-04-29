using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using ShmupSpace.Screens;

namespace ShmupSpace.Entities;

public class Enemy : Entity
{
    private Sprite _sprite = null!;
    private AARect _body = null!;

    private float _zigTimer;

    public override void CustomInitialize()
    {
        var screen = (GameScreen)Engine.CurrentScreen;

        _sprite = new Sprite { AnimationChains = screen.Animations };
        Add(_sprite);
        _sprite.PlayAnimation("EnemyClam");

        _body = new AARect
        {
            Width = 14f,
            Height = 14f,
            IsVisible = false,
        };
        Add(_body);

        var enemy = screen.Config.Enemy;
        VelocityY = enemy.FallSpeed;
        VelocityX = Engine.Random.NextSingle() < 0.5f ? -enemy.ZigSpeed : enemy.ZigSpeed;
        _zigTimer = enemy.ZigInterval;
    }

    public override void CustomActivity(FrameTime time)
    {
        var enemy = ((GameScreen)Engine.CurrentScreen).Config.Enemy;

        _zigTimer -= time.DeltaSeconds;
        if (_zigTimer <= 0f)
        {
            // Re-apply magnitude from config so mid-flight tuning lands on live enemies.
            VelocityX = VelocityX < 0f ? enemy.ZigSpeed : -enemy.ZigSpeed;
            _zigTimer = enemy.ZigInterval;
        }

        if (Y < Engine.CurrentScreen.Camera.Bottom - 16f)
            Destroy();
    }
}
