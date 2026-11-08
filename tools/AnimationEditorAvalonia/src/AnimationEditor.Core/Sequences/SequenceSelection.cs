using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// Which sequences of a <c>.seqj</c> the list has selected, by name, in selection (click) order, and which one is the
/// primary. View state: it is never saved to the file and has no undo entry. The primary is the one the timeline,
/// Inspector and editing commands act on; the others only play in the preview, as in an <c>.achx</c> tab.
/// </summary>
public sealed class SequenceSelection
{
    private List<string> _names = new();

    /// <summary>The selected names in selection order, primary included. The preview draws them first to last, back to front.</summary>
    public IReadOnlyList<string> Names => _names;

    /// <summary>The sequence the timeline and Inspector show, or null when nothing is selected.</summary>
    public string? Primary { get; private set; }

    /// <summary>
    /// Takes the list's selection, in the order the list reports it. The primary stays when it is still selected, as the
    /// first-selected row of an <c>.achx</c> multi-selection does; otherwise the first selected one becomes primary.
    /// </summary>
    public void Apply(IReadOnlyList<string> selected)
    {
        _names = selected.Distinct().ToList();
        if (Primary is null || !_names.Contains(Primary)) Primary = _names.FirstOrDefault();
    }

    /// <summary>
    /// Brings the selection in line with the document after an edit, undo or redo: names that no longer exist drop out.
    /// <paramref name="primaryIndex"/> is the document's selected sequence; when it is outside the selection (an add,
    /// duplicate or paste selected a new sequence) the selection collapses to it, as a plain click would.
    /// </summary>
    public void Reconcile(IReadOnlyList<string> existing, int primaryIndex)
    {
        if (primaryIndex < 0 || primaryIndex >= existing.Count)
        {
            _names = new List<string>();
            Primary = null;
            return;
        }
        string primary = existing[primaryIndex];
        var kept = _names.Where(existing.Contains).ToList();
        if (!kept.Contains(primary)) kept = new List<string> { primary };
        _names = kept;
        Primary = primary;
    }

    /// <summary>Follows a rename so the sequence stays selected.</summary>
    public void Rename(string from, string to)
    {
        for (int i = 0; i < _names.Count; i++)
            if (_names[i] == from) _names[i] = to;
        if (Primary == from) Primary = to;
    }
}
