using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Nodes;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// GlueProject plays the role of the factories for loaded entities, and lives for the whole run. A
// screen change destroys every factory's instances; the entities it tracked must go the same way,
// or the next screen's lists and collision relationships inherit the last screen's corpses.
public class GlueScreenChangeTests
{
    private const string BlobName = @"Entities\Blob";
    private const string GameScreenName = @"Screens\GameScreen";

    private static (FlatRedBallService Engine, GlueProject Project) Boot(string fixture)
    {
        var engine = new FlatRedBallService();
        var project = GlueProject.Load(Path.Combine(
            AppContext.BaseDirectory, "Glue", "Fixtures", fixture, fixture + ".gluj"));
        engine.GlueProject = project;
        engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });
        return (engine, project);
    }

    private static void Step(FlatRedBallService engine) => engine.Update(new GameTime());

    [Fact]
    public void RestartScreen_SpawnedGlueEntity_IsDestroyedAndNoLongerTracked()
    {
        var (engine, project) = Boot("TwoLists");
        var spawned = project.CreateEntity(BlobName, engine.CurrentScreen);
        bool destroyed = false;
        spawned.Destroyed += () => destroyed = true;

        engine.CurrentScreen.RestartScreen();
        Step(engine);

        destroyed.ShouldBeTrue();
        project.InstancesOf(BlobName).ShouldNotContain(spawned);
        project.InstancesOfList("ListA").ShouldNotContain(spawned);
        project.InstancesOfList("ListC").ShouldNotContain(spawned);
    }

    // The fixture's screen authors one Blob in ListA (BlobInA). Each restart must leave exactly the
    // new screen's own, however many times the same screen type is retried.
    [Fact]
    public void RestartScreen_TwiceInARow_ListHoldsOnlyTheCurrentScreensMembers()
    {
        var (engine, project) = Boot("TwoLists");
        int authoredInA = project.InstancesOfList("ListA").Count;
        authoredInA.ShouldBe(1);

        for (int i = 0; i < 2; i++)
        {
            project.CreateEntity(BlobName, engine.CurrentScreen);
            engine.CurrentScreen.RestartScreen();
            Step(engine);
        }

        project.InstancesOfList("ListA").Count.ShouldBe(authoredInA);
        project.InstancesOfList("ListB").Count.ShouldBe(1);
        project.InstancesOfList("ListC").ShouldBeEmpty();
        project.InstancesOf(BlobName).Count.ShouldBe(2);
    }

    // The new screen's relationship binds to the project's live list, so a stale entry in it is
    // collided against forever.
    [Fact]
    public void RestartScreen_RelationshipBuiltInNewScreen_DoesNotSeeOldEntities()
    {
        var (engine, project) = Boot("TwoLists");
        var old = project.CreateEntity(BlobName, engine.CurrentScreen);
        old.X = 0f;
        old.Y = 0f;

        engine.CurrentScreen.RestartScreen();
        Step(engine);

        var screen = (GlueScreen)engine.CurrentScreen;
        var relationship =
            (CollisionRelationship<GlueEntity, GlueEntity>)screen.Objects["ListAVsTarget"];
        var seen = new List<GlueEntity>();
        relationship.CollisionOccurred += (blob, _) => seen.Add(blob);
        relationship.RunCollisions();

        seen.ShouldNotContain(old);
    }

    [Fact]
    public void MoveToScreen_ToAnotherGlueScreen_ReleasesTheOldScreensEntities()
    {
        var (engine, project) = Boot("DoorsDemo");
        const string doorName = @"Entities\Door";
        var door = project.CreateEntity(doorName, engine.CurrentScreen);

        engine.CurrentScreen.MoveToScreen(@"Screens\Level1");
        Step(engine);

        project.InstancesOf(doorName).ShouldNotContain(door);
        project.InstancesOf(@"Entities\Player").ShouldAllBe(p => engine.CurrentScreen.Entities.Contains(p));
    }

    // The loaded definitions outlive every screen: only instances are released.
    [Fact]
    public void RestartScreen_KeepsTheLoadedProjectData()
    {
        var (engine, project) = Boot("TwoLists");

        engine.CurrentScreen.RestartScreen();
        Step(engine);

        project.FindScreen(GameScreenName).ShouldNotBeNull();
        project.FindEntity(BlobName).ShouldNotBeNull();
        project.CreateEntity(BlobName, engine.CurrentScreen).ShouldNotBeNull();
        project.InstancesOfList("ListA").Count.ShouldBe(2);
    }

    // Every level derived from a shared base repeats the base's lists, so the same list name is
    // declared by many screens. The factory-list index is built from all of them: a spawn must
    // still land in "ListA" once, and leave it when destroyed.
    [Fact]
    public void CreateEntity_WhenSeveralScreensDeclareTheSameList_JoinsItOnceAndLeavesWhenDestroyed()
    {
        string root = Path.Combine(Path.GetTempPath(), "frb2-glue-samelist-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", "TwoLists"), root);
            string screens = Path.Combine(root, "Screens");
            string second = File.ReadAllText(Path.Combine(screens, "GameScreen.glsj"))
                .Replace("Screens\\\\GameScreen", "Screens\\\\Second");
            File.WriteAllText(Path.Combine(screens, "Second.glsj"), second);
            string gluj = Path.Combine(root, "TwoLists.gluj");
            File.WriteAllText(gluj, File.ReadAllText(gluj).Replace(
                "\"Name\": \"Screens\\\\GameScreen\"",
                "\"Name\": \"Screens\\\\GameScreen\" }, { \"Name\": \"Screens\\\\Second\""));
            var project = GlueProject.Load(gluj);
            project.FindScreen(@"Screens\Second").ShouldNotBeNull();
            var engine = new FlatRedBallService();
            engine.GlueProject = project;
            engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });

            var spawned = project.CreateEntity(BlobName, engine.CurrentScreen);
            project.InstancesOfList("ListA").Count(e => e == spawned).ShouldBe(1);

            spawned.Destroy();
            project.InstancesOfList("ListA").ShouldNotContain(spawned);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    // Characterization, not an endorsement: lists are tracked by bare name, so a list an entity
    // declares for itself lands in the same bucket as a screen list with that name. Here Target
    // declares its own ListA (holding one Blob) and the screen declares ListA (holding one Blob);
    // each would be 1 if scoped, but the project's ListA holds both. Flip this when lists are scoped.
    [Fact]
    public void InstancesOfList_EntityAndScreenDeclareSameListName_AreStillConflated()
    {
        string root = Path.Combine(Path.GetTempPath(), "frb2-glue-scope-" + Guid.NewGuid().ToString("N"));
        try
        {
            CopyDirectory(Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", "TwoLists"), root);

            var screenJson = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "Screens", "GameScreen.glsj")))!;
            var listA = screenJson["NamedObjects"]!.AsArray()
                .Single(n => (string?)n!["InstanceName"] == "ListA")!.DeepClone();
            string targetPath = Path.Combine(root, "Entities", "Target.glej");
            var targetJson = JsonNode.Parse(File.ReadAllText(targetPath))!;
            targetJson["NamedObjects"]!.AsArray().Add(listA);
            File.WriteAllText(targetPath, targetJson.ToJsonString());

            var project = GlueProject.Load(Path.Combine(root, "TwoLists.gluj"));
            var engine = new FlatRedBallService();
            engine.GlueProject = project;
            engine.Start<GlueScreen>(s => { s.Save = project.StartUpScreen; s.Project = project; });

            var screenListA = (List<object>)((GlueScreen)engine.CurrentScreen).Objects["ListA"];
            screenListA.Count.ShouldBe(1);
            project.InstancesOfList("ListA").Count.ShouldBe(2);
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (string file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), true);
        foreach (string directory in Directory.GetDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}
