using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Strikers1945Sample.Entities;

public class EnemyBullet : Entity
{
    /// <summary>
    /// Multiplier applied to all enemy bullet velocities. Set per level for difficulty scaling.
    /// </summary>
    public static float SpeedMultiplier = 1f;

    private Sprite _sprite = null!;
    public Circle CollisionCircle { get; private set; } = null!;
    public bool HasBeenGrazed { get; set; }

    public override void CustomInitialize()
    {
        var texture = Engine.ContentManager.Load<Texture2D>("tile_0002");
        _sprite = new Sprite
        {
            Texture = texture,
            TextureScale = 1.8f,
            Color = new Color(255, 100, 100), // red tint to distinguish from player bullets
        };
        Add(_sprite);

        CollisionCircle = new Circle
        {
            Radius = 5,
            Visible = false,
        };
        Add(CollisionCircle);
    }

    public override void CustomActivity(FrameTime time)
    {
        var halfH = Engine.CurrentScreen.Camera.TargetHeight / 2f;
        var halfW = Engine.CurrentScreen.Camera.TargetWidth / 2f;
        if (Y < -(halfH + 30f) || Y > halfH + 30f ||
            X < -(halfW + 30f) || X > halfW + 30f)
            Destroy();
    }

    public override void CustomDestroy()
    {
        _sprite.Destroy();
        CollisionCircle.Destroy();
    }
}
