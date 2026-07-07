using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace AnimationEditor.Browser;

/// <summary>
/// JSImport bridge to a Blob + anchor-download (wwwroot/download.js) -- the browser has no
/// filesystem to write an exported file to directly, so "save" means "hand the browser a Blob
/// and let it drop the file in the user's Downloads folder", same shape as every other
/// export-from-a-web-app tool. Must be initialized via <see cref="InitializeAsync"/> before any
/// download call, same requirement as <see cref="LocalStorageInterop"/>.
/// <para>
/// Thin, untestable browser wiring (same category as <see cref="LocalStorageInterop"/> -- no
/// dedicated test project, see docs/BROWSER_FOLDER_WATCH_DECISION.md); the actual export data is
/// produced by the portable, already-tested <c>PixiJsSpriteSheetExporter.Export</c>.
/// </para>
/// </summary>
internal static partial class DownloadInterop
{
    private const string ModuleName = "download.js";

    // Same "../" quirk as LocalStorageInterop.InitializeAsync: the WASM runtime resolves this
    // path relative to _framework/, not wwwroot's root.
    public static async Task InitializeAsync() => await JSHost.ImportAsync(ModuleName, "../download.js");

    [JSImport("downloadText", ModuleName)]
    internal static partial void DownloadText(string filename, string text, string mimeType);

    [JSImport("downloadBase64", ModuleName)]
    internal static partial void DownloadBase64(string filename, string base64, string mimeType);
}
