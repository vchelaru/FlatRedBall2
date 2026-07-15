using System.IO;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;
using Avalonia.Platform.Storage;

namespace AnimationEditor.Browser;

/// <summary>
/// Adapts an Avalonia <see cref="IStorageFile"/> (from drag-drop, or a Save As pick) to
/// <see cref="IEditorFile"/> so it can flow through the same Save/<see cref="BrowserProjectLoader"/>
/// code as an Open-Folder-sourced <see cref="NativeReadWriteFile"/>.
/// </summary>
internal sealed class AvaloniaFileAdapter : IEditorFile
{
    private readonly IStorageFile _file;

    public AvaloniaFileAdapter(IStorageFile file) => _file = file;

    public string Name => _file.Name;
    public Task<Stream> OpenReadAsync() => _file.OpenReadAsync();
    public Task<Stream> OpenWriteAsync() => _file.OpenWriteAsync();

    public async Task<FolderEntrySnapshot> GetBasicPropertiesAsync()
    {
        var props = await _file.GetBasicPropertiesAsync();
        return new FolderEntrySnapshot(props.Size, props.DateModified);
    }
}
