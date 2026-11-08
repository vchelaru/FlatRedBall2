using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A sequence tab's preview lets the user grab a track's sprite and drag it: the offset of the track follows the pointer.
/// The control only finds the sprite under the pointer and turns pointer travel into a world delta
/// (<see cref="TrackPlacement"/>); the host owns the selection and the document, through <see cref="GrabTrack"/>.
/// </summary>
public partial class PreviewControl
{
    private TrackMoveGesture? _trackMove;
    private int _pressedTrack = -1;
    private Point _trackPress;
    private bool _trackDragging;

    /// <summary>
    /// The sequence whose sprites can be grabbed (the one the timeline and Inspector edit), or -1 for none. The sprites of
    /// other selected sequences are drawn but neither block nor take a press.
    /// </summary>
    public int EditableSequence { get; set; } = -1;

    /// <summary>
    /// Asked when a press lands on a track's sprite. The host selects the track (keeping a multi-selection that already
    /// holds it) and returns the drag over every selected track, or null to refuse. Without it the preview takes no sprite input.
    /// </summary>
    public Func<int, TrackMoveGesture?>? GrabTrack { get; set; }

    /// <summary>Raised with the track when a press on its sprite ends without a drag: the host narrows a multi-selection to it.</summary>
    public event Action<int>? TrackClicked;

    /// <summary>True from the moment a press on a sprite has travelled past the drag threshold until it is released or cancelled.</summary>
    internal bool IsDraggingTrack => _trackMove is not null && _trackDragging;

    /// <summary>The boxes of the editable sequence's sprites on screen, back to front; a sprite whose texture is not loaded yet has none.</summary>
    private List<SpriteBox> EditableBoxes()
    {
        var boxes = new List<SpriteBox>();
        if (_composite is null || EditableSequence < 0 || _thumbnailService is null || _appState is null) return boxes;
        float centerX = GetCenterX();
        float centerY = GetCenterY();
        foreach (var sprite in _composite)
        {
            if (sprite.Sequence != EditableSequence) continue;
            if (CacheKey(sprite.TexturePath) is not { } key || !_thumbnailService.BitmapCache.TryGetValue(key, out var bitmap) || bitmap is null) continue;
            var (_, _, width, height) = ComputeSourceRect(sprite.Frame, bitmap.Width, bitmap.Height);
            boxes.Add(SpriteBox.Of(sprite, width, height, _zoom, _appState.OffsetMultiplier, centerX, centerY));
        }
        return boxes;
    }

    /// <summary>The sprite a press at (<paramref name="px"/>, <paramref name="py"/>) would grab: the frontmost, the top lane's before the ones below it.</summary>
    private SpriteBox? TrackSpriteAt(float px, float py) =>
        GrabTrack is null || px < RulerSize || py < RulerSize ? null : TrackPlacement.TopmostAt(EditableBoxes(), px, py, EditableSequence);

    /// <summary>Test-only: where track <paramref name="track"/>'s sprite is centred, in this control's coordinates; null when it is not drawn.</summary>
    internal Point? TrackSpriteCenterForTest(int track) =>
        EditableBoxes().Where(box => box.Track == track).Select(box => (Point?)new Point(box.CenterX, box.CenterY)).FirstOrDefault();

    /// <summary>
    /// A left press in a composited preview: on a sprite it grabs that track (the drag starts once the pointer has travelled
    /// 3 px); on empty space it does nothing, so the selection stays. Returns false outside a composite.
    /// </summary>
    private bool TryBeginTrackPress(PointerPressedEventArgs e, Point pos)
    {
        if (_composite is null) return false;
        if (TrackSpriteAt((float)pos.X, (float)pos.Y) is not { } hit || GrabTrack?.Invoke(hit.Track) is not { } gesture) return true;
        _trackMove = gesture;
        _pressedTrack = hit.Track;
        _trackPress = pos;
        _trackDragging = false;
        e.Pointer.Capture(this);
        return true;
    }

    /// <summary>Follows the pointer with the grabbed tracks. Returns false when no sprite is grabbed.</summary>
    private bool MoveTrackDrag(PointerEventArgs e, Point pos)
    {
        if (_trackMove is not { } move) return false;
        var travel = pos - _trackPress;
        if (!_trackDragging)
        {
            if (!TrackPlacement.PastDragThreshold(travel.X, travel.Y)) return true;
            _trackDragging = true;
        }
        var (dx, dy) = TrackPlacement.ScreenToWorld(travel.X, travel.Y, _zoom, _appState!.OffsetMultiplier);
        move.Update(dx, dy, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        Cursor = new Cursor(StandardCursorType.SizeAll);
        return true;
    }

    /// <summary>Ends the press: a drag becomes one undo entry, a press that never travelled is a click on the track.</summary>
    private bool EndTrackDrag(PointerReleasedEventArgs e)
    {
        if (_trackMove is not { } move) return false;
        bool dragged = _trackDragging;
        int track = _pressedTrack;
        ClearTrackDrag();
        e.Pointer.Capture(null);
        if (dragged) move.Commit();
        else TrackClicked?.Invoke(track);
        UpdateHoverCursor(e.GetPosition(this));
        return true;
    }

    /// <summary>Escape: puts the tracks back where the press found them, with no undo entry. False when no sprite is grabbed.</summary>
    private bool CancelTrackDrag()
    {
        if (_trackMove is not { } move) return false;
        bool dragged = _trackDragging;
        ClearTrackDrag();
        if (dragged) move.Cancel();
        return true;
    }

    private void ClearTrackDrag()
    {
        _trackMove = null;
        _pressedTrack = -1;
        _trackDragging = false;
    }
}
