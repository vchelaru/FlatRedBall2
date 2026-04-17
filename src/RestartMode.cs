namespace FlatRedBall2;

/// <summary>
/// Mode passed to <see cref="Screen.RestartScreen(RestartMode)"/> to choose how the engine
/// preserves (or discards) game state across the restart.
/// </summary>
public enum RestartMode
{
    /// <summary>
    /// Death/retry restart. Fresh state — no Save/Restore hooks fire. The new screen instance
    /// only sees what the retained configure callback sets and what <c>CustomInitialize</c> builds.
    /// This is the default and the path you want bulletproof.
    /// </summary>
    DeathRetry,

    /// <summary>
    /// Hot-reload restart. The engine calls <see cref="Screen.SaveHotReloadState"/> on the old
    /// instance before teardown, then <see cref="Screen.RestoreHotReloadState"/> on the new
    /// instance after <c>CustomInitialize</c>. Use this when a content file changed on disk
    /// and you want the player roughly where they were so iteration doesn't feel jarring.
    /// </summary>
    HotReload,
}
