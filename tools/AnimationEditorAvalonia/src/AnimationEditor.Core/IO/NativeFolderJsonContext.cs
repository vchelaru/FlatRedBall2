using System.Text.Json.Serialization;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Source-generated serialization context for the browser folder-listing JSON payload (a plain
/// JSON array of file names, from <c>nativeFolder.js</c>'s <c>listFileNames</c>).
/// Microsoft.NET.Sdk.WebAssembly disables reflection-based System.Text.Json serialization at
/// runtime -- <c>JsonSerializer.Deserialize&lt;string[]&gt;(json)</c> threw
/// <c>JsonSerializerIsReflectionDisabled</c> and crashed the whole Mono runtime the first time
/// Open Folder ran a load (confirmed live; same root cause
/// <see cref="AnimationEditor.Core.Export.PixiJsJsonContext"/> already documents and works
/// around). Desktop tests never caught this because desktop's runtime has reflection-based
/// serialization enabled.
///
/// Public (unlike the sibling <c>PixiJsJsonContext</c>, which is <c>internal</c>) because the
/// only caller, <c>NativeFolderInterop.ListFileNamesAsync</c>, lives in the browser-only
/// <c>AnimationEditor.Browser</c> assembly, which Core's <c>InternalsVisibleTo</c> does not cover.
/// </summary>
[JsonSerializable(typeof(string[]))]
public sealed partial class NativeFolderJsonContext : JsonSerializerContext
{
}
