using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace FlatRedBall2.Glue.Model;

/// <summary>
/// Shared shape of a Glue Screen and Entity — the contents of one <c>.glsj</c> or <c>.glej</c> file.
/// </summary>
public abstract class GlueElement
{
    /// <summary>
    /// The element's identity, project-relative and backslash-separated with no extension
    /// (<c>Screens\Level1</c>). Normalize separators when building a path from it, but compare it
    /// as-is: this same form is what <c>StartUpScreen</c> and <c>BaseScreen</c> reference.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>Objects declared in this element.</summary>
    public List<NamedObjectSave> NamedObjects { get; set; } = new();

    /// <summary>Variables exposed on this element. Applied in Phase 3.</summary>
    public List<CustomVariable> CustomVariables { get; set; } = new();

    /// <summary>Assets this element needs. Loaded in Phase 4.</summary>
    public List<ReferencedFileSave> ReferencedFiles { get; set; } = new();

    /// <summary>Uncategorized states. Applied in Phase 7.</summary>
    public List<StateSave> States { get; set; } = new();

    /// <summary>Categorized states. Applied in Phase 7.</summary>
    public List<StateSaveCategory> StateCategoryList { get; set; } = new();

    /// <summary>Glue's name/value bag for this element.</summary>
    public List<PropertySave> Properties { get; set; } = new();

    /// <summary>Whether this element's content is loaded into the global content manager.</summary>
    public bool UseGlobalContent { get; set; }

    /// <summary>
    /// The element this one derives from, in the same backslash form as <see cref="Name"/>, or null
    /// when it derives from nothing.
    /// </summary>
    /// <remarks>
    /// Computed rather than bound: a screen writes this alongside <c>BaseScreen</c> while an entity
    /// writes only <c>BaseEntity</c>, so binding it would leave entities reading null.
    /// </remarks>
    [JsonIgnore]
    public abstract string? BaseElement { get; }

    /// <summary>
    /// Whether the element leaves an object for a derived element to supply, and therefore cannot be
    /// instantiated on its own.
    /// </summary>
    /// <remarks>
    /// Computed, and deliberately not read from disk even though Glue writes it: the serialized
    /// value is a stale snapshot, and entities never write it at all.
    /// <para>Only objects count. A <c>SetByDerived</c> <em>variable</em> does not make an element
    /// abstract — every DoorsDemo and Beefball entity has several and all are concrete.</para>
    /// </remarks>
    [JsonIgnore]
    public bool IsAbstract => AllNamedObjects.Any(o => o.SetByDerived);

    /// <summary>
    /// This element's objects plus their immediate children, matching the depth FRB1 uses when it
    /// decides what is abstract. Does not reach into base elements.
    /// </summary>
    [JsonIgnore]
    public IEnumerable<NamedObjectSave> AllNamedObjects =>
        NamedObjects.Concat(NamedObjects.SelectMany(o => o.ContainedObjects));

    /// <inheritdoc />
    public override string ToString() => Name ?? base.ToString()!;
}
