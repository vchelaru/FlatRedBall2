using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace AnimationEditor.Core.Sequences;

/// <summary>The words on the hover card for an event flag, in the same card the filmstrip uses.</summary>
public static class EventReadout
{
    private const int MaxListedEvents = 8;

    /// <summary>
    /// The card for <paramref name="marker"/> on a chain with <paramref name="frameCount"/> frames whose block starts at
    /// <paramref name="blockStart"/> seconds. One event names itself, its frame and its time; a merged flag lists each.
    /// </summary>
    public static FilmstripCard At(string chain, int frameCount, double blockStart, EventMarker marker)
    {
        var first = marker.Occurrences[0];
        var lines = new List<string>();
        if (marker.Count == 1)
        {
            lines.Add(NameOf(first));
            lines.Add($"frame {first.Frame + 1} / {frameCount}");
            lines.Add($"at {Seconds(blockStart + first.Time)}");
            if (first.IsReturn) lines.Add("return");
        }
        else
        {
            lines.Add($"{marker.Count} events");
            foreach (var occurrence in marker.Occurrences.Take(MaxListedEvents))
                lines.Add($"{NameOf(occurrence)}: frame {occurrence.Frame + 1}, {Seconds(blockStart + occurrence.Time)}");
            if (marker.Count > MaxListedEvents) lines.Add("…");
        }
        return new FilmstripCard(chain, first.Frame, lines);
    }

    private static string NameOf(EventOccurrence occurrence) => occurrence.Name.Length == 0 ? "(unnamed)" : occurrence.Name;

    private static string Seconds(double value) => value.ToString("0.00#", CultureInfo.InvariantCulture) + " s";
}
