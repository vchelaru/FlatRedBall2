using System;

namespace FlatRedBall2.Content;

/// <summary>
/// Abstraction over a single-file change source. Implementations notify subscribers when the
/// underlying file changes; the event may fire on any thread. Used by <see cref="ContentWatcher"/>
/// so the debounce / dispatch logic is testable without touching the real filesystem.
/// </summary>
public interface IFileWatcher : IDisposable
{
    /// <summary>Raised when the watched file is reported changed. May fire on any thread.</summary>
    event Action Changed;
}
