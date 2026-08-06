using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using FlatRedBall2.Entities;
using FlatRedBall2.Glue.Model;
using FlatRedBall2.Tiled;

namespace FlatRedBall2.Glue;

/// <summary>
/// Finishes a <see cref="CameraControllingEntity"/> once the objects it references exist.
/// </summary>
/// <remarks>
/// Two of its nine authorable members name <em>other objects</em> — the list to follow and the map
/// to stay inside — so they cannot be applied by the ordinary instruction pass.
/// <para>Two more use FRB1's <b>obsolete</b> names. <c>LerpSmooth</c> and <c>LerpCoefficient</c> are
/// the only spellings Glue writes, because renaming them would break existing projects;
/// <c>TargetApproachStyle</c> never appears on disk at all.</para>
/// </remarks>
internal static class GlueCameraBuilder
{
    /// <summary>Wires every camera controller in the element against the objects already built.</summary>
    internal static void Build(
        List<NamedObjectSave> namedObjects,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics)
    {
        foreach (var save in namedObjects)
        {
            if (string.IsNullOrEmpty(save.InstanceName) ||
                !objects.TryGetValue(save.InstanceName, out object? built) ||
                built is not CameraControllingEntity camera)
            {
                continue;
            }

            foreach (var instruction in save.InstructionSaves)
                Apply(camera, instruction, save, elementName, objects, diagnostics);
        }
    }

    private static void Apply(
        CameraControllingEntity camera,
        InstructionSave instruction,
        NamedObjectSave save,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics)
    {
        switch (instruction.Member)
        {
            case "LerpSmooth":
                // Glue's obsolete name for the approach style, and the only one it writes.
                camera.TargetApproachStyle = instruction.Value.ValueKind == JsonValueKind.False
                    ? TargetApproachStyle.Immediate
                    : TargetApproachStyle.Smooth;
                break;

            case "LerpCoefficient":
                if (instruction.Value.ValueKind == JsonValueKind.Number)
                    camera.TargetApproachCoefficient = instruction.Value.GetSingle();
                break;

            case "Targets":
                ApplyTargets(camera, instruction, save, elementName, objects, diagnostics);
                break;

            case "Map":
                ApplyMap(camera, instruction, save, elementName, objects, diagnostics);
                break;
        }
    }

    private static void ApplyTargets(
        CameraControllingEntity camera,
        InstructionSave instruction,
        NamedObjectSave save,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics)
    {
        string? name = Text(instruction);

        if (name is null || !objects.TryGetValue(name, out object? target))
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' follows '{name}', which this build did not create.");
            return;
        }

        camera.Targets.Clear();

        switch (target)
        {
            // FRB1 aliases the list, so entities spawned later are followed too. FRB2's Targets is
            // get-only, so this is a snapshot — anything spawned after load is not followed.
            case List<object> list:
                camera.Targets.AddRange(list.OfType<Entity>());
                break;

            case Entity single:
                camera.Targets.Add(single);
                break;

            default:
                Warn(diagnostics, elementName,
                    $"'{save.InstanceName}' follows '{name}', which is not an entity or a list of them.");
                break;
        }
    }

    private static void ApplyMap(
        CameraControllingEntity camera,
        InstructionSave instruction,
        NamedObjectSave save,
        string? elementName,
        Dictionary<string, object> objects,
        List<GlueLoadDiagnostic> diagnostics)
    {
        string? name = Text(instruction);

        if (name is null || !objects.TryGetValue(name, out object? map) || map is not TileMap tileMap)
        {
            Warn(diagnostics, elementName,
                $"'{save.InstanceName}' bounds itself to '{name}', which is not a loaded tile map.");
            return;
        }

        // FRB1 keeps a live reference and re-reads the map's edges every frame; FRB2 takes a
        // rectangle. Equivalent for a TMX, which does not move.
        camera.Map = tileMap.Bounds;
    }

    private static string? Text(InstructionSave instruction) =>
        instruction.Value.ValueKind == JsonValueKind.String ? instruction.Value.GetString() : null;

    private static void Warn(List<GlueLoadDiagnostic> diagnostics, string? elementName, string message) =>
        diagnostics.Add(new GlueLoadDiagnostic(GlueDiagnosticSeverity.Warning, message, elementName));
}
