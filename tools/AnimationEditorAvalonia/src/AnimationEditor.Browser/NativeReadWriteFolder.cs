using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace AnimationEditor.Browser;

/// <summary>
/// Wraps a native <c>FileSystemDirectoryHandle</c> from the HTML Open Folder path as an
/// <see cref="IEditorFolder"/> for loading, folder watching, and reload.
/// </summary>
internal sealed class NativeReadWriteFolder : IEditorFolder
{
    private readonly JSObject _dirHandle;

    public NativeReadWriteFolder(JSObject dirHandle) => _dirHandle = dirHandle;

    public string Name => NativeFolderInterop.DirectoryName(_dirHandle);

    public async IAsyncEnumerable<IEditorFile> GetItemsAsync()
    {
        foreach (var name in await NativeFolderInterop.ListFileNamesAsync(_dirHandle))
            yield return new NativeReadWriteFile(_dirHandle, name);
    }

    public async Task<IEditorFile?> GetFileAsync(string name)
    {
        var names = await NativeFolderInterop.ListFileNamesAsync(_dirHandle);
        foreach (var candidate in names)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
                return new NativeReadWriteFile(_dirHandle, candidate);
        }

        return null;
    }
}
