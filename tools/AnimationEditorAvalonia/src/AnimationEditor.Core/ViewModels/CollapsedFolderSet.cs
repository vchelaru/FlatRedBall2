using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace AnimationEditor.Core.ViewModels;

/// <summary>A folder-tree row whose expanded state <see cref="CollapsedFolderSet"/> can remember.</summary>
public interface ICollapsibleFolderNode : INotifyPropertyChanged
{
    /// <summary>
    /// Identifies the folder across tree rebuilds, e.g. its path relative to the tree's root.
    /// Null for rows that are not folders.
    /// </summary>
    string? FolderKey { get; }

    bool IsExpanded { get; set; }

    IEnumerable<ICollapsibleFolderNode> ChildNodes { get; }
}

/// <summary>
/// Remembers which folders the user collapsed in a file tree that is rebuilt from scratch with
/// fresh view-models, e.g. after a file changes on disk (issues #1207, #1209). Call
/// <see cref="Track"/> after every rebuild: it restores each folder's state and records the
/// user's later expand/collapse toggles.
/// </summary>
public sealed class CollapsedFolderSet
{
    // Case-insensitive to match how the folder tree builders group folder names.
    private readonly HashSet<string> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ICollapsibleFolderNode> _tracked = new();

    public IReadOnlyCollection<string> Keys => _keys;

    /// <summary>Raised when a user toggle changes <see cref="Keys"/>. Not raised by <see cref="Load"/>.</summary>
    public event Action? Changed;

    /// <summary>Replaces the collapsed keys, e.g. from persisted settings. Does not touch tracked rows.</summary>
    public void Load(IEnumerable<string>? keys)
    {
        _keys.Clear();
        if (keys is not null) _keys.UnionWith(keys);
    }

    /// <summary>
    /// Stops tracking the previous rows, then sets <see cref="ICollapsibleFolderNode.IsExpanded"/> on
    /// every folder under <paramref name="roots"/> from <see cref="Keys"/> and tracks its later changes.
    /// Pass an empty list to stop tracking without restoring anything.
    /// </summary>
    public void Track(IEnumerable<ICollapsibleFolderNode> roots)
    {
        foreach (var node in _tracked)
            node.PropertyChanged -= OnNodePropertyChanged;
        _tracked.Clear();

        TrackRecursive(roots);
    }

    private void TrackRecursive(IEnumerable<ICollapsibleFolderNode> nodes)
    {
        foreach (var node in nodes)
        {
            if (node.FolderKey is not { } key) continue;

            node.IsExpanded = !_keys.Contains(key);
            node.PropertyChanged += OnNodePropertyChanged;
            _tracked.Add(node);
            TrackRecursive(node.ChildNodes);
        }
    }

    private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ICollapsibleFolderNode.IsExpanded)) return;
        if (sender is not ICollapsibleFolderNode { FolderKey: { } key } node) return;

        bool changed = node.IsExpanded ? _keys.Remove(key) : _keys.Add(key);
        if (changed) Changed?.Invoke();
    }
}
