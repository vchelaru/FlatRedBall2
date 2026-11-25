using AnimationEditor.Core.Models;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Core.Tests;

/// <summary>The one writer, <see cref="ProjectManager.WriteDocument"/>, without <c>AppCommands</c> around it.</summary>
internal static class ProjectManagerSaveExtensions
{
    /// <summary>Writes the open .achx/.achj.</summary>
    public static void SaveAnimationChainList(this IProjectManager pm, string targetPath) =>
        ProjectManager.WriteDocument(TabEditorCache.FromProject(pm, targetPath), targetPath);

    /// <summary>Writes a document that is not the open one.</summary>
    public static void SaveAnimationChainList(
        this IProjectManager _, AnimationChainListSave model, string targetPath, TextureCoordinateType diskFormat) =>
        ProjectManager.WriteDocument(
            new TabEntry(new FilePath(targetPath)) { CachedEditorModel = model, CachedOnDiskCoordinateType = diskFormat }, targetPath);

    /// <summary>Writes the open .tsx, to its own file by default. Does nothing when none is open.</summary>
    public static IReadOnlyList<string> SaveTsxProject(this IProjectManager pm, string? targetPath = null)
    {
        if (!pm.IsNativeTsxProject) return [];
        targetPath ??= pm.FileName!;
        TabEntry document = TabEditorCache.FromProject(pm, targetPath);
        var warnings = ProjectManager.WriteDocument(document, targetPath);
        pm.RestoreTsxState(document.CachedTsxState);
        return warnings;
    }
}
