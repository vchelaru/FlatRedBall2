using FlatRedBall2;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Graphics;

namespace ShipSample;

public class Shot : Entity
{
    private Sprite _sprite = null!;
    private float _lifetime = 2f;

    public override void CustomInitialize()
    {
        var texture = Engine.ContentManager.Load<Texture2D>("tile_0003");
        _sprite = new Sprite
        {
            Texture = texture,
            IsVisible = true,
        };
        AddChild(_sprite);
    }

    public override void CustomActivity(FrameTime time)
    {
        _lifetime -= time.DeltaSeconds;
        if (_lifetime <= 0f)
            Destroy();
    }

    public override void CustomDestroy() => _sprite.Destroy();
}
