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
