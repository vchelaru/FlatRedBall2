using System;
using System.Globalization;

namespace AnimationEditor.Core.Sequences;

/// <summary>
/// The text on a timeline block: <c>Name ×loops (1.20 s)</c>. As the block narrows the length goes first, then the
/// loop count, and last the name is truncated (by the caller, which owns the font).
/// </summary>
public static class BlockLabel
{
    /// <summary>Whether <paramref name="loops"/> is a single play, which the block label leaves unmarked.</summary>
    public static bool IsSingleLoop(double loops) => Math.Abs(loops - 1) <= 1e-6;

    /// <summary>The block's length as shown, rounded for display only: <c>1.20 s</c>.</summary>
    public static string Seconds(double seconds) => seconds.ToString("0.00", CultureInfo.InvariantCulture) + " s";

    private static string Loops(double loops) => "×" + loops.ToString("0.##", CultureInfo.InvariantCulture);

    /// <summary>The label with everything it can show. A single loop hides the <c>×1</c>.</summary>
    public static string Full(string name, double loops, double seconds) =>
        $"{WithLoops(name, loops)} ({Seconds(seconds)})";

    /// <summary>The hover text: always the loop count, even <c>×1</c>.</summary>
    public static string Tooltip(string name, double loops, double seconds) =>
        $"{name} {Loops(loops)} ({Seconds(seconds)})";

    private static string WithLoops(string name, double loops) => IsSingleLoop(loops) ? name : $"{name} {Loops(loops)}";

    /// <summary>
    /// The most complete label whose width, by <paramref name="measure"/>, fits <paramref name="width"/>: the full label,
    /// else without the length, else without the loop count. If not even the name fits it is returned whole for the caller to truncate.
    /// </summary>
    public static string Fit(string name, double loops, double seconds, double width, Func<string, double> measure)
    {
        string full = Full(name, loops, seconds);
        if (measure(full) <= width) return full;
        string noLength = WithLoops(name, loops);
        if (measure(noLength) <= width) return noLength;
        return name;
    }
}
