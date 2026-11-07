using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// An open <c>.seqj</c>: its saved data plus the source <c>.achx</c>/<c>.achj</c> files it uses, loaded in the
/// editor's UV form. Every edit is one undo entry and saves the file right away, like an <c>.achx</c> tab.
/// </summary>
/// <remarks>
/// Undo snapshots the whole file as JSON. A <c>.seqj</c> is small, so this stays cheap and needs no per-edit
/// command classes; revisit if files grow large.
/// </remarks>
public sealed class SequenceDocument
{
    private const string DefaultSequenceName = "Sequence";

    // Keyed by the source string as written in the file. A null value is a source that could not be loaded.
    private readonly Dictionary<string, AnimationChainListSave?> _sources = new(StringComparer.OrdinalIgnoreCase);

    private SequenceDocument(string path, SequenceListSave save)
    {
        Path = new FilePath(path);
        Save = save;
        LoadMissingSources();
    }

    public FilePath Path { get; }

    /// <summary>The file's data. Replaced by undo and redo, so do not hold on to it or to anything inside it.</summary>
    public SequenceListSave Save { get; private set; }

    /// <summary>Why the last save failed, or null when it succeeded.</summary>
    public string? SaveError { get; private set; }

    /// <summary>Raised after any edit, undo or redo.</summary>
    public event Action? Changed;

    /// <summary>Reads <paramref name="path"/> and loads its sources. A source that can't be read is kept and flagged.</summary>
    /// <exception cref="System.Text.Json.JsonException">The file is not a valid <c>.seqj</c>.</exception>
    public static SequenceDocument Load(string path) => new(path, SequenceListSave.FromFile(path));

    /// <summary>True when <paramref name="source"/> is missing or could not be read.</summary>
    public bool IsSourceMissing(string source) => SourceChains(source) is null;

    /// <summary>The chains of <paramref name="source"/>, with UV coordinates, or null when it is missing.</summary>
    public AnimationChainListSave? SourceChains(string source) =>
        _sources.TryGetValue(source, out var list) ? list : null;

    /// <summary>
    /// Adds the <c>.achx</c>/<c>.achj</c> files at <paramref name="absolutePaths"/> as sources, in one undo entry, stored
    /// relative to this file. Returns a message for each file refused: a file of another type (a <c>.seqj</c> included)
    /// or one that already is a source. The rest are still added.
    /// </summary>
    public IReadOnlyList<string> AddSources(IUndoManager undo, IEnumerable<string> absolutePaths)
    {
        var errors = new List<string>();
        var taken = Save.Sources.Select(SourcePath).ToHashSet();
        var added = new List<FilePath>();
        foreach (var file in absolutePaths.Select(path => new FilePath(path)))
        {
            if (file.Extension is not ("achx" or "achj"))
                errors.Add($"{file.NoPath} can't be a source. Only .achx and .achj files can.");
            else if (!taken.Add(file))
                errors.Add($"{file.NoPath} is already in the library.");
            else
                added.Add(file);
        }
        if (added.Count == 0) return errors;

        string description = added.Count == 1 ? $"Add Source '{added[0].NoPath}'" : $"Add {added.Count} Sources";
        var folder = Path.GetDirectoryContainingThis();
        Edit(undo, description, save => save.Sources.AddRange(added.Select(file => file.RelativeTo(folder))));
        return errors;
    }

    /// <summary>
    /// Removes <paramref name="source"/> from the library. Returns why it was refused (a chain on the timeline still
    /// uses it), or null.
    /// </summary>
    public string? RemoveSource(IUndoManager undo, string source)
    {
        int uses = Save.Sequences.SelectMany(s => s.Tracks).SelectMany(t => t.Chains)
            .Count(reference => string.Equals(reference.Source, source, StringComparison.OrdinalIgnoreCase));
        if (uses > 0)
            return $"{source} is used by {Plural.Format(uses, "chain")} on the timeline. Remove those first.";
        Edit(undo, $"Remove Source '{new FilePath(source).NoPath}'", save => save.Sources.Remove(source));
        return null;
    }

    /// <summary>
    /// The absolute path of <paramref name="frame"/>'s texture. A relative texture name resolves against
    /// <paramref name="source"/>'s folder, since each source can sit in a different folder from this file.
    /// </summary>
    public string? ResolveTexturePath(string source, AnimationFrameSave frame)
    {
        if (string.IsNullOrEmpty(frame.TextureName)) return null;
        if (System.IO.Path.IsPathRooted(frame.TextureName)) return new FilePath(frame.TextureName).FullPath;
        string sourceFolder = SourcePath(source).GetDirectoryContainingThis().FullPath;
        return new FilePath(System.IO.Path.Combine(sourceFolder, frame.TextureName)).FullPath;
    }

    /// <summary>Adds an empty sequence with a name no other sequence uses. Returns that name.</summary>
    public string AddSequence(IUndoManager undo)
    {
        string name = UniqueName(DefaultSequenceName);
        Edit(undo, $"Add Sequence '{name}'", save => save.Sequences.Add(new SequenceSave { Name = name }));
        return name;
    }

    /// <summary>Renames the sequence at <paramref name="index"/>. Returns why it was refused, or null.</summary>
    public string? RenameSequence(IUndoManager undo, int index, string name)
    {
        name = name.Trim();
        string oldName = Save.Sequences[index].Name;
        if (name.Length == 0) return "A sequence needs a name.";
        if (name == oldName) return null;
        if (Save.Sequences.Where((_, i) => i != index).Any(s => s.Name == name))
            return $"A sequence named \"{name}\" already exists.";
        Edit(undo, $"Rename Sequence '{oldName}' to '{name}'", save => save.Sequences[index].Name = name);
        return null;
    }

    public void DeleteSequence(IUndoManager undo, int index)
    {
        string name = Save.Sequences[index].Name;
        Edit(undo, $"Delete Sequence '{name}'", save => save.Sequences.RemoveAt(index));
    }

    /// <summary>Writes the file. A failure is reported through <see cref="SaveError"/>, not thrown.</summary>
    public void SaveToDisk()
    {
        try
        {
            Save.Save(Path.FullPath);
            SaveError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            SaveError = ex.Message;
        }
    }

    private string UniqueName(string baseName)
    {
        var used = Save.Sequences.Select(s => s.Name).ToHashSet();
        if (!used.Contains(baseName)) return baseName;
        int suffix = 2;
        while (used.Contains($"{baseName} {suffix}")) suffix++;
        return $"{baseName} {suffix}";
    }

    /// <summary>Applies <paramref name="mutate"/> to a copy of the file and runs the result as one undo entry.</summary>
    private void Edit(IUndoManager undo, string description, Action<SequenceListSave> mutate)
    {
        string before = Save.ToJsonString();
        var copy = SequenceListSave.FromJsonString(before);
        mutate(copy);
        string after = copy.ToJsonString();
        if (after == before) return;
        undo.Execute(new SnapshotCommand(this, description, before, after));
    }

    private void Restore(string json)
    {
        Save = SequenceListSave.FromJsonString(json);
        LoadMissingSources();
        SaveToDisk();
        Changed?.Invoke();
    }

    private FilePath SourcePath(string source) =>
        new(System.IO.Path.Combine(Path.GetDirectoryContainingThis().FullPath, source));

    private void LoadMissingSources()
    {
        foreach (var source in Save.Sources.Where(s => !_sources.ContainsKey(s)))
            _sources[source] = TryLoad(SourcePath(source).FullPath);
    }

    private static AnimationChainListSave? TryLoad(string absolutePath)
    {
        try
        {
            return AnimationFileLoader.Load(absolutePath).List;
        }
        catch (Exception ex)
        {
            // Unreadable sources are flagged, not fatal: the sequence file still opens and keeps the reference.
            System.Diagnostics.Debug.WriteLine($"[Sequence] source {absolutePath} not loaded: {ex.Message}");
            return null;
        }
    }

    private sealed class SnapshotCommand(SequenceDocument document, string description, string before, string after) : IUndoableCommand
    {
        public string Description => description;

        public bool Do()
        {
            document.Restore(after);
            return true;
        }

        public void Undo() => document.Restore(before);
    }
}
