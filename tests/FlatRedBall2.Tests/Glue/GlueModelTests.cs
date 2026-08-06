using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers the POCO mirror of FRB1's Glue save classes. The mirror's job is to read what Glue
// actually writes — which is not the same as what its C# classes declare, because element files are
// written with defaults omitted and much of the data lives in name/value bags.
public class GlueModelTests
{
    private static string FixturePath(params string[] parts) =>
        Path.Combine(new[] { AppContext.BaseDirectory, "Glue", "Fixtures" }.Concat(parts).ToArray());

    private static GlueProjectSave LoadProject(string project, string glujFileName) =>
        JsonSerializer.Deserialize(
            File.ReadAllText(FixturePath(project, glujFileName)),
            GlueJsonContext.Default.GlueProjectSave)!;

    private static ScreenSave LoadScreen(string project, string screenFileName) =>
        JsonSerializer.Deserialize(
            File.ReadAllText(FixturePath(project, "Screens", screenFileName)),
            GlueJsonContext.Default.ScreenSave)!;

    private static EntitySave LoadEntity(string project, string entityFileName) =>
        JsonSerializer.Deserialize(
            File.ReadAllText(FixturePath(project, "Entities", entityFileName)),
            GlueJsonContext.Default.EntitySave)!;

    [Fact]
    public void Deserialize_CustomVariableBagBackedMembers_ReadThroughProperties()
    {
        // Beefball's ScoreHud.Score1 shape: the variable is declared string but exposed as int, with
        // a converter between them. All three live in the bag, and CreatesProperty's key is plural.
        string json = @"{
            ""Name"": ""Score1"",
            ""Properties"": [
                { ""Name"": ""Type"", ""Value"": ""string"" },
                { ""Name"": ""OverridingPropertyType"", ""Value"": ""int"" },
                { ""Name"": ""TypeConverter"", ""Value"": ""<default>"" },
                { ""Name"": ""CreatesProperties"", ""Value"": true }
            ]
        }";

        var variable = JsonSerializer.Deserialize(json, GlueJsonContext.Default.CustomVariable)!;

        variable.Type.ShouldBe("string");
        variable.OverridingPropertyType.ShouldBe("int");
        variable.TypeConverter.ShouldBe("<default>");
        variable.CreatesProperty.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_CustomVariableOmittingDefaultValue_ReportsNoAuthoredValue()
    {
        // 423 of FRB1's 590 CustomVariables have no DefaultValue, including X/Y/Z on every Beefball
        // entity. FRB1 skips those entirely; treating absent as 0 would move every entity to origin.
        string json = @"{ ""Name"": ""X"", ""SetByDerived"": true }";

        var variable = JsonSerializer.Deserialize(json, GlueJsonContext.Default.CustomVariable)!;

        variable.HasAuthoredValue.ShouldBeFalse();
    }

    [Fact]
    public void Deserialize_CustomVariableWithNoneSentinel_NormalizesToNull()
    {
        // FRB1 maps "<NONE>" in its property setters, which Newtonsoft runs and STJ does not. Left
        // literal, this tunnels to an object named "<NONE>".
        string json = @"{
            ""Name"": ""Thing"",
            ""SourceObject"": ""<NONE>"",
            ""SourceObjectProperty"": ""<NONE>"",
            ""DefaultValue"": ""<NONE>""
        }";

        var variable = JsonSerializer.Deserialize(json, GlueJsonContext.Default.CustomVariable)!;

        variable.SourceObject.ShouldBeNull();
        variable.SourceObjectProperty.ShouldBeNull();
        variable.IsTunneling.ShouldBeFalse();
        variable.HasAuthoredValue.ShouldBeFalse();
    }

    [Fact]
    public void Deserialize_DerivedElement_ComputesIsAbstractAndBaseElement()
    {
        // Both are computed in FRB1. IsAbstract is get-only yet still written to disk, so the
        // serialized value must not be trusted; BaseElement is written by screens and never by
        // entities, so binding it would make the two disagree.
        var gameScreen = LoadScreen("DoorsDemo", "GameScreen.glsj");
        var level1 = LoadScreen("DoorsDemo", "Level1.glsj");

        // GameScreen leaves Map, SolidCollision and CloudCollision for a derived screen to supply.
        gameScreen.IsAbstract.ShouldBeTrue();
        level1.IsAbstract.ShouldBeFalse();
        level1.BaseElement.ShouldBe(@"Screens\GameScreen");
    }

    [Fact]
    public void Deserialize_EntityImplementsICollidable_ReadsThroughTheBag()
    {
        // FRB1 declares this one [JsonIgnore] over Properties while its three siblings are plain
        // members. Bound as a plain member it reads false for every project that sets it.
        var player = LoadEntity("DoorsDemo", "Player.glej");

        player.ImplementsICollidable.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_EntityWithBaseEntity_ExposesItAsBaseElement()
    {
        // Entities write only BaseEntity — EntitySave.BaseElement is [JsonIgnore] in FRB1.
        string json = @"{ ""Name"": ""Entities\\Derived"", ""BaseEntity"": ""Entities\\Base"" }";

        var entity = JsonSerializer.Deserialize(json, GlueJsonContext.Default.EntitySave)!;

        entity.BaseElement.ShouldBe(@"Entities\Base");
    }

    [Fact]
    public void Deserialize_ListNamedObject_ExposesItsElementTypeAndFullDefinition()
    {
        // SourceClassType is the literal "PositionedObjectList<T>" with an unresolved argument; the
        // real element type lives in a sibling field, which nothing read until now.
        var gameScreen = LoadScreen("DoorsDemo", "GameScreen.glsj");

        var playerList = gameScreen.NamedObjects.Single(o => o.InstanceName == "PlayerList");

        playerList.SourceClassGenericType.ShouldBe(@"Entities\Player");
        playerList.IsFullyDefined.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_NamedObjectOmittingIsDisabled_DefaultsToEnabled()
    {
        string json = @"{ ""InstanceName"": ""Thing"" }";

        var namedObject = JsonSerializer.Deserialize(json, GlueJsonContext.Default.NamedObjectSave)!;

        namedObject.IsDisabled.ShouldBeFalse();
        namedObject.CurrentState.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_DerivedScreen_RetainsOwnObjectsWithDefinedByBase()
    {
        // Level1 derives from GameScreen and redeclares four of its objects with DefinedByBase set.
        // Phase 1 must retain them exactly as written — merging and deduping are Phase 6's job.
        var level1 = LoadScreen("DoorsDemo", "Level1.glsj");

        level1.BaseScreen.ShouldBe(@"Screens\GameScreen");
        level1.NamedObjects.Select(o => o.InstanceName)
            .ShouldBe(new[] { "Map", "SolidCollision", "CloudCollision", "PlayerList" });
        level1.NamedObjects.ShouldAllBe(o => o.DefinedByBase);

        var playerList = level1.NamedObjects.Single(o => o.InstanceName == "PlayerList");
        playerList.InstantiatedByBase.ShouldBeTrue();
        playerList.ExposedInDerived.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_DuplicateJsonKey_TakesLastValue()
    {
        // A real committed sample (BeefballWeb.gluj) carries "FileVersion" twice. Newtonsoft hid
        // this behind last-one-wins; pin that System.Text.Json agrees rather than assuming it.
        string json = @"{ ""FileVersion"": 42, ""FileVersion"": 55 }";

        var project = JsonSerializer.Deserialize(json, GlueJsonContext.Default.GlueProjectSave)!;

        project.FileVersion.ShouldBe(55);
    }

    [Fact]
    public void Deserialize_EntityWithReferencedFiles_RetainsThemUnloaded()
    {
        var player = LoadEntity("DoorsDemo", "Player.glej");

        player.NamedObjects.Count.ShouldBe(2);
        player.CustomVariables.Count.ShouldBe(6);
        player.ReferencedFiles.Count.ShouldBe(5);
    }

    [Fact]
    public void Deserialize_GlujHeader_ReadsVersionStartUpScreenAndReferences()
    {
        var project = LoadProject("DoorsDemo", "DoorsDemo.gluj");

        project.FileVersion.ShouldBe(60);
        project.StartUpScreen.ShouldBe(@"Screens\Level1");
        project.ScreenReferences.Select(r => r.Name)
            .ShouldBe(new[] { @"Screens\GameScreen", @"Screens\Level1" });
        project.EntityReferences.Select(r => r.Name)
            .ShouldBe(new[] { @"Entities\Door", @"Entities\Player" });
    }

    [Fact]
    public void Deserialize_NamedObjectOmittingAttachToContainer_DefaultsToFalse()
    {
        // The counter-example to the constructor-defaults rule: FRB1 deliberately leaves
        // AttachToContainer out of its constructor, so absent must mean false here.
        string json = @"{ ""InstanceName"": ""Thing"" }";

        var namedObject = JsonSerializer.Deserialize(json, GlueJsonContext.Default.NamedObjectSave)!;

        namedObject.AttachToContainer.ShouldBeFalse();
    }

    [Fact]
    public void Deserialize_NamedObjectOmittingConstructorDefaults_KeepsThemTrue()
    {
        // Element files are written with defaults omitted, and FRB1 restores these in its
        // constructor. A mirror that lets them fall to false reads every real project as empty.
        string json = @"{ ""InstanceName"": ""CircleInstance"" }";

        var namedObject = JsonSerializer.Deserialize(json, GlueJsonContext.Default.NamedObjectSave)!;

        namedObject.Instantiate.ShouldBeTrue();
        namedObject.AddToManagers.ShouldBeTrue();
        namedObject.IncludeInICollidable.ShouldBeTrue();
        namedObject.IncludeInIClickable.ShouldBeTrue();
        namedObject.CallActivity.ShouldBeTrue();
        namedObject.GenerateTimedEmit.ShouldBeTrue();
    }

    [Fact]
    public void Deserialize_OutdatedProjectWithCustomClasses_LoadsCleanly()
    {
        // ChickenClicker is FileVersion 42 and carries a populated CustomClasses array, which this
        // epic excludes. Excluded shapes must be ignored, not rejected.
        var project = LoadProject("ChickenClicker", "ChickenClicker.gluj");

        project.FileVersion.ShouldBe(42);
        project.StartUpScreen.ShouldBe(@"Screens\MenuScreen");
        project.ScreenReferences.Count.ShouldBe(3);
    }

    [Fact]
    public void Deserialize_ScreenWithNestedObjects_PreservesContainedObjectNesting()
    {
        // GameScreen has nine top-level objects; a tenth lives inside PlayerList.ContainedObjects.
        // Flattening that into the top level would silently corrupt the object graph.
        var gameScreen = LoadScreen("DoorsDemo", "GameScreen.glsj");

        gameScreen.NamedObjects.Count.ShouldBe(9);

        var playerList = gameScreen.NamedObjects.Single(o => o.InstanceName == "PlayerList");
        playerList.ContainedObjects.Count.ShouldBe(1);
        playerList.ContainedObjects[0].SourceClassType.ShouldBe(@"Entities\Player");
    }

    [Fact]
    public void Deserialize_ValueBagBackedMember_ReadsThroughProperties()
    {
        // CustomVariable.Type has no JSON field of its own — it lives in the Properties bag, so the
        // mirror must expose it as an accessor rather than a deserialized property.
        var player = LoadEntity("DoorsDemo", "Player.glej");

        var withType = player.CustomVariables.First(v => v.Type is not null);
        withType.Type.ShouldNotBeNullOrEmpty();
    }
}
