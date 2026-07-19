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

    public async Task<string?> TryReadAsync(string fileName)
    {
        var names = await NativeFolderInterop.ListFileNamesAsync(_dirHandle);
        var found = false;
        foreach (var name in names)
        {
            if (string.Equals(name, fileName, System.StringComparison.OrdinalIgnoreCase))
            {
                found = true;
                break;
            }
        }
        if (!found) return null;

        var bytes = await NativeFolderInterop.ReadFileBytesAsync(_dirHandle, fileName);
        return Encoding.UTF8.GetString(bytes);
    }
}
