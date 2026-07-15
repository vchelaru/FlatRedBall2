using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Text.Json;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;

namespace AnimationEditor.Browser;

/// <summary>
/// Bridges to <c>wwwroot/nativeFolder.js</c>. Forces Avalonia's folder picker to request
/// <c>mode:'readwrite'</c> (via an early <c>index.html</c> patch plus
/// <see cref="PatchAvaloniaStorageProviderAsync"/>), and exposes native-handle helpers for Save.
/// File → Load Folder stays on Avalonia's menu — no separate web-only Open Folder chrome.
/// </summary>
internal static partial class NativeFolderInterop
{
    private const string ModuleName = "nativeFolder.js";

    public static async Task InitializeAsync()
    {
        await JSHost.ImportAsync(ModuleName, "../nativeFolder.js");
        try
        {
            await PatchAvaloniaStorageProviderAsync();
        }
        catch (Exception ex)
        {
            // Must not block Avalonia boot — otherwise the loading spinner never clears.
            // Console.WriteLine (not Debug.WriteLine) because browser-wasm reliably pipes stdout
            // to the DevTools console with no listener setup required; Debug.WriteLine does not
            // -- this exception was previously invisible.
            Console.WriteLine($"[OpenFolder] patchAvaloniaStorageProvider failed: {ex}");
        }
    }

    [JSImport("pickFolderReadWrite", ModuleName)]
    public static partial Task<JSObject?> PickFolderReadWriteAsync();

    [JSImport("patchAvaloniaStorageProvider", ModuleName)]
    private static partial Task PatchAvaloniaStorageProviderAsync();

    [JSImport("bindOpenFolderButton", ModuleName)]
    public static partial void BindOpenFolderButton();

    [JSImport("setFolderPickedHandler", ModuleName)]
    private static partial void SetFolderPickedHandler(
        [JSMarshalAs<JSType.Function<JSType.Object>>] Action<JSObject> handler);

    [JSImport("clickOpenFolderButton", ModuleName)]
    public static partial void ClickOpenFolderButton();

    [JSImport("directoryName", ModuleName)]
    public static partial string DirectoryName(JSObject dirHandle);

    [JSImport("listFileNames", ModuleName)]
    private static partial Task<string> ListFileNamesJsonAsync(JSObject dirHandle);

    [JSImport("fileInfo", ModuleName)]
    private static partial Task<string> FileInfoJsonAsync(JSObject dirHandle, string name);

    [JSImport("readFileBase64", ModuleName)]
    private static partial Task<string> ReadFileBase64JsAsync(JSObject dirHandle, string name);

    [JSImport("writeFileBytes", ModuleName)]
    private static partial Task<string> WriteFileBytesJsAsync(JSObject dirHandle, string name, byte[] bytes);

    [JSImport("ensureReadWrite", ModuleName)]
    private static partial Task<string> EnsureReadWriteJsAsync(JSObject dirHandle);

    [JSImport("queryWritePermission", ModuleName)]
    private static partial Task<string> QueryWritePermissionJsAsync(JSObject dirHandle);

    /// <summary>
    /// Optional: wires the hidden DOM button callback (unused when Avalonia picker is primary).
    /// </summary>
    public static void RegisterFolderPickedHandler(Action<JSObject> handler)
    {
        SetFolderPickedHandler(handler);
        BindOpenFolderButton();
    }

    public static Task<string> EnsureReadWriteAsync(JSObject dirHandle) =>
        EnsureReadWriteJsAsync(dirHandle);

    public static Task<string> QueryWritePermissionAsync(JSObject dirHandle) =>
        QueryWritePermissionJsAsync(dirHandle);

    public static async Task<byte[]> ReadFileBytesAsync(JSObject dirHandle, string name)
    {
        var base64 = await ReadFileBase64JsAsync(dirHandle, name);
        return Convert.FromBase64String(base64);
    }

    public static async Task WriteFileBytesAsync(JSObject dirHandle, string name, byte[] bytes)
    {
        var result = await WriteFileBytesJsAsync(dirHandle, name, bytes);
        if (result == "ok") return;
        throw new IOException($"Native write failed: {result}");
    }

    public static async Task<IReadOnlyList<string>> ListFileNamesAsync(JSObject dirHandle)
    {
        var json = await ListFileNamesJsonAsync(dirHandle);
        // Reflection-based JsonSerializer.Deserialize<string[]>(json) crashes the whole Mono
        // runtime here -- see NativeFolderJsonContext's doc comment.
        return JsonSerializer.Deserialize(json, NativeFolderJsonContext.Default.StringArray) ?? Array.Empty<string>();
    }

    public static async Task<(ulong Size, DateTimeOffset Modified)> GetFileInfoAsync(JSObject dirHandle, string name)
    {
        var json = await FileInfoJsonAsync(dirHandle, name);
        using var doc = JsonDocument.Parse(json);
        var size = (ulong)doc.RootElement.GetProperty("size").GetInt64();
        var lastModifiedMs = doc.RootElement.GetProperty("lastModified").GetInt64();
        return (size, DateTimeOffset.FromUnixTimeMilliseconds(lastModifiedMs));
    }
}
