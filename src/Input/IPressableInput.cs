using System.Collections.Generic;

namespace FlatRedBall2.Input;

/// <summary>
/// Boolean "button-like" input abstraction. Concrete implementations include
/// <see cref="KeyboardPressableInput"/>, <see cref="GamepadPressableInput"/>, and the composite
/// <see cref="AnyPressableInput"/>. Use this anywhere you want to accept "any button" rather than
/// hard-coding a specific key or gamepad button.
/// </summary>
public interface IPressableInput
{
    /// <summary>True every frame the input is held.</summary>
    bool IsDown { get; }

    /// <summary>True only on the first frame the input transitions from up to down.</summary>
    bool WasJustPressed { get; }

    /// <summary>True only on the first frame the input transitions from down to up.</summary>
    bool WasJustReleased { get; }

    /// <summary>
    /// An <see cref="IPressableInput"/> that is never down and never pressed. Use as a non-null
    /// default for fields that will be replaced later (e.g. an entity's action input assigned
    /// from a screen).
    /// </summary>
    public static IPressableInput Zero { get; } = new ZeroPressable();

    private sealed class ZeroPressable : IPressableInput
    {
        public bool IsDown => false;
        public bool WasJustPressed => false;
        public bool WasJustReleased => false;
    }
}

/// <summary>
/// Combines multiple <see cref="IPressableInput"/> instances into one using OR semantics:
/// each property is true if <em>any</em> of the contained inputs reports it as true.
/// Typically created via <see cref="IPressableInputExtensions.Or"/>.
/// </summary>
public class AnyPressableInput : IPressableInput
{
    /// <summary>
    /// The list of inputs being combined. Add inputs directly or use the fluent
    /// <see cref="IPressableInputExtensions.Or"/> method.
    /// </summary>
    public List<IPressableInput> Inputs { get; } = new();

    /// <summary>Creates a composite seeded with two inputs. Additional inputs can be appended via <see cref="IPressableInputExtensions.Or"/> or directly to <see cref="Inputs"/>.</summary>
    public AnyPressableInput(IPressableInput input1, IPressableInput input2)
    {
        Inputs.Add(input1);
        Inputs.Add(input2);
    }

    /// <summary>True if any contained input is currently down.</summary>
    public bool IsDown
    {
        get
        {
            foreach (var input in Inputs)
                if (input.IsDown) return true;
            return false;
        }
    }

    /// <summary>True if any contained input transitioned from up to down this frame.</summary>
    public bool WasJustPressed
    {
        get
        {
            foreach (var input in Inputs)
                if (input.WasJustPressed) return true;
            return false;
        }
    }

    /// <summary>
    /// Returns true if any contained input was just released this frame. Note that if
    /// multiple inputs are held and only one is released, this returns true even though
    /// the combined input is still down via the remaining held inputs.
    /// </summary>
    public bool WasJustReleased
    {
        get
        {
            foreach (var input in Inputs)
                if (input.WasJustReleased) return true;
            return false;
        }
    }
}

/// <summary>Fluent helpers for combining <see cref="IPressableInput"/> sources.</summary>
public static class IPressableInputExtensions
{
    /// <summary>
    /// Returns an <see cref="AnyPressableInput"/> that combines this input with
    /// <paramref name="other"/> using OR semantics. Calling Or on an existing
    /// <see cref="AnyPressableInput"/> adds to it in place rather than wrapping it.
    /// </summary>
    /// <remarks>
    /// If <paramref name="thisInput"/> is already an <see cref="AnyPressableInput"/>, it is
    /// mutated and returned — not wrapped. This enables fluent chaining
    /// (<c>a.Or(b).Or(c)</c>) without extra allocations, but means stored intermediate
    /// results will also reflect the added input.
    /// </remarks>
    public static AnyPressableInput Or(this IPressableInput thisInput, IPressableInput other)
    {
        if (thisInput is AnyPressableInput orInput)
        {
            orInput.Inputs.Add(other);
            return orInput;
        }
        return new AnyPressableInput(thisInput, other);
    }
}
