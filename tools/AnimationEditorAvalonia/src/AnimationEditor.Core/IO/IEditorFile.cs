using System;
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

    /// <summary>
    /// Resolves a <c>/</c>- or <c>\</c>-separated path relative to this folder (issue #839's
    /// project-tree thumbnails, which must resolve a frame's <c>TextureName</c> relative to the
    /// <c>.achx</c>'s own folder without a full <see cref="AnimationEditor.Core.ProjectManager"/> load).
    /// <para>
    /// This default implementation only walks <em>downward</em> (same folder or a subfolder) via
    /// <see cref="GetSubfoldersAsync"/>/<see cref="GetFileAsync"/> — it returns <see langword="null"/>
    /// for a <c>..</c> segment, a segment that doesn't match any subfolder, or a leaf file that
    /// doesn't exist. <c>IEditorFolder</c> has no way to reach a parent folder, so a
    /// cross-folder-up <c>../Shared/x.png</c> texture cannot be resolved generically. Desktop's
    /// <c>DiskEditorFolder</c> overrides this with real <c>System.IO</c> resolution, which handles
    /// <c>..</c> correctly.
    /// </para>
    /// </summary>
    async Task<IEditorFile?> ResolveRelativeFileAsync(string relativePath)
    {
        var segments = relativePath.Replace('\\', '/')
            .Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0) return null;
        if (Array.IndexOf(segments, "..") >= 0) return null;

        IEditorFolder current = this;
        for (int i = 0; i < segments.Length - 1; i++)
        {
            IEditorFolder? next = null;
            await foreach (var sub in current.GetSubfoldersAsync())
            {
                if (string.Equals(sub.Name, segments[i], StringComparison.OrdinalIgnoreCase))
                {
                    next = sub;
                    break;
                }
            }
            if (next is null) return null;
            current = next;
        }

        return await current.GetFileAsync(segments[^1]);
    }
}
