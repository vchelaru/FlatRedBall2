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
// Shield and a BulletList-vs-Shield relationship.
public class GlueOwnedListTests
{
    private const string BlobName = @"Entities\Blob";
    private const string EnemyName = @"Entities\Enemy";

    private static (FlatRedBallService Engine, GlueProject Project, GlueScreen Screen) Boot()
    {
        var engine = new FlatRedBallService();
        var project = GlueProject.Load(Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", "OwnedLists", "OwnedLists.gluj"));
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

        var bullet = first.CreateInList("BulletList");

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
        var firstBullet = first.CreateInList("BulletList");
        var secondBullet = second.CreateInList("BulletList");

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
    public void FactoryPathSpawn_JoinsTheScreensAssociatedList_NotAnEntitysOwnList()
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
    public void ScreenAndEntityListsWithTheSameName_StaySeparate()
    {
        var (_, project, screen) = Boot();
        var enemy = project.CreateEntity(EnemyName, screen);

        project.CreateEntity(BlobName, screen);
        enemy.CreateInList("BulletList");
        enemy.CreateInList("BulletList");

        screen.GetList("BulletList").Count.ShouldBe(1);
        enemy.GetList("BulletList").Count.ShouldBe(2);
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
    public void DestroyedMember_LeavesItsList()
    {
        var (_, project, screen) = Boot();
        var enemy = project.CreateEntity(EnemyName, screen);
        var bullet = enemy.CreateInList("BulletList");
        var spawned = project.CreateEntity(BlobName, screen);

        bullet.Destroy();
        spawned.Destroy();

        enemy.GetList("BulletList").ShouldBeEmpty();
        screen.GetList("BulletList").ShouldBeEmpty();
    }

    [Fact]
    public void DestroyingTheOwner_DestroysTheMembersOfItsLists()
    {
        var (_, project, screen) = Boot();
        var enemy = project.CreateEntity(EnemyName, screen);
        var bullet = enemy.CreateInList("BulletList");
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
