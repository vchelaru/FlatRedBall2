using FlatRedBall2;
using FlatRedBall2.Rendering;
using ShmupSpace.Screens;

namespace ShmupSpace.Entities;

public class Explosion : Entity
{
    private Sprite _sprite = null!;

    public override void CustomInitialize()
    {
        _sprite = new Sprite
        {
            AnimationChains = ((GameScreen)Engine.CurrentScreen).Animations,
            IsLooping = false,
        };
        Add(_sprite);
        _sprite.PlayAnimation("Explosion");
        _sprite.AnimationFinished += Destroy;
    }
}
