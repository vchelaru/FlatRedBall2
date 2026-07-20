using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.VisualTree;
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
    private string _searchQuery = string.Empty;

    // Guards against re-entrant SelectionChanged while ClearSearchAndReveal restores the
    // selection post-rebuild -- without it, that restore would re-fire FileSelected for the
    // same entry a second time.
    private bool _isRestoringSelectionAfterSearchClear;

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
        files = AchxSearchFilter.Filter(files, _searchQuery).ToList();

        EmptyMessage.IsVisible = files.Count == 0;
        ProjectTree.IsVisible = files.Count > 0;
        EmptyMessage.Text = _allEntries.Count == 0
            ? "File → Open Project Folder… to browse its .achx files."
            : string.IsNullOrWhiteSpace(_searchQuery)
                ? "No .achx files match the current filter."
                : "No .achx files match your search.";

        foreach (var node in AchxFolderTreeBuilder.Build(files))
            TreeRoots.Add(AchxTreeNodeVm.FromNode(node));
    }

    private void OnSearchQueryChanged(object? sender, string query)
    {
        _searchQuery = query;
        Rebuild();
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isRestoringSelectionAfterSearchClear) return;
        if (ProjectTree.SelectedItem is not AchxTreeNodeVm { IsFile: true, Entry: { } entry }) return;

        FileSelected?.Invoke(entry);

        // The pick came from a filtered result -- clear the search so the tree returns to its
        // full contents, then re-select/reveal the same entry in it rather than leaving the
        // user looking at an empty selection in a suddenly-repopulated tree.
        if (!string.IsNullOrEmpty(_searchQuery))
            ClearSearchAndReveal(entry);
    }

    private void ClearSearchAndReveal(AchxFileEntry entry)
    {
        ProjectSearchBox.Clear(); // synchronously fires QueryChanged("") -> Rebuild()

        var match = FindNode(TreeRoots, entry);
        if (match is null) return;

        _isRestoringSelectionAfterSearchClear = true;
        try
        {
            ProjectTree.SelectedItem = match;
            ProjectTree.ScrollIntoView(match);
        }
        finally
        {
            _isRestoringSelectionAfterSearchClear = false;
        }
    }

    private static AchxTreeNodeVm? FindNode(IEnumerable<AchxTreeNodeVm> nodes, AchxFileEntry entry)
    {
        foreach (var node in nodes)
        {
            if (ReferenceEquals(node.Entry, entry)) return node;

            var found = FindNode(node.Children, entry);
            if (found is not null) return found;
        }
        return null;
    }

    private void OnFolderExpanderToggled(object? sender, EventArgs e)
    {
        if (sender is not Control control) return;
        var item = control.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (item?.DataContext is not AchxTreeNodeVm { IsFolder: true } node) return;

        node.IsExpanded = !node.IsExpanded;
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

    public bool IsFolderOpen => _isExpanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFolderOpen));
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
