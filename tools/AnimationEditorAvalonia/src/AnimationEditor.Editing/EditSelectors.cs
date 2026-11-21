using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Addresses a selector expanded to, or why it could not be expanded.</summary>
public sealed record SelectorExpansion(IReadOnlyList<string>? Targets, string? Error, EditErrorCode Code = EditErrorCode.Unspecified)
{
    public bool Ok => Targets is not null;
}

/// <summary>Bulk addressing (#1811): ranges, lists and wildcards for frames, globs for chains.</summary>
public static class EditSelectors
{
    public static SelectorExpansion ExpandChains(AnimationChainListSave doc, string pattern) => throw new NotImplementedException();
    public static SelectorExpansion ExpandFrames(AnimationChainListSave doc, string address) => throw new NotImplementedException();
    public static SelectorExpansion ExpandShapes(AnimationChainListSave doc, string address) => throw new NotImplementedException();
}
