using System;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class CollisionRelationshipRemovalTests
{
    private class TestScreen : Screen { }

    private static FrameTime Frame() =>
        new FrameTime(TimeSpan.FromSeconds(1f / 60f), TimeSpan.FromSeconds(1f / 60f), TimeSpan.Zero, TimeSpan.Zero);

    private static Entity Box(float x, float size = 32f)
    {
        var e = new Entity { X = x };
        e.Add(new AARect { Width = size, Height = size });
        return e;
    }

    private static TestScreen NewScreen() => new TestScreen { Engine = new FlatRedBallService() };

    [Fact]
    public void RemoveCollisionRelationship_AfterRemoval_StopsColliding()
    {
        var screen = NewScreen();
        var actor = Box(0f);
        var wall = Box(20f);
        screen.Register(actor);
        screen.Register(wall);
        var rel = screen.AddCollisionRelationship(new[] { actor }, new[] { wall });
        rel.MoveFirstOnCollision();

        screen.RemoveCollisionRelationship(rel);
        screen.Update(Frame());

        actor.X.ShouldBe(0f);
    }

    [Fact]
    public void RemoveCollisionRelationship_WhileOverlapping_FiresCollisionEndedOnce()
    {
        var screen = NewScreen();
        var actor = Box(0f);
        var wall = Box(20f);
        screen.Register(actor);
        screen.Register(wall);
        var rel = screen.AddCollisionRelationship(new[] { actor }, new[] { wall });
        int ended = 0;
        rel.CollisionEnded += (_, _) => ended++;
        screen.Update(Frame());

        screen.RemoveCollisionRelationship(rel);
        screen.RemoveCollisionRelationship(rel);

        ended.ShouldBe(1);
    }

    [Fact]
    public void RemoveCollisionRelationship_FromOwnHandler_DoesNotSkipNextRelationship()
    {
        var screen = NewScreen();
        var actor = Box(0f);
        var wallA = Box(20f);
        var wallB = Box(-20f);
        screen.Register(actor);
        screen.Register(wallA);
        screen.Register(wallB);
        var first = screen.AddCollisionRelationship(new[] { actor }, new[] { wallA });
        var second = screen.AddCollisionRelationship(new[] { actor }, new[] { wallB });
        first.CollisionOccurred += (_, _) => screen.RemoveCollisionRelationship(first);
        int secondHits = 0;
        second.CollisionOccurred += (_, _) => secondHits++;

        screen.Update(Frame());

        secondHits.ShouldBe(1);
    }

    [Fact]
    public void RemoveCollisionRelationship_FromHandlerMidPass_NeverStartsAfterEnded()
    {
        var screen = NewScreen();
        var actor = Box(0f);
        var wallA = Box(20f);
        var wallB = Box(-20f);
        screen.Register(actor);
        screen.Register(wallA);
        screen.Register(wallB);
        var rel = screen.AddCollisionRelationship(new[] { actor }, new[] { wallA, wallB });
        int started = 0, ended = 0;
        rel.CollisionStarted += (_, _) => started++;
        rel.CollisionEnded += (_, _) => ended++;
        rel.CollisionOccurred += (_, _) => screen.RemoveCollisionRelationship(rel);

        screen.Update(Frame());
        screen.Update(Frame());

        started.ShouldBeGreaterThan(0);
        ended.ShouldBe(started);
    }

    [Fact]
    public void RemoveCollisionRelationship_AfterContact_RemovesDestroyHookFromEntities()
    {
        var screen = NewScreen();
        var actor = Box(0f);
        var wall = Box(20f);
        screen.Register(actor);
        screen.Register(wall);
        var rel = screen.AddCollisionRelationship(new[] { actor }, new[] { wall });
        rel.CollisionStarted += (_, _) => { };
        screen.Update(Frame());
        actor._onDestroy!.GetInvocationList().Length.ShouldBe(2);

        screen.RemoveCollisionRelationship(rel);

        actor._onDestroy!.GetInvocationList().Length.ShouldBe(1);
        wall._onDestroy!.GetInvocationList().Length.ShouldBe(1);
    }
}
