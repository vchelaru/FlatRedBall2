using System.Collections.Generic;
using System.Threading.Tasks;

namespace AnimationEditor.Core.IO;

/// <summary>One selectable entry in a save dialog's file-type dropdown, e.g. ("achj", "Animation Chain JSON (*.achj)").</summary>
public readonly record struct FileTypeChoice(string Extension, string Description);

/// <summary>
/// Abstracts native file-picker dialogs so that commands depending on them
/// (e.g. Save As) can be unit-tested by injecting a stub implementation.
/// </summary>
public interface IFileDialogService
{
    /// <summary>
    /// Show a save-file dialog offering every entry in <paramref name="fileTypeChoices"/> as a
    /// selectable file type, with <paramref name="defaultExtension"/> chosen initially. Returns
    /// the chosen path, or <c>null</c> if cancelled.
    /// </summary>
    Task<string?> PickSaveFileAsync(string title, string defaultExtension, IReadOnlyList<FileTypeChoice> fileTypeChoices);

    /// <summary>
    /// Show an open-file dialog. Returns the chosen path, or <c>null</c> if cancelled.
    /// </summary>
    Task<string?> PickOpenFileAsync(string title, string defaultExtension, string fileTypeDescription);
}
