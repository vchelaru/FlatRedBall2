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
/// On success <see cref="EditResult.ChangedItems"/> holds the stored texture name.
/// </summary>
public static class FrameTextureEdit
{
    /// <summary>
    /// Brings <paramref name="texturePath"/> into the project: when it sits outside the .achx's folder
    /// (<see cref="TextureCopyDecider"/>), asks <paramref name="chooser"/> and carries the answer out.
    /// </summary>
    public static Task<TextureResolveResult> ResolveForProjectAsync(
        IProjectManager projectManager, string texturePath, TextureCopyChooser chooser) =>
        throw new NotImplementedException();

    /// <summary>
    /// Applies the text of the texture-name field to <paramref name="frames"/>: empty clears, otherwise the text
    /// is a path relative to the .achx or absolute. Frames already showing that path are left alone.
    /// </summary>
    public static EditResult SetFromText(
        IProjectManager projectManager, IAppCommands commands,
        IReadOnlyList<AnimationFrameSave> frames, string? text, ITextureProbe probe) =>
        throw new NotImplementedException();

    /// <summary>Resolves <paramref name="pickedPath"/> via <see cref="ResolveForProjectAsync"/>, then applies it.</summary>
    public static Task<EditResult> SetFromPickedAsync(
        IProjectManager projectManager, IAppCommands commands,
        IReadOnlyList<AnimationFrameSave> frames, string pickedPath, ITextureProbe probe, TextureCopyChooser chooser) =>
        throw new NotImplementedException();

    /// <summary>Applies an already-resolved absolute texture path to every frame as one undo step.</summary>
    public static EditResult SetResolved(
        IProjectManager projectManager, IAppCommands commands,
        IReadOnlyList<AnimationFrameSave> frames, string absolutePath, ITextureProbe probe) =>
        throw new NotImplementedException();
}
