using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FlatRedBall2.Glue.Model;

/// <summary>
/// A variable exposed on a Glue element — the values an author tunes in Glue's variable grid.
/// </summary>
/// <remarks>
/// Most of what matters about a variable — including its declared type — lives in
/// <see cref="Properties"/> rather than in named JSON members, so the accessors below read through
/// the bag.
/// </remarks>
public class CustomVariable
{
    private string? _sourceObject;
    private string? _sourceObjectProperty;

    /// <summary>The variable's name as exposed on the element.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// The authored value. Check <see cref="HasAuthoredValue"/> before using it: most variables have
    /// none, and that means "leave the target alone" rather than "assign the default".
    /// </summary>
    public JsonElement DefaultValue { get; set; }

    /// <summary>The object this variable forwards to, when it tunnels into a member.</summary>
    public string? SourceObject
    {
        get => _sourceObject;
        set => _sourceObject = GlueSentinel.NullIfUnset(value);
    }

    /// <summary>The member on <see cref="SourceObject"/> this variable forwards to.</summary>
    public string? SourceObjectProperty
    {
        get => _sourceObjectProperty;
        set => _sourceObjectProperty = GlueSentinel.NullIfUnset(value);
    }

    /// <summary>Editor grouping. Carried for fidelity; not behavioral.</summary>
    public string? Category { get; set; }

    /// <summary>Whether derived elements may override this variable.</summary>
    public bool SetByDerived { get; set; }

    /// <summary>
    /// Whether this variable re-declares one from a base element. Such an entry is a stub: with no
    /// <see cref="DefaultValue"/> it inherits the base's value, and with one it overrides.
    /// </summary>
    public bool DefinedByBase { get; set; }

    /// <summary>
    /// Whether FRB1 would generate this as a <c>static</c> member, which also suppresses the
    /// per-instance assignment. Five occurrences across every FRB1 sample.
    /// </summary>
    public bool IsShared { get; set; }

    /// <summary>Whether FRB1 generates before/after-set events for this variable.</summary>
    public bool CreatesEvent { get; set; }

    /// <summary>Author-supplied documentation. Carried for fidelity; not behavioral.</summary>
    public string? Summary { get; set; }

    /// <summary>Glue's name/value bag. The accessors below are stored here, not as JSON members.</summary>
    public List<PropertySave> Properties { get; set; } = new();

    /// <summary>
    /// The variable's declared type, as Glue's own type string (<c>float</c>, <c>Color</c>, an enum
    /// name, sometimes a CSV path). Has no JSON member of its own — it is only ever stored in
    /// <see cref="Properties"/>, and FRB1 treats its absence as malformed.
    /// </summary>
    [JsonIgnore]
    public string? Type => Properties.GetValue<string>(nameof(Type));

    /// <summary>
    /// The type the variable exposes, when it differs from <see cref="Type"/>. When set, the stored
    /// <see cref="DefaultValue"/> is in <em>this</em> type and reaches the target through
    /// <see cref="TypeConverter"/>.
    /// </summary>
    [JsonIgnore]
    public string? OverridingPropertyType => Properties.GetValue<string>(nameof(OverridingPropertyType));

    /// <summary>
    /// Names the conversion between <see cref="OverridingPropertyType"/> and <see cref="Type"/> —
    /// <c>&lt;default&gt;</c> or <c>Comma Separating</c> in practice.
    /// </summary>
    [JsonIgnore]
    public string? TypeConverter => Properties.GetValue<string>(nameof(TypeConverter));

    /// <summary>Visibility scope, stored in <see cref="Properties"/>.</summary>
    [JsonIgnore]
    public int Scope => Properties.GetValue<int>(nameof(Scope));

    /// <summary>Whether FRB1 emits a property rather than a field.</summary>
    /// <remarks>The bag key is <c>CreatesProperties</c> — plural, unlike this member.</remarks>
    [JsonIgnore]
    public bool CreatesProperty => Properties.GetValue<bool>("CreatesProperties");

    /// <summary>Whether FRB1 emits a companion <c>&lt;Name&gt;Velocity</c> member.</summary>
    [JsonIgnore]
    public bool HasAccompanyingVelocityProperty =>
        Properties.GetValue<bool>(nameof(HasAccompanyingVelocityProperty));

    /// <summary>Whether this variable forwards to a member of another object rather than to itself.</summary>
    [JsonIgnore]
    public bool IsTunneling => SourceObject is not null && SourceObjectProperty is not null;

    /// <summary>
    /// Whether the author actually chose a value. False for a member that is absent, explicitly
    /// null, or the <c>&lt;NONE&gt;</c> sentinel.
    /// </summary>
    /// <remarks>
    /// The distinction is load-bearing and easy to lose: most variables carry no value, and FRB1
    /// skips those rather than assigning <c>default</c>. Treating absent as zero moves every entity
    /// whose position is exposed — which is most of them — back to the origin.
    /// </remarks>
    [JsonIgnore]
    public bool HasAuthoredValue => DefaultValue.ValueKind switch
    {
        JsonValueKind.Undefined or JsonValueKind.Null => false,
        JsonValueKind.String => DefaultValue.GetString() != GlueSentinel.None,
        _ => true,
    };

    /// <inheritdoc />
    public override string ToString() => Type is null ? Name ?? "" : $"{Type} {Name}";
}
