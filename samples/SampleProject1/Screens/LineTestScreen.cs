using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using SampleProject1.Entities;
using Vec2 = System.Numerics.Vector2;

namespace SampleProject1.Screens;

/// <summary>
/// Tests line drawing, mouse-position reading, line-line collision, and
/// same-factory self-collision. Move the mouse to drag a line from the screen
/// center; it turns red whenever it intersects any static line. Static lines
/// that cross each other also turn red simultaneously.
/// </summary>
public class LineTestScreen : Screen
{
    private Factory<LineSegmentEntity> _mouseLineFactory = null!;
    private Factory<LineSegmentEntity> _staticLineFactory = null!;

    private LineSegmentEntity _mouseLine = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(15, 15, 30);

        _mouseLineFactory = new Factory<LineSegmentEntity>(this);
        _staticLineFactory = new Factory<LineSegmentEntity>(this);

        // Mouse line — starts at world origin (0, 0); endpoint updated each frame.
        _mouseLine = _mouseLineFactory.Create();
        _mouseLine.IdleColor = new Color(255, 220, 80, 220); // yellow so it reads as distinct

        // Static lines — two X-shaped crossing pairs in separate screen regions.
        // Neither pair passes through or near the origin, so only the mouse line touches (0,0).

        // Lower-left X — A and B cross at (-150, -150).
        var a = _staticLineFactory.Create();
        a.X = -250f; a.Y = -50f;
        a.EndPoint = new Vec2(200f, -200f); // world end: (-50, -250)

        var b = _staticLineFactory.Create();
        b.X = -250f; b.Y = -250f;
        b.EndPoint = new Vec2(200f, 200f); // world end: (-50, -50)

        // Upper-right X — C and D cross at (150, 200).
        var c = _staticLineFactory.Create();
        c.X = 50f; c.Y = 100f;
        c.EndPoint = new Vec2(200f, 200f); // world end: (250, 300)

        var d = _staticLineFactory.Create();
        d.X = 50f; d.Y = 300f;
        d.EndPoint = new Vec2(200f, -200f); // world end: (250, 100)

        // Collision: mouse line vs static lines.
        AddCollisionRelationship<LineSegmentEntity, LineSegmentEntity>(_mouseLineFactory, _staticLineFactory)
            .CollisionOccurred += (mouse, stat) =>
            {
                mouse.CollidingThisFrame = true;
                stat.CollidingThisFrame = true;
            };

        // Self-collision: static lines vs themselves.
        // Single-arg overload creates a SelfCollisionRelationship — each unordered pair checked once.
        AddCollisionRelationship(_staticLineFactory)
            .CollisionOccurred += (lineA, lineB) =>
            {
                lineA.CollidingThisFrame = true;
                lineB.CollidingThisFrame = true;
            };
    }

    public override void CustomActivity(FrameTime time)
    {
        // Update the mouse line's far endpoint to track the cursor each frame.
        // The entity sits at world origin (0, 0), so EndPoint == world cursor position.
        _mouseLine.EndPoint = Engine.Input.Cursor.WorldPosition;
    }

    public override void CustomDestroy()
    {
        _mouseLineFactory.DestroyAll();
        _staticLineFactory.DestroyAll();
    }
}
