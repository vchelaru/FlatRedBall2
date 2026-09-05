using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class CollisionRelationshipIsEnabledTests
{
    private static AARect Rect(float x, float y = 0f, float size = 32f) =>
        new() { Width = size, Height = size, X = x, Y = y };

    [Fact]
    public void IsEnabled_DefaultsToTrue()
    {
        var rel = new CollisionRelationship<AARect, AARect>(new[] { Rect(0f) }, new[] { Rect(20f) });

        rel.IsEnabled.ShouldBeTrue();
    }

    [Fact]
    public void IsEnabled_SetToFalseTwiceWhileOverlapping_FiresCollisionEndedOnce()
    {
        var rel = new CollisionRelationship<AARect, AARect>(new[] { Rect(0f) }, new[] { Rect(20f) });
        int ended = 0;
        rel.CollisionEnded += (_, _) => ended++;
        rel.RunCollisions();

        rel.IsEnabled = false;
        rel.IsEnabled = false;

        ended.ShouldBe(1);
    }

    [Fact]
    public void IsEnabled_SetToFalseWhileNotOverlapping_DoesNotFireCollisionEnded()
    {
        var rel = new CollisionRelationship<AARect, AARect>(new[] { Rect(0f) }, new[] { Rect(200f) });
        int ended = 0;
        rel.CollisionEnded += (_, _) => ended++;
        rel.RunCollisions();

        rel.IsEnabled = false;

        ended.ShouldBe(0);
    }

    [Fact]
    public void IsEnabled_SetToFalseWhileOverlapping_FiresCollisionEnded()
    {
        var rel = new CollisionRelationship<AARect, AARect>(new[] { Rect(0f) }, new[] { Rect(20f) });
        int ended = 0;
        rel.CollisionEnded += (_, _) => ended++;
        rel.RunCollisions();           // overlapping — Ended not yet fired
        ended.ShouldBe(0);

        rel.IsEnabled = false;

        ended.ShouldBe(1);
    }

    [Fact]
    public void IsEnabled_SetToFalse_ZeroesDeepCollisionCount()
    {
        var rel = new CollisionRelationship<AARect, AARect>(new[] { Rect(0f) }, new[] { Rect(20f) });
        rel.RunCollisions();
        rel.DeepCollisionCount.ShouldBe(1);

        rel.IsEnabled = false;

        rel.DeepCollisionCount.ShouldBe(0);
    }

    [Fact]
    public void IsEnabled_ReEnabledWhileStillOverlapping_FiresCollisionStartedAgain()
    {
        var rel = new CollisionRelationship<AARect, AARect>(new[] { Rect(0f) }, new[] { Rect(20f) });
        int started = 0;
        rel.CollisionStarted += (_, _) => started++;
        rel.RunCollisions();           // started #1
        started.ShouldBe(1);

        rel.IsEnabled = false;
        rel.IsEnabled = true;
        rel.RunCollisions();

        started.ShouldBe(2);
    }

    [Fact]
    public void RunCollisions_CalledManuallyWhileDisabled_StillCollidesAndTracksContacts()
    {
        var a = Rect(0f);
        var b = Rect(20f);
        var rel = new CollisionRelationship<AARect, AARect>(new[] { a }, new[] { b });
        int started = 0, ended = 0;
        rel.CollisionStarted += (_, _) => started++;
        rel.CollisionEnded += (_, _) => ended++;
        rel.IsEnabled = false;

        rel.RunCollisions();           // manual run while disabled — overlapping
        started.ShouldBe(1);
        rel.DeepCollisionCount.ShouldBe(1);

        b.X = 200f;
        rel.RunCollisions();

        ended.ShouldBe(1);
    }
}
