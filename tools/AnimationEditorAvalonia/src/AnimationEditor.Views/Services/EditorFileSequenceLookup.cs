using AnimationEditor.Core.Sequences;
using AnimationEditor.Core.Utilities;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.App.Services;

/// <summary>
/// The chains of a <c>.seqj</c> read through <c>IEditorFile</c>s rather than opened as a document: its sources, parsed as
/// stored (UV coordinates are converted per sprite when it is drawn, see <see cref="IsUvFrame"/>). Textures are named
/// by their path relative to the folder holding the <c>.seqj</c>.
/// </summary>
internal sealed class EditorFileSequenceLookup : ISequenceChainLookup
{
    private readonly Dictionary<string, AnimationChainListSave> _sources = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<AnimationFrameSave> _uvFrames = new(ReferenceEqualityComparer.Instance);

    public void Add(string source, AnimationChainListSave list)
    {
        _sources[source] = list;
        if (list.CoordinateType != TextureCoordinateType.UV) return;
        foreach (var frame in list.AnimationChains.SelectMany(chain => chain.Frames))
            _uvFrames.Add(frame);
    }

    /// <summary>Whether <paramref name="frame"/> came from a source stored in UV coordinates.</summary>
    public bool IsUvFrame(AnimationFrameSave frame) => _uvFrames.Contains(frame);

    public AnimationChainSave? FindChain(SequenceChainReferenceSave reference) =>
        _sources.TryGetValue(reference.Source, out var list)
            ? list.AnimationChains.FirstOrDefault(chain => chain.Name == reference.Chain)
            : null;

    public FrameLengthUnit UnitOf(SequenceChainReferenceSave reference) =>
        FrameLengthUnit.Of(_sources.GetValueOrDefault(reference.Source));

    public string? ResolveTexturePath(string source, AnimationFrameSave frame)
    {
        if (string.IsNullOrEmpty(frame.TextureName) || System.IO.Path.IsPathRooted(frame.TextureName)) return null;
        return RelativeFilePaths.ResolveFrom(source, frame.TextureName);
    }
}
