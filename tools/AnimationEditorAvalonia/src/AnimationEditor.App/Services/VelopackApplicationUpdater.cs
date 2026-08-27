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

internal static class ApplicationUpdateSource
{
    internal const string Production = "https://github.com/vchelaru/FlatRedBall2";

    private const string TestUpdateSourceEnvironmentVariable = "ANIMATION_EDITOR_TEST_UPDATE_SOURCE";

    internal static string ForCurrentBuild()
    {
        // A local feed can validate the installed-app path without publishing a release. This is
        // deliberately compiled out of Release builds so a shipped app has one trusted source.
#if DEBUG
        return Resolve(Environment.GetEnvironmentVariable(TestUpdateSourceEnvironmentVariable), isTestBuild: true);
#else
        return Resolve(testSource: null, isTestBuild: false);
#endif
    }

    internal static string Resolve(string? testSource, bool isTestBuild)
    {
        if (isTestBuild && !string.IsNullOrWhiteSpace(testSource))
            return testSource;

        return Production;
    }
}

internal sealed class VelopackApplicationUpdater : IApplicationUpdater
{
    private readonly string _updateSource;
    private UpdateManager? _updateManager;
    private UpdateInfo? _downloadedUpdate;

    public VelopackApplicationUpdater()
        : this(ApplicationUpdateSource.ForCurrentBuild())
    {
    }

    internal VelopackApplicationUpdater(string updateSource)
    {
        _updateSource = updateSource;
    }

    public async Task<ApplicationUpdateResult> DownloadUpdateAsync(
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var updateManager = CreateUpdateManager();

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

    private UpdateManager CreateUpdateManager()
    {
        if (_updateSource.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase))
            return new UpdateManager(new GithubSource(_updateSource, accessToken: null, prerelease: false));

        return new UpdateManager(_updateSource);
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
