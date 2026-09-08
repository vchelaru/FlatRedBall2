using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Builds a hierarchical folder tree from flat PNG file entries.
/// </summary>
public static class PngFolderTreeBuilder
{
    /// <param name="filesRoot">
    /// Absolute root the scan started from -- combined with each folder's path to give folder
    /// nodes an <see cref="PngFilesTreeNode.AbsolutePath"/> too (issue #1059: needed so "View in
    /// Explorer" works on a folder row, not just a file row). Pass null/empty only when no
    /// filesystem reveal will ever be attempted (e.g. a test that only checks names/hierarchy).
    /// </param>
    public static IReadOnlyList<PngFilesTreeNode> Build(IReadOnlyList<PngFileEntry> files, string? filesRoot = null)
    {
        var root = new BuilderNode();
        foreach (var file in files)
        {
            var parts = file.RelativePath.Replace('\\', '/').Split('/');
            root.Insert(parts, file);
        }

        return root.ToSortedNodes(filesRoot);
    }

    private sealed class BuilderNode
    {
        private readonly Dictionary<string, BuilderNode> _folders =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly List<PngFileEntry> _files = new();

        public void Insert(string[] pathParts, PngFileEntry file)
        {
            if (pathParts.Length == 1)
            {
                _files.Add(file);
                return;
            }

            string folderName = pathParts[0];
            if (!_folders.TryGetValue(folderName, out var child))
                _folders[folderName] = child = new BuilderNode();

            var remaining = pathParts.Length == 2
                ? new[] { pathParts[1] }
                : pathParts.Skip(1).ToArray();
            child.Insert(remaining, file);
        }

        public List<PngFilesTreeNode> ToSortedNodes(string? folderAbsolutePath)
        {
            var nodes = new List<PngFilesTreeNode>();

            foreach (var (name, folder) in _folders.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                var childAbsolutePath = folderAbsolutePath is null ? null : Path.Combine(folderAbsolutePath, name);
                nodes.Add(new PngFilesTreeNode
                {
                    Name = name,
                    IsFolder = true,
                    AbsolutePath = childAbsolutePath,
                    Children = folder.ToSortedNodes(childAbsolutePath),
                });
            }

            foreach (var file in _files.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase))
            {
                nodes.Add(new PngFilesTreeNode
                {
                    Name = file.FileName,
                    IsFolder = false,
                    AbsolutePath = file.AbsolutePath,
                    RelativePath = file.RelativePath,
                    Children = Array.Empty<PngFilesTreeNode>(),
                });
            }

            return nodes;
        }
    }
}

/// <summary>
/// A folder or PNG file node in the files-panel tree. <see cref="AbsolutePath"/> is populated for
/// both -- use <see cref="IsFolder"/>, not its nullability, to tell them apart (issue #1059: a
/// folder's path is needed too, for "View in Explorer").
/// </summary>
public sealed class PngFilesTreeNode
{
    public required string Name { get; init; }
    public required bool IsFolder { get; init; }
    public string? AbsolutePath { get; init; }
    public string? RelativePath { get; init; }
    public IReadOnlyList<PngFilesTreeNode> Children { get; init; } = Array.Empty<PngFilesTreeNode>();
}
