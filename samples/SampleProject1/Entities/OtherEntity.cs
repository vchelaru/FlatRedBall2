using FlatRedBall2;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;

namespace SampleProject1.Entities;

public class OtherEntity : Entity
{
    public override void CustomInitialize()
    {
        var sprite = new Sprite
        {
            Texture = Engine.ContentManager.CreateSolidColor(32, 32, new Color(220, 130, 60)),
            TextureScale = 2f,
            Y = 16f,
        };
        Add(sprite);
    }
}
