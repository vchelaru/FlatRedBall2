using System.Collections.Generic;

namespace FlatRedBall2.Glue.Model;

/// <summary>
/// The contents of one Glue <c>.gluj</c> file — the project root.
/// </summary>
/// <remarks>
/// As written to disk, <see cref="Screens"/> and <see cref="Entities"/> are empty and the elements
/// live in sibling <c>.glsj</c>/<c>.glej</c> files named by <see cref="ScreenReferences"/> and
/// <see cref="EntityReferences"/>. <c>GlueProjectLoader</c> resolves those and fills the lists in,
/// matching what FRB1 does on load.
/// </remarks>
public class GlueProjectSave
{
    /// <summary>
    /// The schema version this project was last saved at. Compared against
    /// <see cref="GlueVersions.Latest"/> for a diagnostic only — the reader implements one schema
    /// and never branches on this.
    /// </summary>
    public int FileVersion { get; set; }

    /// <summary>The screen to start on, in backslash form (<c>Screens\Level1</c>).</summary>
    public string? StartUpScreen { get; set; }

    /// <summary>Names of the <c>.glsj</c> files to load. Cleared once resolved into <see cref="Screens"/>.</summary>
    public List<GlueElementFileReference> ScreenReferences { get; set; } = new();

    /// <summary>Names of the <c>.glej</c> files to load. Cleared once resolved into <see cref="Entities"/>.</summary>
    public List<GlueElementFileReference> EntityReferences { get; set; } = new();

    /// <summary>Loaded screens. Empty on disk; populated by the loader.</summary>
    public List<ScreenSave> Screens { get; set; } = new();

    /// <summary>Loaded entities. Empty on disk; populated by the loader.</summary>
    public List<EntitySave> Entities { get; set; } = new();

    /// <summary>Project-level assets. Loaded in Phase 4.</summary>
    public List<ReferencedFileSave> GlobalFiles { get; set; } = new();

    /// <summary>Glue's name/value bag for the project.</summary>
    public List<PropertySave> Properties { get; set; } = new();

    /// <summary>Resolution and window setup. Mapped onto FRB2's own settings in Phase 13.</summary>
    public DisplaySettings? DisplaySettings { get; set; }
}

/// <summary>A pointer to an element file, by element name rather than by path.</summary>
public class GlueElementFileReference
{
    /// <summary>
    /// The element's name in backslash form and without an extension (<c>Screens\Level1</c>). The
    /// file is this name plus <c>.glsj</c> or <c>.glej</c>, relative to the <c>.gluj</c>.
    /// </summary>
    public string? Name { get; set; }

    /// <inheritdoc />
    public override string ToString() => Name ?? base.ToString()!;
}
