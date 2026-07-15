using System.IO;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;

namespace AnimationEditor.Browser;

/// <summary>
/// Buffers writes in memory and flushes them to the native file handle as one
/// createWritable()/write()/close() round trip on <see cref="DisposeAsync"/> — mirroring the
/// "await using stream" shape every other <c>IStorageFile.OpenWriteAsync()</c> caller
/// (<c>ProjectManager.SaveAnimationChainListAsync</c>) already writes through.
/// </summary>
internal sealed class NativeWriteStream : MemoryStream
{
    private readonly JSObject _dirHandle;
    private readonly string _name;
    private bool _flushedToNative;

    public NativeWriteStream(JSObject dirHandle, string name)
    {
        _dirHandle = dirHandle;
        _name = name;
    }

    public override async ValueTask DisposeAsync()
    {
        if (!_flushedToNative)
        {
            _flushedToNative = true;
            await NativeFolderInterop.WriteFileBytesAsync(_dirHandle, _name, ToArray());
        }

        await base.DisposeAsync();
    }
}
