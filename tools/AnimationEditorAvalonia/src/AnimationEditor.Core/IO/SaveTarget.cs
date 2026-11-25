using AnimationEditor.Core.Paths;
using System;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>Checks a path picked to save to. A save dialog's file types do not limit what the user types.</summary>
public static class SaveTarget
{
    /// <summary>
    /// Whether <paramref name="path"/>'s extension belongs to a type the editor opens (a document or an image) that is
    /// not one of <paramref name="ownExtensions"/>. No extension, or one the editor does not know, is not. A known
    /// extension followed by dots or spaces is never an own one: Windows drops them and writes the file without them.
    /// </summary>
    public static bool IsAnotherType(string path, params string[] ownExtensions)
    {
        string extension = new FilePath(path.TrimEnd('.', ' ')).Extension;
        return !ownExtensions.Contains(new FilePath(path).Extension)
            && OpenFileFilters.AnimationFiles.Concat(OpenFileFilters.Textures)
                .Any(filter => filter.Patterns.Contains("*." + extension, StringComparer.Ordinal));
    }

    /// <summary>The refusal shown for <paramref name="path"/>; <paramref name="kind"/> is what it may be, with its article ("a .tsx").</summary>
    public static string WrongTypeMessage(string path, string kind) =>
        $"{new FilePath(path).NoPath} is not {kind} file. Give it {kind} extension.";

    /// <summary>The refusal shown for a <paramref name="path"/> that another tab of the window holds.</summary>
    public static string OpenInAnotherTabMessage(string path) =>
        $"{new FilePath(path).NoPath} is open in another tab. Close that tab first.";

    /// <summary>The refusal shown for a .tsx <paramref name="path"/> that <paramref name="owner"/> pushes its animations to.</summary>
    public static string AssociatedTilesetMessage(string path, string owner) =>
        $"{new FilePath(path).NoPath} is an associated Tiled tileset of {new FilePath(owner).NoPath}, which writes its animations there. Pick another name.";

    /// <summary>The refusal shown when <paramref name="path"/> changed on disk after a save to it failed.</summary>
    public static string ChangedOnDiskMessage(string path) =>
        $"\"{new FilePath(path).NoPath}\" changed on disk, and this tab has edits that could not be saved. " +
        "Saving would overwrite that change. Use File > Reload from Disk to drop the edits, or Save As a different file.";

    /// <summary>The refusal shown when <paramref name="path"/> changed on disk into something that does not load.</summary>
    public static string UnreadableOnDiskMessage(string path, string reason) =>
        $"\"{new FilePath(path).NoPath}\" changed on disk and couldn't be reloaded ({reason}). " +
        "Saving would overwrite that change. Fix the file and use File > Reload from Disk to drop the edits, or Save As a different file.";
}
