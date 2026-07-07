using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using System;
using System.Linq;

namespace AnimationEditor.App.Controls;

/// <summary>
/// The reusable <c>[−][editable %][+]</c> zoom widget: an editable percent field flanked by
/// step-to-preset buttons. Bind it to an <see cref="IZoomTarget"/> with <see cref="Attach"/>;
/// after that the field tracks the target's live zoom and every edit/step drives it back.
/// Used by the wireframe toolbar, the preview toolbar, and the PNG diff bar so all three share
/// one implementation.
/// </summary>
public partial class ZoomControl : UserControl
{
    /// <summary>Zoom presets (percent) the +/- buttons and mouse-wheel step through.</summary>
    public static readonly int[] Presets =
        { 5, 10, 16, 25, 33, 50, 66, 75, 100, 150, 200, 300, 400, 800, 1600, 3200 };

    private static readonly string[] PresetTexts = Presets.Select(p => $"{p}%").ToArray();

    private IZoomTarget? _target;

    // Breaks the ZoomChanged → SyncCombo → LostFocus/SelectionChanged → commit feedback loop:
    // set while SyncCombo writes the live percent back so the commit path ignores its own echo.
    private bool _suppressComboChanged;

    public ZoomControl()
    {
        InitializeComponent();

        Combo.ItemsSource = PresetTexts;
        Combo.KeyDown += OnComboKeyDown;
        Combo.LostFocus += OnComboLostFocus;
        Combo.SelectionChanged += OnComboSelectionChanged;
        PlusBtn.Click += (_, _) => StepUp();
        MinusBtn.Click += (_, _) => StepDown();
    }

    /// <summary>Current text of the editable percent field (e.g. <c>"150%"</c>).</summary>
    public string? Text => Combo.Text;

    /// <summary>
    /// Binds this widget to <paramref name="target"/>: installs <see cref="Presets"/> as the
    /// target's wheel-zoom steps, follows its <see cref="IZoomTarget.ZoomChanged"/> so the
    /// field shows the live zoom, and routes every field edit / button step into
    /// <see cref="IZoomTarget.SetZoomPercent"/>. Call once after the target exists.
    /// </summary>
    public void Attach(IZoomTarget target)
    {
        _target = target;
        target.WheelZoomPresets = Presets;
        target.ZoomChanged += SyncCombo;
        SyncCombo(target.Zoom * 100f);
    }

    /// <summary>Steps the attached viewport's zoom to the next preset above the current value.</summary>
    public void StepUp() => Step(+1);

    /// <summary>Steps the attached viewport's zoom to the next preset below the current value.</summary>
    public void StepDown() => Step(-1);

    private void Step(int direction)
    {
        if (_target is null) return;
        int next = ZoomPresetStepper.StepToNextPreset(_target.Zoom * 100f, Presets, direction);
        _target.SetZoomPercent(next);
    }

    private void OnComboKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Commit(); e.Handled = true; }
    }

    private void OnComboLostFocus(object? sender, RoutedEventArgs e) => Commit();

    private void OnComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressComboChanged) return;
        if (Combo.SelectedItem is string s) Apply(s);
    }

    private void Commit()
    {
        if (_suppressComboChanged) return;
        Apply(Combo.Text ?? string.Empty);
    }

    private void Apply(string text)
    {
        if (_target is not null && TryParsePercent(text, out int pct)) _target.SetZoomPercent(pct);
    }

    // Writes the live percent (rounded) back into the field. Guarded so the echo doesn't re-enter Commit.
    private void SyncCombo(float zoomPercent)
    {
        _suppressComboChanged = true;
        Combo.Text = $"{(int)MathF.Round(zoomPercent)}%";
        _suppressComboChanged = false;
    }

    private static bool TryParsePercent(string text, out int pct)
    {
        var trimmed = text.Trim().TrimEnd('%').Trim();
        return int.TryParse(trimmed, out pct);
    }
}
