using System;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace AnimationEditor.Browser;

/// <summary>
/// Requests "readwrite" File System Access permission on a drag-drop
/// <see cref="IStorageItem"/> (JSImport bridge to wwwroot/storagePermission.js). Open Folder
/// uses the HTML button + <see cref="NativeFolderInterop"/> instead (Avalonia's folder picker
/// cannot pass <c>mode:'readwrite'</c>). Must be initialized via <see cref="InitializeAsync"/>
/// before any <see cref="EnsureReadWriteAsync(IStorageItem)"/> call.
/// </summary>
internal static partial class StoragePermissionInterop
{
    private const string ModuleName = "storagePermission.js";

    // Same "../" quirk as LocalStorageInterop.InitializeAsync/DownloadInterop.InitializeAsync:
    // the WASM runtime resolves this path relative to _framework/, not wwwroot's root.
    public static async Task InitializeAsync() => await JSHost.ImportAsync(ModuleName, "../storagePermission.js");

    [JSImport("ensureReadWrite", ModuleName)]
    private static partial Task<string> EnsureReadWriteJsAsync(JSObject handle);

    /// <summary>
    /// Requests readwrite permission on <paramref name="item"/>'s underlying File System Access
    /// handle. Used after drag-drop (no picker <c>mode</c> option). Never throws.
    /// </summary>
    public static async Task<(bool Granted, string Diagnostic)> EnsureReadWriteAsync(IStorageItem item)
    {
        JSObject? handle;
        try
        {
            handle = TryGetNativeFileSystemHandle(item);
        }
        catch (Exception ex)
        {
            return (false, $"reflection failed: {ex.GetType().Name}: {ex.Message}");
        }

        if (handle is null) return (false, "no FileHandle found on this IStorageItem");

        try
        {
            var result = await EnsureReadWriteJsAsync(handle);
            return (result == "granted", result);
        }
        catch (JSException ex)
        {
            return (false, $"JS call failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Returns the browser-native <c>FileSystemFileHandle</c>/<c>FileSystemDirectoryHandle</c>
    /// behind an Avalonia <see cref="IStorageItem"/>, unwrapping Avalonia's JS
    /// <c>StorageItem</c> wrapper when present.
    /// </summary>
    public static JSObject? TryGetNativeFileSystemHandle(IStorageItem item)
    {
        JSObject? avaloniaOrNative;
        try
        {
            avaloniaOrNative = TryGetFileSystemHandle(item);
        }
        catch
        {
            return null;
        }

        if (avaloniaOrNative is null) return null;

        try
        {
            if (avaloniaOrNative.HasProperty("handle"))
            {
                var native = avaloniaOrNative.GetPropertyAsJSObject("handle");
                if (native is not null) return native;
            }
        }
        catch (JSException)
        {
        }

        return avaloniaOrNative;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2075", Justification =
        "Safe no-op fallback if trimmed: null return degrades to 'permission not granted', " +
        "which callers already handle by falling back to Save As.")]
    private static JSObject? TryGetFileSystemHandle(IStorageItem item)
    {
        for (var type = item.GetType(); type is not null; type = type.BaseType)
        {
            var prop = type.GetProperty(
                "FileHandle",
                BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (prop is not null)
                return prop.GetValue(item) as JSObject;
        }
        return null;
    }
}
