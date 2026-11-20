using DotTiled;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// A Tiled custom enum property (<c>propertytype="Biome"</c>). With no custom type definitions
/// loaded, DotTiled reads one as a plain string/int property and drops the <c>propertytype</c>
/// reference, so <see cref="TsxLoader"/> swaps it for this to keep the reference, the raw value
/// (a flags enum is a comma-separated string) and the storage type for <see cref="TsxWriter"/>.
/// </summary>
public sealed class EnumReferenceProperty : IProperty
{
    public string Name { get; set; } = "";

    /// <inheritdoc/>
    public DotTiled.PropertyType Type => DotTiled.PropertyType.Enum;

    /// <summary>Name of the enum type defined in the Tiled project.</summary>
    public string PropertyType { get; set; } = "";

    /// <summary>The raw <c>value</c> attribute, written back unchanged.</summary>
    public string Value { get; set; } = "";

    /// <summary>The raw <c>type</c> attribute ("int" for an int-backed enum), or null when the
    /// file had none (a string-backed enum).</summary>
    public string? StorageType { get; set; }

    public IProperty Clone() => new EnumReferenceProperty
    {
        Name = Name,
        PropertyType = PropertyType,
        Value = Value,
        StorageType = StorageType,
    };
}
