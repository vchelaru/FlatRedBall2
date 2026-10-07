using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Minimal file abstraction shared by everything downstream of Open Folder/drag-drop, so callers
/// don't care whether a file came from Avalonia's storage provider or the filesystem directly.
/// </summary>
public interface IEditorFile
{
    string Name { get; }
    Task<Stream> OpenReadAsync();
    Task<Stream> OpenWriteAsync();
    Task<FolderEntrySnapshot> GetBasicPropertiesAsync();
}

/// <summary>Folder counterpart of <see cref="IEditorFile"/>.</summary>
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

    /// <summary>
    /// Resolves a <c>/</c>- or <c>\</c>-separated path relative to this folder (issue #839's
    /// project-tree thumbnails, which must resolve a frame's <c>TextureName</c> relative to the
    /// <c>.achx</c>'s own folder without a full <see cref="AnimationEditor.Core.ProjectManager"/> load).
    /// Desktop's <c>DiskEditorFolder</c> resolves this with real <c>System.IO</c>, so <c>..</c>
    /// segments work.
    /// </summary>
    Task<IEditorFile?> ResolveRelativeFileAsync(string relativePath);
}
