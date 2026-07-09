using AnimationEditor.App;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;

namespace AnimationEditor.DocScreenshots;

/// <summary>
/// Per-test service graph for headless doc-screenshot generation. Mirrors
/// <c>AnimationEditor.App.Tests.TestServices</c> — kept separate because that project's
/// <c>UseHeadlessDrawing = true</c> default would make captured screenshots blank (see
/// <see cref="TestAppBuilder"/>).
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

    public string SettingsRoot { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "AnimationEditorDocScreenshots", System.Guid.NewGuid().ToString("N"));

    public TestServices()
    {
        ProjectManager    = new ProjectManager();
        ApplicationEvents = new ApplicationEvents();
        SelectedState     = new SelectedState(ProjectManager);
        AppState          = new AppState(ApplicationEvents, SelectedState);
        IoManager         = new IoManager(AppState);
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
            ThumbnailService, FileAssociationService, SettingsRoot);
}

internal static class TestHelpers
{
    internal static TestServices BuildServices() => new TestServices();
}
