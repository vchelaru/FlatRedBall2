using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Minimal file abstraction shared by everything downstream of Open Folder/drag-drop so callers
/// don't care whether a file came from Avalonia's storage provider (desktop, or the browser's
/// Avalonia-backed paths), the browser's native-handle Open Folder path (no <c>mode:'readwrite'</c>
/// on Avalonia's folder pick there), or the desktop filesystem directly.
/// <para>
/// Public (not <c>internal</c>) so both <c>AnimationEditor.App</c> (desktop) and
/// <c>AnimationEditor.Browser</c> (web) can implement/consume it — Core's <c>InternalsVisibleTo</c>
/// does not cover either assembly (see <see cref="NativeFolderJsonContext"/> for the same reasoning).
/// </para>
/// </summary>
public interface IEditorFile
{
    string Name { get; }
    Task<Stream> OpenReadAsync();
    Task<Stream> OpenWriteAsync();
    Task<FolderEntrySnapshot> GetBasicPropertiesAsync();
}

/// <summary>Folder counterpart of <see cref="IEditorFile"/>. Desktop can produce one for any
/// directory; on the web only Open Folder produces one (drag-drop hands over loose files with no
/// enclosing folder handle).</summary>
public interface IEditorFolder
{
    string Name { get; }

    /// <summary>Files directly inside this folder — never recurses into subfolders. Use
    /// <see cref="GetSubfoldersAsync"/> to walk deeper (see <c>AchxFolderScanner</c> for the
    /// recursive discovery this splits enable).</summary>
    IAsyncEnumerable<IEditorFile> GetItemsAsync();

    Task<IEditorFile?> GetFileAsync(string name);

    /// <summary>Subfolders directly inside this folder — never recurses.</summary>
    IAsyncEnumerable<IEditorFolder> GetSubfoldersAsync();
}
