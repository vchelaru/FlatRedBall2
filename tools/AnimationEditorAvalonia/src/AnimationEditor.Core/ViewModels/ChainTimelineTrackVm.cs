using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AnimationEditor.Core.ViewModels;

/// <summary>
/// One row of the multi-track group-preview timeline (#576): a chain's display name plus its own
/// independent set of <see cref="TimelineFrameVm"/> cells, built the same way as the single-chain
/// <see cref="TimelineBuilder"/> strip, and the state of its own play/pause button.
/// </summary>
public sealed class ChainTimelineTrackVm : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string p = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));

    public AnimationChainSave Chain { get; }
    public string ChainName => Chain.Name ?? string.Empty;
    public ObservableCollection<TimelineFrameVm> Frames { get; }

    private bool _isPlaying = true;
    /// <summary>Whether this track is playing (true) or pinned to a frame (false).</summary>
    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (_isPlaying == value) return;
            _isPlaying = value;
            Notify();
            Notify(nameof(PlayPauseIconPath));
            Notify(nameof(PlayPauseTip));
        }
    }

    public string PlayPauseIconPath => IsPlaying
        ? "avares://AnimationEditor.Views/Assets/icons/svg/IconPause.svg"
        : "avares://AnimationEditor.Views/Assets/icons/svg/IconPlay.svg";

    public string PlayPauseTip => IsPlaying ? "Pause this animation" : "Play this animation";

    public ChainTimelineTrackVm(AnimationChainSave chain, IEnumerable<TimelineFrameVm> frames)
    {
        Chain = chain;
        Frames = new ObservableCollection<TimelineFrameVm>(frames);
    }
}
