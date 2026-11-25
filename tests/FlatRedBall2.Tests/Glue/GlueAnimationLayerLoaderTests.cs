using System.Collections.Generic;
using FlatRedBall2.Glue;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests.Glue;

// The .PlatformerAnimations.json / .TopDownAnimations.json sidecar path convention and diagnostics
// for authored data this evaluator cannot honor (Custom Condition, unimplemented speed modes).
public class GlueAnimationLayerLoaderTests
{
    [Fact]
    public void PlatformerSidecarPath_BackslashElementName_BecomesForwardSlashJsonPath()
    {
        GlueAnimationLayerLoader.PlatformerSidecarPath(@"Entities\Player")
            .ShouldBe("Entities/Player.PlatformerAnimations.json");
    }

    [Fact]
    public void TopDownSidecarPath_BackslashElementName_BecomesForwardSlashJsonPath()
    {
        GlueAnimationLayerLoader.TopDownSidecarPath(@"Entities\Player")
            .ShouldBe("Entities/Player.TopDownAnimations.json");
    }

    [Fact]
    public void ParsePlatformer_ValidJson_ReturnsLayersWithNoDiagnostics()
    {
        const string json = """
            { "Values": [ { "AnimationName": "Idle" }, { "AnimationName": "Walk", "MinXVelocityAbsolute": 10 } ] }
            """;
        var diagnostics = new List<GlueLoadDiagnostic>();

        var layers = GlueAnimationLayerLoader.ParsePlatformer(json, "Entities\\Player", diagnostics);

        layers.Count.ShouldBe(2);
        layers[1].MinXVelocityAbsolute.ShouldBe(10f);
        diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void ParsePlatformer_MalformedJson_ReturnsEmptyListAndWarns()
    {
        const string json = "{ not valid json";
        var diagnostics = new List<GlueLoadDiagnostic>();

        var layers = GlueAnimationLayerLoader.ParsePlatformer(json, "Entities\\Player", diagnostics);

        layers.ShouldBeEmpty();
        diagnostics.ShouldHaveSingleItem();
        diagnostics[0].Severity.ShouldBe(GlueDiagnosticSeverity.Warning);
    }

    [Fact]
    public void ParsePlatformer_LayerWithCustomCondition_Warns()
    {
        const string json = """
            { "Values": [ { "AnimationName": "IsTiredAnim", "CustomCondition": "IsTired" } ] }
            """;
        var diagnostics = new List<GlueLoadDiagnostic>();

        var layers = GlueAnimationLayerLoader.ParsePlatformer(json, "Entities\\Player", diagnostics);

        layers.Count.ShouldBe(1);
        diagnostics.ShouldHaveSingleItem();
        diagnostics[0].Message.ShouldContain("Custom Condition");
        diagnostics[0].Message.ShouldContain("IsTiredAnim");
    }

    [Fact]
    public void ParseTopDown_LayerWithUnsupportedSpeedAssignment_Warns()
    {
        const string json = """
            { "Values": [ { "AnimationName": "Walk", "AnimationSpeedAssignment": 5 } ] }
            """;
        var diagnostics = new List<GlueLoadDiagnostic>();

        var layers = GlueAnimationLayerLoader.ParseTopDown(json, "Entities\\Player", diagnostics);

        layers.Count.ShouldBe(1);
        diagnostics.ShouldHaveSingleItem();
        diagnostics[0].Message.ShouldContain("BasedOnInputMultiplier");
    }

    [Theory]
    [InlineData("2", "AbsoluteXVelocityAnimationSpeedMultiplier", "-0.1")]
    [InlineData("2", "AbsoluteYVelocityAnimationSpeedMultiplier", "-0.1")]
    [InlineData("3", "MaxSpeedXRatioMultiplier", "-1")]
    [InlineData("3", "MaxSpeedYRatioMultiplier", "-1")]
    [InlineData("2", "AbsoluteXVelocityAnimationSpeedMultiplier", "1e999")]
    public void ParsePlatformer_MultiplierThatMakesAnInvalidSpeed_Warns(string assignment, string member, string value)
    {
        string json = $$"""{ "Values": [ { "AnimationName": "Walk", "AnimationSpeedAssignment": {{assignment}}, "{{member}}": {{value}} } ] }""";
        var diagnostics = new List<GlueLoadDiagnostic>();

        GlueAnimationLayerLoader.ParsePlatformer(json, "Entities/Player", diagnostics).Count.ShouldBe(1);

        diagnostics.ShouldHaveSingleItem().Message.ShouldContain(member);
        diagnostics[0].Message.ShouldContain("IsReversed");
    }

    [Theory]
    [InlineData("2", "AbsoluteVelocityAnimationSpeedMultiplier")]
    [InlineData("3", "MaxSpeedRatioMultiplier")]
    public void ParseTopDown_NegativeMultiplier_Warns(string assignment, string member)
    {
        string json = $$"""{ "Values": [ { "AnimationName": "Walk", "AnimationSpeedAssignment": {{assignment}}, "{{member}}": -1 } ] }""";
        var diagnostics = new List<GlueLoadDiagnostic>();

        GlueAnimationLayerLoader.ParseTopDown(json, "Entities/Player", diagnostics).Count.ShouldBe(1);

        diagnostics.ShouldHaveSingleItem().Message.ShouldContain(member);
    }

    [Fact]
    public void ParsePlatformer_NegativeMultiplierTheLayerDoesNotUse_DoesNotWarn()
    {
        const string json = """{ "Values": [ { "AnimationName": "Walk", "AnimationSpeedAssignment": 0, "MaxSpeedXRatioMultiplier": -1 } ] }""";
        var diagnostics = new List<GlueLoadDiagnostic>();

        GlueAnimationLayerLoader.ParsePlatformer(json, "Entities/Player", diagnostics);

        diagnostics.ShouldBeEmpty();
    }
}
