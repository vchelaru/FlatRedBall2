using AnimationEditor.Core.Sequences;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A small card (thumbnail, chain name, what a drop would do) that follows the mouse cursor while a library chip is
/// dragged. It lives in the window's overlay layer so it floats over every panel.
/// </summary>
internal sealed class ChipDragGhost
{
    private const double CursorOffset = 16;

    private static readonly Cursor CopyCursor = new(StandardCursorType.DragCopy);
    private static readonly Cursor NotAllowedCursor = new(StandardCursorType.No);

    /// <summary>The platform cursor for <paramref name="cursor"/>.</summary>
    internal static Cursor CursorFor(ChipDragCursor cursor) => cursor == ChipDragCursor.Copy ? CopyCursor : NotAllowedCursor;

    // Avalonia shows the cursor of the element under the pointer, even while another control holds the capture, so each
    // cursor kind gets a transparent sheet over the whole window. Changing kind swaps sheets, which takes effect on the
    // next pointer move.
    private readonly Border _copySheet = Sheet(CopyCursor);
    private readonly Border _notAllowedSheet = Sheet(NotAllowedCursor);

    private static Border Sheet(Cursor cursor) => new() { Background = Brushes.Transparent, Cursor = cursor };

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
        var sheet = feedback.Cursor == ChipDragCursor.Copy ? _copySheet : _notAllowedSheet;
        var other = sheet == _copySheet ? _notAllowedSheet : _copySheet;
        _layer.Children.Remove(other);
        sheet.Width = _layer.Bounds.Width;
        sheet.Height = _layer.Bounds.Height;
        if (!_layer.Children.Contains(sheet)) _layer.Children.Insert(0, sheet);
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

    // TEMPORARY diagnostic, see ChipDragLog.
    public void Log(Control chip, PointerEventArgs e, string drag, string lastSet)
    {
        var sheet = _layer is not null && _layer.Children.Contains(_copySheet) ? _copySheet : _layer is not null && _layer.Children.Contains(_notAllowedSheet) ? _notAllowedSheet : null;
        ChipDragLog.Move(chip, e, drag, lastSet, _layer, sheet);
    }

    public void Hide()
    {
        _layer?.Children.Remove(_card);
        _layer?.Children.Remove(_copySheet);
        _layer?.Children.Remove(_notAllowedSheet);
    }
}
