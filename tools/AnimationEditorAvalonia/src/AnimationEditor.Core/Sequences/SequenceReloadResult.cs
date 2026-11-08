namespace AnimationEditor.Core.Sequences;

/// <summary>What <see cref="SequenceDocument.ReloadFromDisk"/> did: whether the document's data changed, or why the file could not be read.</summary>
public readonly record struct SequenceReloadResult(bool Changed, string? Error);
