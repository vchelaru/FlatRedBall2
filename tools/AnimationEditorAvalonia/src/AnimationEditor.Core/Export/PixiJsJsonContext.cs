using System.Text.Json.Serialization;

namespace AnimationEditor.Core.Export;

/// <summary>
/// Source-generated serialization context for <see cref="PixiJsSpriteSheet"/>. The reflection-based
/// <c>JsonSerializer.Serialize(sheet, options)</c> overload throws
/// <c>JsonSerializerIsReflectionDisabled</c> at runtime in the browser/WASM build (confirmed live --
/// Microsoft.NET.Sdk.WebAssembly disables reflection-based System.Text.Json serialization by
/// default, independent of Debug/Release or trimming settings). Desktop tests never caught this
/// because desktop's runtime has reflection-based serialization enabled.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(PixiJsSpriteSheet))]
internal sealed partial class PixiJsJsonContext : JsonSerializerContext
{
}
