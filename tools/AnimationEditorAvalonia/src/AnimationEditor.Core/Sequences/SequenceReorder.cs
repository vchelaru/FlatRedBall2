using System;
using System.Collections.Generic;

namespace AnimationEditor.Core.Sequences;

/// <summary>The result of dropping sequences on a gap: <c>Order[position]</c> is the old index of the sequence now at that position.</summary>
public sealed record SequenceMovePlan(int[] Order);

/// <summary>
/// Reordering sequences by dragging rows of the sequence list. The file's sequence order is the list order, so a drop
/// rewrites it. A gap (slot) runs from 0 (above the first row) to the sequence count (below the last).
/// </summary>
public static class SequenceReorder
{
    /// <summary>
    /// The gap a pointer at <paramref name="y"/> points at: a row's upper half points at the gap above it, its lower half
    /// at the gap below. <paramref name="rows"/> are each row's top and height in the pointer's coordinates, in list order.
    /// </summary>
    public static int SlotAt(IReadOnlyList<(double Top, double Height)> rows, double y)
    {
        for (int row = 0; row < rows.Count; row++)
            if (y < rows[row].Top + rows[row].Height / 2) return row;
        return rows.Count;
    }

    /// <summary>
    /// Where every sequence ends up when the <paramref name="moved"/> ones are dropped on gap <paramref name="slot"/> as one
    /// block (keeping their relative order, whatever order they are given in), or null when the drop changes nothing.
    /// </summary>
    public static SequenceMovePlan? Plan(int count, IReadOnlyList<int> moved, int slot)
    {
        var block = new SortedSet<int>();
        foreach (int index in moved)
            if (index >= 0 && index < count) block.Add(index);
        if (block.Count == 0) return null;

        slot = Math.Clamp(slot, 0, count);
        var order = new List<int>(count);
        bool inserted = false;
        for (int index = 0; index < count; index++)
        {
            // The block goes in front of the first row at or after the gap.
            if (!inserted && index >= slot)
            {
                order.AddRange(block);
                inserted = true;
            }
            if (!block.Contains(index)) order.Add(index);
        }
        if (!inserted) order.AddRange(block);

        for (int position = 0; position < count; position++)
            if (order[position] != position) return new SequenceMovePlan(order.ToArray());
        return null;
    }

    /// <summary>
    /// The gap that moves the <paramref name="indices"/> one row up (<paramref name="delta"/> -1) or down (+1) when passed to
    /// <see cref="Plan"/>, or null when the first of them is already at the top (or the last at the bottom). This is what Alt+Up
    /// and Alt+Down drop the selection on, so a key press and a drag go through the same move.
    /// </summary>
    public static int? StepSlot(int count, IReadOnlyList<int> indices, int delta)
    {
        int first = int.MaxValue, last = int.MinValue;
        foreach (int index in indices)
        {
            if (index < 0 || index >= count) continue;
            first = Math.Min(first, index);
            last = Math.Max(last, index);
        }
        if (last < 0) return null;
        // A gap sits above the row of the same number, so one below the last row is last + 2.
        return delta < 0 ? (first > 0 ? first - 1 : null) : (last < count - 1 ? last + 2 : null);
    }

    /// <summary>
    /// How far to scroll a list while a row is dragged with the pointer at <paramref name="y"/> in a viewport
    /// <paramref name="viewportHeight"/> tall: negative (up) within <paramref name="edge"/> of the top, positive within it of the
    /// bottom, ramping up to <paramref name="maxStep"/> at the edge itself, and 0 elsewhere.
    /// </summary>
    public static double AutoScrollDelta(double y, double viewportHeight, double edge, double maxStep)
    {
        // Two zones that would overlap in a short list shrink to half the height each.
        edge = Math.Min(edge, viewportHeight / 2);
        if (edge <= 0) return 0;
        if (y < edge) return -maxStep * Math.Min(1, (edge - y) / edge);
        if (y > viewportHeight - edge) return maxStep * Math.Min(1, (y - (viewportHeight - edge)) / edge);
        return 0;
    }
}
