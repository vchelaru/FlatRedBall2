namespace AnimationEditor.App.Models;

/// <summary>View model for a single row in the History panel.</summary>
/// <param name="Description">Human-readable description of the command.</param>
/// <param name="Foreground">Hex colour for the text (muted for redo items).</param>
internal sealed record HistoryEntryVm(string Description, string Foreground);
