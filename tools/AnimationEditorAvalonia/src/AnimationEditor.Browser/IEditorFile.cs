using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;

namespace AnimationEditor.Browser;

/// <summary>
/// Minimal file abstraction shared by everything downstream of Open Folder/drag-drop
/// (<see cref="BrowserProjectLoader"/>, <see cref="BrowserFolderWatcher"/>, App.axaml.cs's Save
/// handlers) so they don't care whether a file came from Avalonia's storage provider
/// (<see cref="AvaloniaFileAdapter"/>, drag-drop / Save As) or the HTML Open Folder path
/// (<see cref="NativeReadWriteFile"/> — Avalonia has no <c>mode:'readwrite'</c> on folder pick).
/// <para>
/// Avalonia's <c>IStorageFile</c> is a closed interface, so native-handle-backed files need their
/// own type regardless.
/// </para>
/// </summary>
internal interface IEditorFile
{
    string Name { get; }
    Task<Stream> OpenReadAsync();
    Task<Stream> OpenWriteAsync();
    Task<FolderEntrySnapshot> GetBasicPropertiesAsync();
}

/// <summary>Folder counterpart of <see cref="IEditorFile"/>; only Open Folder produces one (drag-drop
/// hands over loose files with no enclosing folder handle).</summary>
internal interface IEditorFolder
{
    string Name { get; }
    IAsyncEnumerable<IEditorFile> GetItemsAsync();
    Task<IEditorFile?> GetFileAsync(string name);
}
