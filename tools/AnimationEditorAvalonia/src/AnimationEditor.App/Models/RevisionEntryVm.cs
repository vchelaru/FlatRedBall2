namespace AnimationEditor.App.Models;

/// <summary>
/// View model for one row in the PNG Diff/Blame revision list (issue #606). <see cref="Index"/> maps
/// the row back to its entry in the <c>PngBlameService</c> revision list so a selection can request
/// that revision's changed-region overlay.
/// </summary>
/// <param name="Index">Position in the service's revision list (0 = newest / working tree).</param>
/// <param name="Subject">Commit subject, or "Working tree (uncommitted)" for the synthetic entry.</param>
/// <param name="Meta">Secondary line: short hash and date, or "uncommitted" for the working-tree entry.</param>
internal sealed record RevisionEntryVm(int Index, string Subject, string Meta);
