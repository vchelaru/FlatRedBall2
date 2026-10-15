using FlatRedBall.Glue.StateInterpolation;

namespace AnimationEditor.Core.Keyframes;

/// <summary>The property group a keyframe diamond and curve picker edit on a frame.</summary>
public enum KeyframeGroup
{
    /// <summary>The sprite offset (<c>RelativeX/Y</c>); stored as <c>SkipOffsetKeyframe</c>, <c>OffsetInterpolation</c>, <c>OffsetEasing</c>.</summary>
    Offset,

    /// <summary>The sprite color channels; stored as <c>SkipColorKeyframe</c>, <c>ColorInterpolation</c>, <c>ColorEasing</c>.</summary>
    Color,
}

/// <summary>The property group a keyframe diamond and curve picker edit on a shape (#1478).</summary>
public enum ShapeKeyframeGroup
{
    /// <summary>The shape offset (<c>X/Y</c>); stored as <c>SkipOffsetKeyframe</c>, <c>OffsetInterpolation</c>, <c>OffsetEasing</c>.</summary>
    Offset,

    /// <summary>The shape size (<c>ScaleX/ScaleY</c> or <c>Radius</c>; rectangles and circles only); stored as <c>SkipSizeKeyframe</c>, <c>SizeInterpolation</c>, <c>SizeEasing</c>.</summary>
    Size,

    /// <summary>The polygon points (polygons only); stored as <c>SkipPointsKeyframe</c>, <c>PointsInterpolation</c>, <c>PointsEasing</c>.</summary>
    Points,
}

/// <summary>The eight curves the inspector offers. Each maps to one fixed (type, easing) pair in the file.</summary>
public enum KeyframeCurveChoice
{
    Snap,
    Linear,
    EaseIn,
    EaseOut,
    EaseInOut,
    Anticipate,
    Overshoot,
    Bounce,
}

/// <summary>
/// Maps between the inspector's flat eight-icon curve picker and the (type, easing) names stored on a
/// frame. Reading is strict: a stored pair outside the eight (Cubic In, an unknown name) maps to
/// <c>null</c> so the picker shows nothing selected and nothing rewrites the file until the user picks.
/// </summary>
public static class KeyframeCurvePicker
{
    /// <summary>The picker's icons, in display order.</summary>
    public static IReadOnlyList<KeyframeCurveChoice> All { get; } = new[]
    {
        KeyframeCurveChoice.Snap, KeyframeCurveChoice.Linear,
        KeyframeCurveChoice.EaseIn, KeyframeCurveChoice.EaseOut, KeyframeCurveChoice.EaseInOut,
        KeyframeCurveChoice.Anticipate, KeyframeCurveChoice.Overshoot, KeyframeCurveChoice.Bounce,
    };

    /// <summary>The names to store for <paramref name="choice"/>. Snap stores nothing for both.</summary>
    public static (string? Type, string? Easing) ToStored(KeyframeCurveChoice choice) => choice switch
    {
        KeyframeCurveChoice.Snap => (null, null),
        KeyframeCurveChoice.Linear => (nameof(InterpolationType.Linear), nameof(Easing.Out)),
        KeyframeCurveChoice.EaseIn => (nameof(InterpolationType.Quadratic), nameof(Easing.In)),
        KeyframeCurveChoice.EaseOut => (nameof(InterpolationType.Quadratic), nameof(Easing.Out)),
        KeyframeCurveChoice.EaseInOut => (nameof(InterpolationType.Quadratic), nameof(Easing.InOut)),
        KeyframeCurveChoice.Anticipate => (nameof(InterpolationType.Back), nameof(Easing.In)),
        KeyframeCurveChoice.Overshoot => (nameof(InterpolationType.Back), nameof(Easing.Out)),
        KeyframeCurveChoice.Bounce => (nameof(InterpolationType.Bounce), nameof(Easing.Out)),
        _ => (null, null),
    };

    /// <summary>
    /// The choice a stored pair corresponds to, or <c>null</c> when it is none of the eight. An omitted
    /// or <c>Instant</c> type is Snap (its easing is ignored); an omitted easing is Out; Linear ignores
    /// its easing, as the evaluator does.
    /// </summary>
    public static KeyframeCurveChoice? FromStored(string? type, string? easing)
    {
        if (string.IsNullOrWhiteSpace(type) || Is(type, nameof(InterpolationType.Instant)))
            return KeyframeCurveChoice.Snap;
        if (Is(type, nameof(InterpolationType.Linear)))
            return KeyframeCurveChoice.Linear;

        string direction = string.IsNullOrWhiteSpace(easing) ? nameof(Easing.Out) : easing;
        bool isIn = Is(direction, nameof(Easing.In));
        bool isOut = Is(direction, nameof(Easing.Out));
        bool isInOut = Is(direction, nameof(Easing.InOut));

        if (Is(type, nameof(InterpolationType.Quadratic)))
            return isIn ? KeyframeCurveChoice.EaseIn
                : isOut ? KeyframeCurveChoice.EaseOut
                : isInOut ? KeyframeCurveChoice.EaseInOut
                : null;
        if (Is(type, nameof(InterpolationType.Back)))
            return isIn ? KeyframeCurveChoice.Anticipate : isOut ? KeyframeCurveChoice.Overshoot : null;
        if (Is(type, nameof(InterpolationType.Bounce)))
            return isOut ? KeyframeCurveChoice.Bounce : null;
        return null;
    }

    /// <summary>The tooltip for a picker icon.</summary>
    public static string DisplayName(KeyframeCurveChoice choice) => choice switch
    {
        KeyframeCurveChoice.Snap => "Snap",
        KeyframeCurveChoice.Linear => "Linear",
        KeyframeCurveChoice.EaseIn => "Ease In",
        KeyframeCurveChoice.EaseOut => "Ease Out",
        KeyframeCurveChoice.EaseInOut => "Ease In-Out",
        KeyframeCurveChoice.Anticipate => "Anticipate",
        KeyframeCurveChoice.Overshoot => "Overshoot",
        KeyframeCurveChoice.Bounce => "Bounce",
        _ => choice.ToString(),
    };

    /// <summary>Names a stored pair for a tooltip, e.g. <c>Cubic In</c>. An omitted type reads as Snap.</summary>
    public static string DescribeStored(string? type, string? easing)
    {
        if (string.IsNullOrWhiteSpace(type)) return "Snap";
        return string.IsNullOrWhiteSpace(easing) ? type : $"{type} {easing}";
    }

    private static bool Is(string value, string name) =>
        string.Equals(value.Trim(), name, StringComparison.OrdinalIgnoreCase);
}
