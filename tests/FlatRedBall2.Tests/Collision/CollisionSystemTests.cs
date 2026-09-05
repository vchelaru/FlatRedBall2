using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class CollisionSystemTests
{
    // Minimal stub to verify RunCollisions is called.
    private sealed class CallCountRelationship : ICollisionRelationship
    {
        public int CallCount { get; private set; }
        public int DeepCollisionCount => 0;
        public PartitionStatus PartitionStatus => PartitionStatus.NotApplicable;
        public string DisplayName => "CallCountRelationship";
        public bool IsEnabled { get; set; } = true;
        public void RunCollisions() => CallCount++;
    }

    [Fact]
    public void Add_ThenRunAllCollisions_InvokesRunCollisionsOnRelationship()
    {
        var system = new CollisionSystem();
        var rel = new CallCountRelationship();
        system.Add(rel);

        system.RunAllCollisions();

        rel.CallCount.ShouldBe(1);
    }

    [Fact]
    public void RunAllCollisions_WithDisabledRelationship_DoesNotInvokeRunCollisions()
    {
        var system = new CollisionSystem();
        var rel = new CallCountRelationship { IsEnabled = false };
        system.Add(rel);

        system.RunAllCollisions();

        rel.CallCount.ShouldBe(0);
    }

    [Fact]
    public void RunAllCollisions_WithNoRelationships_DoesNotThrow()
    {
        var system = new CollisionSystem();

        Should.NotThrow(() => system.RunAllCollisions());
    }
}
