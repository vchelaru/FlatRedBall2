using System.Linq;
using FlatRedBall2.AnimationEditorCommon;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// #1478 final phase: Sprite glides a named shape's offset and size between keyframes. CPU-side only
// (AnimateSelf reconciles shapes on the parent entity), so it runs without a GraphicsDevice.
public class SpriteShapeKeyframeTests
{
    private static AARectSave Rect(string name, float x, float sx, string? offsetType = null,
        string? sizeType = null, bool skipOffset = false)
        => new()
        {
            Name = name, X = x, ScaleX = sx, ScaleY = sx,
            OffsetInterpolation = offsetType, SizeInterpolation = sizeType, SkipOffsetKeyframe = skipOffset,
        };

    private static AnimationFrameSave Frame(float length, params ShapeSave[] shapes)
    {
        var frame = new AnimationFrameSave { FrameLength = length, ShapesSave = new ShapesSave() };
        foreach (var s in shapes) frame.ShapesSave.Add(s);
        return frame;
    }

    private static (Entity Entity, Sprite Sprite) Play(bool loop, params AnimationFrameSave[] frames)
    {
        var save = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "c", Loop = loop };
        chain.Frames.AddRange(frames);
        save.AnimationChains.Add(chain);

        var entity = new Entity();
        var sprite = new Sprite { AnimationChains = save.ToAnimationChainList(null!) };
        entity.Add(sprite);
        sprite.PlayAnimation("c");
        return (entity, sprite);
    }

    private static T Shape<T>(Entity entity, string name) where T : class =>
        (T)(object)entity.FindShapeByName(name)!;

    [Fact]
    public void AnimateSelf_LinearShapeOffset_RectGlidesBetweenFrames()
    {
        var (entity, sprite) = Play(true,
            Frame(1, Rect("Sword", 0, 8, offsetType: "Linear")),
            Frame(1, Rect("Sword", 100, 8)));

        sprite.AnimateSelf(0.5);

        Shape<AARect>(entity, "Sword").X.ShouldBe(50f, 0.001f);
    }

    [Fact]
    public void AnimateSelf_LinearShapeSize_RectWidthAndHeightGlide()
    {
        var (entity, sprite) = Play(true,
            Frame(1, Rect("Sword", 0, 10, sizeType: "Linear")),
            Frame(1, Rect("Sword", 0, 30)));

        sprite.AnimateSelf(0.5);

        var rect = Shape<AARect>(entity, "Sword");
        rect.Width.ShouldBe(40f, 0.001f);  // ScaleX 20 -> full width 40
        rect.Height.ShouldBe(40f, 0.001f);
    }

    [Fact]
    public void AnimateSelf_LinearShapeSize_CircleRadiusGlides()
    {
        var (entity, sprite) = Play(true,
            Frame(1, new CircleSave { Name = "Aura", Radius = 10, SizeInterpolation = "Linear" }),
            Frame(1, new CircleSave { Name = "Aura", Radius = 30 }));

        sprite.AnimateSelf(0.5);

        Shape<Circle>(entity, "Aura").Radius.ShouldBe(20f, 0.001f);
    }

    [Fact]
    public void AnimateSelf_DefaultSnap_ShapeStaysAtFrameValueMidFrame()
    {
        var (entity, sprite) = Play(true,
            Frame(1, Rect("Sword", 7, 8)),
            Frame(1, Rect("Sword", 100, 8)));

        sprite.AnimateSelf(0.7);

        Shape<AARect>(entity, "Sword").X.ShouldBe(7f);
    }

    [Fact]
    public void AnimateSelf_SkippedFrame_ShapeGlideSpansThroughIt()
    {
        var (entity, sprite) = Play(true,
            Frame(1, Rect("Sword", 0, 8, offsetType: "Linear")),
            Frame(1, Rect("Sword", 999, 8, skipOffset: true)),
            Frame(1, Rect("Sword", 100, 8)));

        sprite.AnimateSelf(1.5);

        Shape<AARect>(entity, "Sword").X.ShouldBe(75f, 0.001f);
    }

    [Fact]
    public void AnimateSelf_ShapeAbsentFromNextFrame_HoldsUntilItDisappears()
    {
        var (entity, sprite) = Play(true,
            Frame(1, Rect("Sword", 0, 8, offsetType: "Linear")),
            Frame(1));

        sprite.AnimateSelf(0.5);

        Shape<AARect>(entity, "Sword").X.ShouldBe(0f);
    }

    [Fact]
    public void AnimateSelf_GlideInsideLongFrame_MovesCollisionShape()
    {
        var (entity, sprite) = Play(true,
            Frame(1, Rect("Sword", 0, 5, offsetType: "Linear")),
            Frame(1, Rect("Sword", 100, 5)));

        sprite.AnimateSelf(0.5);

        entity.CollidesWith(new AARect { X = 50f, Y = 0f, Width = 1f, Height = 1f }).ShouldBeTrue();
        entity.CollidesWith(new AARect { X = 0f, Y = 0f, Width = 1f, Height = 1f }).ShouldBeFalse();
    }
}
