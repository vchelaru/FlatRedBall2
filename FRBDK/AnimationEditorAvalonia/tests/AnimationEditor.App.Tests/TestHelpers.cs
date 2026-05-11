using AnimationEditor.App.Controls;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;

namespace AnimationEditor.App.Tests;

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

    public TestServices()
    {
        ProjectManager    = new ProjectManager();
        ApplicationEvents = new ApplicationEvents();
        SelectedState     = new SelectedState(ProjectManager);
        AppState          = new AppState(ApplicationEvents, SelectedState);
        IoManager         = new IoManager(AppState);
        ObjectFinder      = new ObjectFinder(ProjectManager);
        UndoManager       = new UndoManager();
        AppCommands       = new AppCommands(ProjectManager, SelectedState, ApplicationEvents,
                                            IoManager, ObjectFinder, UndoManager);
    }

    public MainWindow CreateMainWindow() =>
        new MainWindow(
            ProjectManager, SelectedState, AppCommands, AppState,
            ApplicationEvents, IoManager, ObjectFinder, UndoManager);

    public WireframeControl CreateWireframeControl()
    {
        var ctrl = new WireframeControl();
        ctrl.InitializeServices(SelectedState, AppState, AppCommands, ApplicationEvents, ProjectManager, UndoManager);
        return ctrl;
    }

    public PreviewControl CreatePreviewControl()
    {
        var ctrl = new PreviewControl();
        ctrl.InitializeServices(SelectedState, AppState, AppCommands, ApplicationEvents, ProjectManager);
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
