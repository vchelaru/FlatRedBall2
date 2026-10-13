using FlatRedBall2.Math;
using FlatRedBall2.Rendering;
using FlatRedBall2.Tests;
using Microsoft.Xna.Framework;
using Xunit;

namespace FlatRedBall2.GoldenImage.Tests.Scenes;

[Collection(GraphicsDeviceCollection.Name)]
public class SpriteSceneGoldenTests
{
    private readonly GraphicsDeviceFixture _fixture;

    public SpriteSceneGoldenTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    [GoldenFact]
    public void Draw_PlainFlippedRotatedTintedAndStretchedSprites_MatchesGolden()
    {
        var device = GoldenDevice.Require(_fixture);
        var texture = SceneArt.Quadrants(device);

        Sprite Make(float x, float y) => new() { Texture = texture, X = x, Y = y, TextureScale = 2f };

        var screen = new Screen();
        screen.Add(Make(-40, 20));                                          // plain, 2x scale
        var flipped = Make(0, 20);
        flipped.FlipHorizontal = true;                                      // marker moves to the top-right
        screen.Add(flipped);
        var rotated = Make(40, 20);
        rotated.Rotation = Angle.FromDegrees(90);                           // counterclockwise: marker moves to the bottom-left
        screen.Add(rotated);
        var tinted = Make(-20, -25);
        tinted.Color = new Color(255, 120, 120);
        tinted.Alpha = 0.6f;
        screen.Add(tinted);
        var stretched = new Sprite { Texture = texture, X = 25, Y = -25, TextureScale = null, Width = 48, Height = 16 };
        screen.Add(stretched);

        var image = OffscreenRenderer.Render(device, screen, width: 128, height: 96);

        GoldenImage.Verify("sprite-basic", image);
    }
}
