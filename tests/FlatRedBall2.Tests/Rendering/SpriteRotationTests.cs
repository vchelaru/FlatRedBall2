using System;
using FlatRedBall2.Collision;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;
using NVec2 = System.Numerics.Vector2;
using XVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tests.Rendering;

// Issue #378. We can't create a GraphicsDevice headlessly (no display), so we can't read back
// pixels — but we don't need to. SpriteBatch computes every vertex on the CPU as
//     pos + (dx*cos(r) - dy*sin(r),  dx*sin(r) + dy*cos(r))
// and then multiplies by the Begin() matrix; the GPU only rasterizes those vertices. We run that
// exact formula with the rotation the engine actually passes (Sprite.RenderRotationRadians) and
// the engine's real camera matrix, then compare against the Polygon, whose CCW rotation is proven
// in PolygonTests.Rotation_PositiveAngle_RotatesCounterclockwise.
//
// Screen space is Y+ down, so a feature on the right (+X) that rotates COUNTERCLOCKWISE must move
// UP, i.e. to a SMALLER screen Y than the viewport center.
public class SpriteRotationTests
{
    static Camera MakeCamera()
    {
        var camera = new Camera();
        camera.SetViewport(new Viewport(0, 0, 1280, 720)); // center screen Y = 360
        return camera;
    }

    // Where the engine's SpriteBatch call places a point at sprite-local offset (dx, dy).
    // dy = 0 sits on the vertical-flip axis, so SpriteEffects.FlipVertically does not move it.
    static float SpriteScreenY(Sprite sprite, float dx, float dy, Camera camera)
    {
        float r = sprite.RenderRotationRadians;
        var preMatrix = new XVec2(dx * MathF.Cos(r) - dy * MathF.Sin(r),
                                  dx * MathF.Sin(r) + dy * MathF.Cos(r));
        return XVec2.Transform(preMatrix, camera.GetTransformMatrix()).Y;
    }

    // Issue #991: a sprite's local offset must rotate with the parent's AbsoluteRotation
    // (lever-arm orbit), not stay at a fixed world offset.
    [Fact]
    public void AbsoluteX_ParentRotated_OrbitsAroundParent()
    {
        var parentRotation = Angle.FromDegrees(90f);
        float childX = 10f, childY = 0f;
        float expectedAbsoluteX = 0f;
        float expectedAbsoluteY = 10f;

        var parent = new Entity { Rotation = parentRotation };
        var sprite = new Sprite { X = childX, Y = childY };
        parent.Add(sprite);

        sprite.AbsoluteX.ShouldBe(expectedAbsoluteX, tolerance: 0.001f);
        sprite.AbsoluteY.ShouldBe(expectedAbsoluteY, tolerance: 0.001f);
    }

    [Fact]
    public void Sprite_PositiveRotation_RotatesCounterclockwise_LikePolygon()
    {
        var camera = MakeCamera();
        float center = camera.WorldToScreen(NVec2.Zero).Y; // 360

        // POLYGON reference: a +X point rotated +90° in world (CCW) lands at world (0, 1); project it.
        float polygonScreenY = camera.WorldToScreen(new NVec2(0f, 1f)).Y;

        // SPRITE: same +90°, same +X feature, via the engine's actual render rotation.
        var sprite = new Sprite { Rotation = Angle.FromDegrees(90f) };
        float spriteScreenY = SpriteScreenY(sprite, dx: 1f, dy: 0f, camera);

        polygonScreenY.ShouldBeLessThan(center); // polygon goes UP (CCW) — the correct reference
        spriteScreenY.ShouldBeLessThan(center);  // sprite must match — fails today: it goes DOWN (CW)
    }
}
