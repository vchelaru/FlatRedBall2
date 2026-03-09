using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace SampleProject1.Entities;

public class ShipEntity : Entity
{
    private readonly TopDownBehavior _topDown = new();

    public override void CustomInitialize()
    {
        var sprite = new Sprite
        {
            Texture = Engine.Content.CreateSolidColor(16, 16, new Color(80, 160, 230)),
            TextureScale = 3f,
            Y = 8f,
            Alpha= .5f
        };
        Add(sprite);

        // Solid body circle at the base — participates in default collision
        var body = new Circle
        {
            Radius = 14f,
            Y = 0f,
            Color = new Color(80, 160, 230, 180),
            IsVisible = true,
        };
        Add(body);

        // Outlined circle offset upward — attached for visual purposes only
        var range = new Circle
        {
            Radius = 22f,
            Y = 18f,
            IsFilled = false,
            OutlineThickness = 1.5f,
            Color = new Color(180, 220, 255, 100),
            IsVisible = true,
        };
        Add(range, isDefaultCollision: false);

        _topDown.MovementValues = new TopDownValues
        {
            MaxSpeed = 200f,
            UsesAcceleration = true,
            AccelerationTime = 0.1f,
            DecelerationTime = 0.08f,
        };

        var keyboard = Engine.Input.Keyboard;
        _topDown.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down)
            .Or(new KeyboardInput2D(keyboard, Keys.A, Keys.D, Keys.W, Keys.S));
    }

    public override void CustomActivity(FrameTime time)
    {
        _topDown.Update(this, time);
    }
}
