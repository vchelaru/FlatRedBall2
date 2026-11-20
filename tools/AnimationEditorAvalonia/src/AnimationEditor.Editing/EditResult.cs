namespace AnimationEditor.Editing;

/// <summary>
/// Structured outcome of an editing operation. <see cref="Success"/> is true when there are no
/// <see cref="Errors"/>; warnings and changed items never affect it.
/// </summary>
/// <param name="ChangedItems">What the operation wrote or modified (file paths for file operations).</param>
/// <param name="Errors">Failures, one human-readable line each.</param>
/// <param name="Warnings">Non-fatal notes, such as an item that was skipped.</param>
public sealed record EditResult(
    IReadOnlyList<string> ChangedItems,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool Success => Errors.Count == 0;
}
