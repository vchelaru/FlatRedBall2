using System;
using FlatRedBall2.Animation;
using FlatRedBall2.Collision;
using FlatRedBall2.Rendering;
using Microsoft.Xna.Framework.Graphics;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class ScreenCreateFireAndForgetTests
{
    private static FrameTime Frame(float dt) =>
        new FrameTime(TimeSpan.FromSeconds(dt), TimeSpan.FromSeconds(dt), TimeSpan.Zero, TimeSpan.Zero);

    private static AnimationChainList MakeChain(string name, int frames, float frameLength)
    {
        var chain = new AnimationChain { Name = name };
        for (int i = 0; i < frames; i++)
            chain.Add(new AnimationFrame { FrameLength = TimeSpan.FromSeconds(frameLength) });
        var list = new AnimationChainList();
        list.Add(chain);
        return list;
    }

    [Fact]
    public void Screen_CreateFireAndForget_AnimationFinish_DestroysEntity()
    {
        var engine = new FlatRedBallService();
        var screen = new Screen { Engine = engine };
        // 2 frames at 0.1s = 0.2s total length.
        var chains = MakeChain("Explode", 2, 0.1f);

        var fx = screen.CreateFireAndForget(chains, "Explode", x: 0f, y: 0f);

        screen.Entities.ShouldContain(fx);

        // Advance enough time for animation to finish.
        screen.Update(Frame(0.5f));

        screen.Entities.ShouldNotContain(fx);
    }

    [Fact]
    public void Screen_CreateFireAndForget_Texture_DestroysAfterDuration()
    {
        var engine = new FlatRedBallService();
        var screen = new Screen { Engine = engine };

        var fx = screen.CreateFireAndForget((Texture2D)null!, x: 0f, y: 0f, duration: 0.5f);

        screen.Entities.ShouldContain(fx);

        // 0.4s elapsed — still alive.
        screen.Update(Frame(0.4f));
        screen.Entities.ShouldContain(fx);

        // +0.2s = 0.6s total — past duration, destroyed.
        screen.Update(Frame(0.2f));
        screen.Entities.ShouldNotContain(fx);
    }

    [Fact]
    public void Screen_CreateFireAndForget_LoopingAnimation_StillDestroys()
    {
        var engine = new FlatRedBallService();
        var screen = new Screen { Engine = engine };
        // Default Sprite.IsLooping is true; CreateFireAndForget must override to play once.
        var chains = MakeChain("Loop", 2, 0.1f); // total 0.2s

        var fx = screen.CreateFireAndForget(chains, "Loop", x: 0f, y: 0f);

        screen.Update(Frame(0.5f));

        screen.Entities.ShouldNotContain(fx);
    }

    [Fact]
    public void Screen_CreateFireAndForget_VelocitySurvives()
    {
        var engine = new FlatRedBallService();
        var screen = new Screen { Engine = engine };
        var chains = MakeChain("Idle", 1, 10f); // long enough not to expire during test

        var fx = screen.CreateFireAndForget(chains, "Idle", x: 0f, y: 0f);
        fx.VelocityX = 100f;

        screen.Update(Frame(0.1f));

        fx.X.ShouldBe(10f, tolerance: 0.01f);
    }

    [Fact]
    public void Screen_CreateFireAndForget_AddShapePostSpawn()
    {
        var engine = new FlatRedBallService();
        var screen = new Screen { Engine = engine };
        var chains = MakeChain("Idle", 1, 10f);

        var fx = screen.CreateFireAndForget(chains, "Idle", x: 0f, y: 0f);
        var hitbox = new Circle { Radius = 8f };
        fx.Add(hitbox);

        // Stationary target overlapping fx's hitbox at origin.
        var target = new Entity();
        target.Add(new Circle { Radius = 8f });
        screen.Register(target);

        bool collided = false;
        screen.AddCollisionRelationship(new[] { fx }, new[] { target })
              .CollisionOccurred += (a, b) => collided = true;

        screen.Update(Frame(1f / 60f));

        collided.ShouldBeTrue();
    }
}
