using System.Text.Json.Serialization;

namespace AnimationEditor.Core.Data;

/// <summary>
/// Source-generated serialization context for <see cref="AETiledSyncSave"/>, so serialization
/// stays trim- and AOT-safe instead of relying on reflection (same as
/// <see cref="AnimationEditor.Core.Export.PixiJsJsonContext"/>).
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(AETiledSyncSave))]
internal sealed partial class AETiledSyncJsonContext : JsonSerializerContext
{
}
