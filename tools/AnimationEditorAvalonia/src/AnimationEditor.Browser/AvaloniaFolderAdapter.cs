using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace AnimationEditor.Browser;

/// <summary>
/// Adapts an Avalonia <see cref="IStorageFolder"/> to <see cref="IEditorFolder"/>. Used when a
/// folder handle comes from Avalonia (not the HTML Open Folder path, which uses
/// <see cref="NativeReadWriteFolder"/>).
/// </summary>
internal sealed class AvaloniaFolderAdapter : IEditorFolder
{
    private readonly IStorageFolder _folder;

    public AvaloniaFolderAdapter(IStorageFolder folder) => _folder = folder;

    public string Name => _folder.Name;

    public async IAsyncEnumerable<IEditorFile> GetItemsAsync()
    {
        await foreach (var item in _folder.GetItemsAsync())
        {
            if (item is IStorageFile file)
                yield return new AvaloniaFileAdapter(file);
        }
    }

    public async Task<IEditorFile?> GetFileAsync(string name)
    {
        var file = await _folder.GetFileAsync(name);
        return file is null ? null : new AvaloniaFileAdapter(file);
    }
}
