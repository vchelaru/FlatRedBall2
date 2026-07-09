using System;
using System.Collections.Generic;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.Rendering;
using Avalonia.Controls;
using Avalonia.Input;
using FlatRedBall2.Animation;
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
/// Frame fields (pixel region, length, relative offset, flip, color channels/mode) are editable
/// when <see cref="EnableEditing"/> is given a texture-size resolver; texture name stays
/// read-only (texture picker deferred).
/// </para>
/// </summary>
public partial class InspectorControl : UserControl
{
    private ISelectedState? _selectedState;
    private IAppCommands? _appCommands;
    private Func<string?, (int Width, int Height)?>? _resolveTextureSize;

    // Guards Commit* against firing while Refresh() is populating the panel's own fields from a
    // newly selected item (see Refresh()'s doc comment). Mirrors MainWindow's _suppressPropRefresh.
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
    /// Enables editing Rectangle/Circle panels and (when <paramref name="resolveTextureSize"/> is
    /// supplied) Frame pixel/timing/transform fields. Numeric fields commit on <c>ValueChanged</c>
    /// (matching MainWindow) rather than <c>LostFocus</c> -- a NumericUpDown's spin buttons never
    /// cause the control itself to lose focus.
    /// </summary>
    public void EnableEditing(
        IAppCommands appCommands,
        Func<string?, (int Width, int Height)?>? resolveTextureSize = null)
    {
        _appCommands = appCommands;
        _resolveTextureSize = resolveTextureSize;

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

        FramePixelXInput.ValueChanged += (_, _) => CommitFramePixelRegion();
        FramePixelYInput.ValueChanged += (_, _) => CommitFramePixelRegion();
        FramePixelWInput.ValueChanged += (_, _) => CommitFramePixelRegion();
        FramePixelHInput.ValueChanged += (_, _) => CommitFramePixelRegion();
        FrameLengthInput.ValueChanged += (_, _) => CommitFrameLength();
        FrameRelXInput.ValueChanged += (_, _) => CommitFrameRelative();
        FrameRelYInput.ValueChanged += (_, _) => CommitFrameRelative();
        FrameFlipHToggle.IsCheckedChanged += (_, _) => CommitFrameFlip();
        FrameFlipVToggle.IsCheckedChanged += (_, _) => CommitFrameFlip();
        FrameFlipDToggle.IsCheckedChanged += (_, _) => CommitFrameFlip();

        FrameRedInput.ValueChanged += (_, _) => CommitFrameColor();
        FrameGreenInput.ValueChanged += (_, _) => CommitFrameColor();
        FrameBlueInput.ValueChanged += (_, _) => CommitFrameColor();
        FrameAlphaInput.ValueChanged += (_, _) => CommitFrameAlpha();
        FrameColorModeCombo.SelectionChanged += (_, _) => CommitFrameColorOperation();
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

    /// <summary>Applies frame pixel X/Y/W/H via <see cref="IAppCommands.SetFramePixelRegion"/>.</summary>
    public void CommitFramePixelRegion()
    {
        if (_suppressCommit || _appCommands is null) return;
        if (_selectedState?.SelectedFrame is not { } frame) return;
        if (_resolveTextureSize?.Invoke(frame.TextureName) is not { } size) return;
        if (FramePixelXInput.Value is not { } x || FramePixelYInput.Value is not { } y ||
            FramePixelWInput.Value is not { } w || FramePixelHInput.Value is not { } h) return;

        _appCommands.SetFramePixelRegion(
            new List<AnimationFrameSave> { frame },
            (int)x, (int)y, (int)w, (int)h,
            size.Width, size.Height);
    }

    /// <summary>Applies frame length via <see cref="IAppCommands.SetFrameLength"/>.</summary>
    public void CommitFrameLength()
    {
        if (_suppressCommit || _appCommands is null) return;
        if (_selectedState?.SelectedFrame is not { } frame) return;
        if (FrameLengthInput.Value is not { } length) return;

        _appCommands.SetFrameLength(new List<AnimationFrameSave> { frame }, (float)length);
    }

    /// <summary>Applies RelativeX/Y via <see cref="IAppCommands.SetFrameRelative"/>.</summary>
    public void CommitFrameRelative()
    {
        if (_suppressCommit || _appCommands is null) return;
        if (_selectedState?.SelectedFrame is not { } frame) return;

        float? relX = FrameRelXInput.Value is { } x ? (float)x : null;
        float? relY = FrameRelYInput.Value is { } y ? (float)y : null;
        if (relX is null && relY is null) return;

        _appCommands.SetFrameRelative(new List<AnimationFrameSave> { frame }, relX, relY);
    }

    /// <summary>Applies flip toggles via <see cref="IAppCommands.SetFrameFlip"/>.</summary>
    public void CommitFrameFlip()
    {
        if (_suppressCommit || _appCommands is null) return;
        if (_selectedState?.SelectedFrame is not { } frame) return;

        _appCommands.SetFrameFlip(
            new List<AnimationFrameSave> { frame },
            FrameFlipHToggle.IsChecked,
            FrameFlipVToggle.IsChecked,
            FrameFlipDToggle.IsChecked);
    }

    /// <summary>Applies R/G/B via <see cref="IAppCommands.SetFrameColor"/> (null = omit channel).</summary>
    public void CommitFrameColor()
    {
        if (_suppressCommit || _appCommands is null) return;
        if (_selectedState?.SelectedFrame is not { } frame) return;

        static int? ToChannel(decimal? v) => v.HasValue ? (int)v.Value : null;
        _appCommands.SetFrameColor(
            new List<AnimationFrameSave> { frame },
            ToChannel(FrameRedInput.Value),
            ToChannel(FrameGreenInput.Value),
            ToChannel(FrameBlueInput.Value));
    }

    /// <summary>Applies alpha via <see cref="IAppCommands.SetFrameAlpha"/>.</summary>
    public void CommitFrameAlpha()
    {
        if (_suppressCommit || _appCommands is null) return;
        if (_selectedState?.SelectedFrame is not { } frame) return;

        _appCommands.SetFrameAlpha(
            new List<AnimationFrameSave> { frame },
            FrameAlphaInput.Value.HasValue ? (int)FrameAlphaInput.Value.Value : null);
    }

    /// <summary>Applies color mode via <see cref="IAppCommands.SetFrameColorOperation"/>.</summary>
    public void CommitFrameColorOperation()
    {
        if (_suppressCommit || _appCommands is null) return;
        if (_selectedState?.SelectedFrame is not { } frame) return;

        // ComboBox order matches desktop: 0 = None (null), 1 = Multiply, 2 = Add.
        ColorOperation? operation = FrameColorModeCombo.SelectedIndex switch
        {
            1 => ColorOperation.Multiply,
            2 => ColorOperation.Add,
            _ => null,
        };
        _appCommands.SetFrameColorOperation(new List<AnimationFrameSave> { frame }, operation);
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
        FrameTextureText.Text = frame.TextureName ?? "(none)";
        FrameLengthText.Text = $"Length: {frame.FrameLength:0.###}s";
        FrameLengthInput.Value = (decimal)frame.FrameLength;

        var size = _resolveTextureSize?.Invoke(frame.TextureName);
        if (size is { } bmp)
        {
            int px = FrameDisplayValues.GetPixelX(frame, bmp.Width);
            int py = FrameDisplayValues.GetPixelY(frame, bmp.Height);
            int pw = FrameDisplayValues.GetPixelWidth(frame, bmp.Width);
            int ph = FrameDisplayValues.GetPixelHeight(frame, bmp.Height);
            FramePixelXInput.Value = px;
            FramePixelYInput.Value = py;
            FramePixelWInput.Value = pw;
            FramePixelHInput.Value = ph;
            FrameCoordinatesText.Text = $"Coords: X{px} Y{py} W{pw} H{ph}";
            FramePixelXInput.IsEnabled = true;
            FramePixelYInput.IsEnabled = true;
            FramePixelWInput.IsEnabled = true;
            FramePixelHInput.IsEnabled = true;
        }
        else
        {
            FrameCoordinatesText.Text =
                $"Coords: L{frame.LeftCoordinate:0.###} R{frame.RightCoordinate:0.###} " +
                $"T{frame.TopCoordinate:0.###} B{frame.BottomCoordinate:0.###}";
            FramePixelXInput.Value = null;
            FramePixelYInput.Value = null;
            FramePixelWInput.Value = null;
            FramePixelHInput.Value = null;
            FramePixelXInput.IsEnabled = false;
            FramePixelYInput.IsEnabled = false;
            FramePixelWInput.IsEnabled = false;
            FramePixelHInput.IsEnabled = false;
        }

        FrameOffsetText.Text = $"Offset: X{frame.RelativeX:0.###} Y{frame.RelativeY:0.###}";
        FrameRelXInput.Value = (decimal)frame.RelativeX;
        FrameRelYInput.Value = (decimal)frame.RelativeY;
        FrameFlipText.Text =
            $"Flip: H={frame.FlipHorizontal} V={frame.FlipVertical} D={frame.FlipDiagonal}";
        FrameFlipHToggle.IsChecked = frame.FlipHorizontal;
        FrameFlipVToggle.IsChecked = frame.FlipVertical;
        FrameFlipDToggle.IsChecked = frame.FlipDiagonal;
        FrameColorText.Text =
            $"Color: R{frame.Red?.ToString() ?? "-"} G{frame.Green?.ToString() ?? "-"} " +
            $"B{frame.Blue?.ToString() ?? "-"} A{frame.Alpha?.ToString() ?? "-"} Op={frame.ColorOperation}";
        FrameRedInput.Value = frame.Red.HasValue ? frame.Red.Value : null;
        FrameGreenInput.Value = frame.Green.HasValue ? frame.Green.Value : null;
        FrameBlueInput.Value = frame.Blue.HasValue ? frame.Blue.Value : null;
        FrameAlphaInput.Value = frame.Alpha.HasValue ? frame.Alpha.Value : null;
        FrameColorModeCombo.SelectedIndex = frame.ColorOperation switch
        {
            ColorOperation.Multiply => 1,
            ColorOperation.Add => 2,
            _ => 0,
        };
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
