using FlatRedBall2.Animation;
using FlatRedBall2.Diagnostics;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

// Issue #1064. AddColorShaderMathTests and SpriteAddColorBatchTests each cover one half of this
// path in isolation (shader formula in C#, batch-selection logic) but neither proves the GPU
// actually produces the right pixels when Sprite, Camera, and WorldSpaceAddColorBatch run
// together. This renders a real Sprite playing an Add-color AnimationChain frame to an offscreen
// RenderTarget2D and reads the pixels back, on the one GraphicsDeviceFixture-backed machine that
// has a display to create a device with.
[Collection(GraphicsDeviceCollection.Name)]
public class SpriteAddColorRenderTests
{
    private readonly GraphicsDeviceFixture _fixture;

    public SpriteAddColorRenderTests(GraphicsDeviceFixture fixture) => _fixture = fixture;

    // 2x1 texture: left texel opaque gray, right texel fully transparent. Camera and render
    // target both 2x1 so the sprite covers exactly one world unit per texel with no filtering
    // blur, and only X flips ever move between texels (Y-flip subtlety from Sprite's default
    // batch doesn't come into play with a single row).
    private static (Sprite sprite, RenderTarget2D target, Camera camera, SpriteBatch batch) MakeScene(
        GraphicsDevice device, int? red)
    {
        var texture = new Texture2D(device, 2, 1);
        texture.SetData(new[] { new Color(50, 50, 50, 255), new Color(0, 0, 0, 0) });

        var chain = new AnimationChain { Name = "Chain" };
        chain.Add(new AnimationFrame
        {
            Texture = texture,
            ColorOperation = red.HasValue ? ColorOperation.Add : null,
            Red = red,
        });
        var sprite = new Sprite { AnimationChains = new AnimationChainList { chain } };
        sprite.PlayAnimation("Chain");

        var target = new RenderTarget2D(device, 2, 1);
        var camera = new Camera();
        camera.ApplyToHostRect(new Viewport(0, 0, 2, 1), orthogonalHeight: 1);

        return (sprite, target, camera, new SpriteBatch(device));
    }

    private static Color[] RenderAndReadBack(
        GraphicsDevice device, Sprite sprite, RenderTarget2D target, Camera camera, SpriteBatch spriteBatch)
    {
        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);

        sprite.Batch.Begin(spriteBatch, camera);
        sprite.Draw(spriteBatch, camera);
        sprite.Batch.End(spriteBatch);

        device.SetRenderTarget(null);

        var pixels = new Color[2];
        target.GetData(pixels);
        return pixels;
    }

    [Fact]
    public void Draw_AddColorFrame_BoostsOpaqueTexelAndLeavesTransparentTexelUntouched()
    {
        if (!_fixture.IsAvailable) return;
        var device = _fixture.GraphicsDevice!;

        var (sprite, target, camera, batch) = MakeScene(device, red: 200);
        var pixels = RenderAndReadBack(device, sprite, target, camera, batch);

        pixels[0].ShouldBe(new Color(250, 50, 50, 255)); // opaque: R += 200/255, G/B untouched
        pixels[1].ShouldBe(new Color(0, 0, 0, 0));        // transparent: Add must not leak color
    }

    [Fact]
    public void Draw_NoColorOperation_RendersTextureUnmodified()
    {
        if (!_fixture.IsAvailable) return;
        var device = _fixture.GraphicsDevice!;

        var (sprite, target, camera, batch) = MakeScene(device, red: null);
        var pixels = RenderAndReadBack(device, sprite, target, camera, batch);

        pixels[0].ShouldBe(new Color(50, 50, 50, 255));
        pixels[1].ShouldBe(new Color(0, 0, 0, 0));
    }

    // The two tests above each isolate a single Sprite in its own Begin/End pair. A real Screen
    // draws every renderable in one pass and breaks/re-begins the batch only when Batch actually
    // changes between neighbors (Screen.Draw) - normal -> Add -> normal forces the batch to swap
    // to WorldSpaceAddColorBatch and back mid-frame. This proves that round-trip doesn't leak
    // shader/blend state onto the sprites on either side of the Add-color one.
    [Fact]
    public void ScreenDraw_NormalAddNormalSpritesInOnePass_EachRendersCorrectly()
    {
        if (!_fixture.IsAvailable) return;
        var device = _fixture.GraphicsDevice!;

        Sprite MakeOneByOneSprite(float x, int? red)
        {
            var texture = new Texture2D(device, 1, 1);
            texture.SetData(new[] { new Color(50, 50, 50, 255) });

            var chain = new AnimationChain { Name = "Chain" };
            chain.Add(new AnimationFrame
            {
                Texture = texture,
                ColorOperation = red.HasValue ? ColorOperation.Add : null,
                Red = red,
                RelativeX = x, // Sprite.X is clobbered by RelativeX on PlayAnimation - set it here, not on the Sprite.
            });
            var sprite = new Sprite { AnimationChains = new AnimationChainList { chain } };
            sprite.PlayAnimation("Chain");
            return sprite;
        }

        var screen = new Screen();
        screen.Add(MakeOneByOneSprite(x: 0.5f, red: null));  // normal
        screen.Add(MakeOneByOneSprite(x: 1.5f, red: 200));   // Add - forces a batch break each way
        screen.Add(MakeOneByOneSprite(x: 2.5f, red: null));  // normal again

        using var target = new RenderTarget2D(device, 3, 1);
        var camera = new Camera();
        camera.X = 1.5f;
        camera.ApplyToHostRect(new Viewport(0, 0, 3, 1), orthogonalHeight: 1);

        using var spriteBatch = new SpriteBatch(device);
        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);

        screen.Draw(spriteBatch, new RenderDiagnostics(), camera);

        device.SetRenderTarget(null);

        var pixels = new Color[3];
        target.GetData(pixels);

        pixels.ShouldBe(new[]
        {
            new Color(50, 50, 50, 255),  // normal, before the Add sprite
            new Color(250, 50, 50, 255), // Add: R += 200/255
            new Color(50, 50, 50, 255),  // normal, after the Add sprite
        });
    }
}
