using System;
using System.Collections.Generic;
using System.IO;

namespace AnimationEditor.Core.IO;

/// <summary>
/// The recursive file listing. A folder that cannot be listed (no access, deleted meanwhile) counts as empty, so it never
/// stops the listing of the rest.
/// </summary>
public static class FolderWalk
{
    /// <summary>Paths of the files in <paramref name="folder"/> and everything below it, spelled as <paramref name="folder"/> is.</summary>
    public static IEnumerable<string> Files(string folder)
    {
        var pending = new Queue<string>();
        pending.Enqueue(folder);
        while (pending.TryDequeue(out string? current))
        {
            foreach (FileSystemInfo entry in List(current))
            {
                string path = Path.Combine(current, entry.Name);
                if (entry is DirectoryInfo) pending.Enqueue(path);
                else yield return path;
            }
        }
    }

    private static FileSystemInfo[] List(string folder)
    {
        try
        {
            return new DirectoryInfo(folder).GetFileSystemInfos();
        }
        catch (Exception ex) when (IsUnlistable(ex))
        {
            return Array.Empty<FileSystemInfo>();
        }
    }

    /// <summary>Whether <paramref name="ex"/> is what listing a folder that cannot be listed throws.</summary>
    public static bool IsUnlistable(Exception ex) => ex is IOException or UnauthorizedAccessException;
}
