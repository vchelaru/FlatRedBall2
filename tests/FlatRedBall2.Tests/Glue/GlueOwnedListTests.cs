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

    [Fact]
    public void AddList_OnABaseEntity_AppliesToDerivedSpawns()
    {
        var (_, project, screen) = Boot("BaseTypedLists");
        var extra = new GlueEntityList();

        project.AddList(@"Entities\Enemy", extra);
        var zombie = project.CreateEntity(@"Entities\Zombie", screen);

        extra.ShouldContain(zombie);
        screen.GetList("EnemyList").ShouldContain(zombie);
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
