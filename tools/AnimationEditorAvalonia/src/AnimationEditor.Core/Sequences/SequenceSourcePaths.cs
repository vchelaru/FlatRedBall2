using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Rewrites the source paths of a <c>.seqj</c>: when the folder they are relative to changes (Save As to another folder, and
/// undo or redo across one), and when a source is converted to another format.
/// </summary>
public static class SequenceSourcePaths
{
    /// <summary>
    /// Rewrites every relative source of <paramref name="save"/> (the source list and each chain reference) so it still points
    /// at the same file when read from <paramref name="toFolder"/> instead of <paramref name="fromFolder"/>. An absolute
    /// source resolves from anywhere and is left alone. Returns the sources that could not be written relative to the new
    /// folder (another drive); those are written as absolute paths, which still resolve.
    /// </summary>
    public static IReadOnlyList<string> Rebase(SequenceListSave save, FilePath fromFolder, FilePath toFolder)
    {
        var unreachable = new List<string>();
        if (fromFolder == toFolder) return unreachable;

        // Case-insensitive like the document's own source lookup, so a reference spelled another way follows its source.
        var rewritten = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string Rewrite(string source)
        {
            if (source.Length == 0 || SequenceClipboard.IsAbsolute(source)) return source;
            if (rewritten.TryGetValue(source, out string? done)) return done;
            var file = new FilePath(System.IO.Path.Combine(fromFolder.FullPath, source));
            string relative = file.RelativeTo(toFolder);
            if (SequenceClipboard.IsAbsolute(relative))
            {
                unreachable.Add(source);
                relative = file.FullPath;
            }
            return rewritten[source] = relative;
        }

        for (int i = 0; i < save.Sources.Count; i++)
            save.Sources[i] = Rewrite(save.Sources[i]);
        foreach (var reference in save.Sequences.SelectMany(sequence => sequence.Tracks).SelectMany(track => track.Chains))
            reference.Source = Rewrite(reference.Source);
        return unreachable;
    }

    /// <summary>The full paths of <paramref name="files"/>, compared as <see cref="Using"/> compares them.</summary>
    public static HashSet<string> FileSet(IEnumerable<string> files) => new(
        files.Select(file => new FilePath(file).FullPath),
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal);

    /// <summary>
    /// Which of <paramref name="files"/> (a <see cref="FileSet"/>) are sources of <paramref name="save"/>, stored in
    /// <paramref name="folder"/>, and how many blocks play one of them. A block whose source is not in the library plays nothing.
    /// </summary>
    public static (IReadOnlyCollection<string> Files, int Blocks) Using(SequenceListSave save, FilePath folder, IReadOnlySet<string> files)
    {
        var used = save.Sources.Select(source => Resolve(folder, source)).OfType<string>().Where(files.Contains).ToHashSet();
        int blocks = Blocks(save).Count(block =>
            save.Sources.Contains(block.Source, StringComparer.OrdinalIgnoreCase)
            && Resolve(folder, block.Source) is { } file && files.Contains(file));
        return (used, blocks);
    }

    /// <summary>
    /// Gives every source of <paramref name="save"/> (the source list and each block) that is one of <paramref name="files"/>
    /// the extension <paramref name="extension"/>, leaving the rest of the path as written.
    /// </summary>
    public static void ChangeExtension(SequenceListSave save, FilePath folder, IEnumerable<string> files, string extension)
    {
        var changing = FileSet(files);
        string Rewrite(string source) =>
            Resolve(folder, source) is { } file && changing.Contains(file) ? System.IO.Path.ChangeExtension(source, extension) : source;

        // The library may already list the new file, missing until now; a source is listed once.
        save.Sources = save.Sources.Select(Rewrite).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var block in Blocks(save))
            block.Source = Rewrite(block.Source);
    }

    private static IEnumerable<SequenceChainReferenceSave> Blocks(SequenceListSave save) =>
        save.Sequences.SelectMany(sequence => sequence.Tracks).SelectMany(track => track.Chains);

    private static string? Resolve(FilePath folder, string source)
    {
        try { return SequenceClipboard.AbsolutePath(folder.FullPath, source); }
        catch (InvalidOperationException) { return null; } // more ../ than folders
    }
}
