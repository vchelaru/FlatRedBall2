using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A small card (thumbnail, chain name, what a drop would do) that follows the mouse cursor while a library chip is
/// dragged. It lives in the window's overlay layer so it floats over every panel.
/// </summary>
internal sealed class ChipDragGhost
{
    private const double CursorOffset = 16;

    private readonly Border _card;
    private readonly Image _image = new() { Stretch = Stretch.None };
    private readonly TextBlock _hint;
    private OverlayLayer? _layer;

    public ChipDragGhost(string name)
    {
        var nameText = new TextBlock { Text = name, FontSize = 11, MaxWidth = 140, TextTrimming = TextTrimming.CharacterEllipsis };
        SequenceLibraryControl.BindResource(nameText, TextBlock.ForegroundProperty, "Ink");
        _hint = new TextBlock { FontSize = 10 };
        var stack = new StackPanel { Spacing = 2 };
        stack.Children.Add(_image);
        stack.Children.Add(nameText);
        stack.Children.Add(_hint);
        _card = new Border
        {
            Child = stack, Padding = new Thickness(6), CornerRadius = new CornerRadius(5), BorderThickness = new Thickness(1),
            Opacity = 0.92, IsHitTestVisible = false,
        };
        SequenceLibraryControl.BindResource(_card, Border.BackgroundProperty, "BgPanel");
    }

    /// <summary>The card, once shown; null before.</summary>
    public Control Card => _card;

    /// <summary>Shows the card (on first call) next to <paramref name="pointInAnchor"/> with <paramref name="feedback"/>.</summary>
    public void Update(Visual anchor, IImage? thumbnail, ChipDragFeedback feedback, Point pointInAnchor)
    {
        _layer ??= OverlayLayer.GetOverlayLayer(anchor);
        if (_layer is null) return;
        if (!_layer.Children.Contains(_card)) _layer.Children.Add(_card);
        _image.Source = thumbnail;
        _hint.Text = feedback.Hint;
        string key = feedback.Cursor == ChipDragCursor.Copy ? "AccentCool" : "InkMid";
        SequenceLibraryControl.BindResource(_hint, TextBlock.ForegroundProperty, key);
        SequenceLibraryControl.BindResource(_card, Border.BorderBrushProperty, key);
        var point = anchor.TranslatePoint(pointInAnchor, _layer) ?? pointInAnchor;
        Canvas.SetLeft(_card, point.X + CursorOffset);
        Canvas.SetTop(_card, point.Y + CursorOffset);
    }

    public void Hide() => _layer?.Children.Remove(_card);
}
