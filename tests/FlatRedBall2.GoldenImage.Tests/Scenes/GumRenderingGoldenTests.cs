using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using FlatRedBall2.Tests;
using Gum.DataTypes;
using Gum.GueDeriving;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Xunit;
using HorizontalAlignment = RenderingLibrary.Graphics.HorizontalAlignment;
using VerticalAlignment = RenderingLibrary.Graphics.VerticalAlignment;

namespace FlatRedBall2.GoldenImage.Tests.Scenes;

/// <summary>
/// Gum drawn through the engine's real boot, so these cover the Gum paths the headless suite can only
/// check by logic. Scenes use solid colors only: text goldens would shift with Gum or KernSmith font
/// changes.
/// </summary>
[Collection(GraphicsDeviceCollection.Name)]
public class GumRenderingGoldenTests
{
    private readonly GraphicsDeviceFixture _fixture;

    public GumRenderingGoldenTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    private static readonly Color MarkerRed = new(220, 50, 50);
    private static readonly Color MarkerGreen = new(60, 200, 90);
    private static readonly Color Slate = new(150, 160, 175);

    private static ColoredRectangleRuntime Rect(float x, float y, float width, float height, Color color) =>
        new() { X = x, Y = y, Width = width, Height = height, Color = color };

    // #1510: a Gum IsRenderTarget container is baked offscreen, then a sprite composites the result.
    [GoldenFact]
    public void Draw_SpriteReferencingARenderTargetContainer_ShowsTheBakedContainer()
    {
        var device = GoldenDevice.Require(_fixture);
        using var gum = GoldenGum.Boot(_fixture);

        gum.Engine.Start<RenderTargetScreen>();
        var image = OffscreenRenderer.RenderCameras(device, gum.Engine.CurrentScreen, width: 128, height: 96);

        GoldenImage.Verify("gum-render-target-sprite", image);
    }

    // #1499: an entity-attached visual rotates with its entity, counterclockwise-positive like a Sprite.
    [GoldenFact]
    public void Draw_EntityAttachedVisuals_RotateWithTheirEntity()
    {
        var device = GoldenDevice.Require(_fixture);
        using var gum = GoldenGum.Boot(_fixture);

        gum.Engine.Start<EntityRotationScreen>();
        var image = OffscreenRenderer.RenderCameras(device, gum.Engine.CurrentScreen, width: 128, height: 96);

        GoldenImage.Verify("gum-entity-visual-rotation", image);
    }

    // #1500/#1503: camera HUD is zoom-coupled (anchors to the visible canvas), a screen-space layer is not.
    [GoldenTheory]
    [InlineData(2f, "gum-hud-zoom-in")]
    [InlineData(0.5f, "gum-hud-zoom-out")]
    public void Draw_CameraHudAtNonUnitZoom_AnchorsToTheVisibleCanvas(float zoom, string goldenName)
    {
        var device = GoldenDevice.Require(_fixture);
        using var gum = GoldenGum.Boot(_fixture);

        gum.Engine.Start<HudZoomScreen>(s =>
        {
            s.Texture = SceneArt.Quadrants(device);
            s.Zoom = zoom;
        });
        var image = OffscreenRenderer.RenderCameras(device, gum.Engine.CurrentScreen, width: 128, height: 96);

        GoldenImage.Verify(goldenName, image);
    }

    private sealed class RenderTargetScreen : Screen
    {
        public override void CustomInitialize()
        {
            // The baker only collects visible roots, so the (invisible) source container lives under a
            // visible one, the way a Gum project nests it.
            var root = new ContainerRuntime { Width = 128, Height = 96 };

            var source = new ContainerRuntime { X = 0, Y = 0, Width = 32, Height = 32, IsRenderTarget = true, Visible = false };
            source.AddChild(Rect(0, 0, 32, 32, Slate));
            source.AddChild(Rect(0, 0, 12, 12, MarkerRed));       // top-left marker
            source.AddChild(Rect(24, 24, 8, 8, MarkerGreen));     // bottom-right marker
            root.AddChild(source);

            root.AddChild(Reference(source, x: 8, y: 8, width: 32, height: 32));   // 1:1
            root.AddChild(Reference(source, x: 52, y: 24, width: 64, height: 32)); // stretched wide

            Add(root);
        }

        private static SpriteRuntime Reference(ContainerRuntime source, float x, float y, float width, float height) =>
            new()
            {
                X = x,
                Y = y,
                WidthUnits = DimensionUnitType.Absolute,
                HeightUnits = DimensionUnitType.Absolute,
                Width = width,
                Height = height,
                RenderTargetTextureSource = source,
            };
    }

    private sealed class EntityRotationScreen : Screen
    {
        public override void CustomInitialize()
        {
            var factory = new Factory<RotatingBadge>(this);
            Spawn(factory, x: -40, degrees: 0);    // control
            Spawn(factory, x: 0, degrees: 90);     // marker moves to the bottom-left
            Spawn(factory, x: 40, degrees: 180);   // marker moves to the bottom-right
        }

        private static void Spawn(Factory<RotatingBadge> factory, float x, float degrees)
        {
            var badge = factory.Create();
            badge.X = x;
            badge.Rotation = Angle.FromDegrees(degrees);
        }
    }

    private sealed class RotatingBadge : Entity
    {
        public override void CustomInitialize()
        {
            var visual = new ContainerRuntime
            {
                Width = 28,
                Height = 14,
                XOrigin = HorizontalAlignment.Center,
                YOrigin = VerticalAlignment.Center,
            };
            visual.AddChild(Rect(0, 0, 28, 14, Slate));
            visual.AddChild(Rect(0, 0, 8, 8, MarkerRed));         // top-left marker makes the angle readable
            Add(visual);
        }
    }

    private sealed class HudZoomScreen : Screen
    {
        public Microsoft.Xna.Framework.Graphics.Texture2D? Texture { get; set; }

        public float Zoom { get; set; } = 1f;

        public override void CustomInitialize()
        {
            // Set here, not in the Start callback: the engine applies camera settings between the two.
            Camera.Zoom = Zoom;

            // Reference in the world: a 16x16 sprite at the origin, scaled by the camera zoom.
            Add(new Sprite { Texture = Texture, TextureScale = 1f });

            // Zoom-coupled HUD: corner anchors follow the visible canvas (OrthogonalSize / Zoom).
            var topLeft = Rect(0, 0, 16, 16, MarkerRed);
            topLeft.Anchor(Anchor.TopLeft);
            topLeft.X = 4;
            topLeft.Y = 4;
            Add(topLeft);

            var bottomRight = Rect(0, 0, 16, 16, MarkerGreen);
            bottomRight.Anchor(Anchor.BottomRight);
            bottomRight.X = -4;
            bottomRight.Y = -4;
            Add(bottomRight);

            // Screen-space layer: immune to zoom, so it keeps the same pixel size at every zoom.
            var bar = Rect(0, 0, 40, 6, Color.Yellow);
            bar.Anchor(Anchor.Top);
            bar.Y = 4;
            Camera.Add(bar, new Layer("hud") { IsScreenSpace = true });
        }
    }
}
