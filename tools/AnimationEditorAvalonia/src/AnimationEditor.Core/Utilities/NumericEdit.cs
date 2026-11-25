using System;
using System.Globalization;

namespace AnimationEditor.Core.Utilities;

/// <summary>
/// A value typed into a numeric field (#1325): either an absolute value, which may be written as
/// arithmetic (<c>3 + 4</c>), or a relative edit that starts with an operator (<c>+ 4</c>,
/// <c>* 2</c>) and applies to each edited item's own current value, so a mixed multi-selection
/// stays mixed. A leading <c>-</c> is relative only when whitespace follows it (<c>- 4</c>);
/// <c>-4</c> is the negative number. Parsing is culture-invariant: <c>.</c> is the decimal point.
/// </summary>
public readonly struct NumericEdit
{
    private readonly char _op;
    private readonly decimal _operand;
    private readonly (decimal Minimum, decimal Maximum, bool Clamps)? _limits;

    private NumericEdit(char op, decimal operand, (decimal, decimal, bool)? limits = null)
    {
        _op = op;
        _operand = operand;
        _limits = limits;
    }

    /// <summary>True when the edit applies an operator to each item's current value.</summary>
    public bool IsRelative => _op != '\0';

    public static NumericEdit Set(decimal value) => new('\0', value);

    public static implicit operator NumericEdit(decimal value) => Set(value);
    public static implicit operator NumericEdit(float value) => Set(ToDecimal(value));
    public static implicit operator NumericEdit(int value) => Set(value);

    /// <summary>This edit kept in the range of the field it was typed in, for each item it applies to: a result outside is
    /// clamped, or leaves the item's value unchanged when <paramref name="clamps"/> is false.</summary>
    public NumericEdit Within(decimal minimum, decimal maximum, bool clamps) => new(_op, _operand, (minimum, maximum, clamps));

    /// <summary>The number as the file writes it. A cast keeps only 7 digits, so it can be another float.</summary>
    /// <exception cref="OverflowException">The value does not fit a decimal.</exception>
    public static decimal ToDecimal(float value) =>
        decimal.TryParse(value.ToString("R", CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var exact)
            ? exact
            : throw new OverflowException($"{value} does not fit a decimal.");

    /// <summary>The edited value of an item whose value is currently <paramref name="current"/>.
    /// An edit that overflows, or is refused by its limits (<see cref="Within"/>), leaves the value unchanged.</summary>
    public decimal Apply(decimal current) => TryApply(current, out decimal value) ? value : current;

    /// <summary><see cref="Apply(decimal)"/> for callers that must report an edit that overflows or is out of range: returns false for one.</summary>
    public bool TryApply(decimal current, out decimal value)
    {
        try
        {
            value = _op switch
            {
                '+' => current + _operand,
                '-' => current - _operand,
                '*' => current * _operand,
                '/' => current / _operand,
                _ => _operand,
            };
            if (_limits is not { } limits) return true;
            decimal inRange = Math.Clamp(value, limits.Minimum, limits.Maximum);
            bool accepted = limits.Clamps || inRange == value;
            value = inRange;
            return accepted;
        }
        catch (OverflowException)
        {
            value = default;
            return false;
        }
    }

    public float Apply(float current)
    {
        if (!IsRelative) return (float)_operand;
        try
        {
            return (float)Apply(ToDecimal(current));
        }
        catch (OverflowException) // current does not fit a decimal
        {
            return current;
        }
    }

    /// <summary>Rounds to the nearest integer, for whole-pixel and color-channel fields.</summary>
    public int Apply(int current) => (int)Math.Round(Apply((decimal)current), MidpointRounding.AwayFromZero);

    /// <summary><see cref="Apply(int)"/> for a <paramref name="current"/> that is rounded from what is stored: writing it
    /// back would change the item, so a refused edit (false) must write nothing.</summary>
    public bool TryApply(int current, out int value)
    {
        bool accepted = TryApply((decimal)current, out decimal result);
        value = accepted ? (int)Math.Round(result, MidpointRounding.AwayFromZero) : current;
        return accepted;
    }

    /// <summary>Returns false (leaving the target untouched) for empty, malformed, or
    /// divide-by-zero text.</summary>
    public static bool TryParse(string? text, out NumericEdit edit)
    {
        edit = default;
        if (string.IsNullOrWhiteSpace(text)) return false;

        string trimmed = text.Trim();
        char op = '\0';
        int start = 0;
        if (trimmed[0] is '+' or '*' or '/' || (trimmed[0] == '-' && trimmed.Length > 1 && char.IsWhiteSpace(trimmed[1])))
        {
            op = trimmed[0];
            start = 1;
        }

        try
        {
            var parser = new Parser(trimmed, start);
            decimal value = parser.ParseExpression();
            if (!parser.AtEnd) return false;
            if (op == '/' && value == 0) return false;
            edit = new NumericEdit(op, value);
            return true;
        }
        catch (Exception e) when (e is FormatException or OverflowException or DivideByZeroException)
        {
            return false;
        }
    }

    /// <summary>Recursive-descent parser for + - * /, parentheses, unary minus, and decimals.</summary>
    private sealed class Parser(string text, int position)
    {
        private int _pos = position;

        public bool AtEnd
        {
            get
            {
                SkipWhitespace();
                return _pos == text.Length;
            }
        }

        public decimal ParseExpression()
        {
            decimal value = ParseTerm();
            while (TryConsume('+', '-', out char op))
            {
                decimal right = ParseTerm();
                value = op == '+' ? value + right : value - right;
            }
            return value;
        }

        private decimal ParseTerm()
        {
            decimal value = ParseUnary();
            while (TryConsume('*', '/', out char op))
            {
                decimal right = ParseUnary();
                value = op == '*' ? value * right : value / right;
            }
            return value;
        }

        private decimal ParseUnary()
        {
            if (TryConsume('-', '-', out _)) return -ParseUnary();
            if (TryConsume('+', '+', out _)) return ParseUnary();
            return ParsePrimary();
        }

        private decimal ParsePrimary()
        {
            if (TryConsume('(', '(', out _))
            {
                decimal value = ParseExpression();
                if (!TryConsume(')', ')', out _)) throw new FormatException("Missing ')'.");
                return value;
            }

            SkipWhitespace();
            int begin = _pos;
            while (_pos < text.Length && (char.IsAsciiDigit(text[_pos]) || text[_pos] == '.')) _pos++;
            return decimal.Parse(text.AsSpan(begin, _pos - begin), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture);
        }

        private bool TryConsume(char a, char b, out char consumed)
        {
            SkipWhitespace();
            consumed = _pos < text.Length ? text[_pos] : '\0';
            if (consumed != '\0' && (consumed == a || consumed == b))
            {
                _pos++;
                return true;
            }
            return false;
        }

        private void SkipWhitespace()
        {
            while (_pos < text.Length && char.IsWhiteSpace(text[_pos])) _pos++;
        }
    }
}
