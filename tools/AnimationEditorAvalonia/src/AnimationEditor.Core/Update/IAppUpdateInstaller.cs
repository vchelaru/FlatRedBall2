namespace AnimationEditor.Core.Update;

/// <summary>
/// Downloads and installs an update in place, then restarts the app. <see cref="IsSupported"/>
/// gates auto-update to platforms with a real implementation (Windows only for now — see
/// <see cref="WindowsAppUpdateInstaller"/>); callers fall back to opening the release page
/// on other platforms.
/// </summary>
public interface IAppUpdateInstaller
{
    bool IsSupported { get; }

    /// <summary>
    /// On success, this never returns to the caller — the real implementation exits the
    /// process once the swap-and-relaunch helper is queued up, since the running exe has to
    /// close before it can be overwritten.
    /// </summary>
    Task InstallAndRestartAsync(string downloadUrl, CancellationToken cancellationToken = default);
}
