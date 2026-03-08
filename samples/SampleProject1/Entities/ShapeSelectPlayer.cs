using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Input;
using FlatRedBall2.Movement;
using Microsoft.Xna.Framework.Input;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SampleProject1.Entities;

public class ShapeSelectPlayer : Entity
{
    private readonly TopDownBehavior _topDown = new();

    // Public so the screen can reference them in WithFirstShape / WithSecondShape selectors.
    public AxisAlignedRectangle CollisionRect = null!;
    public Circle BodyCircle = null!;

    public override void CustomInitialize()
    {
        // Large body circle — visual only. Does NOT stop at walls.
        // Radius 40 → diameter 80px, which is wider than the 64px passage.
        BodyCircle = new Circle
        {
            Radius = 40f,
            IsFilled = false,
            OutlineThickness = 2f,
            Color = new XnaColor(80, 200, 255, 200),
            IsVisible = true,
        };
        Add(BodyCircle, isDefaultCollision: false);

        // Small collision rect — this is what WithFirstShape wires to the wall relationship.
        // Width 20px fits cleanly through the 64px passage; the body circle (80px) does not.
        CollisionRect = new AxisAlignedRectangle
        {
            Width = 20f,
            Height = 20f,
            IsFilled = true,
            Color = new XnaColor(255, 220, 60, 230),
            IsVisible = true,
        };
        Add(CollisionRect, isDefaultCollision: false);

        _topDown.MovementValues = new TopDownValues
        {
            MaxSpeed = 180f,
            AccelerationTime = 0.08f,
            DecelerationTime = 0.06f,
            UsesAcceleration = true,
        };

        var keyboard = Engine.InputManager.Keyboard;
        _topDown.MovementInput = new KeyboardInput2D(keyboard, Keys.Left, Keys.Right, Keys.Up, Keys.Down).Or(
            new KeyboardInput2D(keyboard, Keys.A, Keys.D, Keys.W, Keys.S));
    }

    public override void CustomActivity(FrameTime time)
    {
        _topDown.Update(this, time);
    }
}
