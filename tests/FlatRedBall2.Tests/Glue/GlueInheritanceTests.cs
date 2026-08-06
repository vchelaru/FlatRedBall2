using System;
using System.IO;
using System.Linq;
using FlatRedBall2.Glue;
using FlatRedBall2.Glue.Model;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// Covers resolving BaseScreen/BaseEntity into a flattened element. FRB1 expresses inheritance as C#
// class inheritance; with one CLR type per element kind it has to become a data merge, and the
// merge rules are not "derived wins" — which side owns an object depends on its flags.
public class GlueInheritanceTests
{
    private static string Gluj(string project, string glujFileName) =>
        Path.Combine(AppContext.BaseDirectory, "Glue", "Fixtures", project, glujFileName);

    private static GlueLoadResult LoadFrom(params (string Path, string Json)[] files)
    {
        var options = new GlueLoadOptions
        {
            ReadAllText = path => files.Single(f => path.EndsWith(f.Path, StringComparison.Ordinal)).Json,
            ResolveFilePath = path => files.Any(f => path.EndsWith(f.Path, StringComparison.Ordinal))
                ? path
                : null,
        };

        return GlueProjectLoader.Load("Test.gluj", options);
    }

    [Fact]
    public void Load_BaseEntityNamingAnEngineType_IsReportedRatherThanResolved()
    {
        // 12 FRB1 entities derive from an engine type instead of an element, which FRB1 expresses as
        // `class Foo : Sprite`. One shared CLR type has no equivalent, so it must be visible.
        var result = LoadFrom(
            ("Test.gluj", @"{ ""FileVersion"": 60, ""EntityReferences"": [ { ""Name"": ""Entities\\Derived"" } ] }"),
            ("Entities/Derived.glej", @"{ ""Name"": ""Entities\\Derived"", ""BaseEntity"": ""FlatRedBall.Sprite"" }"));

        result.HasErrors.ShouldBeFalse();
        result.Diagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Warning && d.Message.Contains("FlatRedBall.Sprite"));
    }

    [Fact]
    public void Load_DerivedElementWithMissingBase_WarnsAndKeepsItsOwnObjects()
    {
        var result = LoadFrom(
            ("Test.gluj", @"{ ""FileVersion"": 60, ""ScreenReferences"": [ { ""Name"": ""Screens\\Derived"" } ] }"),
            ("Screens/Derived.glsj", @"{
                ""Name"": ""Screens\\Derived"",
                ""BaseScreen"": ""Screens\\Gone"",
                ""NamedObjects"": [ { ""InstanceName"": ""Own"", ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"" } ]
            }"));

        var derived = result.Project.Screens.Single();

        derived.NamedObjects.Select(o => o.InstanceName).ShouldBe(new[] { "Own" });
        result.Diagnostics.ShouldContain(d => d.Message.Contains(@"Screens\Gone"));
    }

    [Fact]
    public void Load_DerivedScreen_MergesEveryObjectItsBaseDeclares()
    {
        // Level1 declares four objects; GameScreen declares nine. Booting Level1 un-merged loses the
        // door list, all three collision relationships, and the camera — and Level1 is the project's
        // start-up screen.
        var result = GlueProjectLoader.Load(Gluj("DoorsDemo", "DoorsDemo.gluj"));

        var level1 = result.Project.Screens.Single(s => s.Name == @"Screens\Level1");

        level1.NamedObjects.Select(o => o.InstanceName).ShouldBe(new[]
        {
            "Map", "SolidCollision", "CloudCollision", "PlayerList", "DoorList",
            "PlayerVsCloudCollision", "PlayerVsSolidCollision", "PlayerVsDoor",
            "CameraControllingEntityInstance",
        }, ignoreOrder: true);
    }

    [Fact]
    public void Load_DerivedScreen_DerivedFileSourcedObjectReplacesTheBasePlaceholder()
    {
        // GameScreen's Map is an abstract SetByDerived placeholder; Level1 points it at its own .tmx.
        // FRB1 re-instantiates in this exact case because SourceFile is only read at construction —
        // skipping it makes every level render the base's map.
        var result = GlueProjectLoader.Load(Gluj("DoorsDemo", "DoorsDemo.gluj"));

        var map = result.Project.Screens
            .Single(s => s.Name == @"Screens\Level1").NamedObjects
            .Single(o => o.InstanceName == "Map");

        map.SourceType.ShouldBe(SourceType.File);
        map.SourceFile.ShouldBe("Screens/Level1/Level1Map.tmx");
    }

    [Fact]
    public void Load_DerivedScreen_DerivedInstructionsOverlayTheBaseDefinition()
    {
        // CloudCollision is owned by the base; Level1 contributes only an instruction. Both must
        // survive: the base's authored properties and the derived's override.
        var result = GlueProjectLoader.Load(Gluj("DoorsDemo", "DoorsDemo.gluj"));

        var cloud = result.Project.Screens
            .Single(s => s.Name == @"Screens\Level1").NamedObjects
            .Single(o => o.InstanceName == "CloudCollision");

        cloud.InstructionSaves.ShouldContain(i => i.Member == "RepositionUpdateStyle");
        cloud.Properties.ShouldContain(p => p.Name == "CollisionTileTypeName");
    }

    [Fact]
    public void Load_DerivedScreen_InheritsBaseReferencedFiles()
    {
        // Level1 declares no .gusx of its own and inherits GameScreen's Gum screen.
        var result = GlueProjectLoader.Load(Gluj("DoorsDemo", "DoorsDemo.gluj"));

        var level1 = result.Project.Screens.Single(s => s.Name == @"Screens\Level1");

        level1.ReferencedFiles.ShouldContain(f => f.Name!.EndsWith(".gusx", StringComparison.Ordinal));
    }

    [Fact]
    public void Load_DerivedEntity_InheritsBaseVariablesAndOverridesWhereAuthored()
    {
        var result = LoadFrom(
            ("Test.gluj", @"{ ""FileVersion"": 60, ""EntityReferences"": [
                { ""Name"": ""Entities\\Base"" }, { ""Name"": ""Entities\\Derived"" } ] }"),
            ("Entities/Base.glej", @"{
                ""Name"": ""Entities\\Base"",
                ""CustomVariables"": [
                    { ""Name"": ""Speed"", ""DefaultValue"": 100.0,
                      ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] },
                    { ""Name"": ""Health"", ""DefaultValue"": 5.0,
                      ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] } ]
            }"),
            ("Entities/Derived.glej", @"{
                ""Name"": ""Entities\\Derived"",
                ""BaseEntity"": ""Entities\\Base"",
                ""CustomVariables"": [
                    { ""Name"": ""Speed"", ""DefaultValue"": 250.0, ""DefinedByBase"": true,
                      ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] } ]
            }"));

        var derived = result.Project.Entities.Single(e => e.Name == @"Entities\Derived");

        derived.CustomVariables.Single(v => v.Name == "Speed").DefaultValue.GetSingle().ShouldBe(250f);
        derived.CustomVariables.Single(v => v.Name == "Health").DefaultValue.GetSingle().ShouldBe(5f);
    }

    [Fact]
    public void Load_DerivedVariableWithNoAuthoredValue_KeepsTheBaseValue()
    {
        // Glue nulls DefaultValue on a copied-down variable to mean "inherit". Treating the stub as
        // an override would blank the base's value.
        var result = LoadFrom(
            ("Test.gluj", @"{ ""FileVersion"": 60, ""EntityReferences"": [
                { ""Name"": ""Entities\\Base"" }, { ""Name"": ""Entities\\Derived"" } ] }"),
            ("Entities/Base.glej", @"{
                ""Name"": ""Entities\\Base"",
                ""CustomVariables"": [ { ""Name"": ""Speed"", ""DefaultValue"": 100.0,
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] } ]
            }"),
            ("Entities/Derived.glej", @"{
                ""Name"": ""Entities\\Derived"",
                ""BaseEntity"": ""Entities\\Base"",
                ""CustomVariables"": [ { ""Name"": ""Speed"", ""DefinedByBase"": true,
                    ""Properties"": [ { ""Name"": ""Type"", ""Value"": ""float"" } ] } ]
            }"));

        var derived = result.Project.Entities.Single(e => e.Name == @"Entities\Derived");

        derived.CustomVariables.Single(v => v.Name == "Speed").DefaultValue.GetSingle().ShouldBe(100f);
    }

    [Fact]
    public void Load_ThreeLevelChain_MergesMostBaseFirst()
    {
        var result = LoadFrom(
            ("Test.gluj", @"{ ""FileVersion"": 60, ""ScreenReferences"": [
                { ""Name"": ""Screens\\A"" }, { ""Name"": ""Screens\\B"" }, { ""Name"": ""Screens\\C"" } ] }"),
            ("Screens/A.glsj", @"{ ""Name"": ""Screens\\A"",
                ""NamedObjects"": [ { ""InstanceName"": ""FromA"", ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"" } ] }"),
            ("Screens/B.glsj", @"{ ""Name"": ""Screens\\B"", ""BaseScreen"": ""Screens\\A"",
                ""NamedObjects"": [ { ""InstanceName"": ""FromB"", ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"" } ] }"),
            ("Screens/C.glsj", @"{ ""Name"": ""Screens\\C"", ""BaseScreen"": ""Screens\\B"",
                ""NamedObjects"": [ { ""InstanceName"": ""FromC"", ""SourceClassType"": ""FlatRedBall.Math.Geometry.Circle"" } ] }"));

        var c = result.Project.Screens.Single(s => s.Name == @"Screens\C");

        c.NamedObjects.Select(o => o.InstanceName).ShouldBe(new[] { "FromA", "FromB", "FromC" });
    }

    [Fact]
    public void Load_InheritanceCycle_ReportsAnErrorRatherThanHanging()
    {
        var result = LoadFrom(
            ("Test.gluj", @"{ ""FileVersion"": 60, ""ScreenReferences"": [
                { ""Name"": ""Screens\\A"" }, { ""Name"": ""Screens\\B"" } ] }"),
            ("Screens/A.glsj", @"{ ""Name"": ""Screens\\A"", ""BaseScreen"": ""Screens\\B"" }"),
            ("Screens/B.glsj", @"{ ""Name"": ""Screens\\B"", ""BaseScreen"": ""Screens\\A"" }"));

        result.Diagnostics.ShouldContain(d =>
            d.Severity == GlueDiagnosticSeverity.Error && d.Message.Contains("cycle"));
    }
}
