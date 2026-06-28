using Avalonia.Media;

namespace AnimationEditor.App.Models;

/// <summary>View model for a single row in the History panel. Brushes are resolved from
/// theme tokens by the panel builder (see <c>RefreshHistoryPanel</c>) so every row stays
/// legible in both light and dark themes — nothing here is a hard-coded colour.</summary>
/// <param name="Description">Human-readable description of the command.</param>
/// <param name="Foreground">Text brush: full-strength ink for applied rows, muted ink for redo
/// rows, or the on-accent colour for the current entry (so it contrasts with the accent fill).</param>
/// <param name="Background">Row fill: the theme accent for the current entry, transparent otherwise.</param>
/// <param name="IsCurrent">True for the most-recent undo entry (the "you are here" marker).</param>
internal sealed record HistoryEntryVm(string Description, IBrush Foreground, IBrush Background, bool IsCurrent = false);
