using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FlatRedBall2.Glue.Model;

/// <summary>An asset a Glue project or element depends on.</summary>
public class ReferencedFileSave
{
    /// <summary>
    /// Reproduces FRB1's constructor defaults, which are the whole ballgame for this type.
    /// </summary>
    /// <remarks>
    /// Four of these default <c>true</c> in FRB1, and Glue writes element files with defaults
    /// omitted — so <c>true</c> never appears on disk and only <c>false</c> is ever written. A mirror
    /// that lets them fall to <c>false</c> reads every asset in every real project as "do not load".
    /// <para>Test against JSON that <em>omits</em> the member; a test on <c>new ReferencedFileSave()</c>
    /// would pass while the bug shipped.</para>
    /// </remarks>
    public ReferencedFileSave()
    {
        LoadedAtRuntime = true;
        DestroyOnUnload = true;
        IsSharedStatic = true;
        AddToManagers = true;
    }

    /// <summary>
    /// Path to the asset, relative to the project's <c>Content</c> folder, forward-slashed.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The runtime type the asset loads as, when Glue records one. Frequently absent, and a weak
    /// discriminator on its own — <c>.tmx</c>, <c>.scnx</c> and <c>.tilb</c> all share one value.
    /// Resolve on the extension first.
    /// </summary>
    public string? RuntimeType { get; set; }

    /// <summary>Glue's name/value bag for this file.</summary>
    public List<PropertySave> Properties { get; set; } = new();

    /// <summary>Whether one shared instance is used rather than a per-element copy.</summary>
    public bool IsSharedStatic { get; set; }

    /// <summary>Whether the asset is unloaded when its owner is destroyed.</summary>
    public bool DestroyOnUnload { get; set; }

    /// <summary>Whether the asset is exposed as a public member on its owner.</summary>
    public bool HasPublicProperty { get; set; }

    /// <summary>Whether the asset is loaded at all. Absent means <c>true</c>.</summary>
    public bool LoadedAtRuntime { get; set; }

    /// <summary>Whether the loaded asset is registered with the engine's managers.</summary>
    public bool AddToManagers { get; set; }

    /// <summary>Whether a CSV loads as a dictionary keyed on its required column, or as a list.</summary>
    public bool CreatesDictionary { get; set; }

    /// <summary>Whether a file with a non-CSV extension should still be parsed as one.</summary>
    public bool TreatAsCsv { get; set; }

    /// <summary>Whether the asset is edited outside Glue and should not be regenerated.</summary>
    public bool IsManuallyUpdated { get; set; }

    /// <summary>Whether Glue created this entry from a wildcard pattern rather than explicitly.</summary>
    public bool IsCreatedByWildcard { get; set; }

    /// <summary>
    /// Whether the instance name keeps its folder path, disambiguating two same-named files in one
    /// element.
    /// </summary>
    public bool IncludeDirectoryRelativeToContainer { get; set; }

    /// <summary>The build-tool input this asset is produced from — <em>not</em> an element reference.</summary>
    /// <remarks>
    /// Unrelated to <see cref="NamedObjectSave.SourceFile"/>, which is the key that matches an object
    /// to one of these. Same name, different meaning.
    /// </remarks>
    public string? SourceFile { get; set; }

    /// <summary>The external tool that produces this asset from <see cref="SourceFile"/>.</summary>
    public string? BuildTool { get; set; }

    /// <summary>
    /// Whether the asset is deferred until something references it. Stored both as a JSON member and
    /// in <see cref="Properties"/>; the bag is authoritative.
    /// </summary>
    [JsonIgnore]
    public bool LoadedOnlyWhenReferenced =>
        Properties.GetValue<bool>(nameof(LoadedOnlyWhenReferenced));

    /// <summary>
    /// Whether the asset is built by the content pipeline, which means its extension is stripped
    /// from the load path.
    /// </summary>
    [JsonIgnore]
    public bool UseContentPipeline => Properties.GetValue<bool>(nameof(UseContentPipeline));

    /// <summary>Whether this file is a CSV, by extension or by explicit flag.</summary>
    [JsonIgnore]
    public bool IsCsv =>
        TreatAsCsv ||
        (Name is not null && Name.EndsWith(".csv", System.StringComparison.OrdinalIgnoreCase));

    /// <inheritdoc />
    public override string ToString() => Name ?? base.ToString()!;
}
