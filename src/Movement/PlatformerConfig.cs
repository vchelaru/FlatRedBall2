using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlatRedBall2.Movement;

public class PlatformerConfig
{
    public MovementConfig? Movement { get; set; }

    public static PlatformerConfig FromJson(string path)
    {
        string json = File.ReadAllText(path);
        return FromJsonString(json);
    }

    public static PlatformerConfig FromJsonString(string json)
    {
        var config = JsonSerializer.Deserialize<PlatformerConfig>(json, SerializerOptions);
        return config ?? new PlatformerConfig();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };
}

public class MovementConfig
{
    public MovementSlot? Ground { get; set; }
    public MovementSlot? Air { get; set; }
    public MovementSlot? AfterDoubleJump { get; set; }
    public MovementSlot? Climbing { get; set; }
}

/// <summary>
/// Nullable-field mirror of <see cref="PlatformerValues"/>. Omitted fields fall back to
/// <see cref="PlatformerValues"/> defaults rather than the slot's own zero — this lets a file
/// override a single field (e.g. <c>MaxSpeedX</c>) without redefining every slope parameter.
/// <para>
/// <see cref="PlatformerValues.AccelerationTimeX"/>, <see cref="PlatformerValues.DecelerationTimeX"/>,
/// and <see cref="PlatformerValues.JumpApplyLength"/> are <see cref="TimeSpan"/> on the runtime
/// class but are represented as seconds (double) in JSON.
/// </para>
/// <para>
/// Jump configuration supports two mutually-exclusive modes: derived
/// (<see cref="MinJumpHeight"/> + optional <see cref="MaxJumpHeight"/>, which calls
/// <see cref="PlatformerValues.SetJumpHeights"/>) and raw (<see cref="JumpVelocity"/> +
/// <see cref="JumpApplyLength"/> + <see cref="JumpApplyByButtonHold"/>). Specifying fields from
/// both modes in the same slot is an error.
/// </para>
/// </summary>
public class MovementSlot
{
    public float? MaxSpeedX { get; set; }
    public double? AccelerationTimeX { get; set; }
    public double? DecelerationTimeX { get; set; }

    public float? Gravity { get; set; }
    public float? MaxFallSpeed { get; set; }

    public float? JumpVelocity { get; set; }
    public double? JumpApplyLength { get; set; }
    public bool? JumpApplyByButtonHold { get; set; }

    public float? ClimbingSpeed { get; set; }

    [JsonPropertyName("minJumpHeight")]
    public float? MinJumpHeight { get; set; }

    [JsonPropertyName("maxJumpHeight")]
    public float? MaxJumpHeight { get; set; }

    public float? SlopeSnapDistance { get; set; }
    public float? SlopeSnapMaxAngleDegrees { get; set; }

    public float? UphillFullSpeedSlope { get; set; }
    public float? UphillStopSpeedSlope { get; set; }
    public float? DownhillFullSpeedSlope { get; set; }
    public float? DownhillMaxSpeedSlope { get; set; }
    public float? DownhillMaxSpeedMultiplier { get; set; }

    public bool? CanFallThroughOneWayCollision { get; set; }
}
