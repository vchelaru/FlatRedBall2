using System.Collections.Generic;

namespace AnimationEditor.Core.CommandsAndState.Commands
{
    /// <summary>
    /// One row's worth of History-panel ordering/marking metadata. Hosts map this into their
    /// own themed row view model (desktop uses brushes, browser uses opacity/font weight).
    /// </summary>
    /// <param name="IsCurrent">True for the most-recently-applied undo entry ("you are here").</param>
    /// <param name="IsRedo">True for entries that have been undone and are pending redo.</param>
    public readonly record struct HistoryRow(string Description, bool IsCurrent, bool IsRedo);

    /// <summary>
    /// Builds the ordered, marked History-panel row sequence shared by every editor host.
    /// </summary>
    public static class HistoryRowBuilder
    {
        /// <summary>
        /// Orders <paramref name="undoHistory"/> oldest-first (its last entry marked
        /// <see cref="HistoryRow.IsCurrent"/>), then appends <paramref name="redoHistory"/>
        /// in next-to-redo-first order, marked <see cref="HistoryRow.IsRedo"/>.
        /// </summary>
        public static IEnumerable<HistoryRow> BuildRows(
            IReadOnlyList<IUndoableCommand> undoHistory,
            IReadOnlyList<IUndoableCommand> redoHistory)
        {
            for (int i = 0; i < undoHistory.Count; i++)
                yield return new HistoryRow(undoHistory[i].Description, IsCurrent: i == undoHistory.Count - 1, IsRedo: false);

            foreach (var cmd in redoHistory)
                yield return new HistoryRow(cmd.Description, IsCurrent: false, IsRedo: true);
        }
    }
}
