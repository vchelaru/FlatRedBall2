using System.Threading;

namespace AnimationEditor.App.Controls;

/// <summary>
/// Cross-thread mailbox for Skia's GPU resource-cache size (issue #949). The
/// <c>GRContext</c> only exists inside a render-thread lease, so the render path drops the
/// reading here and the UI thread (the memory probe) picks it up. Deliberately lossy: the
/// probe tolerates a reading that is a frame or two stale.
/// </summary>
internal sealed class GpuCacheReadings
{
    private long _usedBytes = -1;
    private long _limitBytes = -1;

    /// <summary>Bytes currently held by the GPU resource cache; -1 if never sampled (software path).</summary>
    public long UsedBytes => Volatile.Read(ref _usedBytes);

    /// <summary>The cache's byte budget; -1 if never sampled.</summary>
    public long LimitBytes => Volatile.Read(ref _limitBytes);

    public void Report(long usedBytes, long limitBytes)
    {
        Volatile.Write(ref _usedBytes, usedBytes);
        Volatile.Write(ref _limitBytes, limitBytes);
    }
}
