using System;
using System.Collections.Generic;

namespace FlatRedBall2.Input;

/// <summary>
/// Single-axis input abstraction. <see cref="Value"/> is conventionally in the range [-1, 1] for
/// directional input or [0, 1] for trigger-style input. Implementations include any source that can
/// produce a scalar — buttons, axes, or composites built via <see cref="I1DInputExtensions.Or"/>.
/// </summary>
public interface I1DInput
{
    /// <summary>Current input value. Range depends on the source — typically [-1, 1] or [0, 1].</summary>
    float Value { get; }
}

/// <summary>
/// Combines multiple <see cref="I1DInput"/> instances into one. Returns whichever input has the
/// largest absolute value. Typically created via <see cref="I1DInputExtensions.Or"/>.
/// </summary>
public class Combined1DInput : I1DInput
{
    /// <summary>
    /// The list of inputs being combined. Add inputs directly or use the fluent
    /// <see cref="I1DInputExtensions.Or"/> method.
    /// </summary>
    public List<I1DInput> Inputs { get; } = new();

    /// <summary>
    /// Value of the contained input with the largest absolute magnitude, with sign preserved.
    /// Returns 0 when <see cref="Inputs"/> is empty.
    /// </summary>
    public float Value
    {
        get
        {
            var result = 0f;
            foreach (var input in Inputs)
            {
                if (MathF.Abs(input.Value) > MathF.Abs(result))
                    result = input.Value;
            }
            return result;
        }
    }
}

/// <summary>Fluent helpers for combining <see cref="I1DInput"/> sources.</summary>
public static class I1DInputExtensions
{
    /// <summary>
    /// Returns a <see cref="Combined1DInput"/> that combines this input with <paramref name="other"/>,
    /// returning whichever has the larger absolute value. Calling Or on an existing
    /// <see cref="Combined1DInput"/> adds to it in place rather than wrapping it.
    /// </summary>
    /// <remarks>
    /// If <paramref name="thisInput"/> is already a <see cref="Combined1DInput"/>, it is mutated
    /// and returned — not wrapped. This enables fluent chaining (<c>a.Or(b).Or(c)</c>) without extra
    /// allocations, but means stored intermediate results will also reflect the added input.
    /// </remarks>
    public static Combined1DInput Or(this I1DInput thisInput, I1DInput other)
    {
        Combined1DInput result;
        if (thisInput is Combined1DInput multi)
        {
            result = multi;
        }
        else
        {
            result = new Combined1DInput();
            result.Inputs.Add(thisInput);
        }
        result.Inputs.Add(other);
        return result;
    }
}
