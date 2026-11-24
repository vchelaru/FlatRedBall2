using System.Globalization;
using AnimationEditor.Core.Utilities;

namespace AnimationEditor.Editing;

/// <summary>The number fields of the ops (#1942): their range, and relative values (<c>+2</c>) turned into absolute ones.</summary>
internal static class NumberFields
{
    /// <summary>The largest magnitude of any number an op takes; some fields have a narrower range of their own.</summary>
    public const double Max = 1_000_000;

    public const string RangeText = "-1000000 to 1000000";

    /// <summary>False for a number past <see cref="Max"/>, an infinity or NaN.</summary>
    public static bool InRange(double value) => Math.Abs(value) <= Max;

    /// <summary>
    /// The op with each relative value replaced by the result of applying it to the target's current value, which
    /// <paramref name="current"/> gives (or why it cannot). The result still has to pass the op's own validation.
    /// </summary>
    public static (EditOp Op, EditResult? Error) ResolveRelative(EditOp op, Func<OpField, (decimal Value, EditResult? Error)> current)
    {
        Dictionary<string, string>? values = null;
        foreach (var field in op.Spec.Fields)
        {
            if (!field.Relative || !op.Values.TryGetValue(field.Name, out var text)) continue;
            if (!NumericEdit.TryParse(text, out var edit) || !edit.IsRelative) continue;

            decimal result;
            try
            {
                var (value, error) = current(field);
                if (error is not null) return (op, error);
                if (!edit.TryApply(value, out result)) return (op, TooLarge(field, text));
            }
            catch (OverflowException) // the stored value does not fit a decimal
            {
                return (op, TooLarge(field, text));
            }

            if (field.Type == "integer")
            {
                result = Math.Round(result, MidpointRounding.AwayFromZero);
                if (result is < int.MinValue or > int.MaxValue) return (op, TooLarge(field, text));
            }
            values ??= new Dictionary<string, string>(op.Values);
            values[field.Name] = result.ToString(CultureInfo.InvariantCulture);
        }
        return (values is null ? op : op with { Values = values }, null);
    }

    private static EditResult TooLarge(OpField field, string text) =>
        new([], [$"{field.Name} {text} gives a number that is too large; use an absolute value."], [], [EditErrorCode.InvalidValue]);
}
