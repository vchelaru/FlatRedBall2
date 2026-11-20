using System.Globalization;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Core.Utilities;

/// <summary>
/// The one place that knows what unit a file's <see cref="AnimationFrameSave.FrameLength"/> values are in.
/// Values in the editor are always in the file's own unit (<see cref="AnimationChainListSave.TimeMeasurementUnit"/>);
/// anything that creates, edits, displays, converts or plays a frame length goes through this type, so
/// <see cref="TimeMeasurementUnit.Undefined"/> (seconds) and <see cref="TimeMeasurementUnit.Millisecond"/>
/// are handled the same way everywhere. The file's unit is never rewritten.
/// </summary>
public readonly record struct FrameLengthUnit(TimeMeasurementUnit Unit)
{
    /// <summary>A new frame lasts a tenth of a second, whatever the unit.</summary>
    private const double DefaultSeconds = 0.1;

    /// <summary>Whole-second lengths.</summary>
    public static FrameLengthUnit Seconds => new(TimeMeasurementUnit.Second);

    /// <summary>Millisecond lengths.</summary>
    public static FrameLengthUnit Milliseconds => new(TimeMeasurementUnit.Millisecond);

    /// <summary>The unit of <paramref name="list"/>; seconds when there is no list.</summary>
    public static FrameLengthUnit Of(AnimationChainListSave? list) =>
        new(list?.TimeMeasurementUnit ?? TimeMeasurementUnit.Second);

    /// <summary>True for <see cref="TimeMeasurementUnit.Millisecond"/>; Second and Undefined are both seconds.</summary>
    public bool IsMilliseconds => Unit == TimeMeasurementUnit.Millisecond;

    /// <summary>How many of this unit make one second.</summary>
    public double UnitsPerSecond => IsMilliseconds ? 1000.0 : 1.0;

    /// <summary>Converts a length in this unit to seconds.</summary>
    public double ToSeconds(double length) => length / UnitsPerSecond;

    /// <summary>Converts a length in this unit to whole milliseconds (Tiled durations).</summary>
    public int ToMilliseconds(double length) => (int)System.Math.Round(ToSeconds(length) * 1000.0);

    /// <summary>Converts seconds to a length in this unit.</summary>
    public float FromSeconds(double seconds) => (float)(seconds * UnitsPerSecond);

    /// <summary>Converts a length written in <paramref name="from"/> to this unit.</summary>
    public float Convert(float length, FrameLengthUnit from) =>
        from.IsMilliseconds == IsMilliseconds ? length : FromSeconds(from.ToSeconds(length));

    /// <summary>Length of a newly created frame: 0.1 s (0.1 or 100 depending on the unit).</summary>
    public float DefaultFrameLength => FromSeconds(DefaultSeconds);

    /// <summary>How long a frame plays, in seconds. A length that is not positive plays as the default.</summary>
    public double PlaybackSeconds(float length) => length > 0 ? ToSeconds(length) : DefaultSeconds;

    /// <summary>Label suffix: <c>s</c> or <c>ms</c>.</summary>
    public string Suffix => IsMilliseconds ? "ms" : "s";

    /// <summary>Spelled-out unit for dialogs: <c>seconds</c> or <c>milliseconds</c>.</summary>
    public string Name => IsMilliseconds ? "milliseconds" : "seconds";

    /// <summary>Smallest length the Inspector accepts: 1 ms.</summary>
    public decimal Minimum => IsMilliseconds ? 1m : 0.001m;

    /// <summary>Largest length the Inspector accepts: 60 s.</summary>
    public decimal Maximum => IsMilliseconds ? 60000m : 60m;

    /// <summary>Inspector arrow/wheel step: 0.05 s.</summary>
    public decimal Step => IsMilliseconds ? 50m : 0.05m;

    /// <summary>Largest total duration the Adjust Frame Time dialog accepts: one hour.</summary>
    public decimal DialogMaximum => IsMilliseconds ? 3600000m : 3600m;

    /// <summary>Adjust Frame Time dialog step: 0.1 s.</summary>
    public decimal DialogStep => IsMilliseconds ? 100m : 0.1m;

    /// <summary>A value with no padded zeros and the suffix, as undo labels show it: <c>0.1s</c> or <c>100ms</c>.</summary>
    public string FormatCompact(decimal length) => length.ToString("0.###", CultureInfo.CurrentCulture) + Suffix;

    /// <summary>A length as shown in the tree and undo labels: <c>0.10s</c> or <c>100ms</c>.</summary>
    public string Format(float length) =>
        length.ToString(IsMilliseconds ? "0.##" : "0.00", CultureInfo.CurrentCulture) + Suffix;
}
