using System;
using System.IO;
using System.Threading.Tasks;

namespace FlatRedBall2.IO;

/// <summary>
/// The stream <see cref="GameStorage.OpenWriteAsync"/> returns: buffers in memory and hands the
/// bytes to the backend in one write when disposed asynchronously.
/// </summary>
internal sealed class StorageWriteStream : MemoryStream
{
    private readonly Func<ReadOnlyMemory<byte>, Task> _commit;
    private bool _finished;

    internal StorageWriteStream(Func<ReadOnlyMemory<byte>, Task> commit)
    {
        _commit = commit;
    }

    public override async ValueTask DisposeAsync()
    {
        if (_finished)
            return;
        _finished = true;
        try
        {
            await _commit(new ReadOnlyMemory<byte>(GetBuffer(), 0, (int)Length)).ConfigureAwait(false);
        }
        finally
        {
            base.Dispose(true);
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (_finished)
        {
            base.Dispose(disposing);
            return;
        }

        _finished = true;
        base.Dispose(disposing);
        // A finalizer must not throw; only a caller's own Dispose() does.
        if (disposing)
        {
            throw new InvalidOperationException(
                "A storage write stream can only be committed with 'await using' or DisposeAsync(). " +
                "Disposing it synchronously discards the data.");
        }
    }
}
