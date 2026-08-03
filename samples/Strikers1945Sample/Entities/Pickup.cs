using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Vector2 = System.Numerics.Vector2;

namespace Strikers1945Sample.Entities;

public enum PickupType { Power, Bomb, Medal }

public class Pickup : Entity
{
    /// <summary>
    /// Set each frame by the screen so pickups can magnetically attract toward the player.
    /// </summary>
    public static Vector2 PlayerPosition;

    private const float AttractionRadius = 120f;
    private const float AttractionSpeed = 300f;

    private Sprite _sprite = null!;
    public Circle CollisionCircle { get; private set; } = null!;
    public PickupType Type { get; private set; }

    private float _bobTimer;

    public override void CustomInitialize()
    {
        CollisionCircle = new Circle
        {
            Radius = 36,
            Visible = false,
        };
        Add(CollisionCircle);
    }

    public void Configure(PickupType type)
    {
        Type = type;
        var texName = type switch
        {
            PickupType.Power => "tile_0006",
            PickupType.Bomb => "tile_0004",
            PickupType.Medal => "tile_0007",
            _ => "tile_0007",
        };

        var texture = Engine.ContentManager.Load<Texture2D>(texName);
        _sprite = new Sprite
        {
            Texture = texture,
            TextureScale = 2f,
        };
        Add(_sprite);

        // Drift downward slowly
        VelocityY = -40f;
    }

    public override void CustomActivity(FrameTime time)
    {
        // Gentle bob effect
        _bobTimer += time.DeltaSeconds * 4f;
        if (_sprite != null)
        {
            _sprite.X = MathF.Sin(_bobTimer) * 3f;

            // Pulsing scale for visibility
            _sprite.TextureScale = 2.5f + 0.5f * MathF.Sin((float)Environment.TickCount / 200f);

            // Pulse color between white and yellow
            float t = (1f + MathF.Sin((float)Environment.TickCount / 200f)) * 0.5f;
            _sprite.Color = Color.Lerp(Color.White, Color.Yellow, t);
        }

        // Magnetic attraction toward player
        float dx = PlayerPosition.X - X;
        float dy = PlayerPosition.Y - Y;
        float dist = MathF.Sqrt(dx * dx + dy * dy);
        if (dist < AttractionRadius && dist > 0.001f)
        {
            float strength = (AttractionRadius - dist) / AttractionRadius;
            float speed = AttractionSpeed * strength;
            VelocityX = dx / dist * speed;
            VelocityY = dy / dist * speed;
        }

        // Off-screen cleanup
        if (Y < -(Engine.CurrentScreen.Camera.TargetHeight / 2f + 30f))
            Destroy();
    }

    /// <summary>
    /// Returns the medal score value based on Y position on screen.
    /// Top 25% = 2000, Middle 50% = 1000, Bottom 25% = 200.
    /// </summary>
    public int GetMedalScore()
    {
        float halfH = Engine.CurrentScreen.Camera.TargetHeight / 2f;
        float normalizedY = (Y + halfH) / (halfH * 2f); // 0 = bottom, 1 = top
        if (normalizedY >= 0.75f) return 2000;
        if (normalizedY >= 0.25f) return 1000;
        return 200;
    }

    public override void CustomDestroy()
    {
        _sprite?.Destroy();
        CollisionCircle.Destroy();
    }
}
