using System;
using System.Collections.Generic;

namespace FlatRedBall2.Input;

public interface I2DInput
{
    float X { get; }
    float Y { get; }

    /// <summary>
    /// An <see cref="I2DInput"/> that always reports (0, 0). Use as a non-null default for
    /// fields that will be replaced later (e.g. an entity's movement input assigned from a screen).
    /// </summary>
    public static I2DInput Zero { get; } = new ZeroInput();

    private sealed class ZeroInput : I2DInput
    {
        public float X => 0f;
        public float Y => 0f;
    }
}

/// <summary>
/// Combines multiple <see cref="I2DInput"/> instances into one. Returns whichever input has the
/// largest absolute value on each axis independently. Typically created via
/// <see cref="I2DInputExtensions.Or"/>.
/// </summary>
public class Multiple2DInputs : I2DInput
{
    /// <summary>
    /// The list of inputs being combined. Add inputs directly or use the fluent
    /// <see cref="I2DInputExtensions.Or"/> method.
    /// </summary>
    public List<I2DInput> Inputs { get; } = new();

    public float X
    {
        get
        {
            var result = 0f;
            foreach (var input in Inputs)
            {
                if (MathF.Abs(input.X) > MathF.Abs(result))
                    result = input.X;
            }
            return result;
        }
    }

    public float Y
    {
        get
        {
            var result = 0f;
            foreach (var input in Inputs)
            {
                if (MathF.Abs(input.Y) > MathF.Abs(result))
                    result = input.Y;
            }
            return result;
        }
    }
}

public static class I2DInputExtensions
{
    /// <summary>
    /// Returns a <see cref="Multiple2DInputs"/> that combines this input with <paramref name="other"/>,
    /// returning whichever has the larger absolute value on each axis. Calling Or on an existing
    /// <see cref="Multiple2DInputs"/> adds to it in place rather than wrapping it.
    /// </summary>
    /// <remarks>
    /// If <paramref name="thisInput"/> is already a <see cref="Multiple2DInputs"/>, it is mutated
    /// and returned — not wrapped. This enables fluent chaining (<c>a.Or(b).Or(c)</c>) without extra
    /// allocations, but means stored intermediate results will also reflect the added input.
    /// </remarks>
    public static Multiple2DInputs Or(this I2DInput thisInput, I2DInput other)
    {
        Multiple2DInputs result;
        if (thisInput is Multiple2DInputs multi)
        {
            result = multi;
        }
        else
        {
            result = new Multiple2DInputs();
            result.Inputs.Add(thisInput);
        }
        result.Inputs.Add(other);
        return result;
    }
}
