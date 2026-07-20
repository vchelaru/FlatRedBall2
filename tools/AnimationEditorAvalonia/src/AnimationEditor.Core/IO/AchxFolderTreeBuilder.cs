using System;
using System.Collections.Generic;
using System.Linq;

namespace AnimationEditor.Core.IO;

/// <summary>
/// Builds a hierarchical folder tree from flat <c>.achx</c> entries (issue #770's Project tab).
/// Mirrors <see cref="PngFolderTreeBuilder"/>'s grouping/sorting logic.
/// </summary>
public static class AchxFolderTreeBuilder
{
    public static IReadOnlyList<AchxTreeNode> Build(IReadOnlyList<AchxFileEntry> files)
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
        private readonly List<AchxFileEntry> _files = new();

        public void Insert(string[] pathParts, AchxFileEntry file)
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

        public List<AchxTreeNode> ToSortedNodes()
        {
            var nodes = new List<AchxTreeNode>();

            foreach (var (name, folder) in _folders.OrderBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase))
            {
                nodes.Add(new AchxTreeNode
                {
                    Name = name,
                    Entry = null,
                    Children = folder.ToSortedNodes(),
                });
            }

            foreach (var file in _files.OrderBy(f => f.FileName, StringComparer.OrdinalIgnoreCase))
            {
                nodes.Add(new AchxTreeNode
                {
                    Name = file.FileName,
                    Entry = file,
                    Children = Array.Empty<AchxTreeNode>(),
                });
            }

            return nodes;
        }
    }
}

/// <summary>A folder or <c>.achx</c> file node in the Project tab tree. Folders have a null
/// <see cref="Entry"/>; file nodes carry the <see cref="AchxFileEntry"/> needed to load them.</summary>
public sealed class AchxTreeNode
{
    public required string Name { get; init; }
    public AchxFileEntry? Entry { get; init; }
    public IReadOnlyList<AchxTreeNode> Children { get; init; } = Array.Empty<AchxTreeNode>();
    public bool IsFolder => Entry is null;
}
