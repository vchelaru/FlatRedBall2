using System;
using System.Collections.Generic;

namespace FlatRedBall2.Input;

/// <summary>
/// Interface for input that returns a single axis value, typically in the range [-1, 1] for directional
/// input or [0, 1] for trigger-style input.
/// </summary>
public interface I1DInput
{
    float Value { get; }
}

/// <summary>
/// Combines multiple <see cref="I1DInput"/> instances into one. Returns whichever input has the
/// largest absolute value. Typically created via <see cref="I1DInputExtensions.Or"/>.
/// </summary>
public class Multiple1DInputs : I1DInput
{
    /// <summary>
    /// The list of inputs being combined. Add inputs directly or use the fluent
    /// <see cref="I1DInputExtensions.Or"/> method.
    /// </summary>
    public List<I1DInput> Inputs { get; } = new();

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

public static class I1DInputExtensions
{
    /// <summary>
    /// Returns a <see cref="Multiple1DInputs"/> that combines this input with <paramref name="other"/>,
    /// returning whichever has the larger absolute value. Calling Or on an existing
    /// <see cref="Multiple1DInputs"/> adds to it in place rather than wrapping it.
    /// </summary>
    /// <remarks>
    /// If <paramref name="thisInput"/> is already a <see cref="Multiple1DInputs"/>, it is mutated
    /// and returned — not wrapped. This enables fluent chaining (<c>a.Or(b).Or(c)</c>) without extra
    /// allocations, but means stored intermediate results will also reflect the added input.
    /// </remarks>
    public static Multiple1DInputs Or(this I1DInput thisInput, I1DInput other)
    {
        Multiple1DInputs result;
        if (thisInput is Multiple1DInputs multi)
        {
            result = multi;
        }
        else
        {
            result = new Multiple1DInputs();
            result.Inputs.Add(thisInput);
        }
        result.Inputs.Add(other);
        return result;
    }
}
