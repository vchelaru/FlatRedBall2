using Avalonia.Media;

namespace AnimationEditor.App.Models;

/// <summary>View model for a single row in the History panel.</summary>
/// <param name="Description">Human-readable description of the command.</param>
/// <param name="Foreground">Hex colour for the text (muted for redo items).</param>
/// <param name="IsCurrent">True for the most-recent undo entry (the "you are here" marker).</param>
internal sealed record HistoryEntryVm(string Description, string Foreground, bool IsCurrent = false)
{
    /// <summary>Accent background for the current position; transparent for all other rows.</summary>
    public IBrush Background => IsCurrent ? new SolidColorBrush(Color.Parse("#d83a3a")) : Brushes.Transparent;
}
