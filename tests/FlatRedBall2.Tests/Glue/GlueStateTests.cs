using System;
using System.IO;
using System.Text.Json;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers applying Glue states. A state is a named snapshot of an element's variables, and the
// non-obvious part is that it is a *full* snapshot: every variable the state covers is assigned,
// falling back to the variable's own default where the state names no value.
public class GlueStateTests
{
    private static EntitySave EntityOf(string json) =>
        JsonSerializer.Deserialize(json, GlueJsonContext.Default.EntitySave)!;

    private static EntitySave LoadFixtureEntity(string project, string fileName) =>
        JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "Glue", "Fixtures", project, "Entities", fileName)),
            GlueJsonContext.Default.EntitySave)!;

    [Fact]
    public void BuildObjects_InitialStateVariable_AppliesThatState()
    {
        // The element's starting state is not a field — it is a CustomVariable whose declared type
        // names the category and whose value names the state.
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""CustomVariables"": [
                    { ""Name"": ""Speed"", ""DefaultValue"": 1.0,
                      ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] },
                    { ""Name"": ""CurrentSpeedsState"", ""DefaultValue"": ""Fast"",
                      ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""Speeds"" } ] } ],
                ""StateCategoryList"": [ {
                    ""Name"": ""Speeds"",
                    ""ExcludedVariables"": [],
                    ""States"": [
                        { ""Name"": ""Fast"", ""InstructionSaves"": [
                            { ""Member"": ""Speed"", ""Value"": 9.0 } ] } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        entity.Get<float>("Speed").ShouldBe(9f);
    }

    [Fact]
    public void BuildObjects_VariableDeclaredAfterTheInitialState_OverridesIt()
    {
        // FRB1's own StateEntity relies on this: its CurrentState sets X, and a later X variable
        // wins because variables apply in declaration order.
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""CustomVariables"": [
                    { ""Name"": ""CurrentSpeedsState"", ""DefaultValue"": ""Fast"",
                      ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""Speeds"" } ] },
                    { ""Name"": ""Speed"", ""DefaultValue"": 2.0,
                      ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] } ],
                ""StateCategoryList"": [ {
                    ""Name"": ""Speeds"",
                    ""ExcludedVariables"": [],
                    ""States"": [
                        { ""Name"": ""Fast"", ""InstructionSaves"": [
                            { ""Member"": ""Speed"", ""Value"": 9.0 } ] } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        entity.Get<float>("Speed").ShouldBe(2f);
    }

    [Fact]
    public void SetState_BeefballDashCategory_ResizesTheCooldownCircle()
    {
        var entity = new GlueEntity { Save = LoadFixtureEntity("Beefball", "PlayerBall.glej") };
        entity.BuildObjects();

        entity.SetState("DashCategory", "Tired");

        ((Circle)entity.Objects["CooldownCircle"]).Radius.ShouldBe(0f);

        entity.SetState("DashCategory", "Rested");

        ((Circle)entity.Objects["CooldownCircle"]).Radius.ShouldBe(16f);
    }

    [Fact]
    public void SetState_ExcludedVariable_IsLeftAlone()
    {
        // ExcludedVariables — not the instruction list — defines what a category covers. Beefball's
        // DashCategory excludes nine of PlayerBall's ten variables.
        var entity = new GlueEntity { Save = LoadFixtureEntity("Beefball", "PlayerBall.glej") };
        entity.BuildObjects();
        entity.Drag = 7f;

        entity.SetState("DashCategory", "Tired");

        entity.Drag.ShouldBe(7f);
    }

    [Fact]
    public void SetState_StateWithNoInstructions_AssignsFromTheVariableDefault()
    {
        // The case a natural implementation gets wrong: iterating InstructionSaves makes this state
        // a no-op, when it should reset the variable to its own default.
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""CustomVariables"": [ { ""Name"": ""Speed"", ""DefaultValue"": 1.0,
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] } ],
                ""StateCategoryList"": [ {
                    ""Name"": ""Speeds"",
                    ""ExcludedVariables"": [],
                    ""States"": [
                        { ""Name"": ""Fast"", ""InstructionSaves"": [
                            { ""Member"": ""Speed"", ""Value"": 9.0 } ] },
                        { ""Name"": ""Normal"", ""InstructionSaves"": [] } ]
                } ]
            }"),
        };
        entity.BuildObjects();

        entity.SetState("Speeds", "Fast");
        entity.Get<float>("Speed").ShouldBe(9f);

        entity.SetState("Speeds", "Normal");
        entity.Get<float>("Speed").ShouldBe(1f);
    }

    [Fact]
    public void SetState_UncategorizedState_AppliesWithoutACategoryName()
    {
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""CustomVariables"": [ { ""Name"": ""Speed"", ""DefaultValue"": 1.0,
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] } ],
                ""States"": [ { ""Name"": ""Fast"", ""InstructionSaves"": [
                    { ""Member"": ""Speed"", ""Value"": 9.0 } ] } ]
            }"),
        };
        entity.BuildObjects();

        entity.SetState("Fast");

        entity.Get<float>("Speed").ShouldBe(9f);
    }

    [Fact]
    public void SetState_UnknownState_WarnsWithoutThrowing()
    {
        var entity = new GlueEntity { Save = LoadFixtureEntity("Beefball", "PlayerBall.glej") };
        entity.BuildObjects();

        entity.SetState("DashCategory", "Nope");

        entity.BuildDiagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Warning && d.Message.Contains("Nope"));
    }
}
