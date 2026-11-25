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
    /// paste into the active document, then remove the sources from the other document.
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
    CopySelectionPayload? Payload { get; }
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

    /// <summary>
    /// True while the system clipboard, holding <paramref name="clipboardText"/>, still has what the cut put on it.
    /// Another window or program may have copied since.
    /// </summary>
    bool IsOnClipboard(string? clipboardText);

    /// <param name="clipboardText">What the cut put on the system clipboard.</param>
    void Set(CopySelectionPayload payload, AnimationChainListSave sourceDocument, string clipboardText);
    void Clear();
    bool Contains(object data);

    /// <summary>Frames that should show a cut outline on the wireframe.</summary>
    IReadOnlyList<AnimationFrameSave> WireframeFrames { get; }

    /// <summary>Shapes that should show a cut outline in the preview panel.</summary>
    IReadOnlyList<object> WireframeShapes { get; }

    /// <summary>True when every pending-cut source still lives in <paramref name="acls"/>.</summary>
    bool SourcesBelongToProject(AnimationChainListSave? acls);

    /// <summary>
    /// Classifies how a paste against <paramref name="activeAcls"/> should treat this pending cut.
    /// Centralizes the decision so every paste entry point uses the same rule.
    /// </summary>
    CutCompletion ResolveCompletion(AnimationChainListSave? activeAcls);
}

public sealed class PendingCutState : IPendingCutState
{
    private string? _clipboardText;

    public event Action? Changed;

    public CopySelectionPayload? Payload { get; private set; }
    public bool IsActive => Payload is not null;
    public CopySelectionKind? Kind => Payload?.Kind;
    public IReadOnlyList<AnimationChainSave> Chains => Payload?.Chains ?? [];
    public IReadOnlyList<AnimationFrameSave> Frames => Payload?.Frames ?? [];
    public IReadOnlyList<object> Shapes => Payload?.Shapes ?? [];
    public AnimationChainListSave? SourceDocument { get; private set; }

    public IReadOnlyList<AnimationFrameSave> WireframeFrames =>
        Payload?.Kind switch
        {
            CopySelectionKind.Chain => Payload.Chains.SelectMany(c => c.Frames).ToList(),
            CopySelectionKind.Frame => Payload.Frames,
            _ => [],
        };

    public IReadOnlyList<object> WireframeShapes =>
        Payload?.Kind == CopySelectionKind.Shape ? Payload.Shapes : [];

    public bool IsOnClipboard(string? clipboardText) => Payload is not null && clipboardText == _clipboardText;

    public void Set(CopySelectionPayload payload, AnimationChainListSave sourceDocument, string clipboardText)
    {
        Payload = payload;
        _clipboardText = clipboardText;
        SourceDocument = sourceDocument;
        Changed?.Invoke();
    }

    public void Clear()
    {
        if (Payload is null) return;
        Payload = null;
        _clipboardText = null;
        SourceDocument = null;
        Changed?.Invoke();
    }

    public bool Contains(object data) =>
        Payload?.Kind switch
        {
            CopySelectionKind.Chain => data is AnimationChainSave c && Payload.Chains.Contains(c),
            CopySelectionKind.Frame => data is AnimationFrameSave f && Payload.Frames.Contains(f),
            CopySelectionKind.Shape => Payload.Shapes.Contains(data),
            _ => false,
        };

    public bool SourcesBelongToProject(AnimationChainListSave? acls)
    {
        if (Payload is null || acls is null) return false;
        return Payload.Kind switch
        {
            CopySelectionKind.Chain => Payload.Chains.All(acls.AnimationChains.Contains),
            CopySelectionKind.Frame => Payload.Frames.All(f => ChainContaining(acls, f) is not null),
            CopySelectionKind.Shape => Payload.Shapes.All(s => FrameContaining(acls, s) is not null),
            _ => false,
        };
    }

    public CutCompletion ResolveCompletion(AnimationChainListSave? activeAcls)
    {
        if (Payload is null) return CutCompletion.None;
        if (SourcesBelongToProject(activeAcls)) return CutCompletion.SameDocument;
        if (SourceDocument is { } src && !ReferenceEquals(src, activeAcls) && SourcesBelongToProject(src))
            return CutCompletion.CrossDocument;
        return CutCompletion.Stale;
    }

    internal static AnimationChainSave? ChainContaining(AnimationChainListSave acls, AnimationFrameSave frame) =>
        acls.AnimationChains.FirstOrDefault(c => c.Frames.Contains(frame));

    internal static AnimationFrameSave? FrameContaining(AnimationChainListSave acls, object shape) =>
        acls.AnimationChains
            .SelectMany(c => c.Frames)
            .FirstOrDefault(f => f.ShapesSave?.Shapes.Contains(shape) == true);
}
