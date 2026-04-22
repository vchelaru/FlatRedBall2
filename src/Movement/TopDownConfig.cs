using System.IO;
using System.Text.Json;

namespace FlatRedBall2.Movement;

public class TopDownConfig
{
    public TopDownMovementConfig? Movement { get; set; }

    public static TopDownConfig FromJson(string path)
    {
        string json = File.ReadAllText(path);
        return FromJsonString(json);
    }

    public static TopDownConfig FromJsonString(string json)
    {
        var config = JsonSerializer.Deserialize<TopDownConfig>(json, SerializerOptions);
        return config ?? new TopDownConfig();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

/// <summary>
/// Nullable-field mirror of <see cref="TopDownValues"/>. Omitted fields fall back to
/// <see cref="TopDownValues"/> defaults rather than zero — this lets a file override a
/// single field (e.g. <c>MaxSpeed</c>) without redefining every other parameter.
/// </summary>
public class TopDownMovementConfig
{
    public float? MaxSpeed { get; set; }
    public float? AccelerationTime { get; set; }
    public float? DecelerationTime { get; set; }

    public bool? UpdateDirectionFromInput { get; set; }
    public bool? UpdateDirectionFromVelocity { get; set; }

    public bool? IsUsingCustomDeceleration { get; set; }
    public float? CustomDecelerationValue { get; set; }
}
