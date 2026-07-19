using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Core.CommandsAndState;

/// <summary>
/// Resolves which chain and frame the timeline strip should display, given the app's
/// selection state. Shared by desktop and browser hosts so the two don't drift.
/// </summary>
public static class TimelineChainResolver
{
    /// <summary>
    /// Returns <see cref="ISelectedState.SelectedChain"/> if set, otherwise the chain
    /// containing <see cref="ISelectedState.SelectedFrame"/> (or <c>null</c> if neither is set).
    /// </summary>
    public static AnimationChainSave? GetChain(ISelectedState selectedState, IObjectFinder objectFinder)
    {
        var chain = selectedState.SelectedChain;
        if (chain is null && selectedState.SelectedFrame is { } selectedFrame)
            chain = objectFinder.GetAnimationChainContaining(selectedFrame);
        return chain;
    }

    /// <summary>
    /// Returns the frame index the timeline scrubber should highlight for <paramref name="chain"/>:
    /// the selected frame's index if it belongs to <paramref name="chain"/>, otherwise
    /// <paramref name="playbackFrameIndex"/>. Returns -1 for a null or empty chain.
    /// </summary>
    public static int GetPreferredFrameIndex(
        ISelectedState selectedState, IObjectFinder objectFinder, AnimationChainSave? chain, int playbackFrameIndex)
    {
        if (chain is null || chain.Frames.Count == 0)
            return -1;

        if (selectedState.SelectedFrame is { } selectedFrame)
        {
            var selectedFrameChain = objectFinder.GetAnimationChainContaining(selectedFrame);
            if (ReferenceEquals(selectedFrameChain, chain))
            {
                var selectedFrameIndex = chain.Frames.IndexOf(selectedFrame);
                if (selectedFrameIndex >= 0)
                    return selectedFrameIndex;
            }
        }

        return playbackFrameIndex;
    }
}
