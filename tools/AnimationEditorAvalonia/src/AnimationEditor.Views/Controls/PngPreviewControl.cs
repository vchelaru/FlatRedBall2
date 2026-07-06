using AnimationEditor.Core.Rendering;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using System;

namespace AnimationEditor.App.Controls;

/// <summary>
/// A read-only PNG viewer tab (issue #604): shows the image fit-to-window over a transparency
/// checkerboard, with mouse-wheel zoom and left-drag pan. Holds no animation-editor state.
/// </summary>
public sealed class PngPreviewControl : UserControl
{
    private readonly Image _image;
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _translate = new();

    private Bitmap? _bitmap;
    private double _fitScale = 1.0;
    private bool _userZoomedOrPanned;
    private bool _isPanning;
    private Point _lastPointer;

    public PngPreviewControl()
    {
        _image = new Image
        {
            // Stretch.None keeps the bitmap at its pixel size; all scaling is done via the
            // RenderTransform so zoom is independent of layout. RenderTransformOrigin.Center
            // makes wheel-zoom grow about the middle of the image.
            Stretch = Stretch.None,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            RenderTransformOrigin = RelativePoint.Center,
            RenderTransform = new TransformGroup { Children = { _scale, _translate } },
        };

        Content = new Panel
        {
            ClipToBounds = true,
            Children = { new Checkerboard(), _image },
        };

        ClipToBounds = true;
        SizeChanged += (_, _) => { if (!_userZoomedOrPanned) ApplyFit(); };
        PointerWheelChanged += OnWheel;
        PointerPressed += OnPointerPressed;
        PointerMoved += OnPointerMoved;
        PointerReleased += OnPointerReleased;
    }

    /// <summary>Decodes and displays <paramref name="path"/>, reset to fit-to-window.</summary>
    public void LoadImage(string path)
    {
        Clear();
        try { _bitmap = new Bitmap(path); }
        catch { _bitmap = null; }
        _image.Source = _bitmap;
        ApplyFit();
    }

    /// <summary>Releases the current image and resets the view.</summary>
    public void Clear()
    {
        _image.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
        _userZoomedOrPanned = false;
        _scale.ScaleX = _scale.ScaleY = 1.0;
        _translate.X = _translate.Y = 0;
    }

    private void ApplyFit()
    {
        if (_bitmap is null) return;
        _fitScale = PngPreviewScale.ComputeInitialScale(
            _bitmap.PixelSize.Width, _bitmap.PixelSize.Height, Bounds.Width, Bounds.Height);
        _scale.ScaleX = _scale.ScaleY = _fitScale;
        _translate.X = _translate.Y = 0;
    }

    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (_bitmap is null) return;
        double factor = e.Delta.Y > 0 ? 1.1 : 1.0 / 1.1;
        double next = Math.Clamp(_scale.ScaleX * factor, _fitScale * 0.1, 32.0);
        _scale.ScaleX = _scale.ScaleY = next;
        _userZoomedOrPanned = true;
        e.Handled = true;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_bitmap is null) return;
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed) return;
        _isPanning = true;
        _lastPointer = point.Position;
        e.Pointer.Capture(this);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPanning) return;
        var pos = e.GetPosition(this);
        _translate.X += pos.X - _lastPointer.X;
        _translate.Y += pos.Y - _lastPointer.Y;
        _lastPointer = pos;
        _userZoomedOrPanned = true;
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _isPanning = false;
        e.Pointer.Capture(null);
    }

    /// <summary>A tiled light/dark square pattern so transparent PNG regions are visible.</summary>
    private sealed class Checkerboard : Control
    {
        private static readonly IBrush Dark = new SolidColorBrush(Color.FromRgb(0x24, 0x24, 0x24));
        private static readonly IBrush Light = new SolidColorBrush(Color.FromRgb(0x2f, 0x2f, 0x2f));
        private const double Cell = 12;

        public override void Render(DrawingContext context)
        {
            var size = Bounds.Size;
            context.FillRectangle(Dark, new Rect(size));
            for (int y = 0; y * Cell < size.Height; y++)
                for (int x = 0; x * Cell < size.Width; x++)
                    if (((x + y) & 1) == 0)
                        context.FillRectangle(Light, new Rect(x * Cell, y * Cell, Cell, Cell));
        }
    }
}
