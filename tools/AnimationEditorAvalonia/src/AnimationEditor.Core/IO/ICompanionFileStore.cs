using System.Threading.Tasks;

namespace AnimationEditor.Core.IO;

/// <summary>
/// A minimal named-file read/write store, abstracting the browser's
/// <c>FileSystemDirectoryHandle</c> (the only platform this is for -- desktop's
/// <see cref="IoManager"/> writes straight to disk instead). Every real operation on a
/// <c>FileSystemDirectoryHandle</c> is Promise-based, so unlike <see cref="ILocalStorage"/> this
/// is async. Kept this narrow so the persistence logic in <see cref="BrowserIoManager"/> is
/// testable with a fake, independent of the actual browser JS interop.
/// </summary>
public interface ICompanionFileStore
{
    /// <summary>Writes <paramref name="contents"/> to <paramref name="fileName"/>, overwriting any existing file.</summary>
    Task WriteAsync(string fileName, string contents);

    /// <summary>Returns the contents of <paramref name="fileName"/>, or <c>null</c> if it doesn't exist.</summary>
    Task<string?> TryReadAsync(string fileName);
}
