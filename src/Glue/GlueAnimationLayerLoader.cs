using System.Collections.Generic;
using System.Text.Json;

namespace FlatRedBall2.Glue;

/// <summary>
/// Resolves and parses the optional <c>.PlatformerAnimations.json</c> / <c>.TopDownAnimations.json</c>
/// sidecar files FRB1's Editor writes next to an entity's own <c>.glej</c>, and reports the authored
/// data this build cannot honor.
/// </summary>
public static class GlueAnimationLayerLoader
{
    /// <summary>The path, relative to a project's content root, FRB1 writes a platformer entity's
    /// animation layers to — matching FRB1's own <c>PlatformerAnimationsFileLocationFor</c> exactly,
    /// so a Glue-authored project's file loads unchanged.</summary>
    public static string PlatformerSidecarPath(string glueElementName) =>
        glueElementName.Replace('\\', '/') + ".PlatformerAnimations.json";

    /// <summary>The top-down equivalent of <see cref="PlatformerSidecarPath"/>.</summary>
    public static string TopDownSidecarPath(string glueElementName) =>
        glueElementName.Replace('\\', '/') + ".TopDownAnimations.json";

    /// <summary>
    /// Parses a platformer sidecar file's already-read JSON text. Malformed JSON yields an empty list
    /// plus a warning rather than throwing — matching this loader's tolerant-by-design posture
    /// elsewhere. Layers authoring <c>CustomCondition</c> or an unimplemented
    /// <see cref="GlueAnimationSpeedAssignment"/> still load (so the rest of the file's data-driven
    /// conditions still work) but get a diagnostic explaining what will not evaluate.
    /// </summary>
    public static List<GluePlatformerAnimationLayer> ParsePlatformer(
        string json, string? elementName, List<GlueLoadDiagnostic> diagnostics)
    {
        List<GluePlatformerAnimationLayer> layers;

        try
        {
            layers = GlueAnimationLayerJson.ParsePlatformer(json);
        }
        catch (JsonException e)
        {
            Warn(diagnostics, elementName,
                $"'{PlatformerSidecarPath(elementName ?? string.Empty)}' could not be parsed: {e.Message}");
            return new List<GluePlatformerAnimationLayer>();
        }

        foreach (var layer in layers)
            WarnUnsupported(layer.AnimationName, layer.CustomCondition, layer.AnimationSpeedAssignment, elementName, diagnostics);

        return layers;
    }

    /// <summary>Top-down equivalent of <see cref="ParsePlatformer"/>.</summary>
    public static List<GlueTopDownAnimationLayer> ParseTopDown(
        string json, string? elementName, List<GlueLoadDiagnostic> diagnostics)
    {
        List<GlueTopDownAnimationLayer> layers;

        try
        {
            layers = GlueAnimationLayerJson.ParseTopDown(json);
        }
        catch (JsonException e)
        {
            Warn(diagnostics, elementName,
                $"'{TopDownSidecarPath(elementName ?? string.Empty)}' could not be parsed: {e.Message}");
            return new List<GlueTopDownAnimationLayer>();
        }

        foreach (var layer in layers)
            WarnUnsupported(layer.AnimationName, layer.CustomCondition, layer.AnimationSpeedAssignment, elementName, diagnostics);

        return layers;
    }

    private static void WarnUnsupported(
        string? animationName, string? customCondition, GlueAnimationSpeedAssignment speedAssignment,
        string? elementName, List<GlueLoadDiagnostic> diagnostics)
    {
        if (!string.IsNullOrEmpty(customCondition))
        {
            Warn(diagnostics, elementName,
                $"Animation layer '{animationName}' authors a Custom Condition ('{customCondition}'), " +
                "which has no data-driven equivalent in this loader and is ignored.");
        }

        if (speedAssignment is GlueAnimationSpeedAssignment.BasedOnHorizontalInputMultiplier
            or GlueAnimationSpeedAssignment.BasedOnInputMultiplier)
        {
            Warn(diagnostics, elementName,
                $"Animation layer '{animationName}' uses AnimationSpeedAssignment " +
                $"'{speedAssignment}', which this build does not evaluate — Sprite.AnimationSpeed " +
                "is left untouched, as if NoAssignment were set.");
        }
    }

    private static void Warn(List<GlueLoadDiagnostic> diagnostics, string? elementName, string message) =>
        diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, elementName));
}
