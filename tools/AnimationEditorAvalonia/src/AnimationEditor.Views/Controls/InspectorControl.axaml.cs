using System;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using Avalonia.Controls;
using Avalonia.Input;
using FlatRedBall2.Animation.Content;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Phase 1 (#603) property display for the currently selected frame/rectangle/circle, driven
/// entirely by <see cref="ISelectedState.SelectionChanged"/> (independent of
/// <see cref="AnimationTreeControl"/> -- either can drive selection). When a shape is selected
/// its panel takes precedence over the owning frame's, since that mirrors what the user actually
/// clicked; see docs/BROWSER_TREE_INSPECTOR_DECISION.md.
/// <para>
/// Phase 2 (#610) made the rectangle/circle panels editable, routed through
/// <see cref="IAppCommands.SetRectProps"/>/<see cref="IAppCommands.SetCircleProps"/> -- the same
/// commit-boundary pattern (Enter + LostFocus) MainWindow's own PropRect*/PropCircle* fields use.
/// Frame fields (texture, length, flip, offset, color, pixel region) stay read-only for now --
/// several need extra plumbing (a texture picker, the selected frame's bitmap size for pixel-
/// region edits) that's out of scope for this pass; tracked under #610.
/// </para>
/// </summary>
public partial class InspectorControl : UserControl
{
    private ISelectedState? _selectedState;
    private IAppCommands? _appCommands;

    // Guards CommitRectProps/CommitCircleProps against firing while Refresh() is populating the
    // panel's own fields from a newly selected shape (see Refresh()'s doc comment). Mirrors
    // MainWindow's _suppressPropRefresh guard around the identical ApplyRectProps/ApplyCircleProps
    // wiring.
    private bool _suppressCommit;

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

    /// <summary>
    /// Enables editing the Rectangle/Circle panels' fields. Numeric fields commit on
    /// <c>ValueChanged</c> (matching MainWindow's own PropRectX/PropRectY/etc. wiring) rather than
    /// <c>LostFocus</c> -- a NumericUpDown's spin buttons never cause the control itself to lose
    /// focus, so a LostFocus-only commit silently dropped single-field edits until a second field
    /// happened to be edited afterward (confirmed live). The Name field stays LostFocus/Enter since
    /// a plain TextBox has no ValueChanged equivalent.
    /// </summary>
    public void EnableEditing(IAppCommands appCommands)
    {
        _appCommands = appCommands;

        RectNameBox.LostFocus += (_, _) => CommitRectProps();
        RectXInput.ValueChanged += (_, _) => CommitRectProps();
        RectYInput.ValueChanged += (_, _) => CommitRectProps();
        RectScaleXInput.ValueChanged += (_, _) => CommitRectProps();
        RectScaleYInput.ValueChanged += (_, _) => CommitRectProps();
        RectNameBox.KeyDown += CommitOnEnter(CommitRectProps);

        CircleNameBox.LostFocus += (_, _) => CommitCircleProps();
        CircleXInput.ValueChanged += (_, _) => CommitCircleProps();
        CircleYInput.ValueChanged += (_, _) => CommitCircleProps();
        CircleRadiusInput.ValueChanged += (_, _) => CommitCircleProps();
        CircleNameBox.KeyDown += CommitOnEnter(CommitCircleProps);
    }

    private static EventHandler<KeyEventArgs> CommitOnEnter(Action commit) => (_, e) =>
    {
        if (e.Key != Key.Return) return;
        e.Handled = true;
        commit();
    };

    /// <summary>
    /// Applies the Rectangle panel's current field values via <see cref="IAppCommands.SetRectProps"/>.
    /// No-op if nothing is selected there (e.g. this fires from a stray LostFocus after the
    /// selection already moved on).
    /// </summary>
    public void CommitRectProps()
    {
        if (_suppressCommit || _appCommands is null) return;
        if (_selectedState?.SelectedRectangle is not { } rect) return;
        if (RectXInput.Value is not { } x || RectYInput.Value is not { } y ||
            RectScaleXInput.Value is not { } scaleX || RectScaleYInput.Value is not { } scaleY) return;

        _appCommands.SetRectProps(
            _selectedState.SelectedFrame, rect, RectNameBox.Text ?? string.Empty,
            (float)x, (float)y, (float)scaleX, (float)scaleY);
    }

    /// <summary>
    /// Applies the Circle panel's current field values via <see cref="IAppCommands.SetCircleProps"/>.
    /// </summary>
    public void CommitCircleProps()
    {
        if (_suppressCommit || _appCommands is null) return;
        if (_selectedState?.SelectedCircle is not { } circle) return;
        if (CircleXInput.Value is not { } x || CircleYInput.Value is not { } y ||
            CircleRadiusInput.Value is not { } radius) return;

        _appCommands.SetCircleProps(
            _selectedState.SelectedFrame, circle, CircleNameBox.Text ?? string.Empty,
            (float)x, (float)y, (float)radius);
    }

    /// <summary>
    /// Suppresses commits while populating the panel's fields from a newly selected shape: without
    /// this, setting RectXInput.Value below would fire ValueChanged -> CommitRectProps immediately,
    /// which reads the *other* Rect fields' still-stale values (the previous selection's) since
    /// they haven't been set yet in this same pass -- corrupting the newly selected shape with a
    /// mix of its own and the old selection's values.
    /// </summary>
    private void Refresh()
    {
        _suppressCommit = true;
        try
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
        finally
        {
            _suppressCommit = false;
        }
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
        RectNameBox.Text = rect.Name;
        RectXInput.Value = (decimal)rect.X;
        RectYInput.Value = (decimal)rect.Y;
        RectScaleXInput.Value = (decimal)rect.ScaleX;
        RectScaleYInput.Value = (decimal)rect.ScaleY;
    }

    private void ShowCircle(CircleSave circle)
    {
        CircleNameBox.Text = circle.Name;
        CircleXInput.Value = (decimal)circle.X;
        CircleYInput.Value = (decimal)circle.Y;
        CircleRadiusInput.Value = (decimal)circle.Radius;
    }
}
