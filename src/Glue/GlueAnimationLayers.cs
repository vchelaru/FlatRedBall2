using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlatRedBall2.Glue;

/// <summary>
/// Speed assignment modes Glue's platformer/top-down animation layers support. Values match FRB1's
/// enum exactly (including <see cref="BasedOnHorizontalInputMultiplier"/> and
/// <see cref="BasedOnInputMultiplier"/>, which parse but are not evaluated — see
/// <see cref="GlueAnimationEvaluator"/>) so a real Glue-authored JSON file, which encodes the enum by
/// its numeric value, always deserializes rather than throwing on an unrecognized number.
/// </summary>
public enum GlueAnimationSpeedAssignment
{
    /// <summary>Sprite.AnimationSpeed is forced to 1 (plays at the .achx-authored speed).</summary>
    ForceTo1,
    /// <summary>Sprite.AnimationSpeed is left untouched.</summary>
    NoAssignment,
    /// <summary>Sprite.AnimationSpeed = multiplier × |velocity| (X or Y, whichever multiplier is set).</summary>
    BasedOnVelocityMultiplier,
    /// <summary>Sprite.AnimationSpeed = multiplier × |velocity| / the active slot's max speed.</summary>
    BasedOnMaxSpeedRatioMultiplier,
    /// <summary>Platformer-only in FRB1. Parses but is not evaluated — treated as <see cref="NoAssignment"/>.</summary>
    BasedOnHorizontalInputMultiplier,
    /// <summary>Top-down's name for the same unimplemented mode as <see cref="BasedOnHorizontalInputMultiplier"/>.</summary>
    BasedOnInputMultiplier,
}

/// <summary>
/// One authored platformer animation layer, matching FRB1's
/// <c>PlatformerPlugin.SaveClasses.IndividualPlatformerAnimationValues</c> field-for-field so the
/// <c>&lt;EntityName&gt;.PlatformerAnimations.json</c> sidecar FRB1's Editor writes loads unchanged.
/// </summary>
/// <remarks>
/// <see cref="CustomCondition"/> parses (so the file round-trips) but is never evaluated — FRB1's
/// version is arbitrary pasted C# with no data-driven equivalent. A layer that authors it gets a
/// build diagnostic; see <see cref="GlueAnimationLayerLoader"/>.
/// </remarks>
public class GluePlatformerAnimationLayer
{
    /// <summary>Base chain name; <see cref="HasLeftAndRight"/> appends "Left"/"Right".</summary>
    public string? AnimationName { get; set; }
    /// <summary>When true, "Left"/"Right" is appended to <see cref="AnimationName"/> based on facing.</summary>
    public bool HasLeftAndRight { get; set; } = true;
    /// <summary>Minimum |VelocityX| required for this layer to win.</summary>
    public float? MinXVelocityAbsolute { get; set; }
    /// <summary>Maximum |VelocityX| allowed for this layer to win.</summary>
    public float? MaxXVelocityAbsolute { get; set; }
    /// <summary>Minimum VelocityY (signed) required for this layer to win.</summary>
    public float? MinYVelocity { get; set; }
    /// <summary>Maximum VelocityY (signed) allowed for this layer to win.</summary>
    public float? MaxYVelocity { get; set; }
    /// <summary>Minimum |horizontal input| required for this layer to win.</summary>
    public float? MinHorizontalInputAbsolute { get; set; }
    /// <summary>Maximum |horizontal input| allowed for this layer to win.</summary>
    public float? MaxHorizontalInputAbsolute { get; set; }
    /// <summary>Used by <see cref="GlueAnimationSpeedAssignment.BasedOnVelocityMultiplier"/>.</summary>
    public float? AbsoluteXVelocityAnimationSpeedMultiplier { get; set; }
    /// <summary>Used by <see cref="GlueAnimationSpeedAssignment.BasedOnVelocityMultiplier"/>.</summary>
    public float? AbsoluteYVelocityAnimationSpeedMultiplier { get; set; }
    /// <summary>Used by <see cref="GlueAnimationSpeedAssignment.BasedOnMaxSpeedRatioMultiplier"/>.</summary>
    public float? MaxSpeedXRatioMultiplier { get; set; }
    /// <summary>Used by <see cref="GlueAnimationSpeedAssignment.BasedOnMaxSpeedRatioMultiplier"/>.</summary>
    public float? MaxSpeedYRatioMultiplier { get; set; }
    /// <summary>Null = either (no restriction), true = ground only, false = air only.</summary>
    public bool? OnGroundRequirement { get; set; }
    /// <summary>When set, this layer only wins while this named movement slot is active.</summary>
    public string? MovementName { get; set; }
    /// <summary>Parsed but never evaluated — see the type-level remarks.</summary>
    public string? CustomCondition { get; set; }
    /// <summary>How this layer drives <c>Sprite.AnimationSpeed</c> when it wins.</summary>
    public GlueAnimationSpeedAssignment AnimationSpeedAssignment { get; set; }
    /// <summary>Author-facing notes; not evaluated.</summary>
    public string? Notes { get; set; }
}

/// <summary>
/// One authored top-down animation layer, matching FRB1's
/// <c>TopDownPlugin.Models.IndividualTopDownAnimationValues</c> field-for-field so the
/// <c>&lt;EntityName&gt;.TopDownAnimations.json</c> sidecar FRB1's Editor writes loads unchanged.
/// </summary>
public class GlueTopDownAnimationLayer
{
    /// <summary>Base chain name; <see cref="IsDirectionFacingAppended"/> appends the facing direction's name.</summary>
    public string? AnimationName { get; set; }
    /// <summary>When true, <c>TopDownDirection.ToString()</c> is appended to <see cref="AnimationName"/>.</summary>
    public bool IsDirectionFacingAppended { get; set; } = true;
    /// <summary>Minimum combined-XY speed required for this layer to win.</summary>
    public float? MinVelocityAbsolute { get; set; }
    /// <summary>Maximum combined-XY speed allowed for this layer to win.</summary>
    public float? MaxVelocityAbsolute { get; set; }
    /// <summary>Used by <see cref="GlueAnimationSpeedAssignment.BasedOnVelocityMultiplier"/>.</summary>
    public float? AbsoluteVelocityAnimationSpeedMultiplier { get; set; }
    /// <summary>Minimum combined-XY movement input magnitude required for this layer to win.</summary>
    public float? MinMovementInputAbsolute { get; set; }
    /// <summary>Maximum combined-XY movement input magnitude allowed for this layer to win.</summary>
    public float? MaxMovementInputAbsolute { get; set; }
    /// <summary>Used by <see cref="GlueAnimationSpeedAssignment.BasedOnMaxSpeedRatioMultiplier"/>.</summary>
    public float? MaxSpeedRatioMultiplier { get; set; }
    /// <summary>When set, this layer only wins while this named movement slot is active.</summary>
    public string? MovementName { get; set; }
    /// <summary>Parsed but never evaluated — see <see cref="GluePlatformerAnimationLayer.CustomCondition"/>.</summary>
    public string? CustomCondition { get; set; }
    /// <summary>How this layer drives <c>Sprite.AnimationSpeed</c> when it wins.</summary>
    public GlueAnimationSpeedAssignment AnimationSpeedAssignment { get; set; }
    /// <summary>Author-facing notes; not evaluated.</summary>
    public string? Notes { get; set; }
}

/// <summary>The <c>{"Values": [...]}</c> wrapper both sidecar JSON files use.</summary>
public class GluePlatformerAnimationLayerFile
{
    /// <summary>Layers in authored (lowest-priority-first) order.</summary>
    public List<GluePlatformerAnimationLayer> Values { get; set; } = new();
}

/// <summary>The <c>{"Values": [...]}</c> wrapper both sidecar JSON files use.</summary>
public class GlueTopDownAnimationLayerFile
{
    /// <summary>Layers in authored (lowest-priority-first) order.</summary>
    public List<GlueTopDownAnimationLayer> Values { get; set; } = new();
}

/// <summary>Parses the <c>.PlatformerAnimations.json</c> / <c>.TopDownAnimations.json</c> sidecar files.</summary>
public static class GlueAnimationLayerJson
{
    /// <summary>Parses a <c>.PlatformerAnimations.json</c> file's contents. Empty list if the JSON is literally <c>null</c>.</summary>
    public static List<GluePlatformerAnimationLayer> ParsePlatformer(string json) =>
        (JsonSerializer.Deserialize(json, GlueAnimationLayerJsonContext.Default.GluePlatformerAnimationLayerFile)
            ?? new GluePlatformerAnimationLayerFile()).Values;

    /// <summary>Parses a <c>.TopDownAnimations.json</c> file's contents. Empty list if the JSON is literally <c>null</c>.</summary>
    public static List<GlueTopDownAnimationLayer> ParseTopDown(string json) =>
        (JsonSerializer.Deserialize(json, GlueAnimationLayerJsonContext.Default.GlueTopDownAnimationLayerFile)
            ?? new GlueTopDownAnimationLayerFile()).Values;
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(GluePlatformerAnimationLayerFile))]
[JsonSerializable(typeof(GlueTopDownAnimationLayerFile))]
internal partial class GlueAnimationLayerJsonContext : JsonSerializerContext;
