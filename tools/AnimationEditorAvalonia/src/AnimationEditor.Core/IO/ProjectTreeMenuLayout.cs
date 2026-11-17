using System;
using System.Collections.Generic;
using AnimationEditor.Core.CommandsAndState;

namespace AnimationEditor.Core.IO;

/// <summary>An item in the Animations tab's right-click menu on a folder or file row.</summary>
public enum ProjectTreeMenuCommand
{
    NewAnimationFile,
    NewSequenceFile,
    ConvertFolderToAchj,
    RevealFolder,
    OpenContainingFolder,
    CopyFullPath,
    Duplicate,
    ConvertFileToAchj,
    Delete,
}

/// <summary>
/// Which commands each row kind's menu offers, as groups the view separates with a divider. A
/// group that would be empty is left out, so the view never draws a doubled or dangling divider.
/// </summary>
public static class ProjectTreeMenuLayout
{
    /// <param name="hasConvertibleAchx">See <see cref="ProjectFolderConvertRule"/>.</param>
    public static IReadOnlyList<IReadOnlyList<ProjectTreeMenuCommand>> ForFolder(bool hasConvertibleAchx)
    {
        var groups = new List<IReadOnlyList<ProjectTreeMenuCommand>>
        {
            new[] { ProjectTreeMenuCommand.NewAnimationFile, ProjectTreeMenuCommand.NewSequenceFile },
        };
        if (hasConvertibleAchx)
            groups.Add(new[] { ProjectTreeMenuCommand.ConvertFolderToAchj });
        groups.Add(new[] { ProjectTreeMenuCommand.RevealFolder, ProjectTreeMenuCommand.CopyFullPath });
        return groups;
    }

    /// <summary>
    /// Both menus share one order: create, edit/convert, location (reveal + copy path), then
    /// delete last.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<ProjectTreeMenuCommand>> ForFile(string fileName)
    {
        var edit = new List<ProjectTreeMenuCommand> { ProjectTreeMenuCommand.Duplicate };
        if (fileName.EndsWith(".achx", StringComparison.OrdinalIgnoreCase))
            edit.Add(ProjectTreeMenuCommand.ConvertFileToAchj);

        return new IReadOnlyList<ProjectTreeMenuCommand>[]
        {
            edit,
            new[] { ProjectTreeMenuCommand.OpenContainingFolder, ProjectTreeMenuCommand.CopyFullPath },
            new[] { ProjectTreeMenuCommand.Delete },
        };
    }

    /// <summary>The Images tab's menu: just the location group, same commands and order as the Animations tab.</summary>
    public static IReadOnlyList<ProjectTreeMenuCommand> ForImageRow(bool isFolder) => new[]
    {
        isFolder ? ProjectTreeMenuCommand.RevealFolder : ProjectTreeMenuCommand.OpenContainingFolder,
        ProjectTreeMenuCommand.CopyFullPath,
    };

    /// <summary>The icon the ANIMATIONS tree's menu gives the matching item.</summary>
    public static TreeMenuIcon Icon(ProjectTreeMenuCommand command) => command switch
    {
        ProjectTreeMenuCommand.NewAnimationFile => TreeMenuIcon.Animation,
        ProjectTreeMenuCommand.NewSequenceFile => TreeMenuIcon.Sequence,
        ProjectTreeMenuCommand.ConvertFolderToAchj or ProjectTreeMenuCommand.ConvertFileToAchj => TreeMenuIcon.Replace,
        ProjectTreeMenuCommand.RevealFolder or ProjectTreeMenuCommand.OpenContainingFolder => TreeMenuIcon.RevealFile,
        ProjectTreeMenuCommand.CopyFullPath => TreeMenuIcon.CopyName,
        ProjectTreeMenuCommand.Duplicate => TreeMenuIcon.Duplicate,
        ProjectTreeMenuCommand.Delete => TreeMenuIcon.Delete,
        _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
    };
}
