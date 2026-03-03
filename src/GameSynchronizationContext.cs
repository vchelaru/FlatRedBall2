using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FlatRedBall2;

/// <summary>
/// Routes async continuations back onto the game's main thread so that
/// <c>async/await</c> in game code executes within the normal Update loop.
/// <para>
/// Set as the active <see cref="SynchronizationContext"/> during
/// <see cref="FlatRedBallService.Initialize"/>. Call <see cref="Update"/>
/// once per frame (after task completion logic, before screen activity).
/// Call <see cref="Clear"/> on screen transition to discard stale continuations.
/// </para>
/// </summary>
internal sealed class GameSynchronizationContext : SynchronizationContext
{
    // Continuations posted from any thread land here.
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _pending = new();
    private readonly object _lock = new();

    // Reused each frame to avoid per-frame allocation.
    private readonly Queue<(SendOrPostCallback Callback, object? State)> _thisFrame = new();

    /// <summary>
    /// Synchronous dispatch is fundamentally incompatible with a single-threaded game loop:
    /// it would deadlock waiting for a frame that can't run. Use <c>Post</c> or <c>await</c> instead.
    /// </summary>
    public override void Send(SendOrPostCallback d, object? state) =>
        throw new NotSupportedException(
            "Synchronous Send on the game thread sync context would deadlock. Use Post or await instead.");

    /// <summary>Queues a continuation to run on the game thread during the next <see cref="Update"/>.</summary>
    public override void Post(SendOrPostCallback d, object? state)
    {
        lock (_lock)
            _pending.Enqueue((d, state));
    }

    /// <summary>
    /// Runs all continuations that were queued since the last call.
    /// Continuations posted <em>during</em> this call are deferred to the next frame.
    /// </summary>
    internal void Update()
    {
        // Drain _pending into _thisFrame under the lock so new Posts during
        // execution land in _pending and are picked up next frame.
        lock (_lock)
        {
            while (_pending.Count > 0)
                _thisFrame.Enqueue(_pending.Dequeue());
        }

        while (_thisFrame.Count > 0)
        {
            var (callback, state) = _thisFrame.Dequeue();
            try
            {
                callback(state);
            }
            catch (TaskCanceledException)
            {
                // Screen switched mid-task — nothing to do.
            }
        }
    }

    /// <summary>
    /// Discards all pending continuations. Called on screen transition so stale
    /// async continuations from the old screen never run on the new one.
    /// </summary>
    internal void Clear()
    {
        lock (_lock)
            _pending.Clear();
    }
}
