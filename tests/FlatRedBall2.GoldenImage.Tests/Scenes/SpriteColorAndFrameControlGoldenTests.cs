using System;
using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using FlatRedBall2.Tests;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.GoldenImage.Tests.Scenes;

// Issues #1398/#1412. Every scene also asserts a few hand-computed pixels so a wrong golden cannot
// enshrine wrong output. Sprites are 16x16 (SceneArt.Quadrants) on a 128x96 image centered on the
// origin, so a sprite at world (cx, cy) has its quadrant centers 4 units off its center.
// Quadrants: TL (220,40,40) red, TR (40,200,60) green, BL (50,90,230) blue, BR (240,210,50) yellow.
// An offset of 0.4 is 102/255.
[Collection(GraphicsDeviceCollection.Name)]
public class SpriteColorAndFrameControlGoldenTests
{
    private const int Width = 128;
    private const int Height = 96;

    private readonly GraphicsDeviceFixture _fixture;

    public SpriteColorAndFrameControlGoldenTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    private enum Quadrant { TopLeft, TopRight, BottomLeft, BottomRight }

    private static Color Sample(PixelImage image, float spriteX, float spriteY, Quadrant quadrant)
    {
        float dx = quadrant is Quadrant.TopLeft or Quadrant.BottomLeft ? -4 : 4;
        float dy = quadrant is Quadrant.TopLeft or Quadrant.TopRight ? 4 : -4;
        return image[(int)(Width / 2 + spriteX + dx), (int)(Height / 2 - (spriteY + dy))];
    }

    private static void ShouldBeNear(Color actual, int r, int g, int b, int tolerance = 2)
    {
        (global::System.Math.Abs(actual.R - r) <= tolerance && global::System.Math.Abs(actual.G - g) <= tolerance
            && global::System.Math.Abs(actual.B - b) <= tolerance && actual.A == 255)
            .ShouldBeTrue($"expected ~({r},{g},{b},255) but was {actual}");
    }

    private static Screen NewScreen() => new() { Engine = new FlatRedBallService() };

    private static FrameTime Frame(float seconds) =>
        new(TimeSpan.FromSeconds(seconds), TimeSpan.FromSeconds(seconds), TimeSpan.Zero, TimeSpan.Zero);

    private static Sprite Plain(Texture2D texture, float x, float y = 0) => new() { Texture = texture, X = x, Y = y };

    [GoldenFact]
    public void Draw_AddColorBrightensSpriteAndLeavesNeighborAlone_MatchesGolden()
    {
        var device = GoldenDevice.Require(_fixture);
        var texture = SceneArt.Quadrants(device);
        var screen = NewScreen();
        screen.Add(Plain(texture, -40));
        var bright = Plain(texture, 0);
        bright.AddColor = new Vector3(0.4f, 0.4f, 0.4f);
        screen.Add(bright);
        screen.Add(Plain(texture, 40));

        var image = OffscreenRenderer.Render(device, screen, Width, Height);

        GoldenImage.Verify("sprite-add-color-brighten", image);
        ShouldBeNear(Sample(image, -40, 0, Quadrant.BottomLeft), 50, 90, 230);   // before: untouched
        ShouldBeNear(Sample(image, 0, 0, Quadrant.TopLeft), 255, 142, 142);      // 220+102 clamps
        ShouldBeNear(Sample(image, 0, 0, Quadrant.BottomLeft), 152, 192, 255);
        ShouldBeNear(Sample(image, 40, 0, Quadrant.BottomLeft), 50, 90, 230);    // after: untouched
    }

    [GoldenFact]
    public void Draw_AddColorWithColorAndAlpha_AddsFirstThenMultiplies()
    {
        var device = GoldenDevice.Require(_fixture);
        var texture = SceneArt.Quadrants(device);
        var screen = NewScreen();
        var tinted = Plain(texture, -30);
        tinted.AddColor = new Vector3(0.4f, 0f, 0f);
        tinted.Color = new Color(128, 255, 128);
        screen.Add(tinted);
        var faded = Plain(texture, 30);
        faded.AddColor = new Vector3(0.4f, 0.4f, 0.4f);
        faded.Alpha = 0.5f;
        screen.Add(faded);

        var image = OffscreenRenderer.Render(device, screen, Width, Height);

        GoldenImage.Verify("sprite-add-color-multiply-order", image);
        // Blue quadrant (50,90,230). Add first: R = (50+102)*128/255 = 76. Multiply first would
        // give 50*128/255+102 = 127.
        ShouldBeNear(Sample(image, -30, 0, Quadrant.BottomLeft), 76, 90, 115);
        // Alpha 0.5 over bg (24,32,64): (152,192,255)*0.498 + bg*0.502.
        ShouldBeNear(Sample(image, 30, 0, Quadrant.BottomLeft), 88, 112, 159, tolerance: 3);
    }

    [GoldenFact]
    public void Draw_NegativeAddColor_DarkensAndClampsAtZero()
    {
        var device = GoldenDevice.Require(_fixture);
        var texture = SceneArt.Quadrants(device);
        var screen = NewScreen();
        var dark = Plain(texture, 0);
        dark.AddColor = new Vector3(-0.4f, -0.4f, -0.4f);
        screen.Add(dark);

        var image = OffscreenRenderer.Render(device, screen, Width, Height);

        GoldenImage.Verify("sprite-add-color-darken", image);
        ShouldBeNear(Sample(image, 0, 0, Quadrant.TopLeft), 118, 0, 0);
        ShouldBeNear(Sample(image, 0, 0, Quadrant.TopRight), 0, 98, 0);
        ShouldBeNear(Sample(image, 0, 0, Quadrant.BottomLeft), 0, 0, 128);
        ShouldBeNear(Sample(image, 0, 0, Quadrant.BottomRight), 138, 108, 0);
    }

    // Left: a long Add frame (still flashing). Middle: Add frame then a plain frame, advanced past
    // the Add frame (flash ended, AddColor reset). Right: same chain but the game owns AddColor
    // (Color group cleared), so the Add frame never lands and the green flash survives the frame change.
    [GoldenFact]
    public void Draw_AddColorFramesAssignThroughTheProperty_MatchesGolden()
    {
        var device = GoldenDevice.Require(_fixture);
        var texture = SceneArt.Quadrants(device);

        AnimationChain Chain(string name, float x, double addSeconds)
        {
            var chain = new AnimationChain { Name = name };
            chain.Add(new AnimationFrame
            {
                Texture = texture,
                FrameLength = TimeSpan.FromSeconds(addSeconds),
                RelativeX = x, // the Offset group writes X from the frame, so position via the frame
                ColorOperation = ColorOperation.Add,
                Red = 102,
                Green = 102,
                Blue = 102,
            });
            chain.Add(new AnimationFrame { Texture = texture, FrameLength = TimeSpan.FromSeconds(10), RelativeX = x });
            return chain;
        }

        Sprite Animated(float x, double addSeconds)
        {
            var sprite = new Sprite { AnimationChains = new AnimationChainList { Chain("Flash", x, addSeconds) } };
            sprite.PlayAnimation("Flash");
            return sprite;
        }

        var screen = NewScreen();
        screen.Add(Animated(-40, addSeconds: 10));   // still on the Add frame
        screen.Add(Animated(0, addSeconds: 0.5));    // moves to the plain frame
        var gameOwned = Animated(40, addSeconds: 0.5);
        gameOwned.FrameControlledProperties = SpriteFrameProperties.All & ~SpriteFrameProperties.Color;
        gameOwned.AddColor = new Vector3(0f, 0.6f, 0f);
        screen.Add(gameOwned);

        screen.Update(Frame(0.6f));
        var image = OffscreenRenderer.Render(device, screen, Width, Height);

        GoldenImage.Verify("sprite-add-color-frames", image);
        ShouldBeNear(Sample(image, -40, 0, Quadrant.BottomLeft), 152, 192, 255);
        ShouldBeNear(Sample(image, 0, 0, Quadrant.BottomLeft), 50, 90, 230);
        ShouldBeNear(Sample(image, 40, 0, Quadrant.BottomLeft), 50, 243, 230);   // 90 + 153
    }

    // The art marker (white 3x3) is at the top-left of an unflipped sprite and the top-right when
    // FlipHorizontal is set. Both sprites start with FlipHorizontal = true over frames that are not
    // flipped. Left clears the Flip group, so its manual flip survives the frame change. Right keeps
    // the default, so the frame overwrites the manual flip.
    [GoldenFact]
    public void Draw_FlipGroupCleared_ManualFlipSurvivesFrameChange()
    {
        var device = GoldenDevice.Require(_fixture);
        var texture = SceneArt.Quadrants(device);

        Sprite Animated(float x)
        {
            var chain = new AnimationChain { Name = "Walk" };
            chain.Add(new AnimationFrame { Texture = texture, FrameLength = TimeSpan.FromSeconds(0.5), RelativeX = x });
            chain.Add(new AnimationFrame { Texture = texture, FrameLength = TimeSpan.FromSeconds(10), RelativeX = x });
            var sprite = new Sprite { AnimationChains = new AnimationChainList { chain } };
            sprite.PlayAnimation("Walk");
            sprite.FlipHorizontal = true;
            return sprite;
        }

        var manual = Animated(-30);
        manual.FrameControlledProperties = SpriteFrameProperties.All & ~SpriteFrameProperties.Flip;
        var frameControlled = Animated(30);
        var screen = NewScreen();
        screen.Add(manual);
        screen.Add(frameControlled);

        screen.Update(Frame(0.6f)); // both advance to frame 1
        var image = OffscreenRenderer.Render(device, screen, Width, Height);

        GoldenImage.Verify("sprite-frame-flip-control", image);
        manual.FlipHorizontal.ShouldBeTrue();
        frameControlled.FlipHorizontal.ShouldBeFalse();
        // Marker pixel 1 unit in from the sprite's top corner: top-right when flipped, top-left otherwise.
        ShouldBeNear(image[Width / 2 - 30 + 6, Height / 2 - 7], 255, 255, 255);   // manual: marker top-right
        ShouldBeNear(image[Width / 2 - 30 - 7, Height / 2 - 7], 40, 200, 60);     // manual: top-left shows the green quadrant (mirrored)
        ShouldBeNear(image[Width / 2 + 30 - 7, Height / 2 - 7], 255, 255, 255);   // frame-controlled: marker top-left
    }
}
