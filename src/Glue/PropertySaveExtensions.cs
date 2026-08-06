using System;
using System.Collections.Generic;
using System.Text.Json;
using FlatRedBall2.Glue.Model;

namespace FlatRedBall2.Glue;

/// <summary>
/// Reads typed values out of a Glue name/value bag, following FRB1's
/// <c>PropertySaveListExtensions.GetValue&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// Deliberately more forgiving than FRB1, which tolerates only a missing name and throws
/// <see cref="InvalidCastException"/> when a value does not match the requested type. Here any
/// undecodable value yields <c>default(T)</c> as well, because a value's real type is not knowable
/// in advance — <see cref="PropertySave.Type"/> is unreliable — and a partially readable project is
/// worth more than an exception. Dropped values are traced to the debug output.
/// </remarks>
public static class PropertySaveExtensions
{
    /// <summary>
    /// Finds <paramref name="name"/> and decodes its value as <typeparamref name="T"/>. The
    /// requested type is authoritative — <see cref="PropertySave.Type"/> is never consulted, since
    /// Glue omits it on some entries and it can disagree with the stored value.
    /// </summary>
    /// <returns>The decoded value, or <c>default(T)</c> if absent or not decodable as this type.</returns>
    public static T? GetValue<T>(this IReadOnlyList<PropertySave>? properties, string name)
    {
        if (properties is null)
            return default;

        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].Name == name)
                return Decode<T>(properties[i].Value);
        }

        return default;
    }

    /// <summary>Whether an entry with this name exists, regardless of whether its value decodes.</summary>
    public static bool ContainsValue(this IReadOnlyList<PropertySave>? properties, string name)
    {
        if (properties is null)
            return false;

        for (int i = 0; i < properties.Count; i++)
        {
            if (properties[i].Name == name)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Decodes a raw Glue value as <typeparamref name="T"/>. Shared with the variable bag, which
    /// stores values undecoded for the same reason: the caller's type is the only reliable signal.
    /// </summary>
    internal static T? Decode<T>(JsonElement value)
    {
        // Unwrap int?/float?/etc. so one code path serves both the nullable and non-nullable request.
        Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);

        if (target == typeof(string))
        {
            if (value.ValueKind == JsonValueKind.String)
                return (T)(object)value.GetString()!;

            return Mismatch<T>(value, target);
        }

        if (target == typeof(bool))
        {
            return value.ValueKind switch
            {
                JsonValueKind.True => (T)(object)true,
                JsonValueKind.False => (T)(object)false,
                _ => Mismatch<T>(value, target),
            };
        }

        // Everything below is numeric or an int-backed enum, and JsonElement's numeric TryGet
        // methods *throw* rather than returning false when the element is not a Number — including
        // for the Undefined element a property with no "Value" key deserializes to. One gate here
        // covers every remaining branch.
        if (value.ValueKind != JsonValueKind.Number)
            return Mismatch<T>(value, target);

        if (target.IsEnum)
        {
            // Glue writes enums as bare ints with no string converter.
            return value.TryGetInt32(out int enumValue)
                ? (T)Enum.ToObject(target, enumValue)
                : Mismatch<T>(value, target);
        }

        if (target == typeof(int))
            return value.TryGetInt32(out int i) ? (T)(object)i : Mismatch<T>(value, target);

        if (target == typeof(long))
            return value.TryGetInt64(out long l) ? (T)(object)l : Mismatch<T>(value, target);

        if (target == typeof(float))
            return value.TryGetSingle(out float f) ? (T)(object)f : Mismatch<T>(value, target);

        if (target == typeof(double))
            return value.TryGetDouble(out double d) ? (T)(object)d : Mismatch<T>(value, target);

        if (target == typeof(decimal))
            return value.TryGetDecimal(out decimal m) ? (T)(object)m : Mismatch<T>(value, target);

        return Mismatch<T>(value, target);
    }

    /// <summary>
    /// Leaves a trail when a value is silently dropped. Returning <c>default</c> keeps a project
    /// loading, but a shape that never appears because its radius was authored as a string is
    /// otherwise undebuggable.
    /// </summary>
    private static T? Mismatch<T>(JsonElement value, Type target)
    {
        System.Diagnostics.Debug.WriteLine(
            $"[Glue] Could not read a {value.ValueKind} value as {target.Name}; using the default.");
        return default;
    }
}
