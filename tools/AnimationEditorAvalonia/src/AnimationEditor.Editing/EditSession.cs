using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Outcome of <see cref="EditSession.OpenAsync"/>: the session, or null with the reason in <see cref="Result"/>.</summary>
public sealed record EditSessionOpen(EditSession? Session, EditResult Result);

/// <summary>
/// A headless editing session over one .achx/.achj (#1811): the same service graph the editor builds
/// (<see cref="ProjectManager"/>, selection, events, undo, <see cref="AppCommands"/>) loaded through the
/// editor's own open workflow, with autosave off. Edits change the in-memory document only;
/// <see cref="Save"/> writes it through the editor's save path, so the file format and
/// coordinate type are preserved. Build one per command; it holds no global state.
/// </summary>
public sealed class EditSession
{
    private readonly ProjectManager _projectManager = new();
    private readonly AppCommands _commands;

    public string Path { get; }
    public ProjectManager ProjectManager => _projectManager;
    public AppCommands Commands => _commands;
    public ObjectFinder ObjectFinder { get; }
    public UndoManager Undo { get; }

    /// <summary>The files a batch wrote before its save, put back when it fails.</summary>
    public FileRollback Rollback { get; } = new();

    /// <summary>The loaded document, in the editor's in-memory (pixel) representation.</summary>
    public AnimationChainListSave Document => _projectManager.AnimationChainListSave!;

    private EditSession(string path)
    {
        Path = path;
        var selected = new SelectedState(_projectManager);
        var events = new ApplicationEvents();
        var appState = new AppState(events, selected);
        // The recovery file is only written when there is no file path, which a session never has.
        var io = new IoManager(appState, System.IO.Path.Combine(System.IO.Path.GetTempPath(), "aecli-recovery.achx"));
        ObjectFinder = new ObjectFinder(_projectManager);
        Undo = new UndoManager(_projectManager);
        _commands = new AppCommands(_projectManager, selected, events, io, ObjectFinder, Undo) { AutosaveOnChange = false };
    }

    /// <summary>
    /// Loads <paramref name="path"/> (.achx or .achj). A legacy UV-coordinate file is refused rather than
    /// converted, since converting rewrites every frame on the next save; open it in the editor once.
    /// </summary>
    public static async Task<EditSessionOpen> OpenAsync(string path)
    {
        var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
        if (ext != ".achx" && ext != ".achj" && ext != ".tsx")
            return Fail($"Only .achx, .achj and .tsx files can be edited: {path}", EditErrorCode.UnsupportedFile);
        if (!File.Exists(path))
            return Fail($"File not found: {path}", EditErrorCode.NotFound);

        var session = new EditSession(System.IO.Path.GetFullPath(path));
        string? loadError = null;
        bool declinedUv = false;
        session._commands.LoadFailed += (_, ex) => loadError = ex.Message;
        session._commands.ConfirmAsync = (_, _) =>
        {
            declinedUv = true;
            return Task.FromResult(false);
        };

        if (ext == ".tsx")
        {
            // The editor's own native-tsx open: the compatibility checker's message is the refusal.
            var opened = await session._commands.OpenTsxWorkflowAsync(session.Path);
            if (!opened || !session._projectManager.IsNativeTsxProject)
                return Fail(loadError ?? $"Could not open {System.IO.Path.GetFileName(path)}.", EditErrorCode.UnsupportedTsx);
            return new EditSessionOpen(session, new EditResult([], [], []));
        }

        var ok = await session._commands.OpenAchxWorkflowAsync(session.Path);
        if (!ok || session._projectManager.AnimationChainListSave is null)
        {
            if (declinedUv)
                return Fail($"{System.IO.Path.GetFileName(path)} uses legacy UV coordinates. Open and save it in the editor once to convert it to pixel coordinates.", EditErrorCode.UnsupportedFile);
            return Fail($"Could not open {System.IO.Path.GetFileName(path)}: {loadError ?? "unknown error"}", EditErrorCode.IoFailed);
        }
        return new EditSessionOpen(session, new EditResult([], [], []));
    }

    /// <summary>Writes the document back to the file it was opened from.</summary>
    /// <remarks>
    /// The editor reports save side effects through events; they are captured here: a .tsx save's
    /// "could not be written" notes become <see cref="EditResult.Warnings"/>.
    /// </remarks>
    public EditResult Save()
    {
        var errors = new List<string>();
        var warnings = new List<string>();
        void OnFailed(string message) => errors.Add(message);
        void OnTsxWarnings(string tsx, IReadOnlyList<string> notes) => warnings.AddRange(notes);
        _commands.SaveFailed += OnFailed;
        _commands.TsxSaveCompletedWithWarnings += OnTsxWarnings;
        try
        {
            _commands.SaveCurrentAnimationChainList(Path);
        }
        finally
        {
            _commands.SaveFailed -= OnFailed;
            _commands.TsxSaveCompletedWithWarnings -= OnTsxWarnings;
        }
        return errors.Count == 0
            ? new EditResult([Path], [], warnings)
            : new EditResult([], errors.Select(e => $"Could not save {System.IO.Path.GetFileName(Path)}: {e}").ToList(), warnings,
                errors.Select(_ => EditErrorCode.IoFailed).ToList());
    }

    /// <summary>True when the document is a Tiled .tsx opened as a native project.</summary>
    public bool IsNativeTsx => _projectManager.IsNativeTsxProject;

    private static EditSessionOpen Fail(string message, EditErrorCode code = EditErrorCode.Unspecified) =>
        new(null, new EditResult([], [message], [], [code]));
}
