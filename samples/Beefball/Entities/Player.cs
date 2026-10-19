using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Vector2 = System.Numerics.Vector2;

namespace Beefball.Entities;

public class Player : Entity
{
    // Set through Factory.Create(configure) so they exist before CustomInitialize.
    public int Team { get; set; } = 1;
    public BeefballConfig Config { get; set; } = null!;

    /// <summary>False freezes the player: no acceleration, no dash.</summary>
    public bool InputEnabled { get; set; } = true;

    /// <summary>Seconds until the next dash is allowed. 0 means ready.</summary>
    public float CooldownRemaining { get; set; }

    public Circle Body { get; private set; } = null!;
    public Color TeamColor => Config.Palette.TeamColor(Team);

    private Circle _cooldownIndicator = null!;
    private I2DInput _movement = null!;
    private IPressableInput _dash = null!;
    private IGamepad _pad = null!;
    private I2DInput _padStick = null!;
    private IPressableInput _padDash = null!;

    public override void CustomInitialize()
    {
        var p = Config.Player;
        var palette = Config.Palette;
        Drag = p.Drag;

        Visuals.AddRadialGlow(this, Config.Glow.Player, Config.Glow.Strength, TeamColor, p.Radius);

        Body = new Circle
        {
            Radius = p.Radius, IsFilled = true, IsVisible = true,
            Color = palette.PlayerBody, OutlineThickness = 0f,
        };
        Add(Body);

        Add(new Circle
        {
            Radius = p.Radius, IsFilled = false, IsVisible = true,
            OutlineThickness = 3f, Color = TeamColor, Z = 0.1f,
        }, isDefaultCollision: false);

        _cooldownIndicator = new Circle
        {
            Radius = 0f, IsFilled = true, IsVisible = false,
            Color = Visuals.WithAlpha(TeamColor, 0.55f), Z = 0.2f,
        };
        Add(_cooldownIndicator, isDefaultCollision: false);

        Add(new Circle
        {
            Radius = 2.5f, IsFilled = true, IsVisible = true,
            Color = Color.White, Z = 0.3f,
        }, isDefaultCollision: false);

        var kb = Engine.Input.Keyboard;
        _movement = Team == 1
            ? new KeyboardInput2D(kb, Keys.A, Keys.D, Keys.W, Keys.S)
            : new KeyboardInput2D(kb, Keys.Left, Keys.Right, Keys.Up, Keys.Down);
        _dash = new KeyboardPressableInput(kb, Team == 1 ? Keys.B : Keys.RightShift);

        _pad = Engine.Input.GetGamepad(Team - 1);
        _padStick = new GamepadInput2D(_pad, GamepadAxis.LeftStickX, GamepadAxis.LeftStickY);
        _padDash = new GamepadPressableInput(_pad, Buttons.A);
    }

    public override void CustomActivity(FrameTime time)
    {
        var p = Config.Player;

        if (CooldownRemaining > 0f)
            CooldownRemaining = Math.Max(0f, CooldownRemaining - time.DeltaSeconds);

        // Indicator grows from nothing to its full size as the cooldown runs out.
        _cooldownIndicator.IsVisible = CooldownRemaining > 0f;
        if (_cooldownIndicator.IsVisible)
        {
            float elapsed = 1f - CooldownRemaining / Math.Max(0.0001f, p.DashCooldownSeconds);
            _cooldownIndicator.Radius = p.CooldownIndicatorRadius * elapsed;
        }

        if (!InputEnabled)
        {
            Acceleration = Vector2.Zero;
            return;
        }

        // A connected pad owns the player; otherwise the keyboard does.
        bool usePad = _pad.IsConnected;
        I2DInput source = usePad ? _padStick : _movement;
        IPressableInput dashInput = usePad ? _padDash : _dash;

        var direction = new Vector2(source.X, source.Y);
        if (direction.LengthSquared() > 1f)
            direction = Vector2.Normalize(direction);

        AccelerationX = direction.X * p.Acceleration;
        AccelerationY = direction.Y * p.Acceleration;

        if (dashInput.WasJustPressed && CooldownRemaining <= 0f && direction.LengthSquared() > 0.0001f)
        {
            direction = Vector2.Normalize(direction);
            VelocityX = direction.X * p.DashSpeed;
            VelocityY = direction.Y * p.DashSpeed;
            CooldownRemaining = p.DashCooldownSeconds;
        }
    }
}
