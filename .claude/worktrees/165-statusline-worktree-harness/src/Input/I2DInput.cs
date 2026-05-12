using System;
using System.Collections.Generic;

namespace FlatRedBall2.Input;

/// <summary>
/// Two-axis input abstraction (X/Y). Used for directional movement, look input, and any input
/// surface that produces a 2D vector. Y+ is up by convention, matching world-space coordinates.
/// Combine sources via <see cref="I2DInputExtensions.Or"/>.
/// <para>
/// <b>Magnitude convention (recommended, not enforced):</b> implementations should produce values
/// bounded to the unit circle — i.e. <c>X² + Y² ≤ 1</c> — so that consumers can treat the vector
/// as a normalized direction × magnitude. Analog sources (gamepad sticks) typically respect this
/// naturally. Digital sources that combine independent axes (e.g. <see cref="KeyboardInput2D"/>)
/// do <b>not</b>: holding two diagonal keys reports magnitude √2. The interface does not enforce
/// the bound because clamping at the source loses information for some use cases. Consumers that
/// need a true unit vector should normalize themselves; consumers that just multiply by a per-axis
/// speed don't need to.
/// </para>
/// </summary>
public interface I2DInput
{
    /// <summary>Current X-axis value. Typically in <c>[-1, 1]</c>; see the magnitude convention on <see cref="I2DInput"/>.</summary>
    float X { get; }

    /// <summary>Current Y-axis value (Y+ up). Typically in <c>[-1, 1]</c>; see the magnitude convention on <see cref="I2DInput"/>.</summary>
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
public class Combined2DInput : I2DInput
{
    /// <summary>
    /// The list of inputs being combined. Add inputs directly or use the fluent
    /// <see cref="I2DInputExtensions.Or"/> method.
    /// </summary>
    public List<I2DInput> Inputs { get; } = new();

    /// <summary>X value of the contained input with the largest absolute X, sign preserved. 0 if empty.</summary>
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

    /// <summary>
    /// Y value of the contained input with the largest absolute Y, sign preserved. 0 if empty.
    /// Selected independently of <see cref="X"/> — the X and Y components may come from different inputs.
    /// </summary>
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

/// <summary>Fluent helpers for combining <see cref="I2DInput"/> sources.</summary>
public static class I2DInputExtensions
{
    /// <summary>
    /// Returns a <see cref="Combined2DInput"/> that combines this input with <paramref name="other"/>,
    /// returning whichever has the larger absolute value on each axis. Calling Or on an existing
    /// <see cref="Combined2DInput"/> adds to it in place rather than wrapping it.
    /// </summary>
    /// <remarks>
    /// If <paramref name="thisInput"/> is already a <see cref="Combined2DInput"/>, it is mutated
    /// and returned — not wrapped. This enables fluent chaining (<c>a.Or(b).Or(c)</c>) without extra
    /// allocations, but means stored intermediate results will also reflect the added input.
    /// </remarks>
    public static Combined2DInput Or(this I2DInput thisInput, I2DInput other)
    {
        Combined2DInput result;
        if (thisInput is Combined2DInput multi)
        {
            result = multi;
        }
        else
        {
            result = new Combined2DInput();
            result.Inputs.Add(thisInput);
        }
        result.Inputs.Add(other);
        return result;
    }
}
