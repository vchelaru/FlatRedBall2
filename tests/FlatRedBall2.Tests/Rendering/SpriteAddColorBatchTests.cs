using FlatRedBall2.Animation;
using FlatRedBall2.Rendering;
using FlatRedBall2.Rendering.Batches;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Rendering;

public class SpriteAddColorBatchTests
{
    private static Sprite MakeSpritePlayingAddFrame()
    {
        var chain = new AnimationChain { Name = "Flash" };
        chain.Add(new AnimationFrame { ColorOperation = ColorOperation.Add, Red = 200 });
        var chainList = new AnimationChainList { chain };

        var sprite = new Sprite { AnimationChains = chainList };
        sprite.PlayAnimation("Flash");
        return sprite;
    }

    private class FakeBatch : IRenderBatch
    {
        public bool FlipsY => true;
        public void Begin(SpriteBatch spriteBatch, Camera camera) { }
        public void End(SpriteBatch spriteBatch) { }
    }

    [Fact]
    public void Batch_AddFrameOnDefaultWorldSpaceBatch_ReturnsWorldSpaceAddColorBatch()
    {
        var sprite = MakeSpritePlayingAddFrame();

        sprite.Batch.ShouldBeSameAs(WorldSpaceAddColorBatch.Instance);
    }

    [Fact]
    public void Batch_AddFrameOnScreenSpaceBatch_ReturnsScreenSpaceAddColorBatch()
    {
        var sprite = MakeSpritePlayingAddFrame();
        sprite.Batch = ScreenSpaceBatch.Instance;

        sprite.Batch.ShouldBeSameAs(ScreenSpaceAddColorBatch.Instance);
    }

    [Fact]
    public void Batch_AddFrameOnCustomBatch_ReturnsCustomBatchUnchanged()
    {
        var sprite = MakeSpritePlayingAddFrame();
        var custom = new FakeBatch();
        sprite.Batch = custom;

        sprite.Batch.ShouldBeSameAs(custom);
    }

    [Fact]
    public void Batch_NoAnimation_ReturnsAssignedBatch()
    {
        var sprite = new Sprite();

        sprite.Batch.ShouldBeSameAs(WorldSpaceBatch.Instance);
    }

    // Issue #1398: add color is a public per-sprite property; .achx Add frames assign through it.

    [Fact]
    public void AddColor_Default_IsZero()
    {
        new Sprite().AddColor.ShouldBe(Vector3.Zero);
    }

    [Fact]
    public void PlayAnimation_AddFrame_AssignsAddColorFromFrameChannels()
    {
        var sprite = MakeSpritePlayingAddFrame();

        sprite.AddColor.X.ShouldBe(200 / 255f, tolerance: 0.0001f);
        sprite.AddColor.Y.ShouldBe(0f);
        sprite.AddColor.Z.ShouldBe(0f);
    }

    [Fact]
    public void AnimateSelf_NextFrameWithoutAdd_ResetsAddColorToZero()
    {
        var chain = new AnimationChain { Name = "Flash" };
        chain.Add(new AnimationFrame { FrameLength = System.TimeSpan.FromSeconds(1), ColorOperation = ColorOperation.Add, Red = 200 });
        chain.Add(new AnimationFrame { FrameLength = System.TimeSpan.FromSeconds(1) });
        var sprite = new Sprite { AnimationChains = new AnimationChainList { chain } };
        sprite.PlayAnimation("Flash");

        sprite.AnimateSelf(1.5);

        sprite.AddColor.ShouldBe(Vector3.Zero);
        sprite.Batch.ShouldBeSameAs(WorldSpaceBatch.Instance);
    }

    [Fact]
    public void AnimationChains_Reassigned_ClearsFrameAssignedAddColor()
    {
        var sprite = MakeSpritePlayingAddFrame();

        sprite.AnimationChains = null;

        sprite.AddColor.ShouldBe(Vector3.Zero);
    }

    [Fact]
    public void Batch_ManualAddColorWithNoAnimation_ReturnsAddColorBatch()
    {
        var sprite = new Sprite { AddColor = new Vector3(1f, 1f, 1f) };

        sprite.Batch.ShouldBeSameAs(WorldSpaceAddColorBatch.Instance);
    }

    [Fact]
    public void Batch_ManualAddColorBackToZero_ReturnsAssignedBatch()
    {
        var sprite = new Sprite { AddColor = new Vector3(1f, 1f, 1f) };

        sprite.AddColor = Vector3.Zero;

        sprite.Batch.ShouldBeSameAs(WorldSpaceBatch.Instance);
    }

    [Fact]
    public void Batch_NegativeAddColor_ReturnsAddColorBatch()
    {
        // Darkening is an offset too; only exactly-zero skips the shader.
        var sprite = new Sprite { AddColor = new Vector3(-0.5f, 0f, 0f) };

        sprite.Batch.ShouldBeSameAs(WorldSpaceAddColorBatch.Instance);
    }

    [Fact]
    public void Batch_TwoSprites_SwapIndependently()
    {
        var flashing = new Sprite { AddColor = new Vector3(1f, 1f, 1f) };
        var normal = new Sprite();

        flashing.Batch.ShouldBeSameAs(WorldSpaceAddColorBatch.Instance);
        normal.Batch.ShouldBeSameAs(WorldSpaceBatch.Instance);
    }

    [Fact]
    public void AnimateSelf_ColorGroupOff_ManualAddColorSurvivesFrameChange()
    {
        var chain = new AnimationChain { Name = "Flash" };
        chain.Add(new AnimationFrame { FrameLength = System.TimeSpan.FromSeconds(1), ColorOperation = ColorOperation.Add, Red = 200 });
        chain.Add(new AnimationFrame { FrameLength = System.TimeSpan.FromSeconds(1) });
        var sprite = new Sprite
        {
            AnimationChains = new AnimationChainList { chain },
            FrameControlledProperties = SpriteFrameProperties.All & ~SpriteFrameProperties.Color,
            AddColor = new Vector3(0.25f, 0.5f, 0.75f),
        };
        sprite.PlayAnimation("Flash"); // frame 0 is an Add frame; it must not land

        sprite.AnimateSelf(1.5);

        sprite.AddColor.ShouldBe(new Vector3(0.25f, 0.5f, 0.75f));
        sprite.Batch.ShouldBeSameAs(WorldSpaceAddColorBatch.Instance);
    }

    [Fact]
    public void FrameControlledProperties_EnablingColorMidAnimation_AssignsAddColorFromCurrentFrame()
    {
        var chain = new AnimationChain { Name = "Flash" };
        chain.Add(new AnimationFrame { ColorOperation = ColorOperation.Add, Green = 255 });
        var sprite = new Sprite
        {
            AnimationChains = new AnimationChainList { chain },
            FrameControlledProperties = SpriteFrameProperties.None,
        };
        sprite.PlayAnimation("Flash");
        sprite.AddColor.ShouldBe(Vector3.Zero);

        sprite.FrameControlledProperties = SpriteFrameProperties.Color;

        sprite.AddColor.ShouldBe(new Vector3(0f, 1f, 0f));
    }
}
