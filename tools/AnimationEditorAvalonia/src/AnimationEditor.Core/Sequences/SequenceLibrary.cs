using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>One chain shown in a sequence's library, from <see cref="Source"/> as written in the <c>.seqj</c>.</summary>
public sealed record LibraryChip(string Source, AnimationChainSave Chain);

/// <summary>One source in a sequence's library. A missing source has no chips; its reference is kept in the file.</summary>
public sealed record LibrarySource(string Source, bool IsMissing, IReadOnlyList<LibraryChip> Chips);

/// <summary>What a <c>.seqj</c> tab's library shows: each source in file order, with a chip per chain.</summary>
public static class SequenceLibrary
{
    public static IReadOnlyList<LibrarySource> Build(SequenceDocument document) =>
        document.Save.Sources
            .Select(source => document.SourceChains(source) is { } list
                ? new LibrarySource(source, false, list.AnimationChains.Select(chain => new LibraryChip(source, chain)).ToList())
                : new LibrarySource(source, true, Array.Empty<LibraryChip>()))
            .ToList();
}
