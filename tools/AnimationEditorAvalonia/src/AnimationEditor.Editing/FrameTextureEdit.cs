using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using FlatRedBall2.AnimationEditorCommon;

namespace AnimationEditor.Editing;

/// <summary>Decides whether a texture file is a usable image. The editor supplies the Skia decode; a CLI supplies its own.</summary>
public interface ITextureProbe
{
    bool CanDecode(string absolutePath);
}

/// <summary>Answers the copy-into-project prompt. A non-interactive caller returns a fixed choice.</summary>
public delegate Task<TextureCopyChoice> TextureCopyChooser(TextureCopyPlan plan);

/// <summary>The absolute path a texture should be referenced by, or null when <see cref="Result"/> failed.</summary>
public sealed record TextureResolveResult(string? AbsolutePath, EditResult Result);

/// <summary>
/// Changes the texture of frames (#1811). Validates the file, applies the copy-into-project
/// choice and stores the path relative to the .achx, as one undo step. Never touches selection.
/// On a change, <see cref="EditResult.ChangedItems"/> holds the stored texture name.
/// </summary>
public static class FrameTextureEdit
{
    /// <summary>
    /// Brings <paramref name="texturePath"/> into the project: when it sits outside the .achx's folder
    /// (<see cref="TextureCopyDecider"/>), asks <paramref name="chooser"/> and carries the answer out.
    /// Fails with <see cref="EditErrorCode.Cancelled"/> or <see cref="EditErrorCode.TextureCopyFailed"/>.
    /// </summary>
    public static async Task<TextureResolveResult> ResolveForProjectAsync(
        IProjectManager projectManager, string texturePath, TextureCopyChooser chooser)
    {
        var plan = TextureCopyDecider.PlanCopyForProject(projectManager, texturePath);
        if (plan is null) return new TextureResolveResult(texturePath, Ok());

        var choice = await chooser(plan);
        try
        {
            var path = TextureCopyDecider.Apply(plan, choice);
            return path is null
                ? new TextureResolveResult(null, Fail(EditErrorCode.Cancelled, "Cancelled"))
                : new TextureResolveResult(path, Ok());
        }
        catch (Exception ex)
        {
            return new TextureResolveResult(null, Fail(EditErrorCode.TextureCopyFailed, $"Could not copy: {ex.Message}"));
        }
    }

    /// <summary>
    /// Applies the text of the texture-name field to <paramref name="frames"/>: empty clears, otherwise the text
    /// is a path relative to the .achx or absolute. Frames already showing that path are left alone.
    /// </summary>
    public static EditResult SetFromText(
        IProjectManager projectManager, IAppCommands commands,
        IReadOnlyList<AnimationFrameSave> frames, string? text, ITextureProbe probe)
    {
        var input = text?.Trim() ?? string.Empty;

        // Clearing touches only frames that have a texture, so re-committing an empty field adds no undo entry.
        if (input.Length == 0)
        {
            var toClear = frames.Where(f => !string.IsNullOrEmpty(f.TextureName)).ToList();
            if (toClear.Count > 0) commands.SetFrameTextureName(toClear, string.Empty);
            return Ok();
        }

        string achxFolder = AchxFolder(projectManager);
        string absolutePath = TexturePathHelper.ResolveDisplayPath(input, achxFolder);
        string storePath = TexturePathHelper.ComputeStorePath(absolutePath, achxFolder);

        var toChange = frames
            .Where(f => TexturePathHelper.ComputeDisplayPath(f.TextureName, projectManager.FileName) != input)
            .ToList();
        if (toChange.Count == 0) return Ok();

        return Commit(commands, toChange, absolutePath, storePath, probe);
    }

    /// <summary>Resolves <paramref name="pickedPath"/> via <see cref="ResolveForProjectAsync"/>, then applies it.</summary>
    public static async Task<EditResult> SetFromPickedAsync(
        IProjectManager projectManager, IAppCommands commands,
        IReadOnlyList<AnimationFrameSave> frames, string pickedPath, ITextureProbe probe, TextureCopyChooser chooser)
    {
        var resolved = await ResolveForProjectAsync(projectManager, pickedPath, chooser);
        return resolved.AbsolutePath is null
            ? resolved.Result
            : SetResolved(projectManager, commands, frames, resolved.AbsolutePath, probe);
    }

    /// <summary>Applies an already-resolved absolute texture path to every frame as one undo step.</summary>
    public static EditResult SetResolved(
        IProjectManager projectManager, IAppCommands commands,
        IReadOnlyList<AnimationFrameSave> frames, string absolutePath, ITextureProbe probe)
    {
        string achxFolder = AchxFolder(projectManager);
        string storePath = achxFolder.Length == 0
            ? absolutePath
            : TexturePathHelper.ComputeStorePath(absolutePath, achxFolder);
        return Commit(commands, frames, absolutePath, storePath, probe);
    }

    // The name is stored only if the image is usable, so no broken reference reaches the undo stack or the saved file.
    private static EditResult Commit(
        IAppCommands commands, IReadOnlyList<AnimationFrameSave> frames,
        string absolutePath, string storePath, ITextureProbe probe)
    {
        if (!File.Exists(absolutePath))
            return Fail(EditErrorCode.TextureNotFound, $"Could not load image: {absolutePath}");
        if (!probe.CanDecode(absolutePath))
            return Fail(EditErrorCode.TextureUndecodable, $"Could not load image: {absolutePath}");

        commands.SetFrameTextureName(frames, storePath);
        return new EditResult([storePath], [], []);
    }

    private static string AchxFolder(IProjectManager projectManager) =>
        string.IsNullOrEmpty(projectManager.FileName)
            ? string.Empty
            : Path.GetDirectoryName(projectManager.FileName) ?? string.Empty;

    private static EditResult Ok() => new([], [], []);

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
