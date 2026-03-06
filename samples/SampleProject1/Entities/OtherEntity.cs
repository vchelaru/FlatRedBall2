using FlatRedBall2;
using FlatRedBall2.Collision;
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

        // Solid body circle at the base — participates in default collision
        var body = new Circle
        {
            Radius = 18f,
            Y = 0f,
            Color = new Color(220, 130, 60, 180),
            Visible = true,
        };
        Add(body);

        // Outlined circle offset upward — attached for visual purposes only
        var range = new Circle
        {
            Radius = 28f,
            Y = 22f,
            IsFilled = false,
            OutlineThickness = 1.5f,
            Color = new Color(255, 200, 140, 100),
            Visible = true,
        };
        Add(range, isDefaultCollision: false);
    }
}
