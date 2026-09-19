using System.Text.Json.Serialization;

namespace AnimationEditor.Core.Data;

/// <summary>
/// Source-generated serialization context for <see cref="AETiledSyncSave"/> -- required for the
/// browser/WASM build, which disables reflection-based System.Text.Json serialization regardless
/// of Debug/Release or trimming settings (same reasoning as
/// <see cref="AnimationEditor.Core.Export.PixiJsJsonContext"/> and
/// <see cref="AnimationEditor.Core.IO.NativeFolderJsonContext"/>).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AETiledSyncSave))]
internal sealed partial class AETiledSyncJsonContext : JsonSerializerContext
{
}
