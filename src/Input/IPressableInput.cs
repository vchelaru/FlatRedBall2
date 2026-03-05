using System.Collections.Generic;

namespace FlatRedBall2.Input;

public interface IPressableInput
{
    bool IsDown { get; }
    bool WasJustPressed { get; }
    bool WasJustReleased { get; }
}

/// <summary>
/// Combines multiple <see cref="IPressableInput"/> instances into one using OR semantics:
/// each property is true if <em>any</em> of the contained inputs reports it as true.
/// Typically created via <see cref="IPressableInputExtensions.Or"/>.
/// </summary>
public class OrPressableInput : IPressableInput
{
    /// <summary>
    /// The list of inputs being combined. Add inputs directly or use the fluent
    /// <see cref="IPressableInputExtensions.Or"/> method.
    /// </summary>
    public List<IPressableInput> Inputs { get; } = new();

    public OrPressableInput(IPressableInput input1, IPressableInput input2)
    {
        Inputs.Add(input1);
        Inputs.Add(input2);
    }

    public bool IsDown
    {
        get
        {
            foreach (var input in Inputs)
                if (input.IsDown) return true;
            return false;
        }
    }

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

public static class IPressableInputExtensions
{
    /// <summary>
    /// Returns an <see cref="OrPressableInput"/> that combines this input with
    /// <paramref name="other"/> using OR semantics. Calling Or on an existing
    /// <see cref="OrPressableInput"/> adds to it in place rather than wrapping it.
    /// </summary>
    /// <remarks>
    /// If <paramref name="thisInput"/> is already an <see cref="OrPressableInput"/>, it is
    /// mutated and returned — not wrapped. This enables fluent chaining
    /// (<c>a.Or(b).Or(c)</c>) without extra allocations, but means stored intermediate
    /// results will also reflect the added input.
    /// </remarks>
    public static OrPressableInput Or(this IPressableInput thisInput, IPressableInput other)
    {
        if (thisInput is OrPressableInput orInput)
        {
            orInput.Inputs.Add(other);
            return orInput;
        }
        return new OrPressableInput(thisInput, other);
    }
}
