using System.Text.Json;

namespace FlatRedBall2.Glue.Model;

/// <summary>
/// One entry in a Glue name/value bag. Glue stores much of an element's meaningful data here
/// rather than as named JSON fields, so most reads go through
/// <see cref="PropertySaveExtensions.GetValue{T}"/> rather than touching <see cref="Value"/>.
/// </summary>
public class PropertySave
{
    /// <summary>The key this entry is looked up by.</summary>
    public string? Name { get; set; }

    /// <summary>
    /// The raw JSON value, left undecoded. Glue writes these untyped, so what a given entry holds
    /// is only knowable from the member being read — see <see cref="Type"/>.
    /// </summary>
    public JsonElement Value { get; set; }

    /// <summary>
    /// Glue's own label for the value's type — a mix of C# keywords (<c>int</c>), CLR simple names
    /// (<c>Boolean</c>), and Glue enum names (<c>SourceType</c>). It is frequently absent and can
    /// disagree with the actual value, so it is diagnostic metadata only: never drive a conversion
    /// from it.
    /// </summary>
    public string? Type { get; set; }

    /// <inheritdoc />
    public override string ToString() => $"{Name} = {Value}";
}
