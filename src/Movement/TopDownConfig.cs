using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using XnaTitleContainer = Microsoft.Xna.Framework.TitleContainer;

namespace FlatRedBall2.Movement;

/// <summary>
/// JSON-loadable container for tunable top-down movement values. Author the file alongside
/// game content, load with <see cref="FromJson"/>, and apply onto a
/// <see cref="TopDownBehavior"/> via <see cref="TopDownConfigExtensions.ApplyTo"/>. Lets
/// designers iterate on feel without recompiling.
/// </summary>
public class TopDownConfig
{
    /// <summary>Movement-tuning slot. Null when the file omits the <c>movement</c> object —
    /// <see cref="TopDownConfigExtensions.ApplyTo"/> short-circuits in that case.</summary>
    public TopDownMovementConfig? Movement { get; set; }

    /// <summary>Loads a <see cref="TopDownConfig"/> from the JSON file at <paramref name="path"/>.
    /// The path is resolved through <c>TitleContainer</c> so it works on both desktop and web
    /// backends. Comments and trailing commas are tolerated; property name matching is
    /// case-insensitive.</summary>
    public static TopDownConfig FromJson(string path)
    {
        using var stream = XnaTitleContainer.OpenStream(path);
        using var reader = new StreamReader(stream);
        return FromJsonString(reader.ReadToEnd());
    }

    /// <summary>Parses a <see cref="TopDownConfig"/> from a JSON string. Returns an empty config
    /// (no movement slot) if the JSON deserializes to null.</summary>
    public static TopDownConfig FromJsonString(string json)
    {
        var config = JsonSerializer.Deserialize(json, TopDownConfigJsonContext.Default.TopDownConfig);
        return config ?? new TopDownConfig();
    }
}

[JsonSourceGenerationOptions(
    PropertyNameCaseInsensitive = true,
    ReadCommentHandling = JsonCommentHandling.Skip,
    AllowTrailingCommas = true)]
[JsonSerializable(typeof(TopDownConfig))]
internal partial class TopDownConfigJsonContext : JsonSerializerContext;

/// <summary>
/// Nullable-field mirror of <see cref="TopDownValues"/>. Omitted fields fall back to
/// <see cref="TopDownValues"/> defaults rather than zero — this lets a file override a
/// single field (e.g. <c>MaxSpeed</c>) without redefining every other parameter.
/// </summary>
public class TopDownMovementConfig
{
    /// <summary>Overrides <see cref="TopDownValues.MaxSpeed"/> (world units/second).</summary>
    public float? MaxSpeed { get; set; }
    /// <summary>Overrides <see cref="TopDownValues.AccelerationTime"/> (seconds from rest to <see cref="MaxSpeed"/>).</summary>
    public float? AccelerationTime { get; set; }
    /// <summary>Overrides <see cref="TopDownValues.DecelerationTime"/> (seconds from <see cref="MaxSpeed"/> to rest).</summary>
    public float? DecelerationTime { get; set; }

    /// <summary>Overrides <see cref="TopDownValues.UpdateDirectionFromInput"/>.</summary>
    public bool? UpdateDirectionFromInput { get; set; }
    /// <summary>Overrides <see cref="TopDownValues.UpdateDirectionFromVelocity"/>.</summary>
    public bool? UpdateDirectionFromVelocity { get; set; }

    /// <summary>Overrides <see cref="TopDownValues.IsUsingCustomDeceleration"/>.</summary>
    public bool? IsUsingCustomDeceleration { get; set; }
    /// <summary>Overrides <see cref="TopDownValues.CustomDecelerationValue"/> (units/second²).</summary>
    public float? CustomDecelerationValue { get; set; }
}
