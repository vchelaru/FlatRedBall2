using System;

namespace FlatRedBall2.Content;

/// <summary>
/// Abstraction over a directory-recursive change source. Each event carries the path of the
/// changed file relative to the watched root. Used by <see cref="ContentDirectoryWatcher"/> so
/// the debounce / batch logic is testable without touching the real filesystem.
/// </summary>
public interface IDirectoryWatcher : IDisposable
{
    /// <summary>
    /// Raised when a file under the watched directory is reported changed. Argument is the file's
    /// path relative to the watched root (e.g. <c>"Configs/player.json"</c>). May fire on any thread.
    /// </summary>
    event Action<string> Changed;
}
