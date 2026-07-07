using AnimationEditor.Core;
using Avalonia.Controls;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Phase 1 (#603) read-only property display for the currently selected frame/rectangle/circle,
/// driven entirely by <see cref="ISelectedState.SelectionChanged"/> (independent of
/// <see cref="AnimationTreeControl"/> -- either can drive selection). No editable fields, no
/// mutation. When a shape is selected its panel takes precedence over the owning frame's, since
/// that mirrors what the user actually clicked; see docs/BROWSER_TREE_INSPECTOR_DECISION.md.
/// </summary>
public partial class InspectorControl : UserControl
{
    private ISelectedState? _selectedState;

    public InspectorControl()
    {
        InitializeComponent();
    }

    public void InitializeServices(ISelectedState selectedState)
    {
        _selectedState = selectedState;
        _selectedState.SelectionChanged += Refresh;
        Refresh();
    }

    private void Refresh()
    {
        var s = _selectedState;
        var rect = s?.SelectedRectangle;
        var circle = s?.SelectedCircle;
        var frame = s?.SelectedFrame;

        FramePanel.IsVisible = frame is not null && rect is null && circle is null;
        RectPanel.IsVisible = rect is not null;
        CirclePanel.IsVisible = circle is not null;
        NoSelectionPanel.IsVisible = frame is null && rect is null && circle is null;

        if (rect is not null) ShowRect(rect);
        if (circle is not null) ShowCircle(circle);
        if (frame is not null && rect is null && circle is null) ShowFrame(frame);
    }

    private void ShowFrame(AnimationFrameSave frame)
    {
        FrameTextureText.Text = $"Texture: {frame.TextureName}";
        FrameLengthText.Text = $"Length: {frame.FrameLength:0.###}s";
        FrameCoordinatesText.Text =
            $"Coords: L{frame.LeftCoordinate:0.###} R{frame.RightCoordinate:0.###} " +
            $"T{frame.TopCoordinate:0.###} B{frame.BottomCoordinate:0.###}";
        FrameOffsetText.Text = $"Offset: X{frame.RelativeX:0.###} Y{frame.RelativeY:0.###}";
        FrameFlipText.Text =
            $"Flip: H={frame.FlipHorizontal} V={frame.FlipVertical} D={frame.FlipDiagonal}";
        FrameColorText.Text =
            $"Color: R{frame.Red?.ToString() ?? "-"} G{frame.Green?.ToString() ?? "-"} " +
            $"B{frame.Blue?.ToString() ?? "-"} A{frame.Alpha?.ToString() ?? "-"} Op={frame.ColorOperation}";
    }

    private void ShowRect(AARectSave rect)
    {
        RectNameText.Text = $"Name: {rect.Name}";
        RectPositionText.Text = $"Position: X{rect.X:0.###} Y{rect.Y:0.###}";
        RectScaleText.Text = $"Scale: X{rect.ScaleX:0.###} Y{rect.ScaleY:0.###}";
    }

    private void ShowCircle(CircleSave circle)
    {
        CircleNameText.Text = $"Name: {circle.Name}";
        CirclePositionText.Text = $"Position: X{circle.X:0.###} Y{circle.Y:0.###}";
        CircleRadiusText.Text = $"Radius: {circle.Radius:0.###}";
    }
}
