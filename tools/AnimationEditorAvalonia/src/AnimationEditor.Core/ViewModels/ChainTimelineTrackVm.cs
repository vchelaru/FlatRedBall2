using FlatRedBall2.Animation.Content;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// One row of the multi-track group-preview timeline (#576): a chain's display name plus its own
/// independent set of <see cref="TimelineFrameVm"/> cells, built the same way as the single-chain
/// <see cref="TimelineBuilder"/> strip.
/// </summary>
public sealed class ChainTimelineTrackVm
{
    public AnimationChainSave Chain { get; }
    public string ChainName => Chain.Name ?? string.Empty;
    public ObservableCollection<TimelineFrameVm> Frames { get; }

    public ChainTimelineTrackVm(AnimationChainSave chain, IEnumerable<TimelineFrameVm> frames)
    {
        Chain = chain;
        Frames = new ObservableCollection<TimelineFrameVm>(frames);
    }
}
