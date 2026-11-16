using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// The problem shown when a <c>.seqj</c> opens or reloads holding sequences that play each other, which only an edit outside
/// the editor can produce. The file still loads; the references closing the cycle play nothing (<see cref="SequenceReferences.Plays"/>).
/// </summary>
public static class SequenceCycleNotice
{
    /// <summary>The cycle as the timeline tooltip and the problem banner name it: <c>A &gt; B &gt; A: these sequences play each other</c>.</summary>
    public static string Describe(IReadOnlyList<string> cycle) => $"{string.Join(" > ", cycle)}: these sequences play each other";

    /// <summary>The problem banner text for <paramref name="save"/>, or null when no references form a cycle.</summary>
    public static string? For(SequenceListSave save, string fileName) =>
        SequenceReferences.FindCycle(save.Sequences) is { } cycle
            ? $"In {fileName}, {Describe(cycle)}, so those references play nothing until one is removed or retargeted."
            : null;
}
