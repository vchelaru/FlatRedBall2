using FlatRedBall2;
using FlatRedBall2.Animation;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Input;
using ShmupSpace.Screens;

namespace ShmupSpace.Entities;

public class PlayerShip : Entity
{
    private Sprite _ship = null!;
    private Sprite _booster = null!;
    private AxisAlignedRectangle _body = null!;

    private readonly TopDownBehavior _topDown = new();
    public TopDownBehavior TopDown => _topDown;

    private IPressableInput _fire = null!;

    private float _fireCooldown;

    public override void CustomInitialize()
    {
        // Animations live once per screen; grab the cached list rather than re-parsing the .achx.
        var screen = (GameScreen)Engine.CurrentScreen;
        var animations = screen.Animations;

        _booster = new Sprite { AnimationChains = animations };
        Add(_booster);
        _booster.PlayAnimation("ShipBoosterWeak");

        _ship = new Sprite { AnimationChains = animations };
        Add(_ship);
        _ship.PlayAnimation("ShipStraight");

        _body = new AxisAlignedRectangle
        {
            Width = 12f,
            Height = 12f,
            IsVisible = false,
        };
        Add(_body);

        var keyboard = Engine.Input.Keyboard;
        _topDown.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down)
            .Or(new KeyboardInput2D(keyboard, Keys.A, Keys.D, Keys.W, Keys.S));
        screen.PlayerTopDownConfig.ApplyTo(_topDown);

        _fire = new KeyboardPressableInput(keyboard, Keys.Space);
    }

    public override void CustomActivity(FrameTime time)
    {
        _topDown.Update(this, time);

        // Update ship animation from horizontal input direction.
        string chain = _topDown.MovementInput!.X switch
        {
            < 0f => "ShipTurnLeft",
            > 0f => "ShipTurnRight",
            _ => "ShipStraight",
        };
        if (_ship.CurrentAnimation?.Name != chain)
            _ship.PlayAnimation(chain);

        // Booster plume intensifies when thrusting up.
        string boosterChain = _topDown.MovementInput!.Y > 0f ? "ShipBoosterStrong" : "ShipBoosterWeak";
        if (_booster.CurrentAnimation?.Name != boosterChain)
            _booster.PlayAnimation(boosterChain);

        // Clamp to camera-visible bounds so the ship can't leave the play area.
        var config = ((GameScreen)Engine.CurrentScreen).Config;
        var cam = Engine.CurrentScreen.Camera;
        float m = config.Player.EdgeMargin;
        if (X < cam.Left + m) { X = cam.Left + m; VelocityX = 0f; }
        if (X > cam.Right - m) { X = cam.Right - m; VelocityX = 0f; }
        if (Y < cam.Bottom + m) { Y = cam.Bottom + m; VelocityY = 0f; }
        if (Y > cam.Top - m) { Y = cam.Top - m; VelocityY = 0f; }

        _fireCooldown -= time.DeltaSeconds;
        if (_fireCooldown <= 0f && _fire.IsDown)
        {
            var bullet = Engine.GetFactory<PlayerBullet>().Create();
            bullet.X = X;
            bullet.Y = Y + 8f;
            bullet.VelocityY = config.Player.BulletSpeed;
            _fireCooldown = config.Player.FireInterval;
        }
    }
}
