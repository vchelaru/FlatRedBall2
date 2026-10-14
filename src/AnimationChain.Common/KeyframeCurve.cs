using System;
using FlatRedBall.Glue.StateInterpolation;

namespace FlatRedBall2.AnimationEditorCommon;

/// <summary>
/// How a keyframe gets from its own value to the next keyframe's: an
/// <see cref="InterpolationType"/> plus an <see cref="Easing"/> direction. The default value is
/// <see cref="Snap"/> (hold the start value, switch at the segment end), which is also what an
/// omitted or unrecognized type means in the file. Easing defaults to <see cref="Easing.Out"/>.
/// </summary>
/// <remarks>
/// The <c>.achx</c>/<c>.achj</c> files store the type and easing as plain names
/// (<see cref="AnimationFrameSave.OffsetInterpolation"/> etc.), not as the library enums, because
/// the enums' zero values are <c>Back</c> and <c>In</c>; <see cref="Parse"/> maps the names back.
/// Linear ignores easing (the library's three Linear functions are identical), and Snap does too.
/// Snap is evaluated here and never through the library's <c>Instant.EaseIn/EaseInOut</c>, which
/// jump early or oddly.
/// </remarks>
public readonly struct KeyframeCurve : IEquatable<KeyframeCurve>
{
    // Nullable so default(KeyframeCurve) means Snap/Out, matching an omitted file field.
    private readonly InterpolationType? _type;
    private readonly Easing? _easing;

    private KeyframeCurve(InterpolationType type, Easing easing)
    {
        _type = type;
        _easing = easing;
    }

    /// <summary>Hold the start value until the next keyframe, then switch. The default.</summary>
    public static KeyframeCurve Snap => default;

    /// <summary>The curve family. <see cref="InterpolationType.Instant"/> means snap.</summary>
    public InterpolationType Type => _type ?? InterpolationType.Instant;

    /// <summary>The easing direction. Ignored for snap and linear.</summary>
    public Easing Easing => _easing ?? Easing.Out;

    /// <summary>Whether this curve holds the start value rather than gliding.</summary>
    public bool IsSnap => Type == InterpolationType.Instant;

    /// <summary>
    /// Builds a curve from the names stored in a file. An omitted, empty, or unrecognized
    /// <paramref name="type"/> is snap (the recommended runtime behavior for a name it does not know);
    /// an omitted or unrecognized <paramref name="easing"/> is <see cref="Easing.Out"/>. Names match
    /// case-insensitively; numeric strings are not accepted.
    /// </summary>
    public static KeyframeCurve Parse(string? type, string? easing)
    {
        if (!TryParseName(type, out InterpolationType parsedType) || parsedType == InterpolationType.Instant)
            return Snap;
        if (!TryParseName(easing, out Easing parsedEasing))
            parsedEasing = Easing.Out;
        return new KeyframeCurve(parsedType, parsedEasing);
    }

    /// <summary>
    /// Maps linear progress <paramref name="t"/> (0 at the keyframe, 1 at the next one) to eased
    /// progress. Endpoints are exact: <c>0</c> returns 0 and <c>1</c> returns 1, even for curves
    /// that overshoot in between. Snap returns 0 for every <c>t</c> below 1.
    /// </summary>
    public float Evaluate(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        if (IsSnap) return 0f;
        if (Type == InterpolationType.Linear) return t;
        return Tweener.GetInterpolationFunction(Type, Easing)(t, 0f, 1f, 1f);
    }

    /// <inheritdoc/>
    public bool Equals(KeyframeCurve other) => Type == other.Type && Easing == other.Easing;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is KeyframeCurve other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(Type, Easing);

    /// <inheritdoc/>
    public override string ToString() => IsSnap ? "Snap" : $"{Type} {Easing}";

    /// <summary>Compares two curves for equality.</summary>
    public static bool operator ==(KeyframeCurve left, KeyframeCurve right) => left.Equals(right);

    /// <summary>Compares two curves for inequality.</summary>
    public static bool operator !=(KeyframeCurve left, KeyframeCurve right) => !left.Equals(right);

    private static bool TryParseName<TEnum>(string? name, out TEnum value) where TEnum : struct, Enum
    {
        value = default;
        // Enum.TryParse would accept "3" and comma lists; the file stores names only.
        if (string.IsNullOrWhiteSpace(name) || !char.IsLetter(name[0])) return false;
        return Enum.TryParse(name, ignoreCase: true, out value) && Enum.IsDefined(value);
    }
}
