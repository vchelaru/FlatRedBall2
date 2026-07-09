using AnimationEditor.App.Services;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Input;
using FlatRedBall2.Animation.Content;
using System;
using System.Collections.ObjectModel;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Phase 14: single-chain timeline/scrubber strip shared by desktop-parity browser wiring.
/// Frame widths and scrub hit-testing delegate to <see cref="TimelineBuilder"/> /
/// <see cref="TimelineScrubMapper"/>; per-frame thumbnails come from <see cref="ThumbnailService"/>.
/// Multi-chain <c>GroupTimelineTracks</c> is intentionally deferred (browser-first sessions
/// rarely need side-by-side chain comparison).
/// </summary>
public partial class TimelineStripControl : UserControl
{
    private readonly ObservableCollection<TimelineFrameVm> _frames = new();
    private ThumbnailService? _thumbnailService;
    private TimelineStripSignature? _signature;
    private double _effectivePps = TimelineBuilder.PixelsPerSecond;
    private int _currentFrameIndex = -1;
    private bool _isScrubbing;

    public TimelineStripControl()
    {
        InitializeComponent();
        TimelineStrip.ItemsSource = _frames;
        TimelineScrubSurface.PointerPressed += OnTimelinePointerPressed;
        TimelineScrubSurface.PointerMoved += OnTimelinePointerMoved;
        TimelineScrubSurface.PointerReleased += OnTimelinePointerReleased;
    }

    /// <summary>Number of frame cells currently shown.</summary>
    public int FrameCount => _frames.Count;

    /// <summary>Raised when the user scrubs to a frame (frame index + fraction through that frame).</summary>
    public event Action<int, double>? FrameScrubbed;

    public void InitializeServices(ThumbnailService thumbnailService) =>
        _thumbnailService = thumbnailService;

    /// <summary>
    /// Rebuilds cells when the chain structure changes; updates the playhead highlight for
    /// <paramref name="preferredFrameIndex"/>. Pass <paramref name="scrubberLocalX"/> to park the
    /// playhead mid-cell after a scrub or paused playback seek.
    /// </summary>
    public void SetChain(AnimationChainSave? chain, int preferredFrameIndex = -1, double? scrubberLocalX = null)
    {
        TimelineHeaderLabel.Text = chain is { Frames.Count: > 0 }
            ? $"TIMELINE · {TimelineBuilder.FormatSeconds(TimelineBuilder.TotalSeconds(chain))}"
            : "TIMELINE";

        var signature = TimelineStripSignature.From(chain);
        if (!signature.Equals(_signature))
        {
            RebuildCells(chain);
            _signature = signature;
        }

        if (preferredFrameIndex >= 0)
            UpdateCurrentFrame(preferredFrameIndex, scrubberLocalX);
        else if (_frames.Count == 0)
            _currentFrameIndex = -1;
    }

    /// <summary>Moves the playhead during playback without rebuilding cells.</summary>
    public void ApplyPlaybackPosition(int frameIndex, double frameElapsed)
    {
        if (frameIndex < 0 || frameIndex >= _frames.Count)
            return;

        if (_currentFrameIndex != frameIndex)
            UpdateCurrentFrame(frameIndex);

        double travelWidth = Math.Max(0, _frames[frameIndex].Width - TimelineFrameVm.PlayheadWidth);
        _frames[frameIndex].ScrubberOffset = Math.Min(frameElapsed * _effectivePps, travelWidth);
    }

    /// <summary>Pure hit-test helper for tests and callers.</summary>
    public TimelineScrubResult ResolveScrubAt(double contentX) =>
        TimelineScrubMapper.Resolve(contentX, CollectCellWidths());

    /// <summary>Applies a scrub at content-space X and raises <see cref="FrameScrubbed"/>.</summary>
    public void ScrubAt(double contentX)
    {
        if (_frames.Count == 0) return;

        var result = ResolveScrubAt(contentX);
        PreviewScrubResult(result);
    }

    private void RebuildCells(AnimationChainSave? chain)
    {
        _frames.Clear();
        foreach (var item in TimelineBuilder.BuildFrameItems(chain))
            _frames.Add(item);
        _effectivePps = TimelineBuilder.ComputeEffectivePixelsPerSecond(chain);
        _currentFrameIndex = -1;

        if (chain is not null && _thumbnailService is not null)
        {
            var colors = EffectiveFrameColor.ResolveAll(chain.Frames);
            for (int i = 0; i < chain.Frames.Count && i < _frames.Count; i++)
                _frames[i].Thumbnail = _thumbnailService.GetFrameThumbnail(chain.Frames[i], colors[i], 22, 18);
        }
    }

    private void UpdateCurrentFrame(int frameIndex, double? scrubberLocalX = null)
    {
        if (_frames.Count == 0)
        {
            _currentFrameIndex = -1;
            return;
        }

        int clamped = Math.Clamp(frameIndex, 0, _frames.Count - 1);
        if (_currentFrameIndex == clamped && scrubberLocalX is null)
            return;

        if (_currentFrameIndex >= 0 && _currentFrameIndex < _frames.Count)
            _frames[_currentFrameIndex].IsCurrent = false;

        _frames[clamped].IsCurrent = true;
        _currentFrameIndex = clamped;

        if (scrubberLocalX is double localX)
        {
            double travelWidth = Math.Max(0, _frames[clamped].Width - TimelineFrameVm.PlayheadWidth);
            _frames[clamped].ScrubberOffset = Math.Min(localX, travelWidth);
        }
        else
            _frames[clamped].ScrubberOffset = 0;
    }

    private void OnTimelinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(TimelineScrubSurface).Properties.IsLeftButtonPressed) return;
        _isScrubbing = true;
        e.Pointer.Capture(TimelineScrubSurface);
        ScrubFromPointer(e);
    }

    private void OnTimelinePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isScrubbing) ScrubFromPointer(e);
    }

    private void OnTimelinePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isScrubbing) return;
        _isScrubbing = false;
        e.Pointer.Capture(null);
    }

    private void ScrubFromPointer(PointerEventArgs e)
    {
        if (_frames.Count == 0) return;
        double contentX = e.GetPosition(TimelineStrip).X;
        PreviewScrubResult(ResolveScrubAt(contentX));
    }

    private void PreviewScrubResult(TimelineScrubResult result)
    {
        UpdateCurrentFrame(result.FrameIndex, result.LocalX);
        FrameScrubbed?.Invoke(result.FrameIndex, result.Fraction);
    }

    private double[] CollectCellWidths()
    {
        var widths = new double[_frames.Count];
        for (int i = 0; i < widths.Length; i++)
            widths[i] = _frames[i].Width;
        return widths;
    }
}
