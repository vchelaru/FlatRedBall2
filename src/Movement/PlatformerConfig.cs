using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlatRedBall2.Movement;

/// <summary>
/// JSON-loadable container for tunable platformer movement values across the ground, air,
/// after-double-jump, and climbing slots. Author the file alongside game content, load with
/// <see cref="FromJson"/>, and apply onto a <see cref="PlatformerBehavior"/> via
/// <see cref="PlatformerConfigExtensions.ApplyTo"/>. Lets designers iterate on feel without
/// recompiling.
/// </summary>
public class PlatformerConfig
{
    /// <summary>The movement-slot bag. Null when the file omits the <c>movement</c> object —
    /// <see cref="PlatformerConfigExtensions.ApplyTo"/> short-circuits in that case.</summary>
    public MovementConfig? Movement { get; set; }

    /// <summary>Loads a <see cref="PlatformerConfig"/> from the JSON file at <paramref name="path"/>.
    /// Comments and trailing commas are tolerated; property name matching is case-insensitive.</summary>
    public static PlatformerConfig FromJson(string path)
    {
        string json = File.ReadAllText(path);
        return FromJsonString(json);
    }

    /// <summary>Parses a <see cref="PlatformerConfig"/> from a JSON string. Returns an empty
    /// config (no movement slots) if the JSON deserializes to null.</summary>
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

/// <summary>
/// Bag of movement slots authored in JSON. Each slot is optional; omitted slots leave the
/// behavior's existing values untouched when applied.
/// </summary>
public class MovementConfig
{
    /// <summary>Populates <see cref="PlatformerBehavior.GroundMovement"/> when present.</summary>
    public MovementSlot? Ground { get; set; }
    /// <summary>Populates <see cref="PlatformerBehavior.AirMovement"/> when present.</summary>
    public MovementSlot? Air { get; set; }
    /// <summary>Reserved for future double-jump support — parsed for forward compatibility but
    /// ignored at runtime. Authored files using this field today stay valid once the slot lands.</summary>
    public MovementSlot? AfterDoubleJump { get; set; }
    /// <summary>Populates <see cref="PlatformerBehavior.ClimbingMovement"/> when present.
    /// Slope and (most) drop-through fields inside this slot are accepted but ignored at runtime;
    /// jump fields are honored on jump-off.</summary>
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
    /// <summary>Overrides <see cref="PlatformerValues.MaxSpeedX"/> (world units/second).</summary>
    public float? MaxSpeedX { get; set; }
    /// <summary>Overrides <see cref="PlatformerValues.AccelerationTimeX"/>. Authored as seconds in JSON,
    /// converted to <see cref="TimeSpan"/> on apply.</summary>
    public double? AccelerationTimeX { get; set; }
    /// <summary>Overrides <see cref="PlatformerValues.DecelerationTimeX"/>. Authored as seconds in JSON,
    /// converted to <see cref="TimeSpan"/> on apply.</summary>
    public double? DecelerationTimeX { get; set; }

    /// <summary>Overrides <see cref="PlatformerValues.Gravity"/> (positive units/second² downward).
    /// On the air slot, this also drives derived-mode jump trajectory math for every slot.</summary>
    public float? Gravity { get; set; }
    /// <summary>Overrides <see cref="PlatformerValues.MaxFallSpeed"/> (positive units/second; clamps
    /// downward speed magnitude).</summary>
    public float? MaxFallSpeed { get; set; }

    /// <summary>Raw-mode jump: overrides <see cref="PlatformerValues.JumpVelocity"/> (units/second
    /// upward). Mutually exclusive with <see cref="MinJumpHeight"/>/<see cref="MaxJumpHeight"/>.</summary>
    public float? JumpVelocity { get; set; }
    /// <summary>Raw-mode jump: overrides <see cref="PlatformerValues.JumpApplyLength"/>. Authored as
    /// seconds in JSON.</summary>
    public double? JumpApplyLength { get; set; }
    /// <summary>Raw-mode jump: overrides <see cref="PlatformerValues.JumpApplyByButtonHold"/>.</summary>
    public bool? JumpApplyByButtonHold { get; set; }

    /// <summary>Overrides <see cref="PlatformerValues.ClimbingSpeed"/> (vertical units/second on a
    /// ladder when input Y is at full magnitude). Only meaningful on the <c>climbing</c> slot.</summary>
    public float? ClimbingSpeed { get; set; }

    /// <summary>Derived-mode jump: minimum jump height in world units (tap). Required when any
    /// derived field is present. Mutually exclusive with raw-mode jump fields. Triggers
    /// <see cref="PlatformerValues.SetJumpHeights"/> on apply, using the air slot's gravity (or
    /// this slot's, if no air slot is authored) as the trajectory gravity.</summary>
    [JsonPropertyName("minJumpHeight")]
    public float? MinJumpHeight { get; set; }

    /// <summary>Derived-mode jump: maximum jump height in world units (full button hold). Optional
    /// — when omitted the jump is fixed-height; when equal to <see cref="MinJumpHeight"/>, hold
    /// has no effect. Must be &gt;= <see cref="MinJumpHeight"/>.</summary>
    [JsonPropertyName("maxJumpHeight")]
    public float? MaxJumpHeight { get; set; }

    /// <summary>Overrides <see cref="PlatformerValues.SlopeSnapDistance"/> (world units; 0 disables
    /// snapping for this slot).</summary>
    public float? SlopeSnapDistance { get; set; }
    /// <summary>Overrides <see cref="PlatformerValues.SlopeSnapMaxAngleDegrees"/> (0-90).</summary>
    public float? SlopeSnapMaxAngleDegrees { get; set; }

    /// <summary>Overrides <see cref="PlatformerValues.UphillFullSpeedSlope"/> (degrees, 0-90).</summary>
    public float? UphillFullSpeedSlope { get; set; }
    /// <summary>Overrides <see cref="PlatformerValues.UphillStopSpeedSlope"/> (degrees, 0-90).</summary>
    public float? UphillStopSpeedSlope { get; set; }
    /// <summary>Overrides <see cref="PlatformerValues.DownhillFullSpeedSlope"/> (degrees, 0-90).</summary>
    public float? DownhillFullSpeedSlope { get; set; }
    /// <summary>Overrides <see cref="PlatformerValues.DownhillMaxSpeedSlope"/> (degrees, 0-90).</summary>
    public float? DownhillMaxSpeedSlope { get; set; }
    /// <summary>Overrides <see cref="PlatformerValues.DownhillMaxSpeedMultiplier"/> (multiplier; 1
    /// disables boost).</summary>
    public float? DownhillMaxSpeedMultiplier { get; set; }

    /// <summary>Overrides <see cref="PlatformerValues.CanFallThroughOneWayCollision"/>.</summary>
    public bool? CanFallThroughOneWayCollision { get; set; }
}
