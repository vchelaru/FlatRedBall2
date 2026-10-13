using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// A list declared by a screen or entity belongs to that instance. OwnedLists has the screen declare
// BulletList (factory-associated, Blob) and Entities\Enemy declare a BulletList of its own, plus a
// Shield and a BulletList-vs-Shield relationship. A spawn joins the lists registered for its type:
// the screen's associated lists by default, adjustable with AddList / RemoveList.
public class GlueOwnedListTests
{
    private const string BlobName = @"Entities\Blob";
    private const string EnemyName = @"Entities\Enemy";

    private static (FlatRedBallService Engine, GlueProject Project, GlueScreen Screen) Boot(string fixture = "OwnedLists")
    {
        var engine = new FlatRedBallService();
        var project = GlueProject.Load(Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", fixture, fixture + ".gluj"));
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });
        return (engine, project, (GlueScreen)engine.CurrentScreen);
    }

    [Fact]
    public void TwoInstancesOfAnEntityThatDeclaresAList_EachOwnAnIndependentList()
    {
        var (_, project, screen) = Boot();
        var first = project.CreateEntity(EnemyName, screen);
        var second = project.CreateEntity(EnemyName, screen);
        var bullet = project.CreateEntity(BlobName, screen);

        first.GetList("BulletList").Add(bullet);

        first.GetList("BulletList").ShouldBe(new[] { bullet });
        second.GetList("BulletList").ShouldBeEmpty();
        first.GetList("BulletList").ShouldNotBeSameAs(second.GetList("BulletList"));
    }

    [Fact]
    public void RelationshipOnAnEntitysOwnList_SeesOnlyThatInstancesMembers()
    {
        var (_, project, screen) = Boot();
        var first = project.CreateEntity(EnemyName, screen);
        var second = project.CreateEntity(EnemyName, screen);
        var firstBullet = project.CreateEntity(BlobName, screen);
        var secondBullet = project.CreateEntity(BlobName, screen);
        first.GetList("BulletList").Add(firstBullet);
        second.GetList("BulletList").Add(secondBullet);

        var seenByFirst = new List<GlueEntity>();
        var seenBySecond = new List<GlueEntity>();
        var firstRelationship = (CollisionRelationship<GlueEntity, GlueEntity>)first.Objects["BulletListVsShield"];
        var secondRelationship = (CollisionRelationship<GlueEntity, GlueEntity>)second.Objects["BulletListVsShield"];
        firstRelationship.CollisionOccurred += (bullet, _) => seenByFirst.Add(bullet);
        secondRelationship.CollisionOccurred += (bullet, _) => seenBySecond.Add(bullet);

        firstRelationship.RunCollisions();
        secondRelationship.RunCollisions();

        seenByFirst.ShouldBe(new[] { firstBullet });
        seenBySecond.ShouldBe(new[] { secondBullet });
    }

    [Fact]
    public void DefaultSpawn_JoinsTheScreensAssociatedList_NotAnEntitysOwnList()
    {
        var (_, project, screen) = Boot();
        var enemy = project.CreateEntity(EnemyName, screen);

        var spawned = project.CreateEntity(BlobName, screen);

        screen.GetList("BulletList").ShouldContain(spawned);
        enemy.GetList("BulletList").ShouldBeEmpty();
    }

    [Fact]
    public void ScreenListRelationship_SeesAnEntityLaterSpawnedThroughTheFactoryPath()
    {
        var (_, project, screen) = Boot();
        var relationship = (CollisionRelationship<GlueEntity, GlueEntity>)screen.Objects["BulletListVsTarget"];
        var seen = new List<GlueEntity>();
        relationship.CollisionOccurred += (bullet, _) => seen.Add(bullet);

        var spawned = project.CreateEntity(BlobName, screen);
        relationship.RunCollisions();

        seen.ShouldContain(spawned);
    }

    [Fact]
    public void RelationshipSeesAnEntityAddedDirectlyToTheOwnedList()
    {
        var (_, project, screen) = Boot();
        var enemy = project.CreateEntity(EnemyName, screen);
        var relationship = (CollisionRelationship<GlueEntity, GlueEntity>)enemy.Objects["BulletListVsShield"];
        var seen = new List<GlueEntity>();
        relationship.CollisionOccurred += (bullet, _) => seen.Add(bullet);
        var bullet = project.CreateEntity(BlobName, screen);

        enemy.GetList("BulletList").Add(bullet);
        relationship.RunCollisions();

        seen.ShouldBe(new[] { bullet });
    }

    [Fact]
    public void ScreenAndEntityListsWithTheSameName_StaySeparate()
    {
        var (_, project, screen) = Boot();
        var enemy = project.CreateEntity(EnemyName, screen);

        var spawned = project.CreateEntity(BlobName, screen);
        enemy.GetList("BulletList").Add(project.CreateEntity(BlobName, screen));
        enemy.GetList("BulletList").Add(project.CreateEntity(BlobName, screen));

        screen.GetList("BulletList").Count.ShouldBe(3);
        enemy.GetList("BulletList").Count.ShouldBe(2);
        enemy.GetList("BulletList").ShouldNotContain(spawned);
    }

    [Fact]
    public void ListsToAddTo_ByDefault_IsTheScreensAssociatedList()
    {
        var (_, project, screen) = Boot();

        project.ListsToAddTo(BlobName).ShouldBe(new[] { screen.GetList("BulletList") });
        project.ListsToAddTo(EnemyName).ShouldBeEmpty();
    }

    [Fact]
    public void RemoveList_MakesASpawnSkipThatList()
    {
        var (_, project, screen) = Boot();

        project.RemoveList(BlobName, screen.GetList("BulletList"));
        var spawned = project.CreateEntity(BlobName, screen);

        screen.GetList("BulletList").ShouldNotContain(spawned);
        project.ListsToAddTo(BlobName).ShouldBeEmpty();
    }

    [Fact]
    public void AddList_ScreenList_MakesASpawnJoinItAgain()
    {
        var (_, project, screen) = Boot();
        project.RemoveList(BlobName, screen.GetList("BulletList"));

        project.AddList(BlobName, screen.GetList("BulletList"));
        var spawned = project.CreateEntity(BlobName, screen);

        screen.GetList("BulletList").ShouldContain(spawned);
    }

    [Fact]
    public void AddList_EntityList_MakesASpawnJoinItAsWellAsTheScreenList()
    {
        var (_, project, screen) = Boot();
        var enemy = project.CreateEntity(EnemyName, screen);
        var other = project.CreateEntity(EnemyName, screen);

        project.AddList(BlobName, enemy.GetList("BulletList"));
        var spawned = project.CreateEntity(BlobName, screen);

        enemy.GetList("BulletList").ShouldContain(spawned);
        screen.GetList("BulletList").ShouldContain(spawned);
        other.GetList("BulletList").ShouldBeEmpty();
    }

    [Fact]
    public void AddList_IsIdempotent_ASpawnJoinsOnce()
    {
        var (_, project, screen) = Boot();

        project.AddList(BlobName, screen.GetList("BulletList"));
        var spawned = project.CreateEntity(BlobName, screen);

        screen.GetList("BulletList").Count(e => e == spawned).ShouldBe(1);
    }

    // The documented rule: a list typed with the most base entity (Enemy, which has no base) reaches
    // its own factory and every descendant's, each holding its own reference.
    [Fact]
    public void ListsToAddTo_ByDefault_MostBaseTypedList_ReachesEveryDescendant()
    {
        var (_, project, screen) = Boot("BaseTypedLists");
        var enemyList = screen.GetList("EnemyList");

        project.ListsToAddTo(@"Entities\Enemy").ShouldBe(new[] { enemyList });
        project.ListsToAddTo(@"Entities\Zombie").ShouldBe(new[] { enemyList });
        project.ListsToAddTo(@"Entities\Boss").ShouldBe(new[] { enemyList });
        project.ListsToAddTo(@"Entities\Target").ShouldBeEmpty();
    }

    // AddList and RemoveList act on one entity's destination set, like a call on one FRB1 factory.
    [Fact]
    public void AddList_OnABaseEntity_DoesNotReachDerivedSpawns()
    {
        var (_, project, screen) = Boot("BaseTypedLists");
        var extra = new GlueEntityList();

        project.AddList(@"Entities\Enemy", extra);
        var zombie = project.CreateEntity(@"Entities\Zombie", screen);
        var enemy = project.CreateEntity(@"Entities\Enemy", screen);

        extra.ShouldBe(new[] { enemy });
        zombie.ShouldNotBeNull();
    }

    [Fact]
    public void RemoveList_OnABaseEntity_LeavesDerivedSpawnsJoining()
    {
        var (_, project, screen) = Boot("BaseTypedLists");

        project.RemoveList(@"Entities\Enemy", screen.GetList("EnemyList"));
        var zombie = project.CreateEntity(@"Entities\Zombie", screen);
        var enemy = project.CreateEntity(@"Entities\Enemy", screen);

        screen.GetList("EnemyList").ShouldContain(zombie);
        screen.GetList("EnemyList").ShouldNotContain(enemy);
    }

    [Fact]
    public void RemoveList_OnADerivedEntity_LeavesTheBaseEntityJoining()
    {
        var (_, project, screen) = Boot("BaseTypedLists");

        project.RemoveList(@"Entities\Zombie", screen.GetList("EnemyList"));
        var zombie = project.CreateEntity(@"Entities\Zombie", screen);
        var enemy = project.CreateEntity(@"Entities\Enemy", screen);

        screen.GetList("EnemyList").ShouldNotContain(zombie);
        screen.GetList("EnemyList").ShouldContain(enemy);
    }

    // The chain below is Enemy > Zombie > Boss. A list typed with the in-between Zombie reaches only
    // Zombie's own factory; one typed with the most derived Boss reaches only Boss.
    [Fact]
    public void ListsToAddTo_ByDefault_InBetweenTypedList_ReachesOnlyThatEntity()
    {
        var project = LoadBaseTyped();
        RetypeEnemyList(project, @"Entities\Zombie");
        var (_, screen) = Start(project);
        var list = screen.GetList("EnemyList");

        project.ListsToAddTo(@"Entities\Zombie").ShouldBe(new[] { list });
        project.ListsToAddTo(@"Entities\Enemy").ShouldBeEmpty();
        project.ListsToAddTo(@"Entities\Boss").ShouldBeEmpty();
        project.CreateEntity(@"Entities\Boss", screen);
        list.ShouldBeEmpty();
        var zombie = project.CreateEntity(@"Entities\Zombie", screen);
        list.ShouldBe(new[] { zombie });
    }

    [Fact]
    public void ListsToAddTo_ByDefault_MostDerivedTypedList_ReachesOnlyThatEntity()
    {
        var project = LoadBaseTyped();
        RetypeEnemyList(project, @"Entities\Boss");
        var (_, screen) = Start(project);

        project.ListsToAddTo(@"Entities\Boss").ShouldBe(new[] { screen.GetList("EnemyList") });
        project.ListsToAddTo(@"Entities\Zombie").ShouldBeEmpty();
        project.ListsToAddTo(@"Entities\Enemy").ShouldBeEmpty();
    }

    [Fact]
    public void CreateEntity_MostBaseTypedList_IsJoinedByEveryDescendantSpawn()
    {
        var (_, project, screen) = Boot("BaseTypedLists");

        var spawns = new[] { @"Entities\Enemy", @"Entities\Zombie", @"Entities\Boss" }
            .Select(name => project.CreateEntity(name, screen)).ToList();

        screen.GetList("EnemyList").ShouldBe(spawns);
    }

    private static void RetypeEnemyList(GlueProject project, string entityName) =>
        project.StartUpScreen!.NamedObjects.Single(n => n.InstanceName == "EnemyList")
            .SourceClassGenericType = entityName;

    // Only an entity with a factory (CreatedByOtherEntities, not abstract) receives the list at screen
    // init. A most-base list still reaches descendants through a skipped intermediate entity.
    [Fact]
    public void ListsToAddTo_ByDefault_SkipsEntitiesWithoutAFactory()
    {
        var project = LoadBaseTyped();
        project.FindEntity(@"Entities\Zombie")!.CreatedByOtherEntities = false;
        var (_, screen) = Start(project);

        project.ListsToAddTo(@"Entities\Zombie").ShouldBeEmpty();
        project.ListsToAddTo(@"Entities\Enemy").ShouldBe(new[] { screen.GetList("EnemyList") });
        project.ListsToAddTo(@"Entities\Boss").ShouldBe(new[] { screen.GetList("EnemyList") });
    }

    [Fact]
    public void ListsToAddTo_ByDefault_SkipsAbstractEntities()
    {
        var project = LoadBaseTyped();
        project.FindEntity(@"Entities\Zombie")!.NamedObjects.Add(
            new FlatRedBall2.Glue.Model.NamedObjectSave { InstanceName = "Slot", SetByDerived = true });
        var (_, screen) = Start(project);

        project.ListsToAddTo(@"Entities\Zombie").ShouldBeEmpty();
        project.ListsToAddTo(@"Entities\Boss").ShouldBe(new[] { screen.GetList("EnemyList") });
    }

    private static GlueProject LoadBaseTyped() => GlueProject.Load(Path.Combine(
        AppContext.BaseDirectory, "Glue", "Fixtures", "BaseTypedLists", "BaseTypedLists.gluj"));

    private static (FlatRedBallService Engine, GlueScreen Screen) Start(GlueProject project)
    {
        var engine = new FlatRedBallService();
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });
        return (engine, (GlueScreen)engine.CurrentScreen);
    }

    [Fact]
    public void ListsToAddTo_AfterAScreenChange_HoldsOnlyTheNewScreensLists()
    {
        var (engine, project, oldScreen) = Boot();
        var enemy = project.CreateEntity(EnemyName, oldScreen);
        var extra = enemy.GetList("BulletList");
        project.AddList(BlobName, extra);

        oldScreen.RestartScreen();
        engine.Update(new GameTime());

        var newScreen = (GlueScreen)engine.CurrentScreen;
        project.ListsToAddTo(BlobName).ShouldBe(new[] { newScreen.GetList("BulletList") });
        var spawned = project.CreateEntity(BlobName, newScreen);
        extra.ShouldNotContain(spawned);
    }

    [Fact]
    public void ListsOfTheOldScreen_AreNotCarriedIntoTheNext()
    {
        var (engine, project, oldScreen) = Boot();
        var spawned = project.CreateEntity(BlobName, oldScreen);
        var oldList = oldScreen.GetList("BulletList");

        oldScreen.RestartScreen();
        engine.Update(new GameTime());

        var newScreen = (GlueScreen)engine.CurrentScreen;
        newScreen.ShouldNotBeSameAs(oldScreen);
        newScreen.GetList("BulletList").ShouldBeEmpty();
        newScreen.GetList("BulletList").ShouldNotBeSameAs(oldList);
        oldList.ShouldNotContain(spawned);
    }

    [Fact]
    public void DestroyedMember_LeavesEveryListItWasIn()
    {
        var (_, project, screen) = Boot();
        var enemy = project.CreateEntity(EnemyName, screen);
        var bullet = project.CreateEntity(BlobName, screen);
        enemy.GetList("BulletList").Add(bullet);

        bullet.Destroy();

        enemy.GetList("BulletList").ShouldBeEmpty();
        screen.GetList("BulletList").ShouldBeEmpty();
    }

    [Fact]
    public void DestroyingTheOwner_DestroysTheMembersOfItsLists()
    {
        var (_, project, screen) = Boot();
        var enemy = project.CreateEntity(EnemyName, screen);
        var bullet = project.CreateEntity(BlobName, screen);
        enemy.GetList("BulletList").Add(bullet);
        bool destroyed = false;
        bullet.Destroyed += () => destroyed = true;

        enemy.Destroy();

        destroyed.ShouldBeTrue();
        project.InstancesOf(BlobName).ShouldNotContain(bullet);
    }

    [Fact]
    public void GetList_UnknownName_ThrowsNamingTheOwnersLists()
    {
        var (_, _, screen) = Boot();

        var error = Should.Throw<ArgumentException>(() => screen.GetList("Nope"));

        error.Message.ShouldContain("BulletList");
    }
}
