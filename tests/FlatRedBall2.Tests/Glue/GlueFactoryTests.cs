using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers what happens to a loaded entity after it is created: whether the project stops tracking it
// when it dies, and whether a PooledByFactory entity is recycled rather than reallocated.
public class GlueFactoryTests
{
    private static GlueProject LoadDoorsDemo() =>
        GlueProject.Load(Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", "DoorsDemo", "DoorsDemo.gluj"));

    private const string DoorName = @"Entities\Door";

    private static (FlatRedBallService Engine, GlueProject Project) Booted()
    {
        var engine = new FlatRedBallService();
        var project = LoadDoorsDemo();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });
        return (engine, project);
    }

    [Fact]
    public void CreateEntity_TracksTheInstanceUnderItsGlueName()
    {
        var (engine, project) = Booted();

        var door = project.CreateEntity(DoorName, engine.CurrentScreen);

        project.InstancesOf(DoorName).ShouldContain(door);
    }

    // The list feeds collision relationships, so a destroyed entity left in it is collided against
    // every frame forever. Forget existed for this and was never called from anywhere.
    [Fact]
    public void Destroy_AnInstance_StopsTrackingIt()
    {
        var (engine, project) = Booted();
        var door = project.CreateEntity(DoorName, engine.CurrentScreen);

        door.Destroy();

        project.InstancesOf(DoorName).ShouldNotContain(door);
    }

    [Fact]
    public void Destroy_OneOfSeveral_LeavesTheOthersTracked()
    {
        var (engine, project) = Booted();
        var first = project.CreateEntity(DoorName, engine.CurrentScreen);
        var second = project.CreateEntity(DoorName, engine.CurrentScreen);

        first.Destroy();

        project.InstancesOf(DoorName).ShouldBe(new[] { second });
    }

    // PooledByFactory is Glue's opt-in to recycling. Whether it is honoured is observable only by
    // identity: the same instance comes back rather than a new one.
    [Fact]
    public void CreateEntity_WhenPooled_RecyclesADestroyedInstance()
    {
        var (engine, project) = Booted();
        project.FindEntity(DoorName)!.PooledByFactory = true;

        var first = project.CreateEntity(DoorName, engine.CurrentScreen);
        first.Destroy();
        var second = project.CreateEntity(DoorName, engine.CurrentScreen);

        second.ShouldBeSameAs(first);
        project.InstancesOf(DoorName).ShouldBe(new[] { second });
    }

    [Fact]
    public void CreateEntity_WhenNotPooled_AllocatesAFreshInstance()
    {
        var (engine, project) = Booted();
        project.FindEntity(DoorName)!.PooledByFactory = false;

        var first = project.CreateEntity(DoorName, engine.CurrentScreen);
        first.Destroy();
        var second = project.CreateEntity(DoorName, engine.CurrentScreen);

        second.ShouldNotBeSameAs(first);
    }

    // A recycled shell has to come back as a working entity, not a husk: its objects are rebuilt
    // from the same Save, so it is indistinguishable from a fresh one.
    [Fact]
    public void CreateEntity_ARecycledInstance_HasItsObjectsRebuilt()
    {
        var (engine, project) = Booted();
        project.FindEntity(DoorName)!.PooledByFactory = true;

        var first = project.CreateEntity(DoorName, engine.CurrentScreen);
        int objectCount = first.Objects.Count;
        objectCount.ShouldBeGreaterThan(0);
        first.Destroy();

        var second = project.CreateEntity(DoorName, engine.CurrentScreen);

        second.Objects.Count.ShouldBe(objectCount);
        second.Save.ShouldBe(project.FindEntity(DoorName));
    }

    // Pooling is per Glue name. Every loaded entity is the same C# type, so a shared pool would hand
    // a Door back where a Player was asked for -- the exact hazard G80 describes.
    [Fact]
    public void CreateEntity_WhenPooled_DoesNotRecycleAcrossGlueNames()
    {
        var (engine, project) = Booted();
        const string playerName = @"Entities\Player";
        project.FindEntity(DoorName)!.PooledByFactory = true;
        project.FindEntity(playerName)!.PooledByFactory = true;

        var door = project.CreateEntity(DoorName, engine.CurrentScreen);
        door.Destroy();

        var player = project.CreateEntity(playerName, engine.CurrentScreen);

        player.ShouldNotBeSameAs(door);
        player.GlueName.ShouldBe(playerName);
    }
}
