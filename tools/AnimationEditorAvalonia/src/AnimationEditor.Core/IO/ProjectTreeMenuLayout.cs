using System;
using System.Collections.Generic;

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
        groups.Add(new[] { ProjectTreeMenuCommand.RevealFolder });
        return groups;
    }

    public static IReadOnlyList<IReadOnlyList<ProjectTreeMenuCommand>> ForFile(string fileName)
    {
        var edit = new List<ProjectTreeMenuCommand> { ProjectTreeMenuCommand.Duplicate };
        if (fileName.EndsWith(".achx", StringComparison.OrdinalIgnoreCase))
            edit.Add(ProjectTreeMenuCommand.ConvertFileToAchj);

        return new IReadOnlyList<ProjectTreeMenuCommand>[]
        {
            new[] { ProjectTreeMenuCommand.OpenContainingFolder, ProjectTreeMenuCommand.CopyFullPath },
            edit,
            new[] { ProjectTreeMenuCommand.Delete },
        };
    }
}
