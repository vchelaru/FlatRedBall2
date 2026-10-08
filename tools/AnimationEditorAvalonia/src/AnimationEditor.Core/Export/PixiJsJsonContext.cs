using System.Text.Json.Serialization;

namespace AnimationEditor.Core.Export;

/// <summary>
/// Source-generated serialization context for <see cref="PixiJsSpriteSheet"/>, so serialization
/// stays trim- and AOT-safe instead of relying on reflection-based System.Text.Json, which a
/// trimmed or reflection-disabled runtime throws on.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PixiJsSpriteSheet))]
internal sealed partial class PixiJsJsonContext : JsonSerializerContext
{
}
