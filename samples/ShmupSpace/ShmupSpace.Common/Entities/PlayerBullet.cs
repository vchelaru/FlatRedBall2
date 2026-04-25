using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using ShmupSpace.Screens;

namespace ShmupSpace.Entities;

public class PlayerBullet : Entity
{
    private Sprite _sprite = null!;
    private AxisAlignedRectangle _body = null!;

    public override void CustomInitialize()
    {
        _sprite = new Sprite { AnimationChains = ((GameScreen)Engine.CurrentScreen).Animations };
        Add(_sprite);
        _sprite.PlayAnimation("PlayerShot1");

        _body = new AxisAlignedRectangle
        {
            Width = 6f,
            Height = 14f,
            IsVisible = false,
        };
        Add(_body);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Y > Engine.CurrentScreen.Camera.Top + 16f)
            Destroy();
    }
}
