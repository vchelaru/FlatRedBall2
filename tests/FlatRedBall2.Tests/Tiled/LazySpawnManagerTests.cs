using System;
using FlatRedBall2.Tiled;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Tiled;

public class LazySpawnManagerTests
{
    private class Marker : Entity
    {
        public string? Tag { get; set; }
    }

    private class TestScreen : Screen { }

    private static (Factory<Marker> factory, LazySpawnManager manager) Setup(
        LazySpawnMode mode, float buffer = 0f)
    {
        var screen = new TestScreen();
        screen.Engine = new FlatRedBallService();
        var factory = new Factory<Marker>(screen)
        {
            LazySpawn = mode,
            LazySpawnBuffer = buffer
        };
        return (factory, new LazySpawnManager());
    }

    [Fact]
    public void OneShot_DormantBecomesLive_WhenRectOverlapsRecord()
    {
        var (factory, manager) = Setup(LazySpawnMode.OneShot);
        manager.Add(factory, worldX: 50f, worldY: 50f, applyAfterInit: null);

        factory.Count.ShouldBe(0);

        manager.Update(left: 0f, right: 100f, bottom: 0f, top: 100f);

        factory.Count.ShouldBe(1);
        factory[0].X.ShouldBe(50f);
        factory[0].Y.ShouldBe(50f);
    }

    [Fact]
    public void OneShot_DoesNotSpawnAgain_AfterDestroyAndReenter()
    {
        var (factory, manager) = Setup(LazySpawnMode.OneShot);
        manager.Add(factory, worldX: 50f, worldY: 50f, applyAfterInit: null);

        manager.Update(0f, 100f, 0f, 100f);
        factory.Count.ShouldBe(1);
        factory[0].Destroy();
        factory.Count.ShouldBe(0);

        // Rect leaves and re-enters
        manager.Update(200f, 300f, 0f, 100f);
        manager.Update(0f, 100f, 0f, 100f);

        factory.Count.ShouldBe(0);
    }

    [Fact]
    public void Reloadable_DoesNotRespawn_WhileRectStillInside_AfterDestroy()
    {
        var (factory, manager) = Setup(LazySpawnMode.Reloadable);
        manager.Add(factory, 50f, 50f, null);

        manager.Update(0f, 100f, 0f, 100f);
        factory[0].Destroy();

        manager.Update(0f, 100f, 0f, 100f);

        factory.Count.ShouldBe(0);
    }

    [Fact]
    public void Reloadable_DoesNotRespawn_OnlyAfterRectLeaves()
    {
        var (factory, manager) = Setup(LazySpawnMode.Reloadable);
        manager.Add(factory, 50f, 50f, null);

        manager.Update(0f, 100f, 0f, 100f);
        factory[0].Destroy();

        // Rect leaves — record re-arms to Dormant but no spawn (rect not inside).
        manager.Update(200f, 300f, 0f, 100f);
        factory.Count.ShouldBe(0);

        // Rect re-enters — now spawn.
        manager.Update(0f, 100f, 0f, 100f);
        factory.Count.ShouldBe(1);
    }

    [Fact]
    public void Reloadable_DoesNotDoubleSpawn_WhileEntityAlive()
    {
        var (factory, manager) = Setup(LazySpawnMode.Reloadable);
        manager.Add(factory, 50f, 50f, null);

        manager.Update(0f, 100f, 0f, 100f);
        manager.Update(0f, 100f, 0f, 100f);
        manager.Update(0f, 100f, 0f, 100f);

        factory.Count.ShouldBe(1);
    }

    [Fact]
    public void Reloadable_LiveEntitySurvivesRectLeave()
    {
        var (factory, manager) = Setup(LazySpawnMode.Reloadable);
        manager.Add(factory, 50f, 50f, null);

        manager.Update(0f, 100f, 0f, 100f);
        factory.Count.ShouldBe(1);

        manager.Update(200f, 300f, 0f, 100f);

        factory.Count.ShouldBe(1);
    }

    [Fact]
    public void Buffer_ExpandsActivationRect()
    {
        var (factory, manager) = Setup(LazySpawnMode.OneShot, buffer: 32f);
        manager.Add(factory, worldX: 120f, worldY: 50f, applyAfterInit: null);

        // Camera rect 0..100 in X — point at 120 is outside but inside buffer.
        manager.Update(0f, 100f, 0f, 100f);

        factory.Count.ShouldBe(1);
    }

    [Fact]
    public void Buffer_DoesNotSpawnWhenOutsideBuffer()
    {
        var (factory, manager) = Setup(LazySpawnMode.OneShot, buffer: 10f);
        manager.Add(factory, worldX: 120f, worldY: 50f, applyAfterInit: null);

        manager.Update(0f, 100f, 0f, 100f);

        factory.Count.ShouldBe(0);
    }

    [Fact]
    public void ApplyAfterInit_RunsOnSpawn()
    {
        var (factory, manager) = Setup(LazySpawnMode.OneShot);
        manager.Add(factory, 50f, 50f, applyAfterInit: e => e.Tag = "hi");

        manager.Update(0f, 100f, 0f, 100f);

        factory.Count.ShouldBe(1);
        factory[0].Tag.ShouldBe("hi");
    }

    [Fact]
    public void MultiRect_Spawns_WhenAnyRectCoversPlacement()
    {
        var (factory, manager) = Setup(LazySpawnMode.OneShot);
        manager.Add(factory, worldX: 500f, worldY: 50f, applyAfterInit: null);

        // First rect doesn't cover (500,50); second rect does. Placement should spawn.
        ReadOnlySpan<ActivationRect> rects =
        [
            new ActivationRect(0f, 100f, 0f, 100f),
            new ActivationRect(450f, 550f, 0f, 100f),
        ];
        manager.Update(rects);

        factory.Count.ShouldBe(1);
    }

    [Fact]
    public void MultiRect_DoesNotSpawn_WhenNoRectCoversPlacement()
    {
        var (factory, manager) = Setup(LazySpawnMode.OneShot);
        manager.Add(factory, worldX: 1000f, worldY: 50f, applyAfterInit: null);

        ReadOnlySpan<ActivationRect> rects =
        [
            new ActivationRect(0f, 100f, 0f, 100f),
            new ActivationRect(450f, 550f, 0f, 100f),
        ];
        manager.Update(rects);

        factory.Count.ShouldBe(0);
    }

    [Fact]
    public void Reloadable_StaysAwaitingRectExit_WhenOneRectStillCovers()
    {
        var (factory, manager) = Setup(LazySpawnMode.Reloadable);
        manager.Add(factory, 50f, 50f, null);

        // Both rects cover (50,50) — spawn.
        ReadOnlySpan<ActivationRect> bothCover =
        [
            new ActivationRect(0f, 100f, 0f, 100f),
            new ActivationRect(0f, 100f, 0f, 100f),
        ];
        manager.Update(bothCover);
        factory[0].Destroy();

        // First rect leaves but second still covers — record stays in AwaitingRectExit, no respawn.
        ReadOnlySpan<ActivationRect> oneStillCovers =
        [
            new ActivationRect(500f, 600f, 0f, 100f),
            new ActivationRect(0f, 100f, 0f, 100f),
        ];
        manager.Update(oneStillCovers);

        factory.Count.ShouldBe(0);
    }

    [Fact]
    public void Reloadable_ReArms_OnlyWhenAllRectsLeave()
    {
        var (factory, manager) = Setup(LazySpawnMode.Reloadable);
        manager.Add(factory, 50f, 50f, null);

        ReadOnlySpan<ActivationRect> bothCover =
        [
            new ActivationRect(0f, 100f, 0f, 100f),
            new ActivationRect(0f, 100f, 0f, 100f),
        ];
        manager.Update(bothCover);
        factory[0].Destroy();

        // All rects leave — record re-arms (no spawn this tick because rects don't cover).
        ReadOnlySpan<ActivationRect> allLeft =
        [
            new ActivationRect(500f, 600f, 0f, 100f),
            new ActivationRect(700f, 800f, 0f, 100f),
        ];
        manager.Update(allLeft);
        factory.Count.ShouldBe(0);

        // A rect re-enters — spawn.
        manager.Update(bothCover);
        factory.Count.ShouldBe(1);
    }

    [Fact]
    public void OneShot_OnlyFiresOnce_EvenWithSecondRectArrivingLater()
    {
        var (factory, manager) = Setup(LazySpawnMode.OneShot);
        manager.Add(factory, 50f, 50f, null);

        ReadOnlySpan<ActivationRect> firstCovers =
        [
            new ActivationRect(0f, 100f, 0f, 100f),
            new ActivationRect(500f, 600f, 0f, 100f),
        ];
        manager.Update(firstCovers);
        factory.Count.ShouldBe(1);
        factory[0].Destroy();

        // Second rect now also covers — Consumed state must hold.
        ReadOnlySpan<ActivationRect> bothCover =
        [
            new ActivationRect(0f, 100f, 0f, 100f),
            new ActivationRect(40f, 60f, 40f, 60f),
        ];
        manager.Update(bothCover);

        factory.Count.ShouldBe(0);
    }

    [Fact]
    public void Reloadable_DestroyAfterRectLeft_ReSpawnsOnReenter()
    {
        // Spawned entity is alive when rect leaves; entity stays alive offscreen; entity dies
        // while offscreen; rect re-enters — should respawn.
        var (factory, manager) = Setup(LazySpawnMode.Reloadable);
        manager.Add(factory, 50f, 50f, null);

        manager.Update(0f, 100f, 0f, 100f);
        var first = factory[0];

        manager.Update(200f, 300f, 0f, 100f);
        first.Destroy();

        manager.Update(0f, 100f, 0f, 100f);

        factory.Count.ShouldBe(1);
    }
}
