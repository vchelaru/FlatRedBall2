using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Strikers1945Sample.Entities;

public class PlayerBullet : Entity
{
    private Sprite _sprite = null!;
    public AxisAlignedRectangle CollisionRect { get; private set; } = null!;

    public override void CustomInitialize()
    {
        var texture = Engine.ContentManager.Load<Texture2D>("tile_0000");
        _sprite = new Sprite
        {
            Texture = texture,
            TextureScale = 2f, // 16px * 2 = 32px tall bullet
        };
        Add(_sprite);

        CollisionRect = new AxisAlignedRectangle
        {
            Width = 6,
            Height = 20,
            Visible = false,
        };
        Add(CollisionRect);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Y > Engine.CurrentScreen.Camera.TargetHeight / 2f + 30f)
            Destroy();
    }

    public override void CustomDestroy()
    {
        _sprite.Destroy();
        CollisionRect.Destroy();
    }
}
