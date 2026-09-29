using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.IO;

/// <summary>What already sits at the destination a texture would be copied to.</summary>
public enum TextureCopyConflict
{
    None,
    /// <summary>A file with the same name and the same bytes; copying would change nothing.</summary>
    IdenticalFileExists,
    /// <summary>A different file with the same name; copying would replace it.</summary>
    DifferentFileExists,
}

/// <summary>The user's answer to the "does not share a folder" prompt.</summary>
public enum TextureCopyChoice
{
    Cancel,
    /// <summary>Reference the texture where it is.</summary>
    KeepInPlace,
    /// <summary>Copy next to the .achx; never replaces a different file.</summary>
    Copy,
    /// <summary>Copy next to the .achx, replacing the different same-named file there.</summary>
    Overwrite,
    /// <summary>Reference the same-named file already next to the .achx, copying nothing.</summary>
    UseExisting,
}

/// <summary>
/// A pending "copy this texture next to the .achx" decision. <see cref="Choices"/> lists what the
/// prompt offers, first entry being the safe default for Enter.
/// </summary>
public sealed record TextureCopyPlan(string SourcePath, string DestinationPath, TextureCopyConflict Conflict)
{
    public IReadOnlyList<TextureCopyChoice> Choices => Conflict == TextureCopyConflict.DifferentFileExists
        ? new[] { TextureCopyChoice.KeepInPlace, TextureCopyChoice.Overwrite, TextureCopyChoice.UseExisting }
        : new[] { TextureCopyChoice.Copy, TextureCopyChoice.KeepInPlace };
}

/// <summary>
/// Determines whether the user should be prompted to copy a texture file next to the
/// loaded .achx.  Mirrors the "ask to copy" dialog logic from the WinForms
/// AnimationEditor.
/// </summary>
public static class TextureCopyDecider
{
    /// <summary>
    /// Returns <see langword="true"/> when assigning <paramref name="texturePath"/> should show the
    /// "copy file?" dialog, given the editor's current project state. No prompt when the .achx has
    /// never been saved (nowhere to copy to yet), when the texture already sits under the .achx's
    /// own folder, or when the .achx and the texture are both under the open project folder — a
    /// project is shared as a whole, so a path relative to it is already portable.
    /// </summary>
    public static bool ShouldPromptToCopyForProject(IProjectManager projectManager, string? texturePath)
    {
        if (string.IsNullOrEmpty(projectManager.FileName)) return false;

        var achxFolder = new FilePath(projectManager.FileName).GetDirectoryContainingThis().FullPath;
        return ShouldPromptToCopy(texturePath, achxFolder, projectManager.ProjectFolderPath);
    }

    /// <summary>
    /// The copy decision for assigning <paramref name="texturePath"/>, or null when no prompt is
    /// needed (see <see cref="ShouldPromptToCopyForProject"/>).
    /// </summary>
    public static TextureCopyPlan? PlanCopyForProject(IProjectManager projectManager, string texturePath)
    {
        if (!ShouldPromptToCopyForProject(projectManager, texturePath)) return null;

        var achxFolder = new FilePath(projectManager.FileName!).GetDirectoryContainingThis().FullPath;
        return PlanCopy(texturePath, achxFolder);
    }

    /// <summary>
    /// Plans copying <paramref name="sourcePath"/> into <paramref name="achxFolder"/> under the same
    /// file name, reading the destination from disk to detect a same-named file.
    /// </summary>
    public static TextureCopyPlan PlanCopy(string sourcePath, string achxFolder)
    {
        string destination = Path.Combine(achxFolder, Path.GetFileName(sourcePath));
        var conflict = !File.Exists(destination) ? TextureCopyConflict.None
            : HasSameBytes(sourcePath, destination) ? TextureCopyConflict.IdenticalFileExists
            : TextureCopyConflict.DifferentFileExists;
        return new TextureCopyPlan(sourcePath, destination, conflict);
    }

    /// <summary>
    /// Carries out <paramref name="choice"/> and returns the absolute path the frame should now
    /// reference, or null for <see cref="TextureCopyChoice.Cancel"/>. <see cref="TextureCopyChoice.Copy"/>
    /// never replaces a file: if one appeared after planning, it throws <see cref="IOException"/>.
    /// </summary>
    public static string? Apply(TextureCopyPlan plan, TextureCopyChoice choice)
    {
        switch (choice)
        {
            case TextureCopyChoice.Cancel:
                return null;
            case TextureCopyChoice.KeepInPlace:
                return plan.SourcePath;
            case TextureCopyChoice.UseExisting:
                return plan.DestinationPath;
            case TextureCopyChoice.Overwrite:
                File.Copy(plan.SourcePath, plan.DestinationPath, overwrite: true);
                return plan.DestinationPath;
            case TextureCopyChoice.Copy:
                if (plan.Conflict != TextureCopyConflict.IdenticalFileExists)
                    File.Copy(plan.SourcePath, plan.DestinationPath, overwrite: false);
                return plan.DestinationPath;
            default:
                throw new ArgumentOutOfRangeException(nameof(choice), choice, null);
        }
    }

    private static bool HasSameBytes(string a, string b)
    {
        if (new FileInfo(a).Length != new FileInfo(b).Length) return false;
        return File.ReadAllBytes(a).AsSpan().SequenceEqual(File.ReadAllBytes(b));
    }

    /// <summary>
    /// Returns <see langword="true"/> when the user should be shown a "copy file?"
    /// dialog — i.e., the texture lives outside <paramref name="folder"/>.
    /// </summary>
    /// <param name="texturePath">
    /// Absolute or relative path of the texture the user selected.
    /// If null or empty the texture has not been set, so no copy is needed.
    /// </param>
    /// <param name="folder">
    /// Absolute path of the folder the texture may already live in.  If null or empty,
    /// no folder context is available and the prompt should always be shown.
    /// </param>
    public static bool ShouldPromptToCopy(string? texturePath, string? folder)
    {
        if (string.IsNullOrEmpty(texturePath)) return false;
        if (string.IsNullOrEmpty(folder)) return true;

        return !IsInside(texturePath, folder);
    }

    /// <summary>
    /// Overload that also lets <paramref name="projectFolder"/> suppress the prompt — but only when
    /// <paramref name="achxFolder"/> is inside it too. An .achx outside the project would store a
    /// "../.." climb to reach the project's textures, which is what the prompt exists to avoid.
    /// </summary>
    public static bool ShouldPromptToCopy(string? texturePath, string? achxFolder, string? projectFolder)
    {
        if (!ShouldPromptToCopy(texturePath, achxFolder)) return false;
        if (string.IsNullOrEmpty(achxFolder) || string.IsNullOrEmpty(projectFolder)) return true;

        return !(IsInside(texturePath!, projectFolder) && IsInside(achxFolder, projectFolder));
    }

    /// <summary>
    /// Path-prefix containment, tolerant of mixed separators, casing, and trailing separators.
    /// A path equal to the folder counts as inside so a folder can be tested against itself.
    /// </summary>
    private static bool IsInside(string path, string folder)
    {
        // Normalise separators so forward- and back-slash variants compare equally.
        char sep = Path.DirectorySeparatorChar;
        string normPath   = path.Replace('/', sep).Replace('\\', sep).TrimEnd(sep);
        string normFolder = folder.Replace('/', sep).Replace('\\', sep).TrimEnd(sep);

        return normPath.Equals(normFolder, StringComparison.OrdinalIgnoreCase)
            || normPath.StartsWith(normFolder + sep, StringComparison.OrdinalIgnoreCase);
    }
}
