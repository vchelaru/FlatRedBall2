using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;

namespace AnimationEditor.Browser;

/// <summary>
/// JSImport bridge to <c>window.localStorage</c> (wwwroot/localStorage.js). Must be initialized
/// via <see cref="InitializeAsync"/> before any <see cref="ILocalStorage"/> call -- done once in
/// <see cref="Program.Main"/>, alongside the existing bundled-sample fetch. This class is thin,
/// untestable browser wiring (same category as the rest of AnimationEditor.Browser -- no
/// dedicated test project, see docs/BROWSER_FOLDER_WATCH_DECISION.md); the actual persistence
/// logic it's a conduit for lives in the portable, tested <see cref="BrowserSettingsStore"/>.
/// </summary>
internal static partial class LocalStorageInterop
{
    private const string ModuleName = "localStorage.js";

    // The WASM runtime resolves this path relative to _framework/ (where dotnet.runtime.js
    // itself lives), not wwwroot's root -- confirmed by a live browser load that failed with
    // "Failed to fetch dynamically imported module: .../_framework/localStorage.js" when this
    // was "./localStorage.js". One level up reaches wwwroot/localStorage.js.
    public static async Task InitializeAsync() => await JSHost.ImportAsync(ModuleName, "../localStorage.js");

    [JSImport("getItem", ModuleName)]
    internal static partial string? GetItem(string key);

    [JSImport("setItem", ModuleName)]
    internal static partial void SetItem(string key, string value);
}

/// <summary><see cref="ILocalStorage"/> backed by the real browser <see cref="LocalStorageInterop"/>.</summary>
internal sealed class JsLocalStorage : ILocalStorage
{
    public string? GetItem(string key) => LocalStorageInterop.GetItem(key);
    public void SetItem(string key, string value) => LocalStorageInterop.SetItem(key, value);
}
