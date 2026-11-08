namespace AnimationEditor.Core.Sequences;

/// <summary>What a reload from disk did: whether the document's data changed, why the file could not be read, or that the file is gone.</summary>
public readonly record struct SequenceReloadResult(bool Changed, string? Error, bool Deleted = false);
