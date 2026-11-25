using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;

namespace AnimationEditor.Editing.Tests;

/// <summary>
/// Headless service graph for Editing tests: the same wiring the editor uses, with a unique temp
/// <see cref="Root"/> deleted on dispose. Assign <see cref="ProjectManager"/>'s
/// <c>AnimationChainListSave</c> (and <c>FileName</c>) to set up the document under test.
/// </summary>
public sealed class EditingTestGraph : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "AnimationEditorEditingTests", Guid.NewGuid().ToString("N"));
    public ProjectManager ProjectManager { get; } = new();
    public SelectedState SelectedState { get; }
    public ApplicationEvents Events { get; } = new();
    public ObjectFinder ObjectFinder { get; }
    public UndoManager Undo { get; }
    public AppCommands Commands { get; }

    public EditingTestGraph()
    {
        Directory.CreateDirectory(Root);
        SelectedState = new SelectedState(ProjectManager);
        Undo = new UndoManager(ProjectManager);
        var appState = new AppState(Events, SelectedState);
        var io = new IoManager(appState, Path.Combine(Root, "recovery.achx"));
        ObjectFinder = new ObjectFinder(ProjectManager);
        Commands = new AppCommands(ProjectManager, SelectedState, Events, io, ObjectFinder, Undo);
    }

    public void Dispose()
    {
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
    }
}
