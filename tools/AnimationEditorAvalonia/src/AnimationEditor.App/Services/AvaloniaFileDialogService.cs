using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AnimationEditor.App.Services;

internal sealed class AvaloniaFileDialogService : IFileDialogService
{
    private readonly Window _owner;

    public AvaloniaFileDialogService(Window owner) => _owner = owner;

    public async Task<string?> PickSaveFileAsync(string title, string defaultExtension, IReadOnlyList<FileTypeChoice> fileTypeChoices)
    {
        // One FilePickerFileType per choice (rather than one type with multiple patterns) so the
        // OS save dialog lets the user pick achj vs. achx from the "Save as type" dropdown, and
        // appends whichever extension is currently selected there.
        var orderedChoices = fileTypeChoices
            .OrderByDescending(c => c.Extension == defaultExtension)
            .ToArray();

        var file = await _owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = title,
            DefaultExtension = defaultExtension,
            FileTypeChoices = orderedChoices
                .Select(c => new FilePickerFileType(c.Description) { Patterns = new[] { $"*.{c.Extension}" } })
                .ToArray()
        });
        return file?.Path.LocalPath;
    }

    public async Task<string?> PickOpenFileAsync(string title, string defaultExtension, string fileTypeDescription)
    {
        var files = await _owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            FileTypeFilter = new[]
            {
                new FilePickerFileType(fileTypeDescription)
                {
                    Patterns = new[] { $"*.{defaultExtension}" }
                }
            }
        });
        return files.Count > 0 ? files[0].Path.LocalPath : null;
    }
}
