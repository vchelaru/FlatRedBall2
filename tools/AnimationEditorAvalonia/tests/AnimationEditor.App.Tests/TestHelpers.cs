using AnimationEditor.App.Controls;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Update;

namespace AnimationEditor.App.Tests;

/// <summary>
/// No-op <see cref="IUpdateChecker"/> for tests that don't care about the update check — never
/// hits the network. Tests exercising the update-check wiring itself set
/// <see cref="TestServices.UpdateChecker"/> to a fake with a canned <see cref="UpdateCheckResult"/>
/// before calling <see cref="TestServices.CreateMainWindow"/>.
/// </summary>
internal sealed class FakeUpdateChecker : IUpdateChecker
{
    public UpdateCheckResult Result { get; set; } = UpdateCheckResult.NoUpdate;
    public int CallCount { get; private set; }

    public Task<UpdateCheckResult> CheckAsync(Version currentVersion, CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(Result);
    }
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
    public IFileAssociationService FileAssociationService { get; } = new NullFileAssociationService();
    public IUpdateChecker UpdateChecker { get; set; } = new FakeUpdateChecker();

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
            ThumbnailService, FileAssociationService, UpdateChecker, SettingsRoot);

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
