using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using AnimationEditor.Core.Utilities;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace AnimationEditor.Views.Controls;

/// <summary>Raised by a numeric field when a relative edit (<c>+ 4</c>) is committed while the
/// field shows a mixed multi-selection, so there is no single value to apply it to: the owner
/// applies <see cref="Edit"/> to each selected item's own value.</summary>
public sealed class NumericEditCommittedEventArgs(NumericEdit edit)
    : RoutedEventArgs(NumericExpressionInput.RelativeEditCommittedEvent)
{
    public NumericEdit Edit { get; } = edit;
}

/// <summary>
/// Makes every <see cref="NumericUpDown"/> evaluate <see cref="NumericEdit"/> text (#1325):
/// <c>3 + 4</c> sets 7, and a leading operator applies to the value the field held when the edit
/// began, so live re-evaluation while typing <c>+ 4</c> doesn't compound. Call
/// <see cref="Install"/> once per process; <see cref="FlankerNumericField"/> handles its own text.
/// </summary>
public static class NumericExpressionInput
{
    public static readonly RoutedEvent<NumericEditCommittedEventArgs> RelativeEditCommittedEvent =
        RoutedEvent.Register<Control, NumericEditCommittedEventArgs>(
            "RelativeEditCommitted", RoutingStrategies.Bubble);

    private static readonly ConditionalWeakTable<NumericUpDown, EditState> States = new();
    private static bool _installed;

    public static void Install()
    {
        if (_installed) return;
        _installed = true;
        TemplatedControl.TemplateAppliedEvent.AddClassHandler<NumericUpDown>((box, _) => Attach(box));
    }

    private static void Attach(NumericUpDown box)
    {
        if (States.TryGetValue(box, out _)) return;
        var state = new EditState(box);
        States.Add(box, state);
        box.TextConverter = state;
        box.ValueChanged += (_, e) => state.OnValueChanged(e.NewValue);
        box.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter) state.Commit();
        }, RoutingStrategies.Bubble, handledEventsToo: true);
        box.LostFocus += (_, _) => state.Commit();
    }

    /// <summary>Per-box converter plus the value a relative edit is measured from.</summary>
    private sealed class EditState(NumericUpDown box) : IValueConverter
    {
        // Null while the box shows a mixed selection: a relative edit then has no single base.
        private decimal? _base = box.Value;

        // The value Convert last produced from typed text, so the ValueChanged it causes
        // isn't mistaken for an outside change that starts a new edit.
        private (bool Pending, decimal? Value) _fromText;

        public void OnValueChanged(decimal? value)
        {
            bool fromText = _fromText.Pending
                && (value == _fromText.Value
                    || (_fromText.Value is decimal v && value == Math.Clamp(v, box.Minimum, box.Maximum)));
            _fromText = default;
            if (!fromText) _base = value;
        }

        public void Commit()
        {
            if (_base is null && box.Value is null
                && NumericEdit.TryParse(box.Text, out NumericEdit edit) && edit.IsRelative)
            {
                box.RaiseEvent(new NumericEditCommittedEventArgs(edit));
            }
            _base = box.Value;
            _fromText = default;
            box.Text = Format(box.Value);
        }

        // NumericUpDown's TextConverter runs backwards from a binding converter: Convert takes the
        // typed text, ConvertBack formats the value.
        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is decimal d ? Format(d) : string.Empty;

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            string? text = value as string;
            decimal? result;
            if (string.IsNullOrWhiteSpace(text))
            {
                result = null;
            }
            else if (NumericEdit.TryParse(text, out NumericEdit edit) && !(edit.IsRelative && _base is null))
            {
                result = edit.Apply(_base ?? 0m);
            }
            else
            {
                // Throwing keeps the box's current value, so a half-typed "3 +" changes nothing.
                throw new FormatException($"'{text}' is not a complete expression.");
            }
            _fromText = (true, result);
            return result;
        }

        private string Format(decimal? value) =>
            value?.ToString(string.IsNullOrEmpty(box.FormatString) ? "0.###" : box.FormatString, CultureInfo.InvariantCulture)
            ?? string.Empty;
    }
}
