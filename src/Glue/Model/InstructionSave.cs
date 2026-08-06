using System.Text.Json;

namespace FlatRedBall2.Glue.Model;

/// <summary>
/// One value assignment — a member, the value to give it, and when. Phase 1 retains these; applying
/// them is Phase 3 (initial values) and Phase 7 (states).
/// </summary>
public class InstructionSave
{
    /// <summary>The member being assigned.</summary>
    public string? Member { get; set; }

    /// <summary>
    /// The value, left undecoded. What it holds is only knowable from the target member's type, so
    /// it stays raw until the phase that knows applies it.
    /// </summary>
    public JsonElement Value { get; set; }

    /// <summary>Glue's own label for the value's type. Diagnostic only — see <see cref="PropertySave.Type"/>.</summary>
    public string? Type { get; set; }

    /// <summary>When the assignment happens, in seconds. Zero for initial values.</summary>
    public double Time { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{Member} = {Value}";
}
