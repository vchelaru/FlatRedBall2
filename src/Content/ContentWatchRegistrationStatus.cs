namespace FlatRedBall2.Content;

/// <summary>
/// Status result of a content watch registration attempt via <see cref="Screen.TryWatchContentDirectory"/>.
/// </summary>
public enum ContentWatchRegistrationStatus
{
    /// <summary>
    /// Content watch registration succeeded. The directory is being monitored for changes.
    /// </summary>
    Registered,

    /// <summary>
    /// Content watch registration did not happen because no entry in
    /// <see cref="FlatRedBallService.SourceContentRoots"/> contains the requested path. That is
    /// either because the list is empty - typical in shipping builds, where content is pre-built
    /// and hot-reload is off - or because the detected roots exist but none of them contains that
    /// directory. The second case is reachable in a shipping build too, because
    /// <see cref="FlatRedBallService.DetectSourceContentRoots"/> searches upward from the
    /// executable for a solution or project file and can settle on an unrelated ancestor project.
    /// Hot-reload is disabled either way; no watcher is created.
    /// </summary>
    SourceContentRootUnavailable,
}
