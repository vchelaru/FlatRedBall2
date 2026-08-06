using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlatRedBall2.Glue.Model;

/// <summary>
/// One object declared inside a Glue Screen or Entity — a shape, a sprite, a list, a nested entity,
/// or a collision relationship. Phase 1 parses and retains these; constructing real instances is
/// Phase 2.
/// </summary>
public class NamedObjectSave
{
    /// <summary>
    /// Restores the members Glue omits from disk when they hold their default. Element files are
    /// written with defaults ignored, so a mirror that lets these fall to <c>false</c> reads every
    /// real project as empty. Mirrors FRB1's <c>NamedObjectSave</c> constructor — note that
    /// <see cref="AttachToContainer"/> is deliberately absent, matching FRB1.
    /// </summary>
    public NamedObjectSave()
    {
        Instantiate = true;
        AddToManagers = true;
        IncludeInICollidable = true;
        IncludeInIClickable = true;
        CallActivity = true;
        GenerateTimedEmit = true;
    }

    private string? _sourceClassGenericType;
    private string? _currentState;

    /// <summary>The member name this object is addressed by within its element.</summary>
    public string? InstanceName { get; set; }

    /// <summary>
    /// The type to build, as Glue writes it. Not a plain CLR type name: it may carry an unresolved
    /// generic placeholder (<c>PositionedObjectList&lt;T&gt;</c>), closed generics whose arguments
    /// are Glue element names, or an element reference in backslash form. Parse it rather than
    /// matching it whole.
    /// </summary>
    public string? SourceClassType { get; set; }

    /// <summary>Which kind of thing <see cref="SourceClassType"/> names.</summary>
    public SourceType SourceType { get; set; }

    /// <summary>
    /// The element type a list holds — the argument <see cref="SourceClassType"/> leaves as a
    /// literal <c>&lt;T&gt;</c>.
    /// </summary>
    /// <remarks>
    /// This sibling field, not the type string, is where a list's real element type lives
    /// (<c>Entities\Player</c>). Parsing <see cref="SourceClassType"/> alone can never resolve it.
    /// </remarks>
    public string? SourceClassGenericType
    {
        get => _sourceClassGenericType;
        set => _sourceClassGenericType = GlueSentinel.NullIfUnset(value);
    }

    /// <summary>The asset or element this object is sourced from, when applicable.</summary>
    public string? SourceFile { get; set; }

    /// <summary>The member on <see cref="SourceFile"/> to use, for file-sourced objects.</summary>
    public string? SourceName { get; set; }

    /// <summary>Initial value assignments. Applied in Phase 3, not Phase 1.</summary>
    public List<InstructionSave> InstructionSaves { get; set; } = new();

    /// <summary>Nested objects — list members and objects owned by this one.</summary>
    public List<NamedObjectSave> ContainedObjects { get; set; } = new();

    /// <summary>Glue's name/value bag. Several members below read through it rather than from JSON.</summary>
    public List<PropertySave> Properties { get; set; } = new();

    /// <summary>
    /// Whether this object is a list rather than a single instance.
    /// </summary>
    /// <remarks>
    /// Computed, never read from JSON: Glue declares its own <c>IsList</c> as <c>[JsonIgnore]</c> and
    /// derives it the same way, so the flag never appears on disk. A mirror that bound it to JSON
    /// would read <c>false</c> for every list in every real project.
    /// </remarks>
    [JsonIgnore]
    public bool IsList =>
        SourceType == SourceType.FlatRedBallType &&
        GlueTypeName.Parse(SourceClassType).OpenTypeName is
            "PositionedObjectList" or "FlatRedBall.Math.PositionedObjectList";

    /// <summary>
    /// Whether the object has enough information to be built. Computed the way FRB1 computes it, and
    /// never bound to JSON.
    /// </summary>
    /// <remarks>
    /// A file-sourced object needs both a file and a member; a list needs its element type; anything
    /// else needs a type name. FRB1 suppresses generation for an object that fails this, so a build
    /// that ignores it tries to construct half-authored objects.
    /// </remarks>
    [JsonIgnore]
    public bool IsFullyDefined => SourceType switch
    {
        SourceType.File => !string.IsNullOrEmpty(SourceFile) && !string.IsNullOrEmpty(SourceName),
        SourceType.FlatRedBallType when IsList => !string.IsNullOrEmpty(SourceClassGenericType),
        SourceType.FlatRedBallType or SourceType.Entity => !string.IsNullOrEmpty(SourceClassType),
        _ => true,
    };

    /// <summary>
    /// The state this instance starts in. Only ever names an <em>uncategorized</em> state — Glue has
    /// no per-instance categorized state. Applied in Phase 7.
    /// </summary>
    public string? CurrentState
    {
        get => _currentState;
        set => _currentState = GlueSentinel.NullIfUnset(value);
    }

    /// <summary>Whether the author disabled this object, which suppresses building it entirely.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>Whether the containing element assigns this object rather than creating it.</summary>
    public bool SetByContainer { get; set; }

    /// <summary>
    /// Whether this object <em>is</em> its element — the pattern Glue uses when an entity derives
    /// from an engine type. Stored in <see cref="Properties"/>, not as a JSON member.
    /// </summary>
    [JsonIgnore]
    public bool IsContainer => Properties.GetValue<bool>(nameof(IsContainer));

    /// <summary>Whether this object is attached to its container, and follows it.</summary>
    /// <remarks>Deliberately not defaulted true — matches FRB1, which writes it explicitly.</remarks>
    public bool AttachToContainer { get; set; }

    /// <summary>Whether an instance is created at all. Defaults true; omitted from disk when true.</summary>
    public bool Instantiate { get; set; }

    /// <summary>Whether the instance is added to the engine's managers. Defaults true.</summary>
    public bool AddToManagers { get; set; }

    /// <summary>Whether this object participates in its container's collision. Defaults true.</summary>
    public bool IncludeInICollidable { get; set; }

    /// <summary>Whether this object participates in its container's click handling. Defaults true.</summary>
    public bool IncludeInIClickable { get; set; }

    /// <summary>Whether the container calls this object's per-frame activity. Defaults true.</summary>
    public bool CallActivity { get; set; }

    /// <summary>Whether timed instruction emission is generated for this object. Defaults true.</summary>
    public bool GenerateTimedEmit { get; set; }

    /// <summary>Whether the object is exposed as a public member on its container.</summary>
    public bool HasPublicProperty { get; set; }

    /// <summary>Declared on a base element and redeclared here. Merge semantics are Phase 6.</summary>
    public bool DefinedByBase { get; set; }

    /// <summary>Instantiated by the base element rather than by this one.</summary>
    public bool InstantiatedByBase { get; set; }

    /// <summary>Exposed for derived elements to reference.</summary>
    public bool ExposedInDerived { get; set; }

    /// <summary>Derived elements may override this object's values.</summary>
    public bool SetByDerived { get; set; }

    /// <summary>The layer to place this object on, if any.</summary>
    public string? LayerOn { get; set; }

    /// <summary>
    /// Whether a factory is generated for the list this object represents. Stored in
    /// <see cref="Properties"/> rather than as its own JSON member.
    /// </summary>
    [JsonIgnore]
    public bool AssociateWithFactory => Properties.GetValue<bool>(nameof(AssociateWithFactory));

    /// <inheritdoc />
    public override string ToString() => $"{InstanceName} ({SourceClassType})";
}
