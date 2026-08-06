using System;
using System.Collections.Generic;
using FlatRedBall2.IO;
using FlatRedBall2.Movement;

namespace FlatRedBall2.Glue;

/// <summary>
/// Reads Glue's per-entity movement CSVs into FRB2's movement value types.
/// </summary>
/// <remarks>
/// Movement tuning is not in the <c>.glej</c> — it lives in a CSV referenced like any other content
/// file, keyed by a required <c>Name</c> column. A platformer entity then picks three of those rows
/// by name for its ground, air and double-jump slots.
/// </remarks>
public static class GlueMovementValues
{
    /// <summary>Parses a platformer CSV into its named value sets.</summary>
    public static Dictionary<string, PlatformerValues> ReadPlatformer(string csv)
    {
        var table = CsvTable.Parse(csv);
        var key = table.KeyHeader?.Name ?? "Name";
        var result = new Dictionary<string, PlatformerValues>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in table.Rows)
        {
            string? name = table.Value(row, key);

            if (string.IsNullOrEmpty(name))
                continue;

            result[name] = ReadPlatformerRow(table, row);
        }

        return result;
    }

    /// <summary>Parses a top-down CSV into its named value sets.</summary>
    public static Dictionary<string, TopDownValues> ReadTopDown(string csv)
    {
        var table = CsvTable.Parse(csv);
        var key = table.KeyHeader?.Name ?? "Name";
        var result = new Dictionary<string, TopDownValues>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in table.Rows)
        {
            string? name = table.Value(row, key);

            if (string.IsNullOrEmpty(name))
                continue;

            result[name] = ReadTopDownRow(table, row);
        }

        return result;
    }

    private static PlatformerValues ReadPlatformerRow(CsvTable table, IReadOnlyList<string> row)
    {
        var values = new PlatformerValues
        {
            MaxSpeedX = table.Float(row, "MaxSpeedX"),
            Gravity = table.Float(row, "Gravity"),
            MaxFallSpeed = table.Float(row, "MaxFallSpeed"),
            JumpVelocity = table.Float(row, "JumpVelocity"),
            JumpApplyByButtonHold = table.Bool(row, "JumpApplyByButtonHold"),
            AccelerationTimeX = Seconds(table.Float(row, "AccelerationTimeX")),
            DecelerationTimeX = Seconds(table.Float(row, "DecelerationTimeX")),
            JumpApplyLength = Seconds(table.Float(row, "JumpApplyLength")),
            UphillFullSpeedSlope = table.Float(row, "UphillFullSpeedSlope"),
            UphillStopSpeedSlope = table.Float(row, "UphillStopSpeedSlope", 60f),
            DownhillFullSpeedSlope = table.Float(row, "DownhillFullSpeedSlope"),
            DownhillMaxSpeedSlope = table.Float(row, "DownhillMaxSpeedSlope", 60f),

            // FRB1 renamed this and changed its units: it stores a percentage boost, FRB2 a
            // multiplier. 50 becomes 1.5.
            DownhillMaxSpeedMultiplier =
                1f + (table.Float(row, "DownhillMaxSpeedBoostPercentage") / 100f),

            CanFallThroughOneWayCollision = table.Bool(row, "CanFallThroughCloudPlatforms", true),
            ClimbingSpeed = table.Float(row, "MaxClimbingSpeed"),
        };

        // FRB1 expresses "no easing" as a separate flag; FRB2 expresses it as a zero duration.
        // Ignoring the column silently smooths movement that the author wanted to snap.
        if (!table.Bool(row, "UsesAcceleration", true))
        {
            values.AccelerationTimeX = TimeSpan.Zero;
            values.DecelerationTimeX = TimeSpan.Zero;
        }

        return values;
    }

    private static TopDownValues ReadTopDownRow(CsvTable table, IReadOnlyList<string> row)
    {
        var values = new TopDownValues
        {
            MaxSpeed = table.Float(row, "MaxSpeed"),
            AccelerationTime = Seconds(table.Float(row, "AccelerationTime")),
            DecelerationTime = Seconds(table.Float(row, "DecelerationTime")),

            // Both of these default the *opposite* way on the two sides, so read them explicitly
            // and never let either side's default stand.
            UpdateDirectionFromInput = table.Bool(row, "UpdateDirectionFromInput"),
            UpdateDirectionFromVelocity = table.Bool(row, "UpdateDirectionFromVelocity", true),

            IsUsingCustomDeceleration = table.Bool(row, "IsUsingCustomDeceleration"),
            CustomDecelerationValue = table.Float(row, "CustomDecelerationValue", 100f),
        };

        if (!table.Bool(row, "UsesAcceleration", true))
        {
            values.AccelerationTime = TimeSpan.Zero;
            values.DecelerationTime = TimeSpan.Zero;
        }

        return values;
    }

    /// <summary>Glue stores durations as plain seconds; FRB2's are <see cref="TimeSpan"/>.</summary>
    private static TimeSpan Seconds(float seconds) => TimeSpan.FromSeconds(seconds);

    /// <summary>
    /// Resolves Glue's <c>"&lt;Row&gt; in &lt;File&gt;.csv"</c> variable syntax to a row name.
    /// </summary>
    /// <remarks>
    /// Split on the <em>last</em> <c>" in "</c>, so a row legitimately named <c>Walk in Water</c>
    /// still resolves. <c>"&lt;NULL&gt;"</c> means the slot is deliberately empty.
    /// </remarks>
    public static bool TryParseRowReference(string? value, out string rowName, out string fileName)
    {
        rowName = string.Empty;
        fileName = string.Empty;

        if (string.IsNullOrEmpty(value) || value == "<NULL>")
            return false;

        int separator = value.LastIndexOf(" in ", StringComparison.Ordinal);

        if (separator < 0)
            return false;

        rowName = value[..separator].Trim();
        fileName = value[(separator + 4)..].Trim();
        return rowName.Length > 0;
    }
}
