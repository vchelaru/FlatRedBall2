using FlatRedBall2.Rendering;
using FlatRedBall2.Tests;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.GoldenImage.Tests.Scenes;

// Issue #1405. Camera.SnappedSprites rounds a sprite's draw position to the camera's screen pixel grid.
// At an integer scale with Point sampling the rasterizer already rounds an edge to the nearest pixel, so
// these scenes use zoom 1.2 and 1.6 (non-integer, and no texel boundary ever lands on a pixel center):
// there a sprite's texels cover 1 or 2 pixels depending on the sub-pixel phase of its left/top edge, so
// the white 3x3-texel marker in SceneArt.Quadrants measures 4 pixels when the edge sits on a pixel
// boundary (zoom 1.2: 3 texels = 3.6 px) but 3 pixels when the edge sits at a fraction of .55-.9.
// Each scene asserts the marker's exact pixel rectangle, hand-computed below, so a wrong golden cannot
// enshrine wrong output.
[Collection(GraphicsDeviceCollection.Name)]
public class CameraSpriteSnapGoldenTests
{
    private const int Width = 128;
    private const int Height = 96;

    private readonly GraphicsDeviceFixture _fixture;

    public CameraSpriteSnapGoldenTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    private static Screen NewScreen() => new() { Engine = new FlatRedBallService() };

    // 16x16 units; the quadrant texture is 16x16 texels, so TextureScale 1 is one texel per unit.
    private static Sprite Make(Microsoft.Xna.Framework.Graphics.Texture2D texture, float x, float y) =>
        new() { Texture = texture, X = x, Y = y };

    // Bounding box of the pure-white marker pixels inside the search rectangle (inclusive, image pixels).
    private static Rectangle WhiteBounds(PixelImage image, int left, int top, int right, int bottom)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
        for (int y = top; y <= bottom; y++)
        {
            for (int x = left; x <= right; x++)
            {
                if (image[x, y] != Color.White) continue;
                minX = global::System.Math.Min(minX, x); maxX = global::System.Math.Max(maxX, x);
                minY = global::System.Math.Min(minY, y); maxY = global::System.Math.Max(maxY, y);
            }
        }
        maxX.ShouldBeGreaterThanOrEqualTo(minX, "no white marker pixels found in the search area");
        return new Rectangle(minX, minY, maxX - minX + 1, maxY - minY + 1);
    }

    [GoldenFact]
    public void Draw_SnappedAndUnsnappedSpriteAtSubPixelPosition_MatchesGolden()
    {
        // Camera at the origin, zoom 1.2, full 128x96 viewport: 1.2 px per unit, viewport center (64, 48).
        // Left edge px = 64 + 1.2x - 9.6, top edge px = 48 - 1.2y - 9.6 (sprite half size is 8 units = 9.6 px).
        var device = GoldenDevice.Require(_fixture);
        var texture = SceneArt.Quadrants(device);
        var screen = NewScreen();
        screen.Camera.Zoom = 1.2f;

        // Snapped at (-26.4, 6.4): edge (22.72, 30.72) rounds to (23, 31).
        var snapped = Make(texture, -26.4f, 6.4f);
        screen.Add(snapped);
        screen.Camera.SnappedSprites.Add(snapped);

        // Same sub-pixel phase, not snapped, at (3.6, 6.4): edge (58.72, 30.72).
        screen.Add(Make(texture, 3.6f, 6.4f));

        var image = OffscreenRenderer.RenderCameras(device, screen, Width, Height);

        // Snapped: edge on a pixel boundary, pixel centers 23.5..26.5 are inside the 3.6 px marker -> 4x4.
        WhiteBounds(image, 15, 25, 45, 45).ShouldBe(new Rectangle(23, 31, 4, 4));
        // Unsnapped: pixels start at 59 (center 59.5 >= 58.72), centers 59.5..61.5 < 62.32 -> 3x3.
        WhiteBounds(image, 50, 25, 80, 45).ShouldBe(new Rectangle(59, 31, 3, 3));

        GoldenImage.Verify("camera-sprite-snap-single", image);
    }

    [GoldenFact]
    public void Draw_SplitScreenCamerasAtDifferentZoom_EachSnapsToItsOwnGrid()
    {
        // Left camera zoom 1.2, right camera zoom 1.6, each 64x96 and centered on world (0, 0).
        // Viewport-local center is (32, 48). Sprite half size is 8 units: 9.6 px left, 12.8 px right.
        //   left  edge = (22.4 + 1.2x,  38.4 - 1.2y)      right edge = (19.2 + 1.6x, 35.2 - 1.6y)
        var device = GoldenDevice.Require(_fixture);
        var texture = SceneArt.Quadrants(device);
        var screen = NewScreen();
        var left = screen.Camera;
        left.NormalizedViewport = new NormalizedRectangle(0f, 0f, 0.5f, 1f);
        left.Zoom = 1.2f;
        var right = new Camera { NormalizedViewport = new NormalizedRectangle(0.5f, 0f, 0.5f, 1f), Zoom = 1.6f };
        screen.Cameras.Add(right);

        // P: snapped by both cameras. Left edge (13.7, 47.7) -> (14, 48); right edge (7.6, 47.6) -> (8, 48).
        var p = Make(texture, -7.25f, -7.75f);
        screen.Add(p);
        left.SnappedSprites.Add(p);
        right.SnappedSprites.Add(p);

        // Q: snapped by the left camera only. Left edge (28.7, 20.7) -> (29, 21); right edge stays (27.6, 11.6).
        var q = Make(texture, 5.25f, 14.75f);
        screen.Add(q);
        left.SnappedSprites.Add(q);

        var image = OffscreenRenderer.RenderCameras(device, screen, Width, Height);

        // Left half (zoom 1.2, marker 3.6 px): snapped edge -> 4x4.
        WhiteBounds(image, 8, 40, 40, 62).ShouldBe(new Rectangle(14, 48, 4, 4));      // P
        WhiteBounds(image, 24, 15, 56, 36).ShouldBe(new Rectangle(29, 21, 4, 4));     // Q, snapped
        // Right half (zoom 1.6, marker 4.8 px), image x = 64 + viewport x.
        WhiteBounds(image, 64 + 4, 40, 64 + 40, 66).ShouldBe(new Rectangle(64 + 8, 48, 5, 5));   // P, snapped on its own grid
        // Q is not snapped on the right camera: edge (27.6, 11.6) -> pixels 28..31 x 12..15, 4x4 rather than 5x5.
        WhiteBounds(image, 64 + 24, 5, 64 + 56, 30).ShouldBe(new Rectangle(64 + 28, 12, 4, 4));

        GoldenImage.Verify("camera-sprite-snap-split", image);
    }
}
