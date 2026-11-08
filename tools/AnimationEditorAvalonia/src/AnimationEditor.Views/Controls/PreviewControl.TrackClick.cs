using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Input;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A sequence tab's preview lets the user click a track's sprite to select that track. Nothing drags: a track has no
/// offset, so sprites sit at their frames' offsets. The control only finds the sprite under the pointer (<see cref="TrackHitTest"/>);
/// the host owns the selection, through <see cref="TrackClicked"/>.
/// </summary>
public partial class PreviewControl
{
    /// <summary>
    /// The sequence whose sprites can be clicked (the one the timeline and Inspector edit), or -1 for none. The sprites of
    /// other selected sequences are drawn but neither block nor take a click.
    /// </summary>
    public int EditableSequence { get; set; } = -1;

    /// <summary>Raised with the track when a press lands on its sprite: the host selects it. Without a subscriber the preview takes no sprite input.</summary>
    public event Action<int>? TrackClicked;

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

    /// <summary>The sprite a press at (<paramref name="px"/>, <paramref name="py"/>) would select: the frontmost, the top lane's before the ones below it.</summary>
    private SpriteBox? TrackSpriteAt(float px, float py) =>
        TrackClicked is null || px < RulerSize || py < RulerSize ? null : TrackHitTest.TopmostAt(EditableBoxes(), px, py, EditableSequence);

    /// <summary>Test-only: where track <paramref name="track"/>'s sprite is centred, in this control's coordinates; null when it is not drawn.</summary>
    internal Point? TrackSpriteCenterForTest(int track) =>
        EditableBoxes().Where(box => box.Track == track).Select(box => (Point?)new Point(box.CenterX, box.CenterY)).FirstOrDefault();

    /// <summary>
    /// A left press in a composited preview: on a sprite it selects that track; on empty space it does nothing, so the
    /// selection stays. Returns false outside a composite.
    /// </summary>
    private bool TrySelectTrackAt(Point pos)
    {
        if (_composite is null) return false;
        if (TrackSpriteAt((float)pos.X, (float)pos.Y) is { } hit) TrackClicked?.Invoke(hit.Track);
        return true;
    }
}
