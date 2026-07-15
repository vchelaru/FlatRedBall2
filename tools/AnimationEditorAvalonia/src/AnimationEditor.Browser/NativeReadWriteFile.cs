using System;
using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;

namespace AnimationEditor.Browser;

/// <summary>
/// Wraps one file inside a directory handle from the HTML Open Folder button as an
/// <see cref="IEditorFile"/> so it flows through Save/<see cref="BrowserProjectLoader"/> unchanged.
/// </summary>
internal sealed class NativeReadWriteFile : IEditorFile
{
    private readonly JSObject _dirHandle;

    public NativeReadWriteFile(JSObject dirHandle, string name)
    {
        _dirHandle = dirHandle;
        Name = name;
    }

    public string Name { get; }

    /// <summary>Underlying directory handle — used by Save to re-check write permission.</summary>
    public JSObject DirectoryHandle => _dirHandle;

    public async Task<FolderEntrySnapshot> GetBasicPropertiesAsync()
    {
        var (size, modified) = await NativeFolderInterop.GetFileInfoAsync(_dirHandle, Name);
        return new FolderEntrySnapshot(size, modified);
    }

    public async Task<Stream> OpenReadAsync()
    {
        var bytes = await NativeFolderInterop.ReadFileBytesAsync(_dirHandle, Name);
        return new MemoryStream(bytes, writable: false);
    }

    public Task<Stream> OpenWriteAsync() =>
        Task.FromResult<Stream>(new NativeWriteStream(_dirHandle, Name));
}
