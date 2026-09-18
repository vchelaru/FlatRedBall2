using AnimationEditor.App.Controls;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;

namespace AnimationEditor.App.Tests;

internal sealed class FakeApplicationUpdater : IApplicationUpdater
{
    public ApplicationUpdateResult Result { get; set; } = ApplicationUpdateResult.NoUpdate;
    public int DownloadCount { get; private set; }
    public int RestartCount { get; private set; }
    public int? ProgressToReport { get; set; }
    public TaskCompletionSource<ApplicationUpdateResult>? PendingResult { get; set; }

    public Task<ApplicationUpdateResult> DownloadUpdateAsync(
        Action<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        DownloadCount++;
        if (ProgressToReport is not null)
            progress?.Invoke(ProgressToReport.Value);

        if (PendingResult is not null)
            return PendingResult.Task;

        return Task.FromResult(Result);
    }

    public void ApplyUpdateAndRestart() => RestartCount++;
}

internal sealed class FakeTiledExtensionInstaller : ITiledExtensionInstaller
{
    public string? DetectedFolder { get; set; }
    public TiledExtensionInstallStatus Status { get; set; } = TiledExtensionInstallStatus.NotInstalled;
    public string? InstallError { get; set; }
    public string? ValidateError { get; set; }
    public int InstallCount { get; private set; }
    public string? LastInstalledFolder { get; private set; }

    public string? DetectExtensionsFolder() => DetectedFolder;

    public TiledExtensionInstallStatus GetStatus(string extensionsFolder) => Status;

    public string? Install(string extensionsFolder)
    {
        InstallCount++;
        LastInstalledFolder = extensionsFolder;
        return InstallError;
    }

    public string? ValidateFolder(string folderPath) => ValidateError;
}

/// <summary>
/// Per-test service graph for headless App tests. Each call builds a brand-new
/// set of services — no static state. Use <see cref="CreateMainWindow"/> to get
/// a wired <see cref="MainWindow"/> backed by these services.
/// </summary>
internal sealed class TestServices
{
    public ProjectManager ProjectManager { get; }
    public ApplicationEvents ApplicationEvents { get; }
    public SelectedState SelectedState { get; }
    public AppState AppState { get; }
    public IoManager IoManager { get; }
    public ObjectFinder ObjectFinder { get; }
    public UndoManager UndoManager { get; }
    public AppCommands AppCommands { get; }
    public PendingCutState PendingCutState { get; }
    public ThumbnailService ThumbnailService { get; }
    // null diskCacheDirectory: tests never need thumbnails backed by a real per-user cache dir
    // (same rationale as SettingsRoot's isolation -- see class doc).
    public ProjectTreeThumbnailService ProjectTreeThumbnailService { get; } = new(diskCacheDirectory: null);
    public IFileAssociationService FileAssociationService { get; set; } = new NullFileAssociationService();
    public IApplicationUpdater ApplicationUpdater { get; set; } = new FakeApplicationUpdater();
    public FakeTiledExtensionInstaller TiledExtensionInstaller { get; set; } = new();

    /// <summary>
    /// Unique-per-instance temp application-data root. Injected into the <see cref="MainWindow"/>
    /// so its settings file resolves under here instead of the developer's real %APPDATA%
    /// (issue #438). A fresh Guid also isolates tests from one another.
    /// </summary>
    public string SettingsRoot { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnimationEditorTests", System.Guid.NewGuid().ToString("N"));

    public TestServices()
    {
        ProjectManager    = new ProjectManager();
        ApplicationEvents = new ApplicationEvents();
        SelectedState     = new SelectedState(ProjectManager);
        AppState          = new AppState(ApplicationEvents, SelectedState);
        IoManager         = new IoManager(AppState);
        // Each instance gets its own recovery path so concurrent test classes never race on
        // the shared default temp file (see issue #703) — critical now that MainWindow's
        // startup path (OnOpened) calls RecoveryFileExists() on every window it creates.
        IoManager.RecoveryFilePath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), "AnimationEditorAppTests", $"recovery_{System.Guid.NewGuid():N}.achx");
        ObjectFinder      = new ObjectFinder(ProjectManager);
        UndoManager       = new UndoManager();
        PendingCutState   = new PendingCutState();
        AppCommands       = new AppCommands(ProjectManager, SelectedState, ApplicationEvents,
                                            IoManager, ObjectFinder, UndoManager);
        ThumbnailService  = new ThumbnailService(ProjectManager);
    }

    public MainWindow CreateMainWindow() =>
        new MainWindow(
            ProjectManager, SelectedState, AppCommands, AppState,
            ApplicationEvents, IoManager, ObjectFinder, UndoManager, PendingCutState,
            ThumbnailService, ProjectTreeThumbnailService, FileAssociationService, SettingsRoot,
            ApplicationUpdater, TiledExtensionInstaller);

    public WireframeControl CreateWireframeControl(System.Action<string>? showError = null)
    {
        var ctrl = new WireframeControl();
        ctrl.InitializeServices(SelectedState, AppState, AppCommands, ApplicationEvents, ProjectManager, UndoManager, PendingCutState, ObjectFinder, showError);
        return ctrl;
    }

    public PreviewControl CreatePreviewControl(System.Action<string>? showError = null)
    {
        var ctrl = new PreviewControl();
        ctrl.InitializeServices(SelectedState, AppState, AppCommands, ApplicationEvents, ProjectManager, UndoManager, ThumbnailService, PendingCutState, showError);
        return ctrl;
    }
}

internal static class TestHelpers
{
    /// <summary>
    /// Builds a fresh service graph for a test. No global state — services are
    /// addressed directly through the returned context.
    /// </summary>
    internal static TestServices BuildServices() => new TestServices();
}
