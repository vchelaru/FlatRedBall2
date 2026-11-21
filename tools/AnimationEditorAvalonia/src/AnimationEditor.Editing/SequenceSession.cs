using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Sequences;

namespace AnimationEditor.Editing;

/// <summary>Outcome of <see cref="SequenceSession.Open"/>: the session, or null with the reason in <see cref="Result"/>.</summary>
public sealed record SequenceSessionOpen(SequenceSession? Session, EditResult Result);

/// <summary>
/// A headless editing session over one .seqj (#1811): the editor's own <see cref="SequenceDocument"/> and an undo
/// manager, with the document's save-on-every-edit switched off. Edits change the in-memory document only;
/// <see cref="Save"/> writes it once, so a failed batch leaves the file untouched. Build one per command.
/// </summary>
public sealed class SequenceSession
{
    public string Path { get; }
    public SequenceDocument Document { get; }
    public UndoManager Undo { get; } = new();

    private SequenceSession(string path, SequenceDocument document)
    {
        Path = path;
        Document = document;
        // The document saves after every edit and undo; a session saves once, at the end.
        Document.SaveRunner = static (_, _) => { };
    }

    /// <summary>
    /// Loads <paramref name="path"/> (.seqj). Sequences sharing a name are renumbered in memory, as the editor does, and the
    /// notice comes back as a warning; the file keeps its names until an edit is saved.
    /// </summary>
    public static SequenceSessionOpen Open(string path)
    {
        if (!SequenceFileReader.IsSequenceFile(path))
            return Fail($"Only .seqj files can be edited with sequence ops: {path}");
        if (!File.Exists(path))
            return Fail($"File not found: {path}");
        try
        {
            var session = new SequenceSession(System.IO.Path.GetFullPath(path), SequenceDocument.Load(path));
            var warnings = session.Document.DuplicateNamesNotice is { } notice ? new[] { notice } : [];
            return new SequenceSessionOpen(session, new EditResult([], [], warnings));
        }
        catch (Exception ex)
        {
            return Fail($"Could not open {System.IO.Path.GetFileName(path)}: {ex.Message}");
        }
    }

    /// <summary>Writes the document back to the file it was opened from.</summary>
    public EditResult Save()
    {
        try
        {
            Document.Save.Save(Path);
            return new EditResult([Path], [], []);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new EditResult([], [$"Could not save {System.IO.Path.GetFileName(Path)}: {ex.Message}"], []);
        }
    }

    private static SequenceSessionOpen Fail(string message) => new(null, new EditResult([], [message], []));
}
