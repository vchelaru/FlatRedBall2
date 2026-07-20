using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;

namespace AnimationEditor.Browser;

/// <summary>
/// <see cref="ICompanionFileStore"/> backed by the currently-held <c>FileSystemDirectoryHandle</c>
/// (Open Folder / drag-drop), reusing the same <see cref="NativeFolderInterop"/> read/write calls
/// <see cref="NativeReadWriteFile"/> and <see cref="NativeWriteStream"/> already use for the
/// primary .achx save. Thin, untestable browser wiring (no dedicated test project for this
/// assembly, by established precedent -- see docs/BROWSER_FOLDER_WATCH_DECISION.md); the actual
/// persistence logic it's a conduit for lives in the portable, tested
/// <see cref="AnimationEditor.Core.IO.BrowserIoManager"/>.
/// <para>
/// Not yet wired into <c>App.axaml.cs</c>'s service graph -- constructed against whichever
/// directory handle is current requires the app layer to pass in the folder that was Opened
/// (#754 Phase B).
/// </para>
/// </summary>
internal sealed class NativeFolderCompanionFileStore : ICompanionFileStore
{
    private readonly JSObject _dirHandle;

    public NativeFolderCompanionFileStore(JSObject dirHandle) => _dirHandle = dirHandle;

    public Task WriteAsync(string fileName, string contents) =>
        NativeFolderInterop.WriteFileBytesAsync(_dirHandle, fileName, Encoding.UTF8.GetBytes(contents));

    /// <summary>
    /// Fetches <paramref name="fileName"/> directly via <c>getFileHandle</c> instead of
    /// pre-checking existence through directory enumeration -- enumeration
    /// (<c>dirHandle.entries()</c>) can throw <c>NotFoundError</c> on some environments even
    /// though this exact named lookup keeps working (issue #763). A missing file surfaces the
    /// same <c>NotFoundError</c> from the named lookup itself, which this treats as "doesn't
    /// exist yet" -- the same outcome the old enumeration precheck produced, just without
    /// depending on enumeration succeeding.
    /// </summary>
    public async Task<string?> TryReadAsync(string fileName)
    {
        try
        {
            var bytes = await NativeFolderInterop.ReadFileBytesAsync(_dirHandle, fileName);
            return Encoding.UTF8.GetString(bytes);
        }
        catch (JSException ex) when (ex.Message.Contains("NotFoundError"))
        {
            return null;
        }
    }
}
