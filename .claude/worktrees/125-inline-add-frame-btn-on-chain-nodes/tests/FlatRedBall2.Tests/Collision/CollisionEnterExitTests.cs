using System.Collections.Generic;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class CollisionEnterExitTests
{
    private static AARect Rect(float x, float y = 0f, float size = 32f) =>
        new() { Width = size, Height = size, X = x, Y = y };

    [Fact]
    public void CollisionStarted_FiresOnFirstOverlapFrameOnly()
    {
        var a = Rect(0f);
        var b = Rect(100f);
        var rel = new CollisionRelationship<AARect, AARect>(
            new[] { a }, new[] { b });
        int startedCount = 0;
        rel.CollisionStarted += (_, _) => startedCount++;

        rel.RunCollisions();          // not overlapping
        startedCount.ShouldBe(0);

        b.X = 20f;                     // now overlapping
        rel.RunCollisions();
        startedCount.ShouldBe(1);

        rel.RunCollisions();           // still overlapping — no re-fire
        startedCount.ShouldBe(1);
    }

    [Fact]
    public void CollisionEnded_FiresOnFrameOverlapStops()
    {
        var a = Rect(0f);
        var b = Rect(20f);
        var rel = new CollisionRelationship<AARect, AARect>(
            new[] { a }, new[] { b });
        int endedCount = 0;
        rel.CollisionEnded += (_, _) => endedCount++;

        rel.RunCollisions();           // overlapping — no Ended yet
        endedCount.ShouldBe(0);

        b.X = 200f;                    // no longer overlapping
        rel.RunCollisions();
        endedCount.ShouldBe(1);

        rel.RunCollisions();           // still not overlapping — no re-fire
        endedCount.ShouldBe(1);
    }

    [Fact]
    public void StartedAndEnded_CycleMultipleTimes()
    {
        var a = Rect(0f);
        var b = Rect(200f);
        var rel = new CollisionRelationship<AARect, AARect>(
            new[] { a }, new[] { b });
        int started = 0, ended = 0;
        rel.CollisionStarted += (_, _) => started++;
        rel.CollisionEnded += (_, _) => ended++;

        rel.RunCollisions();           // apart

        b.X = 20f;
        rel.RunCollisions();           // started #1
        b.X = 200f;
        rel.RunCollisions();           // ended   #1
        b.X = 20f;
        rel.RunCollisions();           // started #2
        b.X = 200f;
        rel.RunCollisions();           // ended   #2

        started.ShouldBe(2);
        ended.ShouldBe(2);
    }

    [Fact]
    public void NeverOverlapping_FiresNothing()
    {
        var a = Rect(0f);
        var b = Rect(500f);
        var rel = new CollisionRelationship<AARect, AARect>(
            new[] { a }, new[] { b });
        int started = 0, ended = 0;
        rel.CollisionStarted += (_, _) => started++;
        rel.CollisionEnded += (_, _) => ended++;

        rel.RunCollisions();
        rel.RunCollisions();
        rel.RunCollisions();

        started.ShouldBe(0);
        ended.ShouldBe(0);
    }

    [Fact]
    public void Started_FiresBeforeOccurred_OnEntryFrame()
    {
        var a = Rect(0f);
        var b = Rect(20f);
        var rel = new CollisionRelationship<AARect, AARect>(
            new[] { a }, new[] { b });
        var order = new List<string>();
        rel.CollisionStarted += (_, _) => order.Add("Started");
        rel.CollisionOccurred += (_, _) => order.Add("Occurred");

        rel.RunCollisions();

        order.ShouldBe(new[] { "Started", "Occurred" });
    }

    [Fact]
    public void ListVsList_TracksPairsIndependently()
    {
        var a1 = Rect(0f);
        var a2 = Rect(300f);
        var b1 = Rect(20f);         // overlaps a1
        var b2 = Rect(500f);        // apart from everyone
        var rel = new CollisionRelationship<AARect, AARect>(
            new[] { a1, a2 }, new[] { b1, b2 });
        var startedPairs = new List<(AARect, AARect)>();
        var endedPairs = new List<(AARect, AARect)>();
        rel.CollisionStarted += (x, y) => startedPairs.Add((x, y));
        rel.CollisionEnded += (x, y) => endedPairs.Add((x, y));

        rel.RunCollisions();
        startedPairs.Count.ShouldBe(1);
        startedPairs[0].ShouldBe((a1, b1));

        // Now move a2 onto b2 while a1/b1 still overlap: one new pair
        a2.X = 500f;
        rel.RunCollisions();
        startedPairs.Count.ShouldBe(2);
        startedPairs[1].ShouldBe((a2, b2));
        endedPairs.Count.ShouldBe(0);

        // Separate a1 from b1: one Ended, the other still overlapping
        a1.X = -500f;
        rel.RunCollisions();
        endedPairs.Count.ShouldBe(1);
        endedPairs[0].ShouldBe((a1, b1));
    }

    [Fact]
    public void SelfCollision_FiresStartedEndedPerPair()
    {
        var a = Rect(0f);
        var b = Rect(20f);          // overlaps a
        var c = Rect(500f);         // alone
        var list = new[] { a, b, c };
        var rel = new CollisionRelationship<AARect, AARect>(list, list);
        int started = 0, ended = 0;
        rel.CollisionStarted += (_, _) => started++;
        rel.CollisionEnded += (_, _) => ended++;

        rel.RunCollisions();
        started.ShouldBe(1);
        ended.ShouldBe(0);

        b.X = 1000f; // separate b from a and from c (c is at 500)
        rel.RunCollisions();
        started.ShouldBe(1);
        ended.ShouldBe(1);
    }

    [Fact]
    public void PhysicsStillApplied_OnEntryFrame()
    {
        // Regression: adding Started/Ended must not skip physics response on entry frame.
        var a = Rect(0f);
        var b = Rect(20f);
        var rel = new CollisionRelationship<AARect, AARect>(
                      new[] { a }, new[] { b })
                  .MoveFirstOnCollision();
        rel.CollisionStarted += (_, _) => { };

        float aXBefore = a.X;
        rel.RunCollisions();

        a.X.ShouldBeLessThan(aXBefore); // a was pushed away
    }

    [Fact]
    public void EntityDestroyedMidOverlap_FiresEndedSameFrame()
    {
        // Uses Entity (not raw shape) so the _onDestroy hook path is exercised.
        var ent = new Entity();
        var shape = new AARect { Width = 32f, Height = 32f };
        ent.Add(shape);

        var wall = Rect(20f);
        var rel = new CollisionRelationship<Entity, AARect>(
            new[] { ent }, new[] { wall });
        int ended = 0;
        Entity? endedArgFirst = null;
        rel.CollisionEnded += (e, _) => { ended++; endedArgFirst = e; };

        rel.RunCollisions();          // overlapping — Started fires, not Ended
        ended.ShouldBe(0);

        ent.Destroy();                 // fires _onDestroy hook → Ended same frame
        ended.ShouldBe(1);
        endedArgFirst.ShouldBe(ent);

        // Subsequent RunCollisions must not re-fire Ended via the end-of-frame diff.
        rel.RunCollisions();
        ended.ShouldBe(1);
    }

    [Fact]
    public void EntityDestroyedInsideCollisionHandler_FiresEndedSameFrame()
    {
        // The common "player touches coin → coin dies" case.
        var player = new Entity();
        player.Add(new AARect { Width = 32f, Height = 32f });

        var coin = new Entity();
        coin.Add(new AARect { Width = 32f, Height = 32f, X = 20f });

        var rel = new CollisionRelationship<Entity, Entity>(
            new List<Entity> { player }, new List<Entity> { coin });
        int ended = 0;
        rel.CollisionEnded += (_, _) => ended++;
        rel.CollisionOccurred += (_, c) => c.Destroy();

        rel.RunCollisions();

        ended.ShouldBe(1); // same-frame, not delayed
    }

    [Fact]
    public void CrossListCollision_EntityDestroyedAndRemovedFromList_DoesNotThrow()
    {
        // One bullet overlapping two enemies — the bullet is destroyed on the first hit,
        // and the inner loop must not re-index into the now-shorter bullet list.
        var bullets = new List<Entity>();
        var enemies = new List<Entity>();

        var bullet = new Entity();
        bullet.Add(new AARect { Width = 8f, Height = 8f });
        bullets.Add(bullet);

        for (int i = 0; i < 3; i++)
        {
            var enemy = new Entity();
            enemy.Add(new AARect { Width = 8f, Height = 8f });
            enemies.Add(enemy);
        }

        var rel = new CollisionRelationship<Entity, Entity>(bullets, enemies);
        rel.CollisionOccurred += (b, e) =>
        {
            if (bullets.Contains(b)) { bullets.Remove(b); b.Destroy(); }
            if (enemies.Contains(e)) { enemies.Remove(e); e.Destroy(); }
        };

        Should.NotThrow(() => rel.RunCollisions());
    }

    [Fact]
    public void SameListCollision_EntityDestroyedAndRemovedFromList_DoesNotThrow()
    {
        var entities = new List<Entity>();

        for (int i = 0; i < 4; i++)
        {
            var ent = new Entity();
            ent.Add(new AARect { Width = 32f, Height = 32f, X = i * 10f });
            entities.Add(ent);
        }

        var rel = new CollisionRelationship<Entity, Entity>(entities, entities);
        rel.CollisionOccurred += (a, b) =>
        {
            if (entities.Contains(b))
            {
                entities.Remove(b);
                b.Destroy();
            }
        };

        Should.NotThrow(() => rel.RunCollisions());
    }

    [Fact]
    public void NoSubscribers_NoTrackingOverhead()
    {
        // Smoke test: with neither event subscribed, behavior is unchanged and no exceptions.
        var a = Rect(0f);
        var b = Rect(20f);
        var rel = new CollisionRelationship<AARect, AARect>(
            new[] { a }, new[] { b });
        int occurred = 0;
        rel.CollisionOccurred += (_, _) => occurred++;

        rel.RunCollisions();
        rel.RunCollisions();

        occurred.ShouldBe(2); // Occurred still fires each frame as before
    }
}
