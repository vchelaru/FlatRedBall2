using System;
using System.IO;
using System.Text.Json;
using FlatRedBall2.Collision;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers applying an element's CustomVariables — the values an author tunes in Glue's variable grid,
// which are a separate list from NamedObjects and reach three different destinations depending on
// whether the variable tunnels, names an engine property, or names nothing at all.
public class GlueCustomVariableTests
{
    private static EntitySave EntityOf(string json) =>
        JsonSerializer.Deserialize(json, GlueJsonContext.Default.EntitySave)!;

    private static EntitySave LoadFixtureEntity(string project, string fileName) =>
        JsonSerializer.Deserialize(
            File.ReadAllText(Path.Combine(
                AppContext.BaseDirectory, "Glue", "Fixtures", project, "Entities", fileName)),
            GlueJsonContext.Default.EntitySave)!;

    [Fact]
    public void BuildObjects_BeefballPlayerBall_AppliesEveryVariableToItsOwnDestination()
    {
        // The one fixture that exercises all three destinations at once: Drag names an engine
        // property, MovementSpeed names nothing, and CooldownCircleRadius tunnels into a child.
        var entity = new GlueEntity { Save = LoadFixtureEntity("Beefball", "PlayerBall.glej") };

        entity.BuildObjects();

        entity.Drag.ShouldBe(1f);
        entity.Get<float>("MovementSpeed").ShouldBe(300f);
        ((Circle)entity.Objects["CooldownCircle"]).Radius.ShouldBe(16f);
    }

    [Fact]
    public void BuildObjects_ExposedVariableNamingAnEngineProperty_WritesThatProperty()
    {
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""CustomVariables"": [ {
                    ""Name"": ""Drag"", ""DefaultValue"": 2.5,
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        entity.Drag.ShouldBe(2.5f);
    }

    [Fact]
    public void BuildObjects_VariableWithNoAuthoredValue_LeavesTheTargetUntouched()
    {
        // PlayerBall declares X, Y and Z with no DefaultValue. Treating absent as 0 would move every
        // entity in every project back to the origin, after Phase 2 positioned it correctly.
        var entity = new GlueEntity
        {
            X = 40f,
            Save = LoadFixtureEntity("Beefball", "PlayerBall.glej"),
        };

        entity.BuildObjects();

        entity.X.ShouldBe(40f);
    }

    [Fact]
    public void BuildObjects_VariableWithNoMatchingMember_IsReadableFromTheVariableBag()
    {
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""CustomVariables"": [ {
                    ""Name"": ""DashSpeed"", ""DefaultValue"": 600.0,
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        entity.Get<float>("DashSpeed").ShouldBe(600f);
        entity.BuildDiagnostics.ShouldNotContain(d => d.Severity == GlueDiagnosticSeverity.Error);
    }

    [Fact]
    public void BuildObjects_TunneledVariable_WritesTheMemberOnItsSourceObject()
    {
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""Shape"",
                    ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle""
                } ],
                ""CustomVariables"": [ {
                    ""Name"": ""ShapeRadius"", ""DefaultValue"": 24.0,
                    ""SourceObject"": ""Shape"", ""SourceObjectProperty"": ""Radius"",
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        ((Circle)entity.Objects["Shape"]).Radius.ShouldBe(24f);
        entity.Get<float>("ShapeRadius").ShouldBe(24f);
    }

    [Fact]
    public void BuildObjects_TunneledColourVariable_ResolvesNamesBeyondTheCommonOnes()
    {
        // Glue offers the whole XNA colour list and validates nothing, so any of ~140 names can
        // reach the loader. Aquamarine is authored in FRB1's own test project.
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""Shape"",
                    ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle""
                } ],
                ""CustomVariables"": [ {
                    ""Name"": ""ShapeColor"", ""DefaultValue"": ""Aquamarine"",
                    ""SourceObject"": ""Shape"", ""SourceObjectProperty"": ""Color"",
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""Color"" } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        ((Circle)entity.Objects["Shape"]).Color.ShouldBe(
            new Microsoft.Xna.Framework.Color(127, 255, 212));
    }

    [Fact]
    public void BuildObjects_TunneledVariableWithMissingSourceObject_WarnsWithoutThrowing()
    {
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""CustomVariables"": [ {
                    ""Name"": ""GoneRadius"", ""DefaultValue"": 24.0,
                    ""SourceObject"": ""Gone"", ""SourceObjectProperty"": ""Radius"",
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        entity.BuildDiagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Warning && d.Message.Contains("Gone"));
    }

    [Fact]
    public void BuildObjects_TunneledVariableWithOverridingType_CoercesToTheTargetsType()
    {
        // Beefball's ScoreHud.Score1 shape: authored as an int, target member is a string. The
        // stored value is in the overriding type, so converting straight to the target would fail.
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""Shape"",
                    ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle""
                } ],
                ""CustomVariables"": [ {
                    ""Name"": ""ShapeName"", ""DefaultValue"": 7,
                    ""SourceObject"": ""Shape"", ""SourceObjectProperty"": ""Name"",
                    ""Properties"": [
                        { ""Name"": ""Type"", ""Value"": ""string"" },
                        { ""Name"": ""OverridingPropertyType"", ""Value"": ""int"" },
                        { ""Name"": ""TypeConverter"", ""Value"": ""<default>"" }
                    ]
                } ]
            }"),
        };

        entity.BuildObjects();

        ((Circle)entity.Objects["Shape"]).Name.ShouldBe("7");
    }

    [Fact]
    public void BuildObjects_TunneledVariableWithUnsupportedConverter_WarnsRatherThanMisformatting()
    {
        // Glue ships Minutes:Seconds too. Falling through to default formatting would render 125 as
        // "125" instead of "2:05" — wrong, but plausible enough to go unnoticed.
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""Shape"",
                    ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle""
                } ],
                ""CustomVariables"": [ {
                    ""Name"": ""ShapeName"", ""DefaultValue"": 125,
                    ""SourceObject"": ""Shape"", ""SourceObjectProperty"": ""Name"",
                    ""Properties"": [
                        { ""Name"": ""Type"", ""Value"": ""string"" },
                        { ""Name"": ""OverridingPropertyType"", ""Value"": ""int"" },
                        { ""Name"": ""TypeConverter"", ""Value"": ""Minutes:Seconds"" }
                    ]
                } ]
            }"),
        };

        entity.BuildObjects();

        ((Circle)entity.Objects["Shape"]).Name.ShouldBeNull();
        entity.BuildDiagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Warning && d.Message.Contains("ShapeName"));
    }

    [Fact]
    public void BuildObjects_TunneledVariableWithCommaSeparatingConverter_FormatsWithGroupSeparators()
    {
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""Shape"",
                    ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle""
                } ],
                ""CustomVariables"": [ {
                    ""Name"": ""ShapeName"", ""DefaultValue"": 1000,
                    ""SourceObject"": ""Shape"", ""SourceObjectProperty"": ""Name"",
                    ""Properties"": [
                        { ""Name"": ""Type"", ""Value"": ""string"" },
                        { ""Name"": ""OverridingPropertyType"", ""Value"": ""int"" },
                        { ""Name"": ""TypeConverter"", ""Value"": ""Comma Separating"" }
                    ]
                } ]
            }"),
        };

        entity.BuildObjects();

        ((Circle)entity.Objects["Shape"]).Name.ShouldBe("1,000");
    }

    [Fact]
    public void BuildObjects_NumericVariableAuthoredAsEmptyString_IsTreatedAsUnset()
    {
        // Glue writes "" for a numeric or bool variable that was never given a value, and skips the
        // assignment. Parsing it as a number instead would zero a real value.
        var entity = new GlueEntity
        {
            Drag = 3f,
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""CustomVariables"": [ {
                    ""Name"": ""Drag"", ""DefaultValue"": """",
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        entity.Drag.ShouldBe(3f);
    }

    [Fact]
    public void BuildObjects_VariableDeclaredAfterAnInstruction_OverridesIt()
    {
        // FRB1 assigns NamedObject instructions first and element variables second, so the variable
        // wins. Beefball's score labels rely on it: the instruction is placeholder text.
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""NamedObjects"": [ {
                    ""InstanceName"": ""Shape"",
                    ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"",
                    ""InstructionSaves"": [ { ""Member"": ""Radius"", ""Value"": 99.0 } ]
                } ],
                ""CustomVariables"": [ {
                    ""Name"": ""ShapeRadius"", ""DefaultValue"": 8.0,
                    ""SourceObject"": ""Shape"", ""SourceObjectProperty"": ""Radius"",
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        ((Circle)entity.Objects["Shape"]).Radius.ShouldBe(8f);
    }

    [Fact]
    public void BuildObjects_VariablesDeclaredTwice_ApplyInArrayOrderSoTheLastWins()
    {
        // StateEntity relies on this: its CurrentState variable sets X, and a later X variable
        // overrides it. Sorting the list would silently change the loaded value.
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""CustomVariables"": [
                    { ""Name"": ""Drag"", ""DefaultValue"": 1.0,
                      ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] },
                    { ""Name"": ""Drag"", ""DefaultValue"": 5.0,
                      ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] }
                ]
            }"),
        };

        entity.BuildObjects();

        entity.Drag.ShouldBe(5f);
    }

    [Fact]
    public void Get_RequestedTypeDrivesTheRead_NotTheDeclaredType()
    {
        // The declared Type is often not a CLR type at all — a CSV path, a state-category name. The
        // caller's T is authoritative, matching PropertySaveExtensions.GetValue<T>.
        var entity = new GlueEntity
        {
            Save = EntityOf(@"{
                ""Name"": ""Entities\\Test"",
                ""CustomVariables"": [ {
                    ""Name"": ""Count"", ""DefaultValue"": 3,
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""GlobalContent/Some.csv"" } ]
                } ]
            }"),
        };

        entity.BuildObjects();

        entity.Get<int>("Count").ShouldBe(3);
        entity.Get<float>("Count").ShouldBe(3f);
    }

    [Fact]
    public void Get_UnknownName_ReturnsDefaultWithoutThrowing()
    {
        var entity = new GlueEntity { Save = EntityOf(@"{ ""Name"": ""Entities\\Test"" }") };

        entity.BuildObjects();

        entity.Get<float>("Nope").ShouldBe(0f);
    }
}
