using System.Collections.Generic;
using FlatRedBall2.Collision;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Collision;

public class SweepAndPruneTests
{
    private class BallEntity : Entity
    {
        public Circle Circle { get; } = new Circle { Radius = 16f };
        public override void CustomInitialize() => Add(Circle);
    }

    private class TestScreen : Screen { }

    private static (Factory<BallEntity> factory, TestScreen screen) CreateFactory()
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<BallEntity>(screen);
        return (factory, screen);
    }

    [Fact]
    public void DeepCollisionCount_NoPartitionAxis_CountsAllPairs()
    {
        // Without PartitionAxis, O(n*m) = 1 deep check is always performed regardless of distance.
        var (factory, _) = CreateFactory();
        var a = factory.Create();
        var b = factory.Create();
        a.X = 0f;
        b.X = 1000f;
        var rel = new CollisionRelationship<BallEntity, BallEntity>(factory, factory);

        rel.RunCollisions();

        // Same-list self collision: 1 unique pair checked
        rel.DeepCollisionCount.ShouldBe(1);
    }

    [Fact]
    public void PartitionAxis_FarApartObjects_SkipsDeepChecks()
    {
        // 1000 units apart, radii = 16 each — no X overlap, so sweep skips the deep check.
        var (factory, _) = CreateFactory();
        factory.PartitionAxis = Axis.X;
        var a = factory.Create();
        var b = factory.Create();
        a.X = 0f;
        b.X = 1000f;
        ((IFactory)factory).SortForPartition();
        var rel = new CollisionRelationship<BallEntity, BallEntity>(factory, factory);

        rel.RunCollisions();

        rel.DeepCollisionCount.ShouldBe(0);
    }

    [Fact]
    public void PartitionAxis_OverlappingObjects_DetectsCollision()
    {
        var (factory, _) = CreateFactory();
        factory.PartitionAxis = Axis.X;
        var a = factory.Create();
        var b = factory.Create();
        a.X = 0f;
        b.X = 10f; // overlapping — radii are 16 each
        ((IFactory)factory).SortForPartition();
        var rel = new CollisionRelationship<BallEntity, BallEntity>(factory, factory);
        bool fired = false;
        rel.CollisionOccurred += (_, _) => fired = true;

        rel.RunCollisions();

        rel.DeepCollisionCount.ShouldBe(1);
        fired.ShouldBeTrue();
    }

    [Fact]
    public void PartitionAxis_CrossList_FarApart_SkipsDeepChecks()
    {
        // Two separate factories with matching PartitionAxis — cross-list sweep should skip far pairs.
        // Use separate screens so both factories of the same type can coexist without overwriting each other
        // in the engine registry.
        var screenA = new TestScreen();
        screenA.Engine = new FlatRedBallService();
        var screenB = new TestScreen();
        screenB.Engine = new FlatRedBallService();
        var factoryA = new Factory<BallEntity>(screenA);
        var factoryB = new Factory<BallEntity>(screenB);
        factoryA.PartitionAxis = Axis.X;
        factoryB.PartitionAxis = Axis.X;

        var a = factoryA.Create();
        var b = factoryB.Create();
        a.X = 0f;
        b.X = 1000f;
        ((IFactory)factoryA).SortForPartition();
        ((IFactory)factoryB).SortForPartition();

        var rel = new CollisionRelationship<BallEntity, BallEntity>(factoryA, factoryB);

        rel.RunCollisions();

        rel.DeepCollisionCount.ShouldBe(0);
    }

    [Fact]
    public void SortPartitionedFactories_SortsOutOfOrderEntities_SweepStillSkipsFarPairs()
    {
        // Entities added in reverse order (far one first) — without sorting the sweep would
        // see [b(1000), a(0)] and not skip the pair. SortPartitionedFactories must fix the order.
        var (factory, screen) = CreateFactory();
        factory.PartitionAxis = Axis.X;
        var b = factory.Create();
        var a = factory.Create();
        b.X = 1000f; // added first → sits at index 0 before sort
        a.X = 0f;

        screen.Engine.SortPartitionedFactories();

        var rel = new CollisionRelationship<BallEntity, BallEntity>(factory, factory);
        rel.RunCollisions();

        rel.DeepCollisionCount.ShouldBe(0);
    }

    [Fact]
    public void MismatchedPartitionAxes_FallsBackToFullCheck()
    {
        // factoryA sorts by X, factoryB sorts by Y — axes don't match, so no sweep.
        var screenA = new TestScreen();
        screenA.Engine = new FlatRedBallService();
        var screenB = new TestScreen();
        screenB.Engine = new FlatRedBallService();
        var factoryA = new Factory<BallEntity>(screenA);
        var factoryB = new Factory<BallEntity>(screenB);
        factoryA.PartitionAxis = Axis.X;
        factoryB.PartitionAxis = Axis.Y;

        var a = factoryA.Create();
        var b = factoryB.Create();
        a.X = 0f;
        b.X = 1000f;

        var rel = new CollisionRelationship<BallEntity, BallEntity>(factoryA, factoryB);
        rel.RunCollisions();

        // Full O(n×m) check — the far pair is still tested.
        rel.DeepCollisionCount.ShouldBe(1);
    }

    [Fact]
    public void PartitionAxis_DenseClusterOf30Balls_CoversSameUniquePairsAsNaive()
    {
        // Lock in the partitioning correctness contract: at scale, with PartitionAxis set the
        // sweep must report the SAME set of overlapping pairs that the naive O(n²) path reports.
        // Any miss → balls overlap visibly without separation; any spurious hit → wasted work.
        // 30 balls placed in a 6×5 grid spaced 1.2*radius apart so neighbors overlap but distant
        // pairs don't. Position-based, not random, so the test is deterministic.
        const int Cols = 6;
        const int Rows = 5;
        const float Radius = 16f;
        const float Spacing = 1.2f * Radius;

        var pairsNaive = RunAndCollectPairs(partitionAxis: null);
        var pairsSwept = RunAndCollectPairs(partitionAxis: Axis.X);

        pairsSwept.Count.ShouldBe(pairsNaive.Count);
        foreach (var pair in pairsNaive)
            pairsSwept.ShouldContain(pair);

        HashSet<(int, int)> RunAndCollectPairs(Axis? partitionAxis)
        {
            var (factory, _) = CreateFactory();
            factory.PartitionAxis = partitionAxis;

            var balls = new List<BallEntity>();
            for (int row = 0; row < Rows; row++)
                for (int col = 0; col < Cols; col++)
                {
                    var ball = factory.Create();
                    ball.X = col * Spacing;
                    ball.Y = row * Spacing;
                    balls.Add(ball);
                }
            // Tag each ball with its grid index by Name so the (a,b) pair set is stable.
            for (int i = 0; i < balls.Count; i++) balls[i].Name = i.ToString();

            if (partitionAxis != null) ((IFactory)factory).SortForPartition();

            var rel = new CollisionRelationship<BallEntity, BallEntity>(factory, factory);
            var pairs = new HashSet<(int, int)>();
            rel.CollisionOccurred += (a, b) =>
            {
                int ai = int.Parse(a.Name!);
                int bi = int.Parse(b.Name!);
                // Normalize so the pair set doesn't depend on which side fired first.
                pairs.Add(ai < bi ? (ai, bi) : (bi, ai));
            };
            rel.RunCollisions();
            return pairs;
        }
    }

    [Fact]
    public void NullPartitionAxis_OnOneFactory_FallsBackToFullCheck()
    {
        // factoryA has an axis set, factoryB does not — no sweep.
        var screenA = new TestScreen();
        screenA.Engine = new FlatRedBallService();
        var screenB = new TestScreen();
        screenB.Engine = new FlatRedBallService();
        var factoryA = new Factory<BallEntity>(screenA);
        var factoryB = new Factory<BallEntity>(screenB);
        factoryA.PartitionAxis = Axis.X;
        // factoryB.PartitionAxis intentionally left null

        var a = factoryA.Create();
        var b = factoryB.Create();
        a.X = 0f;
        b.X = 1000f;

        var rel = new CollisionRelationship<BallEntity, BallEntity>(factoryA, factoryB);
        rel.RunCollisions();

        rel.DeepCollisionCount.ShouldBe(1);
    }
}
