using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Which sources of a <c>.seqj</c> tab's library the user collapsed, keyed by the source as written in the file so the
/// state outlives every rebuild of the library's controls. View state: it is never part of the document or its undo
/// history. Mirrors <see cref="ViewModels.CollapsedFolderSet"/>: <see cref="Changed"/> fires on user toggles and prunes
/// (so the host can persist), <see cref="Load"/> does not.
/// </summary>
public sealed class CollapsedSourceSet
{
    // Case-insensitive to match how SequenceDocument looks sources up.
    private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<string> Keys => _keys;

    public event Action? Changed;

    public bool IsCollapsed(string source) => _keys.Contains(source);

    public void Toggle(string source)
    {
        if (!_keys.Remove(source)) _keys.Add(source);
        Changed?.Invoke();
    }

    /// <summary>Replaces the collapsed sources, e.g. from persisted settings.</summary>
    public void Load(IEnumerable<string>? keys)
    {
        _keys.Clear();
        if (keys is not null) _keys.UnionWith(keys);
    }

    /// <summary>Forgets collapsed sources that are no longer in <paramref name="sources"/>.</summary>
    public void Prune(IEnumerable<string> sources)
    {
        var keep = new HashSet<string>(sources, StringComparer.OrdinalIgnoreCase);
        if (_keys.RemoveWhere(key => !keep.Contains(key)) > 0) Changed?.Invoke();
    }
}
