using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>What the hover card over a filmstrip cell says: the chain, the frame to enlarge, and terse lines.</summary>
/// <param name="Title">The chain's name.</param>
/// <param name="Frame">The frame to show enlarged (0-based).</param>
/// <param name="Lines">Short facts under the title, such as <c>3 / 8</c>, <c>0.12 s</c>, <c>at 1.40 s</c>.</param>
public sealed record FilmstripCard(string Title, int Frame, IReadOnlyList<string> Lines);

/// <summary>The words on the hover card for the showing under the pointer.</summary>
public static class FilmstripReadout
{
    private const int MaxListedFrames = 8;

    /// <summary>
    /// The card for <paramref name="visit"/> inside <paramref name="cell"/> of a chain with <paramref name="frameCount"/>
    /// frames whose block starts at <paramref name="blockStart"/> seconds. A merged sliver group lists its frames, and a
    /// doubled ping-pong end cell says which half the showing is.
    /// </summary>
    public static FilmstripCard At(string chain, int frameCount, double blockStart, FilmstripCell cell, FilmstripVisit visit)
    {
        var lines = new List<string>
        {
            $"{visit.Frame + 1} / {frameCount}",
            Seconds(visit.End - visit.Start),
            $"at {Seconds(blockStart + visit.Start)}",
        };
        if (cell.IsMerged)
        {
            var frames = cell.Visits.Select(v => v.Frame + 1).Distinct().ToList();
            string list = string.Join(", ", frames.Take(MaxListedFrames));
            lines.Add($"merged: {list}{(frames.Count > MaxListedFrames ? " …" : "")}");
        }
        else if (cell.Visits.Count > 1)
        {
            lines.Add(visit.IsReturn ? "return" : "forward");
        }
        return new FilmstripCard(chain, visit.Frame, lines);
    }

    private static string Seconds(double value) => value.ToString("0.00#", CultureInfo.InvariantCulture) + " s";
}
