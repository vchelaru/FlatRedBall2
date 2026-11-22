using System;
using System.Globalization;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// The text on a timeline block: <c>Name ×cycles (1.20 s)</c>, the cycle count being how many cycles of its chain the length spans. As the block narrows the length goes first, then the
/// cycle count, and last the name is truncated (by the caller, which owns the font).
/// </summary>
public static class BlockLabel
{
    /// <summary>Whether <paramref name="cycles"/> is a single play, which the block label leaves unmarked.</summary>
    public static bool IsSingleLoop(double cycles) => Math.Abs(cycles - 1) <= 1e-6;

    /// <summary>The block's length as shown, rounded for display only, formatted like the Inspector's Length: <c>1.2 s</c>.</summary>
    public static string Seconds(double seconds) => seconds.ToString("0.##", CultureInfo.InvariantCulture) + " s";

    /// <summary>The last lines of a filmstrip hover card, the cycle count and the length.</summary>
    public static string[] CardLines(double cycles, double seconds) =>
        new[] { "Cycles " + cycles.ToString("0.##", CultureInfo.InvariantCulture), "Length " + Seconds(seconds) };

    private static string Cycles(double cycles) => "×" + cycles.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The label with everything it can show. A single loop hides the <c>×1</c>.</summary>
    public static string Full(string name, double cycles, double seconds) =>
        $"{WithCycles(name, cycles)} ({Seconds(seconds)})";

    /// <summary>The hover text: always the cycle count, even <c>×1</c>.</summary>
    public static string Tooltip(string name, double cycles, double seconds) =>
        $"{name} {Cycles(cycles)} ({Seconds(seconds)})";

    private static string WithCycles(string name, double cycles) => IsSingleLoop(cycles) ? name : $"{name} {Cycles(cycles)}";

    /// <summary>
    /// The most complete label whose width, by <paramref name="measure"/>, fits <paramref name="width"/>: the full label,
    /// else without the length, else without the cycle count. If not even the name fits it is returned whole for the caller to truncate.
    /// </summary>
    public static string Fit(string name, double cycles, double seconds, double width, Func<string, double> measure)
    {
        string full = Full(name, cycles, seconds);
        if (measure(full) <= width) return full;
        string noLength = WithCycles(name, cycles);
        if (measure(noLength) <= width) return noLength;
        return name;
    }
}
