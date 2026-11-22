namespace AnimationEditor.Editing;

/// <summary>Why an editing operation failed, for callers that must tell failures apart.</summary>
public enum EditErrorCode
{
    /// <summary>A failure with no dedicated code; only the message describes it.</summary>
    Unspecified,
    /// <summary>The texture file does not exist.</summary>
    TextureNotFound,
    /// <summary>The texture file exists but is not a decodable image.</summary>
    TextureUndecodable,
    /// <summary>Copying the texture into the project folder failed.</summary>
    TextureCopyFailed,
    /// <summary>The caller declined an interactive choice (for example the copy-into-project prompt).</summary>
    Cancelled,
    /// <summary>A rename was given an empty name that cannot be applied.</summary>
    EmptyName,
    /// <summary>A rename collides with another chain of the file or another shape of the frame.</summary>
    DuplicateName,
    /// <summary>A file to create is already on disk and overwriting was not asked for.</summary>
    AlreadyExists,
    /// <summary>A target index is outside the list it addresses.</summary>
    IndexOutOfRange,
    /// <summary>The chain is locked in the editor, so its frames and its removal are protected.</summary>
    Locked,
    /// <summary>A bulk selector (range, list, wildcard or glob) matched nothing.</summary>
    NoMatch,
    /// <summary>A named item (sequence, track, source, chain) is not there.</summary>
    NotFound,
    /// <summary>A value is out of its allowed range (a negative start, a zero length).</summary>
    InvalidValue,
    /// <summary>A source cannot be removed because blocks still use it.</summary>
    SourceInUse,
    /// <summary>The edit would make sequences play each other in a loop.</summary>
    WouldCycle,
    /// <summary>A block op hit a track of the wrong kind (a chain op on a reference track, or the reverse).</summary>
    WrongTrackKind,
    /// <summary>A .tsx cannot be opened as a native project (the compatibility checker's reason is the message), or is already a sync target.</summary>
    UnsupportedTsx,
    /// <summary>The edit has no representation in a Tiled tile animation, so the editor disables it for a .tsx too.</summary>
    NotSupportedInTsx,
    /// <summary>A .tsx-only op ran on an .achx/.achj, or an .achx/.achj-only op ran on a .tsx.</summary>
    WrongFileKind,
    /// <summary>Two chains of a .tsx map to the same owner tile, so only one could be written; give one its own (set-chain-owner-tile).</summary>
    DuplicateOwnerTile,
    /// <summary>A .tsx chain's frames do not line up with whole tiles of the tileset, so it cannot be written back.</summary>
    UnmappableChain,
    /// <summary>An address is malformed: wrong number of parts, a non-numeric index, an unknown shape type, or a name containing '/'.</summary>
    InvalidAddress,
    /// <summary>A shape op addressed a shape of the wrong type (set-rect on a circle).</summary>
    WrongShapeType,
    /// <summary>The file kind or content is not supported for this command (wrong extension, legacy UV coordinates, a texture whose size cannot be read).</summary>
    UnsupportedFile,
    /// <summary>Reading or writing a file failed (unreadable, unparsable, or a write error).</summary>
    IoFailed,
    /// <summary>A name matches more than one item, so it cannot be addressed; rename them first.</summary>
    Ambiguous,
    /// <summary>The editor's own command refused the edit (a keyframe rule, an unchanged-state rule, a library rule).</summary>
    Refused,
    /// <summary>The command line or ops file is malformed (bad JSON, unknown op or field, a missing argument); the CLI exits 2.</summary>
    UsageError,
    /// <summary>The edits applied, but the result would have a check error, so nothing was written.</summary>
    WouldBreakFile,
}

/// <summary>
/// Structured outcome of an editing operation. <see cref="Success"/> is true when there are no
/// <see cref="Errors"/>; warnings and changed items never affect it.
/// </summary>
/// <param name="ChangedItems">What the operation wrote or modified (file paths for file operations).</param>
/// <param name="Errors">Failures, one human-readable line each.</param>
/// <param name="Warnings">Non-fatal notes, such as an item that was skipped.</param>
/// <param name="ErrorCodes">Codes parallel to <paramref name="Errors"/>, by index; missing entries mean
/// <see cref="EditErrorCode.Unspecified"/>.</param>
public sealed record EditResult(
    IReadOnlyList<string> ChangedItems,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<EditErrorCode>? ErrorCodes = null)
{
    public bool Success => Errors.Count == 0;

    /// <summary>The code of the error at <paramref name="index"/>.</summary>
    public EditErrorCode CodeAt(int index) =>
        ErrorCodes is not null && index < ErrorCodes.Count ? ErrorCodes[index] : EditErrorCode.Unspecified;

    /// <summary>True when any error carries <paramref name="code"/>.</summary>
    public bool HasError(EditErrorCode code) => ErrorCodes?.Contains(code) == true;
}
