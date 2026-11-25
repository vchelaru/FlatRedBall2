using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Utilities;
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
        if (Resolved(projectManager, texturePath, out var plan) is { } done) return done;

        return ApplyChoice(plan!, await chooser(plan!));
    }

    /// <summary>
    /// <see cref="ResolveForProjectAsync"/> with the answer already known, such as a retry after a failed copy
    /// or a non-interactive caller's fixed policy.
    /// </summary>
    public static TextureResolveResult ResolveForProject(
        IProjectManager projectManager, string texturePath, TextureCopyChoice choice)
    {
        if (Resolved(projectManager, texturePath, out var plan) is { } done) return done;

        return ApplyChoice(plan!, choice);
    }

    /// <summary>
    /// The result when <paramref name="texturePath"/> needs no copy, or when a file <paramref name="plan"/> would
    /// compare cannot be read; null with the plan to carry out otherwise.
    /// </summary>
    internal static TextureResolveResult? Resolved(IProjectManager projectManager, string texturePath, out TextureCopyPlan? plan)
    {
        plan = null;
        try
        {
            plan = TextureCopyDecider.PlanCopyForProject(projectManager, texturePath);
            return plan is null ? new TextureResolveResult(texturePath, Ok()) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return CopyFailed(ex);
        }
    }

    internal static TextureResolveResult ApplyChoice(TextureCopyPlan plan, TextureCopyChoice choice)
    {
        try
        {
            var path = TextureCopyDecider.Apply(plan, choice);
            return path is null
                ? new TextureResolveResult(null, Fail(EditErrorCode.Cancelled, "Cancelled"))
                : new TextureResolveResult(path, Ok());
        }
        catch (Exception ex)
        {
            return CopyFailed(ex);
        }
    }

    private static TextureResolveResult CopyFailed(Exception ex) =>
        new(null, Fail(EditErrorCode.TextureCopyFailed, $"Could not copy: {ex.Message}"));

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
            if (toClear.Count == 0) return Ok();
            if (projectManager.IsNativeTsxProject) return Fail(EditErrorCode.NotSupportedInTsx, TsxEdit.OtherImageRefusal);
            commands.SetFrameTextureName(toClear, string.Empty);
            return Ok();
        }

        string achxFolder = AchxFolder(projectManager);
        string absolutePath = TexturePathHelper.ResolveDisplayPath(input, achxFolder);
        string storePath = TexturePathHelper.ComputeStorePath(absolutePath, achxFolder);

        var toChange = frames
            .Where(f => TexturePathHelper.ComputeDisplayPath(f.TextureName, projectManager.FileName) != input)
            .ToList();
        if (toChange.Count == 0) return Ok();

        return Commit(projectManager, commands, toChange, absolutePath, storePath, probe);
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
        return Commit(projectManager, commands, frames, absolutePath, storePath, probe);
    }

    // The name is stored only if the image is usable, so no broken reference reaches the undo stack or the saved file.
    private static EditResult Commit(
        IProjectManager projectManager, IAppCommands commands, IReadOnlyList<AnimationFrameSave> frames,
        string absolutePath, string storePath, ITextureProbe probe)
    {
        if (TsxEdit.CheckImage(projectManager, absolutePath) is { } otherImage) return otherImage;
        if (!File.Exists(absolutePath))
            return Fail(EditErrorCode.TextureNotFound, $"Could not load image: {absolutePath}");
        if (!probe.CanDecode(absolutePath))
            return Fail(EditErrorCode.TextureUndecodable, $"Could not load image: {absolutePath}");

        if (CheckTexture(projectManager, commands, absolutePath, frames) is { } refused) return refused;

        commands.SetFrameTextureName(frames, storePath);
        return new EditResult([storePath], [], []);
    }

    /// <summary>
    /// The refusal when frames of the open file cannot take <paramref name="absolutePath"/>: it is not the open .tsx's
    /// image, or a frame would have to show the whole of it and its size cannot be read, so no region can be stored.
    /// Null when there is nothing to refuse. <paramref name="frames"/> are the frames taking the texture (only those with
    /// no region yet need its size); null for a new frame, which always does outside a .tsx.
    /// </summary>
    public static EditResult? CheckTexture(
        IProjectManager projectManager, IAppCommands commands, string absolutePath, IEnumerable<AnimationFrameSave>? frames = null)
    {
        if (projectManager.IsNativeTsxProject) return TsxEdit.CheckImage(projectManager, absolutePath);
        if (frames is not null && !frames.Any(f => FrameRegion.IsUnset(f, commands.GetTextureSize))) return null;
        return commands.GetTextureSize(absolutePath) is null
            ? Fail(EditErrorCode.UnsupportedFile,
                $"A frame showing the whole texture needs the texture's size, which could not be read: {absolutePath}. Use a PNG.")
            : null;
    }

    private static string AchxFolder(IProjectManager projectManager) =>
        string.IsNullOrEmpty(projectManager.FileName)
            ? string.Empty
            : Path.GetDirectoryName(projectManager.FileName) ?? string.Empty;

    private static EditResult Ok() => new([], [], []);

    private static EditResult Fail(EditErrorCode code, string message) => new([], [message], [], [code]);
}
