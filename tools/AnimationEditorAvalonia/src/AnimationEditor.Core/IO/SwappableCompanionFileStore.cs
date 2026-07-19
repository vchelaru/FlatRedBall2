using System.Threading.Tasks;

namespace AnimationEditor.Core.IO;

/// <summary>
/// <see cref="ICompanionFileStore"/> that forwards to whichever folder-backed store is currently
/// active, plugged in via <see cref="Inner"/> once a real directory handle exists (e.g. after
/// Open Folder grants a <c>FileSystemDirectoryHandle</c>). Lets <see cref="BrowserIoManager"/> be
/// constructed once at app startup, before any folder has been opened. Before <see cref="Inner"/>
/// is set -- the bundled sample, or a drag-dropped file with no folder context -- there is nowhere
/// to persist a companion file, so writes/reads are silent no-ops rather than throwing.
/// </summary>
public sealed class SwappableCompanionFileStore : ICompanionFileStore
{
    /// <summary>The real store to forward to, or <c>null</c> when no folder is open.</summary>
    public ICompanionFileStore? Inner { get; set; }

    public Task WriteAsync(string fileName, string contents) =>
        Inner?.WriteAsync(fileName, contents) ?? Task.CompletedTask;

    public Task<string?> TryReadAsync(string fileName) =>
        Inner?.TryReadAsync(fileName) ?? Task.FromResult<string?>(null);
}
