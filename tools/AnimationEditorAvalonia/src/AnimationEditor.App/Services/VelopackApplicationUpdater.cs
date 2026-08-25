using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Velopack;
using Velopack.Sources;

namespace AnimationEditor.App.Services;

internal interface IApplicationUpdater
{
    Task<ApplicationUpdateResult> DownloadUpdateAsync(
        Action<int>? progress = null,
        CancellationToken cancellationToken = default);

    void ApplyUpdateAndRestart();
}

internal sealed record ApplicationUpdateResult(
    ApplicationUpdateStatus Status,
    string? Version = null,
    string? FailureMessage = null)
{
    public static readonly ApplicationUpdateResult NoUpdate = new(ApplicationUpdateStatus.NoUpdate);

    public static ApplicationUpdateResult Failed(string message) =>
        new(ApplicationUpdateStatus.Failed, FailureMessage: message);

    public static ApplicationUpdateResult ReadyToRestart(Version version) =>
        new(ApplicationUpdateStatus.ReadyToRestart, version.ToString());
}

internal enum ApplicationUpdateStatus
{
    NoUpdate,
    ReadyToRestart,
    Failed,
}

internal sealed class VelopackApplicationUpdater : IApplicationUpdater
{
    private const string RepositoryUrl = "https://github.com/vchelaru/FlatRedBall2";

    private UpdateManager? _updateManager;
    private UpdateInfo? _downloadedUpdate;

    public async Task<ApplicationUpdateResult> DownloadUpdateAsync(
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var updateManager = new UpdateManager(new GithubSource(RepositoryUrl, accessToken: null, prerelease: false));

        // Local development builds and the legacy archive distribution are not managed installs.
        // Their normal behavior is unchanged: they never attempt to overwrite themselves.
        if (updateManager.CurrentVersion is null)
            return ApplicationUpdateResult.NoUpdate;

        try
        {
            var update = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);
            if (update is null)
                return ApplicationUpdateResult.NoUpdate;

            await updateManager.DownloadUpdatesAsync(update, progress, cancellationToken).ConfigureAwait(false);
            _updateManager = updateManager;
            _downloadedUpdate = update;
            return new ApplicationUpdateResult(
                ApplicationUpdateStatus.ReadyToRestart,
                update.TargetFullRelease.Version.ToString());
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Animation Editor update download failed: {ex}");
            return ApplicationUpdateResult.Failed("The update could not be downloaded. Please try again.");
        }
    }

    public void ApplyUpdateAndRestart()
    {
        if (_updateManager is null || _downloadedUpdate is null)
            return;

        _updateManager.ApplyUpdatesAndRestart(_downloadedUpdate);
    }
}

internal sealed class NoOpApplicationUpdater : IApplicationUpdater
{
    public Task<ApplicationUpdateResult> DownloadUpdateAsync(
        Action<int>? progress = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(ApplicationUpdateResult.NoUpdate);

    public void ApplyUpdateAndRestart()
    {
    }
}
