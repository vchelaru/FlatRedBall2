using System;
using System.Globalization;
using AnimationEditor.Core.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Reusable "[−][value][+]" numeric field (#963): a plain Border/DockPanel/Button+TextBox, the
/// structure GridSize/Speed already got right, extracted so every consumer stops re-copying it
/// (or worse, re-templating <see cref="NumericUpDown"/>'s ButtonSpinner, which nests an extra
/// border around the flanker buttons — the mistake #957's first cut made).
/// </summary>
public partial class FlankerNumericField : UserControl
{
    public static readonly StyledProperty<decimal?> ValueProperty =
        AvaloniaProperty.Register<FlankerNumericField, decimal?>(nameof(Value));

    public decimal? Value
    {
        get => GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly StyledProperty<decimal> MinimumProperty =
        AvaloniaProperty.Register<FlankerNumericField, decimal>(nameof(Minimum), decimal.MinValue);

    public decimal Minimum
    {
        get => GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly StyledProperty<decimal> MaximumProperty =
        AvaloniaProperty.Register<FlankerNumericField, decimal>(nameof(Maximum), decimal.MaxValue);

    public decimal Maximum
    {
        get => GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly StyledProperty<decimal> IncrementProperty =
        AvaloniaProperty.Register<FlankerNumericField, decimal>(nameof(Increment), 1m);

    public decimal Increment
    {
        get => GetValue(IncrementProperty);
        set => SetValue(IncrementProperty, value);
    }

    public static readonly StyledProperty<string> FormatStringProperty =
        AvaloniaProperty.Register<FlankerNumericField, string>(nameof(FormatString), "0.###");

    public string FormatString
    {
        get => GetValue(FormatStringProperty);
        set => SetValue(FormatStringProperty, value);
    }

    /// <summary>Shown in <c>ValueBox</c> when <see cref="Value"/> is null (the multi-select
    /// "mixed value" convention -- see MainWindow's SetValueOrMixed).</summary>
    public static readonly StyledProperty<string?> PlaceholderTextProperty =
        AvaloniaProperty.Register<FlankerNumericField, string?>(nameof(PlaceholderText));

    public string? PlaceholderText
    {
        get => GetValue(PlaceholderTextProperty);
        set => SetValue(PlaceholderTextProperty, value);
    }

    /// <summary>Raised whenever <see cref="Value"/> changes -- typing+Enter/focus-loss, a +/-
    /// click, or a programmatic assignment (mirrors NumericUpDown.ValueChanged, which fires on
    /// every write regardless of origin; a caller that must ignore programmatic writes already
    /// guards with its own suppress flag around the assignment, e.g. MainWindow's
    /// _suppressPropRefresh around SetValueOrMixed).</summary>
    public event EventHandler? ValueChanged;

    static FlankerNumericField()
    {
        // Registered once per type (not per instance) -- AvaloniaProperty.Changed is a shared,
        // class-wide observable, so subscribing inside the instance constructor would add one
        // handler per instance created and fire N times once N instances exist.
        ValueProperty.Changed.AddClassHandler<FlankerNumericField>((c, _) => c.OnValueChanged());
    }

    public FlankerNumericField()
    {
        InitializeComponent();

        MinusBtn.Click += (_, _) => Step(-1);
        PlusBtn.Click += (_, _) => Step(1);

        ValueBox.LostFocus += (_, _) =>
        {
            Commit();
            // Re-raise on this control so external `control.LostFocus += ...` subscribers (e.g.
            // SealOnLostFocus) keep working -- focus lands on ValueBox, not on this UserControl,
            // so its own inherited LostFocus never fires on its own. LostFocusEvent is declared as
            // RoutedEvent<FocusChangedEventArgs> (not the plain base RoutedEventArgs), so a handler
            // registered via the strongly-typed LostFocus CLR event throws InvalidCastException if
            // raised with a bare RoutedEventArgs instance.
            RaiseEvent(new FocusChangedEventArgs(LostFocusEvent));
        };
        ValueBox.AddHandler(KeyDownEvent, OnValueBoxKeyDown);
    }

    private void OnValueBoxKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        Commit();
        e.Handled = true;
    }

    private void OnValueChanged()
    {
        ValueBox.Text = Format(Value);
        ValueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void Step(int direction)
    {
        decimal current = Value ?? Minimum;
        Value = Math.Clamp(current + direction * Increment, Minimum, Maximum);
    }

    private void Commit()
    {
        decimal fallback = Value ?? Minimum;
        decimal parsed = NumericToolbarInput.ParseClamp(ValueBox.Text, Minimum, Maximum, fallback);

        // Value's setter only raises OnValueChanged (which reformats ValueBox.Text) when the
        // parsed value actually differs -- reformat explicitly here too so stray/uncommitted text
        // (e.g. "0.1500" parsing to the already-current 0.15) always snaps back to FormatString.
        if (Value == parsed)
            ValueBox.Text = Format(parsed);
        else
            Value = parsed;
    }

    private string Format(decimal? value) =>
        value?.ToString(FormatString, CultureInfo.InvariantCulture) ?? string.Empty;
}
