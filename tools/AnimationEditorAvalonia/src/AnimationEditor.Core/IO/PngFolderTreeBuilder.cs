using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Builds a hierarchical folder tree from flat PNG file entries.
/// </summary>
public static class PngFolderTreeBuilder
{
    public static IReadOnlyList<PngFilesTreeNode> Build(IReadOnlyList<PngFileEntry> files)
    {
        var root = new BuilderNode();
        foreach (var file in files)
        {
            var parts = file.RelativePath.Replace('\\', '/').Split('/');
            root.Insert(parts, file);
        }

        return root.ToSortedNodes();
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

        public List<PngFilesTreeNode> ToSortedNodes()
        {
            var nodes = new List<PngFilesTreeNode>();

            foreach (var (name, folder) in _folders.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                nodes.Add(new PngFilesTreeNode
                {
                    Name = name,
                    AbsolutePath = null,
                    Children = folder.ToSortedNodes(),
                });
            }

            foreach (var file in _files.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase))
            {
                nodes.Add(new PngFilesTreeNode
                {
                    Name = file.FileName,
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
/// A folder or PNG file node in the files-panel tree. Folders have a null
/// <see cref="AbsolutePath"/>; file nodes carry the on-disk path for drag/reveal.
/// </summary>
public sealed class PngFilesTreeNode
{
    public required string Name { get; init; }
    public string? AbsolutePath { get; init; }
    public string? RelativePath { get; init; }
    public IReadOnlyList<PngFilesTreeNode> Children { get; init; } = Array.Empty<PngFilesTreeNode>();
    public bool IsFolder => AbsolutePath is null;
}
