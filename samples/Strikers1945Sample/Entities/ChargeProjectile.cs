using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Strikers1945Sample.Entities;

/// <summary>
/// Powerful projectile spawned by charge attack release.
/// P-38 Fork Lightning: two converging streams.
/// </summary>
public class ChargeProjectile : Entity
{
    private Sprite _sprite = null!;
    public Circle CollisionCircle { get; private set; } = null!;

    private int _damage = 3;
    public int Damage => _damage;

    public override void CustomInitialize()
    {
        var texture = Engine.ContentManager.Load<Texture2D>("tile_0000");
        _sprite = new Sprite
        {
            Texture = texture,
            TextureScale = 3f,
            Color = new Color(100, 200, 255), // bright blue tint for charge shots
        };
        Add(_sprite);

        CollisionCircle = new Circle
        {
            Radius = 10,
            Visible = false,
        };
        Add(CollisionCircle);
    }

    public override void CustomActivity(FrameTime time)
    {
        var halfH = Engine.CurrentScreen.Camera.TargetHeight / 2f;
        var halfW = Engine.CurrentScreen.Camera.TargetWidth / 2f;
        if (Y > halfH + 40f || Y < -(halfH + 40f) ||
            X > halfW + 40f || X < -(halfW + 40f))
            Destroy();
    }

    public override void CustomDestroy()
    {
        _sprite.Destroy();
        CollisionCircle.Destroy();
    }
}
