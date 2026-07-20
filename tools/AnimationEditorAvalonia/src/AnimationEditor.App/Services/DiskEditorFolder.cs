using AnimationEditor.Core.IO;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.App.Services;

/// <summary>
/// <c>System.IO</c>-backed <see cref="IEditorFolder"/> for desktop's Open Project Folder (#770) --
/// a real filesystem, so (unlike the browser's native-handle adapter) there's no permission or
/// enumeration failure to guard against.
/// </summary>
public sealed class DiskEditorFolder : IEditorFolder
{
    private readonly string _path;

    public DiskEditorFolder(string path) => _path = path;

    public string Name => new FilePath(_path).NoPath;

    public async IAsyncEnumerable<IEditorFile> GetItemsAsync()
    {
        foreach (var file in Directory.EnumerateFiles(_path))
        {
            await Task.Yield();
            yield return new DiskEditorFile(file);
        }
    }

    public async IAsyncEnumerable<IEditorFolder> GetSubfoldersAsync()
    {
        foreach (var dir in Directory.EnumerateDirectories(_path))
        {
            await Task.Yield();
            yield return new DiskEditorFolder(dir);
        }
    }

    public Task<IEditorFile?> GetFileAsync(string name)
    {
        var full = Path.Combine(_path, name);
        return Task.FromResult<IEditorFile?>(File.Exists(full) ? new DiskEditorFile(full) : null);
    }
}

/// <summary>
/// <c>System.IO</c>-backed <see cref="IEditorFile"/> counterpart of <see cref="DiskEditorFolder"/>.
/// </summary>
public sealed class DiskEditorFile : IEditorFile
{
    public DiskEditorFile(string path) => FullPath = path;

    /// <summary>Real on-disk path -- used by the Project tab's click-to-open handler to call
    /// <c>MainWindow.OpenFileAsTab</c> directly rather than going through the stream abstraction.</summary>
    public string FullPath { get; }

    public string Name => new FilePath(FullPath).NoPath;

    public Task<Stream> OpenReadAsync() => Task.FromResult<Stream>(File.OpenRead(FullPath));

    public Task<Stream> OpenWriteAsync() => Task.FromResult<Stream>(File.Create(FullPath));

    public Task<FolderEntrySnapshot> GetBasicPropertiesAsync()
    {
        var info = new FileInfo(FullPath);
        return Task.FromResult(new FolderEntrySnapshot((ulong)info.Length, info.LastWriteTimeUtc));
    }
}
