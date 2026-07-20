using AnimationEditor.Core.IO;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace AnimationEditor.Core.Tests;

/// <summary>In-memory <see cref="IEditorFile"/> for testing folder-scanning logic without a real
/// filesystem or JS interop.</summary>
internal sealed class FakeEditorFile : IEditorFile
{
    public FakeEditorFile(string name, string content = "") { Name = name; _content = content; }

    private readonly string _content;

    public string Name { get; }

    public Task<Stream> OpenReadAsync() =>
        Task.FromResult<Stream>(new MemoryStream(Encoding.UTF8.GetBytes(_content)));

    public Task<Stream> OpenWriteAsync() => throw new System.NotSupportedException();

    public Task<FolderEntrySnapshot> GetBasicPropertiesAsync() =>
        Task.FromResult(new FolderEntrySnapshot(null, null));
}

/// <summary>In-memory <see cref="IEditorFolder"/> for testing folder-scanning logic without a real
/// filesystem or JS interop. Build a tree with <see cref="Files"/>/<see cref="Subfolders"/>.</summary>
internal sealed class FakeEditorFolder : IEditorFolder
{
    public FakeEditorFolder(string name) => Name = name;

    public string Name { get; }
    public List<FakeEditorFile> Files { get; } = new();
    public List<FakeEditorFolder> Subfolders { get; } = new();

    public async IAsyncEnumerable<IEditorFile> GetItemsAsync()
    {
        foreach (var file in Files)
        {
            await Task.Yield();
            yield return file;
        }
    }

    public Task<IEditorFile?> GetFileAsync(string name)
    {
        foreach (var file in Files)
        {
            if (string.Equals(file.Name, name, System.StringComparison.OrdinalIgnoreCase))
                return Task.FromResult<IEditorFile?>(file);
        }
        return Task.FromResult<IEditorFile?>(null);
    }

    public async IAsyncEnumerable<IEditorFolder> GetSubfoldersAsync()
    {
        foreach (var folder in Subfolders)
        {
            await Task.Yield();
            yield return folder;
        }
    }
}
