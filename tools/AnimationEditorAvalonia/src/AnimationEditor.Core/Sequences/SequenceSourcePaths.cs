using AnimationEditor.Core.Paths;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Rewrites the relative source paths of a <c>.seqj</c> when the folder they are relative to changes: Save As to another
/// folder, and undo or redo across one (a snapshot taken in the old folder).
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
}
