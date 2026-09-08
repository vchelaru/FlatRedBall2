using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core;

/// <summary>
/// How a paste should treat an active pending cut, from <see cref="IPendingCutState.ResolveCompletion"/>.
/// </summary>
public enum CutCompletion
{
    /// <summary>No cut is pending; paste this as a plain copy.</summary>
    None,

    /// <summary>The cut's sources are still in the active document -- delete them there, undoably.</summary>
    SameDocument,

    /// <summary>
    /// The cut's sources are in a different, still-open document (a cross-tab cut, #1026) --
    /// paste into the active document, then remove the sources from the other document directly.
    /// </summary>
    CrossDocument,

    /// <summary>
    /// The cut's sources are gone from both the active and the original document (e.g. the source
    /// tab/file closed since) -- treat this paste as a plain copy.
    /// </summary>
    Stale,
}

/// <summary>
/// Tracks animation items cut to the clipboard that remain in the project until paste completes.
/// </summary>
public interface IPendingCutState
{
    event Action? Changed;

    bool IsActive { get; }
    CopySelectionKind? Kind { get; }
    IReadOnlyList<AnimationChainSave> Chains { get; }
    IReadOnlyList<AnimationFrameSave> Frames { get; }
    IReadOnlyList<object> Shapes { get; }

    /// <summary>
    /// The document the pending cut's sources live in, captured at <see cref="Set"/> time (#1026).
    /// Lets paste tell a stale cut (source tab/file closed) apart from a cut whose source is a
    /// different, still-open document (a cross-tab cut) once the active document has moved on.
    /// </summary>
    AnimationChainListSave? SourceDocument { get; }

    void Set(CopySelectionPayload payload, AnimationChainListSave sourceDocument);
    void Clear();
    bool Contains(object data);

    /// <summary>Frames that should show a cut outline on the wireframe.</summary>
    IReadOnlyList<AnimationFrameSave> WireframeFrames { get; }

    /// <summary>Shapes that should show a cut outline in the preview panel.</summary>
    IReadOnlyList<object> WireframeShapes { get; }

    /// <summary>True when every pending-cut source still lives in <paramref name="acls"/>.</summary>
    bool SourcesBelongToProject(AnimationChainListSave? acls);

    /// <summary>
    /// Directly removes the pending cut's source chains/frames/shapes from <paramref name="acls"/>,
    /// bypassing the undo manager entirely (#1026). Use only when <paramref name="acls"/> is a
    /// document other than the currently active one -- the undo stack only ever tracks the active
    /// document, so a cross-document delete can never be made undoable the normal way.
    /// </summary>
    bool RemoveSourcesFrom(AnimationChainListSave acls);

    /// <summary>
    /// Classifies how a paste against <paramref name="activeAcls"/> should treat this pending cut.
    /// Centralizes the decision so the desktop and browser paste handlers don't each re-derive it.
    /// </summary>
    CutCompletion ResolveCompletion(AnimationChainListSave? activeAcls);
}

public sealed class PendingCutState : IPendingCutState
{
    private CopySelectionPayload? _payload;

    public event Action? Changed;

    public bool IsActive => _payload is not null;
    public CopySelectionKind? Kind => _payload?.Kind;
    public IReadOnlyList<AnimationChainSave> Chains => _payload?.Chains ?? [];
    public IReadOnlyList<AnimationFrameSave> Frames => _payload?.Frames ?? [];
    public IReadOnlyList<object> Shapes => _payload?.Shapes ?? [];
    public AnimationChainListSave? SourceDocument { get; private set; }

    public IReadOnlyList<AnimationFrameSave> WireframeFrames =>
        _payload?.Kind switch
        {
            CopySelectionKind.Chain => _payload.Chains.SelectMany(c => c.Frames).ToList(),
            CopySelectionKind.Frame => _payload.Frames,
            _ => [],
        };

    public IReadOnlyList<object> WireframeShapes =>
        _payload?.Kind == CopySelectionKind.Shape ? _payload.Shapes : [];

    public void Set(CopySelectionPayload payload, AnimationChainListSave sourceDocument)
    {
        _payload = payload;
        SourceDocument = sourceDocument;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (_payload is null) return;
        _payload = null;
        SourceDocument = null;
        Changed?.Invoke();
    }

    public bool Contains(object data) =>
        _payload?.Kind switch
        {
            CopySelectionKind.Chain => data is AnimationChainSave c && _payload.Chains.Contains(c),
            CopySelectionKind.Frame => data is AnimationFrameSave f && _payload.Frames.Contains(f),
            CopySelectionKind.Shape => _payload.Shapes.Contains(data),
            _ => false,
        };

    public bool SourcesBelongToProject(AnimationChainListSave? acls)
    {
        if (_payload is null || acls is null) return false;
        return _payload.Kind switch
        {
            CopySelectionKind.Chain => _payload.Chains.All(acls.AnimationChains.Contains),
            CopySelectionKind.Frame => _payload.Frames.All(f => ChainContaining(acls, f) is not null),
            CopySelectionKind.Shape => _payload.Shapes.All(s => FrameContaining(acls, s) is not null),
            _ => false,
        };
    }

    public bool RemoveSourcesFrom(AnimationChainListSave acls)
    {
        if (_payload is null) return false;
        switch (_payload.Kind)
        {
            case CopySelectionKind.Chain:
                return acls.AnimationChains.RemoveAll(_payload.Chains.Contains) > 0;

            case CopySelectionKind.Frame:
                bool removedAnyFrame = false;
                foreach (var frame in _payload.Frames)
                {
                    if (ChainContaining(acls, frame) is { } chain && chain.Frames.Remove(frame))
                        removedAnyFrame = true;
                }
                return removedAnyFrame;

            case CopySelectionKind.Shape:
                bool removedAnyShape = false;
                foreach (var shape in _payload.Shapes)
                {
                    if (FrameContaining(acls, shape) is { ShapesSave: { } shapes } &&
                        shapes.Shapes.Remove(shape))
                        removedAnyShape = true;
                }
                return removedAnyShape;

            default:
                return false;
        }
    }

    public CutCompletion ResolveCompletion(AnimationChainListSave? activeAcls)
    {
        if (_payload is null) return CutCompletion.None;
        if (SourcesBelongToProject(activeAcls)) return CutCompletion.SameDocument;
        if (SourceDocument is { } src && !ReferenceEquals(src, activeAcls) && SourcesBelongToProject(src))
            return CutCompletion.CrossDocument;
        return CutCompletion.Stale;
    }

    private static AnimationChainSave? ChainContaining(AnimationChainListSave acls, AnimationFrameSave frame) =>
        acls.AnimationChains.FirstOrDefault(c => c.Frames.Contains(frame));

    private static AnimationFrameSave? FrameContaining(AnimationChainListSave acls, object shape) =>
        acls.AnimationChains
            .SelectMany(c => c.Frames)
            .FirstOrDefault(f => f.ShapesSave?.Shapes.Contains(shape) == true);
}
