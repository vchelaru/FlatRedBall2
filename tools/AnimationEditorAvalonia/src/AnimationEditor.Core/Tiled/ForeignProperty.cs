using DotTiled;

namespace AnimationEditor.Core.Tiled;

/// <summary>
/// A tile property that is not a plain string or int. The editor neither reads nor writes its value; it stays in
/// the model so that its name counts as taken and its tile as not empty.
/// </summary>
public sealed class ForeignProperty : IProperty
{
    public string Name { get; set; } = "";

    /// <inheritdoc/>
    public PropertyType Type => PropertyType.Class;

    public IProperty Clone() => new ForeignProperty { Name = Name };
}
