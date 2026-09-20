using System.Collections.Generic;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class MemoryLeakTests
{
    private class CollidableEntity : Entity
    {
        public AARect Body { get; private set; } = null!;

        public override void CustomInitialize()
        {
            Body = new AARect { Width = 32f, Height = 32f };
            Add(Body);
        }
    }

    private class TestScreen : Screen { }

    #region Factory

    [Fact]
    public void RecycledEntity_DoesNotAccumulate_OnDestroyDelegates()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen { Engine = engine };

        var factory = new Factory<CollidableEntity>(screen);
        factory.EnablePooling();

        var targets = new List<CollidableEntity>();
        var target = new CollidableEntity();
        target.CustomInitialize();
        targets.Add(target);

        var relationship = screen.AddCollisionRelationship(factory, targets);
        // Track contacts so HookEntityForDestroy hooks entity._onDestroy
        relationship.CollisionStarted += (a, b) => { };
        relationship.CollisionEnded += (a, b) => { };

        // Repeatedly create, collide, and destroy the entity to recycle it 100 times
        CollidableEntity? firstInstance = null;
        for (int i = 0; i < 100; i++)
        {
            var entity = factory.Create();
            if (firstInstance == null)
            {
                firstInstance = entity;
            }
            else
            {
                // Assert it's the recycled instance from the pool
                entity.ShouldBeSameAs(firstInstance);
            }

            // After recycling, _onDestroy should be reset back to just the base pool return handler
            entity._onDestroy.ShouldNotBeNull();
            entity._onDestroy!.GetInvocationList().Length.ShouldBe(1);

            // Trigger collision to hook entity for destroy
            relationship.RunCollisions();

            // Destroy the entity to return it to the free list
            entity.Destroy();
        }

        // On the final pool return, verify the delegate chain has not grown
        var finalEntity = factory.Create();
        finalEntity.ShouldBeSameAs(firstInstance);
        finalEntity._onDestroy.ShouldNotBeNull();
        finalEntity._onDestroy!.GetInvocationList().Length.ShouldBe(1);
    }

    [Fact]
    public void RecycledEntity_WithMultipleCollisionRelationships_DoesNotAccumulate()
    {
        var engine = new FlatRedBallService();
        var screen = new TestScreen { Engine = engine };

        var factory = new Factory<CollidableEntity>(screen);
        factory.EnablePooling();

        var targets = new List<CollidableEntity>();
        var target = new CollidableEntity();
        target.CustomInitialize();
        targets.Add(target);

        // Two separate collision relationships hooking the same entity pool
        var rel1 = screen.AddCollisionRelationship(factory, targets);
        rel1.CollisionStarted += (a, b) => { };
        rel1.CollisionEnded += (a, b) => { };

        var rel2 = screen.AddCollisionRelationship(factory, targets);
        rel2.CollisionStarted += (a, b) => { };
        rel2.CollisionEnded += (a, b) => { };

        CollidableEntity? firstInstance = null;
        for (int i = 0; i < 50; i++)
        {
            var entity = factory.Create();
            if (firstInstance == null)
            {
                firstInstance = entity;
            }
            else
            {
                entity.ShouldBeSameAs(firstInstance);
            }

            // After recycling, only the base pool return handler should remain
            entity._onDestroy.ShouldNotBeNull();
            entity._onDestroy!.GetInvocationList().Length.ShouldBe(1);

            // Both relationships hook the entity
            rel1.RunCollisions();
            rel2.RunCollisions();

            entity.Destroy();
        }

        var finalEntity = factory.Create();
        finalEntity.ShouldBeSameAs(firstInstance);
        finalEntity._onDestroy.ShouldNotBeNull();
        finalEntity._onDestroy!.GetInvocationList().Length.ShouldBe(1);
    }

    #endregion Factory
}
