using System;
using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

// Issue #1405. Camera.SnappedSprites rounds a sprite's DRAW position to the camera's pixel grid;
// the simulated position is untouched. Everything here runs headless: the rounding is a pure
// function and Sprite.ComputeDrawPosition is the exact value Sprite.Draw hands to SpriteBatch.
public class CameraSpriteSnapTests
{
    private class TestScreen : Screen { }

    // 128x96 viewport, orthogonal height 96: one world unit is one pixel at Zoom 1.
    private static Camera MakeCamera(float zoom = 1f, float x = 0f, float y = 0f)
    {
        var camera = new Camera { X = x, Y = y, Zoom = zoom };
        camera.ApplyToHostRect(new Viewport(0, 0, 128, 96), orthogonalHeight: 96);
        return camera;
    }

    private static Sprite MakeSprite(float x, float y, float size = 16f) =>
        new() { X = x, Y = y, TextureScale = null, Width = size, Height = size };

    // ---------- the set ----------

    [Fact]
    public void SnappedSprites_AddRemoveContains_TracksMembership()
    {
        var camera = new Camera();
        var sprite = new Sprite();

        camera.SnappedSprites.Contains(sprite).ShouldBeFalse();
        camera.SnappedSprites.Add(sprite).ShouldBeTrue();
        camera.SnappedSprites.Contains(sprite).ShouldBeTrue();
        camera.SnappedSprites.Add(sprite).ShouldBeFalse();
        camera.SnappedSprites.Remove(sprite).ShouldBeTrue();
        camera.SnappedSprites.Contains(sprite).ShouldBeFalse();
    }

    [Fact]
    public void SnappedSprites_IsPerCamera()
    {
        var a = new Camera();
        var b = new Camera();
        var sprite = new Sprite();

        a.SnappedSprites.Add(sprite);

        b.SnappedSprites.Contains(sprite).ShouldBeFalse();
    }

    // ---------- cleanup ----------

    [Fact]
    public void ScreenRemove_SnappedSprite_DropsItFromEveryCamera()
    {
        var screen = new TestScreen();
        var second = new Camera();
        screen.Cameras.Add(second);
        var sprite = new Sprite();
        screen.Add(sprite);
        screen.Camera.SnappedSprites.Add(sprite);
        second.SnappedSprites.Add(sprite);

        screen.Remove(sprite);

        screen.Camera.SnappedSprites.Contains(sprite).ShouldBeFalse();
        second.SnappedSprites.Contains(sprite).ShouldBeFalse();
    }

    [Fact]
    public void ScreenRemove_OtherSprite_LeavesSnappedSpriteAlone()
    {
        var screen = new TestScreen();
        var snapped = new Sprite();
        var other = new Sprite();
        screen.Add(snapped);
        screen.Add(other);
        screen.Camera.SnappedSprites.Add(snapped);

        screen.Remove(other);

        screen.Camera.SnappedSprites.Contains(snapped).ShouldBeTrue();
    }

    // Adds its sprite to the camera in CustomInitialize (once per instance) and again in Reset()
    // (every pooled recycle), the pattern the XML docs prescribe.
    private class SnappedEntity : Entity
    {
        public Sprite Sprite { get; } = new Sprite();
        public override void CustomInitialize()
        {
            Add(Sprite);
            Engine.CurrentScreen.Camera.SnappedSprites.Add(Sprite);
        }
        protected override void Reset() => Engine.CurrentScreen.Camera.SnappedSprites.Add(Sprite);
    }

    private class SnapTestScreen : Screen
    {
        public Factory<SnappedEntity> Spawner { get; private set; } = null!;
        public override void CustomInitialize() => Spawner = new Factory<SnappedEntity>(this);
    }

    private static SnapTestScreen StartScreen(bool pooled = false)
    {
        var engine = new FlatRedBallService();
        engine.Start<SnapTestScreen>();
        var screen = (SnapTestScreen)engine.CurrentScreen;
        if (pooled) screen.Spawner.EnablePooling();
        return screen;
    }

    [Fact]
    public void EntityDestroy_SnappedSprite_IsRemovedFromCamera()
    {
        var screen = StartScreen();
        var entity = screen.Spawner.Create();
        screen.Camera.SnappedSprites.Contains(entity.Sprite).ShouldBeTrue();

        entity.Destroy();

        screen.Camera.SnappedSprites.Contains(entity.Sprite).ShouldBeFalse();
    }

    [Fact]
    public void PooledEntityReturn_SnappedSprite_IsRemovedFromCamera()
    {
        var screen = StartScreen(pooled: true);
        var entity = screen.Spawner.Create();
        screen.Camera.SnappedSprites.Contains(entity.Sprite).ShouldBeTrue();

        entity.Destroy();

        screen.Camera.SnappedSprites.Contains(entity.Sprite).ShouldBeFalse();
    }

    [Fact]
    public void PooledEntityRecycle_ResetReAddsSnappedSprite()
    {
        var screen = StartScreen(pooled: true);
        var first = screen.Spawner.Create();
        first.Destroy();

        var second = screen.Spawner.Create();

        second.ShouldBeSameAs(first);
        screen.Camera.SnappedSprites.Contains(second.Sprite).ShouldBeTrue();
    }

    // ---------- the rounding function ----------

    [Theory]
    // zoom 1: 1 world unit = 1 pixel, 16px sprite
    [InlineData(1f, 3.4f, 3f)]
    [InlineData(1f, 3.6f, 4f)]
    [InlineData(1f, -3.4f, -3f)]
    [InlineData(1f, -3.6f, -4f)]
    [InlineData(1f, 0f, 0f)]
    [InlineData(1f, 7f, 7f)]
    // half-pixel boundary rounds toward +screen X (half up)
    [InlineData(1f, 3.5f, 4f)]
    [InlineData(1f, -3.5f, -3f)]
    // zoom 2: grid is 0.5 world units
    [InlineData(2f, 3.1f, 3f)]
    [InlineData(2f, 3.3f, 3.5f)]
    [InlineData(2f, -3.1f, -3f)]
    [InlineData(2f, -3.3f, -3.5f)]
    // zoom 0.5: grid is 2 world units
    [InlineData(0.5f, 3.4f, 4f)]
    [InlineData(0.5f, 2.9f, 2f)]
    [InlineData(0.5f, -2.9f, -2f)]
    [InlineData(0.5f, -3.4f, -4f)]
    public void SnapWorldPosition_X_RoundsToPixelGrid(float zoom, float worldX, float expected)
    {
        var camera = MakeCamera(zoom);

        var snapped = camera.SnapWorldPosition(new Vector2(worldX, 0f), 16f, 16f, 0f);

        snapped.X.ShouldBe(expected, tolerance: 1e-4f);
    }

    [Theory]
    [InlineData(1f, 3.4f, 3f)]
    [InlineData(1f, 2.6f, 3f)]
    [InlineData(1f, -3.4f, -3f)]
    // Y is flipped on screen, so the half-pixel boundary rounds the opposite way in world Y
    [InlineData(1f, 3.5f, 3f)]
    [InlineData(1f, -3.5f, -4f)]
    [InlineData(2f, 3.3f, 3.5f)]
    [InlineData(0.5f, 2.9f, 2f)]
    public void SnapWorldPosition_Y_RoundsToPixelGrid(float zoom, float worldY, float expected)
    {
        var camera = MakeCamera(zoom);

        var snapped = camera.SnapWorldPosition(new Vector2(0f, worldY), 16f, 16f, 0f);

        snapped.Y.ShouldBe(expected, tolerance: 1e-4f);
    }

    [Fact]
    public void SnapWorldPosition_AlreadyOnGrid_IsUnchanged()
    {
        var camera = MakeCamera(zoom: 2f);

        var snapped = camera.SnapWorldPosition(new Vector2(10.5f, -7f), 16f, 16f, 0f);

        snapped.X.ShouldBe(10.5f, tolerance: 1e-4f);
        snapped.Y.ShouldBe(-7f, tolerance: 1e-4f);
    }

    [Fact]
    public void SnapWorldPosition_OddPixelWidth_AlignsLeftEdgeNotCenter()
    {
        // A 15px sprite centered on a whole pixel has its edges on half pixels (blurry/shimmering).
        // Aligning the left edge puts the center on a half pixel instead.
        var camera = MakeCamera();

        var snapped = camera.SnapWorldPosition(new Vector2(3.4f, 0f), 15f, 16f, 0f);

        snapped.X.ShouldBe(3.5f, tolerance: 1e-4f);
    }

    [Fact]
    public void SnapWorldPosition_QuarterTurnSwapsWhichSizeDecidesTheEdge()
    {
        // 15 wide x 16 tall rotated 90 degrees draws 16 wide x 15 tall.
        var camera = MakeCamera();

        var snapped = camera.SnapWorldPosition(new Vector2(3.4f, 3.4f), 15f, 16f, MathF.PI / 2f);

        snapped.X.ShouldBe(3f, tolerance: 1e-3f);
        snapped.Y.ShouldBe(3.5f, tolerance: 1e-3f);
    }

    [Fact]
    public void SnapWorldPosition_CameraOffGrid_SnapsRelativeToScreenNotWorldOrigin()
    {
        // The pixel grid is the screen's, so with the camera at x=0.3 a sprite lands on screen
        // pixel edges at world x = 0.3 + whole numbers.
        var camera = MakeCamera(x: 0.3f);

        var snapped = camera.SnapWorldPosition(new Vector2(3.4f, 0f), 16f, 16f, 0f);

        snapped.X.ShouldBe(3.3f, tolerance: 1e-4f);
    }

    [Fact]
    public void SnapWorldPosition_ResultLandsOnWholeScreenPixel()
    {
        var camera = MakeCamera(zoom: 1.5f, x: 12.34f, y: -5.67f);
        var matrix = camera.GetTransformMatrix();

        var snapped = camera.SnapWorldPosition(new Vector2(40.123f, 17.987f), 16f, 12f, 0f);

        // Left edge = center - half width (in screen space), top edge likewise.
        var center = Vector2.Transform(snapped, matrix);
        float scaleX = camera.Viewport.Width / (float)camera.OrthogonalWidth * camera.Zoom;
        float scaleY = camera.Viewport.Height / (float)camera.OrthogonalHeight * camera.Zoom;
        float left = center.X - 16f * scaleX / 2f;
        float top = center.Y - 12f * scaleY / 2f;
        left.ShouldBe(MathF.Round(left), tolerance: 1e-2f);
        top.ShouldBe(MathF.Round(top), tolerance: 1e-2f);
    }

    [Fact]
    public void SnapWorldPosition_NoViewport_ReturnsInputUnchanged()
    {
        var camera = new Camera();

        var snapped = camera.SnapWorldPosition(new Vector2(3.4f, 1.1f), 16f, 16f, 0f);

        snapped.ShouldBe(new Vector2(3.4f, 1.1f));
    }

    // ---------- Sprite draw position ----------

    [Fact]
    public void ComputeDrawPosition_SnappedSprite_RoundsDrawPositionButNotSimulatedPosition()
    {
        var camera = MakeCamera();
        var sprite = MakeSprite(3.4f, 2.6f);
        camera.SnappedSprites.Add(sprite);

        var position = sprite.ComputeDrawPosition(camera);

        position.X.ShouldBe(3f, tolerance: 1e-4f);
        position.Y.ShouldBe(3f, tolerance: 1e-4f);
        sprite.X.ShouldBe(3.4f);
        sprite.Y.ShouldBe(2.6f);
    }

    [Fact]
    public void ComputeDrawPosition_UnsnappedSprite_KeepsSubPixelPosition()
    {
        var camera = MakeCamera();
        var sprite = MakeSprite(3.4f, 2.6f);

        var position = sprite.ComputeDrawPosition(camera);

        position.ShouldBe(new Vector2(3.4f, 2.6f));
    }

    [Fact]
    public void ComputeDrawPosition_SnappedOnOtherCameraOnly_IsNotSnappedForThisCamera()
    {
        var snapping = MakeCamera();
        var plain = MakeCamera();
        var sprite = MakeSprite(3.4f, 2.6f);
        snapping.SnappedSprites.Add(sprite);

        sprite.ComputeDrawPosition(plain).ShouldBe(new Vector2(3.4f, 2.6f));
    }

    [Fact]
    public void ComputeDrawPosition_ScreenSpaceBatch_IsNeverSnapped()
    {
        var camera = MakeCamera();
        var sprite = MakeSprite(3.4f, 2.6f);
        sprite.Batch = ScreenSpaceBatch.Instance;
        camera.SnappedSprites.Add(sprite);

        sprite.ComputeDrawPosition(camera).ShouldBe(new Vector2(3.4f, 2.6f));
    }

    [Fact]
    public void ComputeDrawPosition_WorldSpaceAddColorBatch_IsSnapped()
    {
        var camera = MakeCamera();
        var sprite = MakeSprite(3.4f, 2.6f);
        sprite.AddColor = new Vector3(0.2f, 0f, 0f);   // swaps Batch to WorldSpaceAddColorBatch
        camera.SnappedSprites.Add(sprite);

        sprite.ComputeDrawPosition(camera).X.ShouldBe(3f, tolerance: 1e-4f);
    }

    [Fact]
    public void ComputeDrawPosition_AttachedToRotatedParent_SnapsTheComposedWorldPosition()
    {
        var camera = MakeCamera();
        var entity = new Entity { X = 10.4f, Y = 0f, Rotation = Angle.FromDegrees(90) };
        var sprite = MakeSprite(3.4f, 0f);   // local +X of a 90-degree parent is world +Y
        entity.Add(sprite);
        camera.SnappedSprites.Add(sprite);

        var position = sprite.ComputeDrawPosition(camera);

        // 16x16 sprite, so the quarter turn does not change the extent parity.
        position.X.ShouldBe(10f, tolerance: 1e-3f);
        position.Y.ShouldBe(3f, tolerance: 1e-3f);
    }
}
