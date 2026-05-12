using System;
using System.Numerics;
using FlatRedBall2.Animation;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Animation;

// Per-frame shape reconciliation: animation frames carry shape definitions which the sprite
// applies to its parent entity at frame switch time. Ownership is at the chainlist level — a
// shape name appearing in any frame of any chain is "owned" and reconciled. Names not in any
// owned set (e.g. body colliders) are never touched.
public class SpriteAnimationShapesTests
{
    private static AnimationChain ChainWithFrames(string name, params AnimationFrame[] frames)
    {
        var chain = new AnimationChain { Name = name };
        chain.AddRange(frames);
        return chain;
    }

    private static AnimationFrame Frame(float length, params AnimationShapeFrame[] shapes)
    {
        var frame = new AnimationFrame { FrameLength = TimeSpan.FromSeconds(length) };
        foreach (var s in shapes) frame.Shapes.Add(s);
        return frame;
    }

    [Fact]
    public void ApplyCurrentFrame_BodyShapeUnnamedByAnyChain_LeftUntouched()
    {
        // Body collider has a name "Body" but no animation frame mentions it — must be left alone.
        var entity = new Entity();
        var body = new AARect { Name = "Body", Width = 20f, Height = 20f, IsVisible = true };
        entity.Add(body);

        var sprite = new Sprite();
        entity.Add(sprite);

        var chain = ChainWithFrames("Idle",
            Frame(0.1f)); // no shapes
        var list = new AnimationChainList();
        list.Add(chain);
        sprite.AnimationChains = list;
        sprite.PlayAnimation("Idle");

        body.IsVisible.ShouldBeTrue();
        body.Width.ShouldBe(20f);
    }

    [Fact]
    public void ApplyCurrentFrame_HitboxOnFrame_EnabledOnEntity()
    {
        var entity = new Entity();
        var sword = new AARect { Name = "Sword", IsVisible = false };
        entity.Add(sword);
        entity.SetDefaultCollision(sword, false);

        var sprite = new Sprite();
        entity.Add(sprite);

        var chain = ChainWithFrames("Attack",
            Frame(0.1f, new AnimationAARectFrame { Name = "Sword", Width = 30f, Height = 10f, RelativeX = 15f }));
        var list = new AnimationChainList();
        list.Add(chain);
        sprite.AnimationChains = list;
        sprite.PlayAnimation("Attack");

        sword.IsVisible.ShouldBeTrue();
        sword.Width.ShouldBe(30f);
        sword.Height.ShouldBe(10f);
        sword.X.ShouldBe(15f);
        entity.CollidesWith(new AARect { X = 15f, Y = 0f, Width = 1f, Height = 1f }).ShouldBeTrue();
    }

    [Fact]
    public void ApplyCurrentFrame_HitboxAbsentFromFrame_HiddenAndCollisionDisabled()
    {
        var entity = new Entity();
        var sprite = new Sprite();
        entity.Add(sprite);

        // Two-frame chain: frame 0 has Sword, frame 1 does not — the chainlist still owns "Sword".
        var chain = ChainWithFrames("Attack",
            Frame(0.1f, new AnimationAARectFrame { Name = "Sword", Width = 30f, Height = 10f }),
            Frame(0.1f));
        var list = new AnimationChainList();
        list.Add(chain);
        sprite.AnimationChains = list;
        sprite.PlayAnimation("Attack");

        // Auto-created on frame 0 (AutoCreateShapes = true by default)
        AARect? sword = null;
        foreach (var c in entity.Children)
            if (c is AARect r && r.Name == "Sword") sword = r;
        sword.ShouldNotBeNull();
        sword!.IsVisible.ShouldBeTrue();

        // Advance to frame 1 — no Sword listed, must be hidden
        sprite.AnimateSelf(0.15);

        sword.IsVisible.ShouldBeFalse();
        entity.CollidesWith(new AARect { X = 0f, Y = 0f, Width = 100f, Height = 100f }).ShouldBeFalse();
    }

    [Fact]
    public void ApplyCurrentFrame_CrossChainSwitchWithinChainList_HidesShapeNotInNewChain()
    {
        var entity = new Entity();
        var sprite = new Sprite();
        entity.Add(sprite);

        var attack = ChainWithFrames("Attack",
            Frame(0.1f, new AnimationAARectFrame { Name = "Sword", Width = 30f, Height = 10f }));
        var idle = ChainWithFrames("Idle",
            Frame(0.1f)); // no Sword

        var list = new AnimationChainList();
        list.Add(attack);
        list.Add(idle);
        sprite.AnimationChains = list;

        sprite.PlayAnimation("Attack");
        AARect? sword = null;
        foreach (var c in entity.Children)
            if (c is AARect r && r.Name == "Sword") sword = r;
        sword.ShouldNotBeNull();
        sword!.IsVisible.ShouldBeTrue();

        sprite.PlayAnimation("Idle");

        // Sword is owned by the chainlist (Attack mentions it), Idle does not list it → hidden.
        sword.IsVisible.ShouldBeFalse();
    }

    [Fact]
    public void ApplyCurrentFrame_CrossChainListSwitch_LeavesResidualOnPriorList()
    {
        // Documents the deliberate non-cleanup: each AnimationChainList's owned-name set is
        // scoped to itself. When a sprite swaps to a different chainlist that doesn't own a
        // shape, the prior chainlist's residual state on that shape is left as-is. This nudges
        // authors to keep related chains in one .achx — the escape hatch (HideOwnedShapes-style
        // API) is deferred until a real use case appears.
        var entity = new Entity();
        var sprite = new Sprite();
        entity.Add(sprite);

        var combat = new AnimationChainList();
        combat.Add(ChainWithFrames("Attack",
            Frame(0.1f, new AnimationAARectFrame { Name = "Sword", Width = 30f, Height = 10f })));

        var movement = new AnimationChainList();
        movement.Add(ChainWithFrames("Walk", Frame(0.1f))); // owns no shapes

        sprite.AnimationChains = combat;
        sprite.PlayAnimation("Attack");

        AARect? sword = null;
        foreach (var c in entity.Children)
            if (c is AARect r && r.Name == "Sword") sword = r;
        sword.ShouldNotBeNull();
        sword!.IsVisible.ShouldBeTrue();

        // Swap chainlists. Movement does not own "Sword", so it must not touch it.
        sprite.AnimationChains = movement;
        sprite.PlayAnimation("Walk");

        sword.IsVisible.ShouldBeTrue();
        sword.Width.ShouldBe(30f);
    }

    [Fact]
    public void ApplyCurrentFrame_TypeMismatch_Throws()
    {
        var entity = new Entity();
        var sword = new AARect { Name = "Sword" };
        entity.Add(sword);

        var sprite = new Sprite();
        entity.Add(sprite);

        // Frame says "Sword" is a Polygon; entity has it as a rectangle → mismatch at apply time.
        var chain = ChainWithFrames("Attack",
            Frame(0.1f, new AnimationPolygonFrame { Name = "Sword", Points = new[] { new Vector2(0,0), new Vector2(10,0), new Vector2(0,10) } }));
        var list = new AnimationChainList();
        list.Add(chain);
        sprite.AnimationChains = list;

        Should.Throw<InvalidOperationException>(() => sprite.PlayAnimation("Attack"));
    }

    [Fact]
    public void ApplyCurrentFrame_AutoCreateShapesFalse_MissingShapeThrows()
    {
        var entity = new Entity();
        var sprite = new Sprite();
        entity.Add(sprite);

        var chain = ChainWithFrames("Attack",
            Frame(0.1f, new AnimationAARectFrame { Name = "Sword", Width = 10f, Height = 10f }));
        var list = new AnimationChainList { AutoCreateShapes = false };
        list.Add(chain);
        sprite.AnimationChains = list;

        Should.Throw<InvalidOperationException>(() => sprite.PlayAnimation("Attack"));
    }

    [Fact]
    public void ApplyCurrentFrame_ShapeWithoutName_Throws()
    {
        var entity = new Entity();
        var sprite = new Sprite();
        entity.Add(sprite);

        var chain = ChainWithFrames("Attack",
            Frame(0.1f, new AnimationAARectFrame { Name = "", Width = 10f }));
        var list = new AnimationChainList();
        list.Add(chain);
        sprite.AnimationChains = list;

        Should.Throw<InvalidOperationException>(() => sprite.PlayAnimation("Attack"));
    }
}
