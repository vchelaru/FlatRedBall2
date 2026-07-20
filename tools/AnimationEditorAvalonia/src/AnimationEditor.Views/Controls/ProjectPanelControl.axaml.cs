using AnimationEditor.Core.IO;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Displays the recursively-discovered <c>.achx</c> tree for a picked project folder (#770).
/// Platform-agnostic: everything it renders comes from <see cref="AchxFolderScanner"/> /
/// <see cref="AchxFolderTreeBuilder"/> over <see cref="IEditorFolder"/>, so this same control is
/// shared unmodified by desktop (real filesystem) and the browser build (native folder handle).
/// </summary>
public partial class ProjectPanelControl : UserControl
{
    private IReadOnlyList<AchxFileEntry> _allEntries = Array.Empty<AchxFileEntry>();

    public ObservableCollection<AchxTreeNodeVm> TreeRoots { get; } = new();

    /// <summary>Raised when the user clicks a file row.</summary>
    public event Action<AchxFileEntry>? FileSelected;

    public ProjectPanelControl()
    {
        InitializeComponent();
        DataContext = this;
        ExcludeBinObjCheck.IsCheckedChanged += (_, _) => Rebuild();
        ProjectTree.SelectionChanged += OnTreeSelectionChanged;
        Rebuild();
    }

    /// <summary>
    /// Replaces the scanned entries (e.g. after a fresh Open Project Folder pick) and rebuilds
    /// the tree respecting the current "Exclude bin/obj" checkbox state. Pass every entry
    /// unfiltered -- toggling the checkbox re-filters this cached list rather than re-scanning.
    /// </summary>
    public void SetEntries(IReadOnlyList<AchxFileEntry> entries)
    {
        _allEntries = entries;
        Rebuild();
    }

    public void Clear() => SetEntries(Array.Empty<AchxFileEntry>());

    private void Rebuild()
    {
        TreeRoots.Clear();

        var excludeBinObj = ExcludeBinObjCheck.IsChecked == true;
        var files = excludeBinObj
            ? _allEntries.Where(f => !BinObjPathFilter.IsExcluded(f.RelativePath)).ToList()
            : _allEntries.ToList();

        EmptyMessage.IsVisible = files.Count == 0;
        ProjectTree.IsVisible = files.Count > 0;
        EmptyMessage.Text = _allEntries.Count == 0
            ? "File → Open Project Folder… to browse its .achx files."
            : "No .achx files match the current filter.";

        foreach (var node in AchxFolderTreeBuilder.Build(files))
            TreeRoots.Add(AchxTreeNodeVm.FromNode(node));
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ProjectTree.SelectedItem is AchxTreeNodeVm { IsFile: true, Entry: { } entry })
            FileSelected?.Invoke(entry);
    }
}

/// <summary>Tree node view-model for <see cref="ProjectPanelControl"/>'s <c>TreeView</c>.</summary>
public sealed class AchxTreeNodeVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;

    public string Name { get; }
    public AchxFileEntry? Entry { get; }
    public bool IsFolder => Entry is null;
    public bool IsFile => Entry is not null;
    public ObservableCollection<AchxTreeNodeVm> Children { get; } = new();

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
        }
    }

    private AchxTreeNodeVm(string name, AchxFileEntry? entry)
    {
        Name = name;
        Entry = entry;
    }

    public static AchxTreeNodeVm FromNode(AchxTreeNode node)
    {
        var vm = new AchxTreeNodeVm(node.Name, node.Entry);
        foreach (var child in node.Children)
            vm.Children.Add(FromNode(child));
        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
