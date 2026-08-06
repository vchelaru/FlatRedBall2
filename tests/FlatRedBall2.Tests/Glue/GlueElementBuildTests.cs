using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers building a whole element's objects — the step that turns a loaded GlueScreen or GlueEntity
// from data into something that renders.
public class GlueElementBuildTests
{
    private static ScreenSave Screen(string json) =>
        JsonSerializer.Deserialize(json, GlueJsonContext.Default.ScreenSave)!;

    private static EntitySave EntityOf(string json) =>
        JsonSerializer.Deserialize(json, GlueJsonContext.Default.EntitySave)!;

    private static EntitySave LoadFixtureEntity(string project, string fileName) =>
        JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "Glue", "Fixtures", project, "Entities", fileName)),
            GlueJsonContext.Default.EntitySave)!;

    [Fact]
    public void BuildObjects_ContainedObjects_AreBuiltIntoTheirList()
    {
        var screen = new GlueScreen
        {
            Save = Screen(@"{
                ""Name"": ""Screens\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""Shapes"",
                    ""SourceClassType"": ""FlatRedBall.Math.PositionedObjectList<T>"",
                    ""SourceType"": 2,
                    ""ContainedObjects"": [
                        { ""InstanceName"": ""A"", ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"" },
                        { ""InstanceName"": ""B"", ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"" }
                    ]
                } ]
            }"),
        };

        screen.BuildObjects();

        var list = screen.Objects["Shapes"].ShouldBeOfType<List<object>>();
        list.Count.ShouldBe(2);
        list.ShouldAllBe(item => item is Circle);
    }

    [Fact]
    public void BuildObjects_DoorsDemoPlayer_BuildsBothOfItsShapes()
    {
        var entity = new GlueEntity { Save = LoadFixtureEntity("DoorsDemo", "Player.glej") };

        entity.BuildObjects();

        // Player has a SpriteInstance and an AxisAlignedRectangleInstance; both are Phase 2 types.
        entity.Objects.Count.ShouldBe(2);
        entity.Objects["AxisAlignedRectangleInstance"].ShouldBeOfType<AARect>();
        entity.Objects["SpriteInstance"].ShouldBeOfType<FlatRedBall2.Rendering.Sprite>();
    }

    [Fact]
    public void BuildObjects_ObjectsAreAddressableByTheirGlueInstanceName()
    {
        var screen = new GlueScreen
        {
            Save = Screen(@"{
                ""Name"": ""Screens\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""CircleInstance"",
                    ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
                    ""InstructionSaves"": [ { ""Member"": ""Radius"", ""Value"": 24.0 } ]
                } ]
            }"),
        };

        screen.BuildObjects();

        ((Circle)screen.Objects["CircleInstance"]).Radius.ShouldBe(24f);
    }

    [Fact]
    public void BuildObjects_NestedEntityInstance_IsDeferredNotErrored()
    {
        // A list of entity instances is the common Glue pattern (PlayerList holding Entities\Player).
        // Instantiating one needs Phase 6, so it must be reported and skipped — not fail the build.
        var screen = new GlueScreen
        {
            Save = Screen(@"{
                ""Name"": ""Screens\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""PlayerList"",
                    ""SourceClassType"": ""FlatRedBall.Math.PositionedObjectList<T>"",
                    ""SourceType"": 2,
                    ""ContainedObjects"": [
                        { ""InstanceName"": ""Player1"", ""SourceClassType"": ""Entities\\Player"", ""SourceType"": 1 }
                    ]
                } ]
            }"),
        };

        screen.BuildObjects();

        ((List<object>)screen.Objects["PlayerList"]).ShouldBeEmpty();
        screen.BuildDiagnostics.ShouldContain(d => d.Severity == GlueDiagnosticSeverity.Warning);
        screen.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
    }

    [Fact]
    public void BuildObjects_TypesOwnedByLaterPhases_AreSkippedWithoutErrors()
    {
        var screen = new GlueScreen
        {
            Save = Screen(@"{
                ""Name"": ""Screens\\Test"",
                ""NamedObjects"": [
                    { ""InstanceName"": ""Label"", ""SourceClassType"": ""FlatRedBall.Graphics.Text"" },
                    { ""InstanceName"": ""Ok"", ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"" }
                ]
            }"),
        };

        screen.BuildObjects();

        screen.Objects.Count.ShouldBe(1);
        screen.Objects.ShouldContainKey("Ok");
        // Tile types and collision relationships are no longer in this set — Phases 10 and 9
        // build them. Text still has no FRB2 type at all (D12).
        screen.BuildDiagnostics.ShouldContain(d => d.Message.Contains("FlatRedBall.Graphics.Text"));
        screen.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
    }

    [Fact]
    public void BuildObjects_CalledTwice_DoesNotLeaveTheFirstBuildsChildrenAttached()
    {
        // Hot reload restarts a screen, which rebuilds it. Without unregistering the previous build
        // every object would be duplicated, and the duplicates would still render.
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""Shape"",
                    ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
                    ""AttachToContainer"": true
                } ]
            }"),
        };

        entity.BuildObjects();
        var first = (Circle)entity.Objects["Shape"];
        entity.BuildObjects();

        entity.Objects.Count.ShouldBe(1);
        entity.Objects["Shape"].ShouldNotBeSameAs(first);
        entity.Children.Count(c => c is Circle).ShouldBe(1);
    }

    [Fact]
    public void BuildObjects_WithoutASave_DoesNothingAndDoesNotThrow()
    {
        var screen = new GlueScreen();

        screen.BuildObjects();

        screen.Objects.ShouldBeEmpty();
    }

    [Fact]
    public void BuildObjects_EntityShapesAreAttachedWhenAuthoredThatWay()
    {
        var entity = new GlueEntity
        {
            X = 50f,
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""Offset"",
                    ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
                    ""AttachToContainer"": true,
                    ""InstructionSaves"": [ { ""Member"": ""RelativeX"", ""Value"": 12.0 } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        var circle = (Circle)entity.Objects["Offset"];
        circle.Parent.ShouldBe(entity);
        circle.AbsoluteX.ShouldBe(62f);
    }
}
