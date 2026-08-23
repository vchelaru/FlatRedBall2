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
}
