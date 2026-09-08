using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AnimationEditor.Views.Controls;

/// <summary>
/// Displays the recursively-discovered <c>.achx</c> tree for a picked project folder (#770).
/// Platform-agnostic: everything it renders comes from <see cref="AchxFolderScanner"/> /
/// <see cref="AchxFolderTreeBuilder"/> over <see cref="IEditorFolder"/>, so this same control is
/// shared unmodified by desktop (real filesystem) and the browser build (native folder handle) --
/// including first-frame thumbnails (issue #839), generated lazily via
/// <see cref="ProjectTreeThumbnailService"/> after <see cref="SetEntries"/>.
/// </summary>
public partial class ProjectPanelControl : UserControl
{
    private const int ThumbnailSize = 28;

    private IReadOnlyList<AchxFileEntry> _allEntries = Array.Empty<AchxFileEntry>();
    private string _searchQuery = string.Empty;
    private ProjectTreeThumbnailService? _thumbnailService;
    private CancellationTokenSource? _thumbnailLoadCts;
    private AchxTreeNodeVm? _contextNode;

    // The un-committed "New Animation File" row and the folder it sits under (issue #1018). Both
    // null whenever no inline edit is in progress; a Rebuild drops the row with the rest of the tree.
    private AchxTreeNodeVm? _pendingNode;
    private AchxTreeNodeVm? _pendingParent;

    // Guards against re-entrant SelectionChanged while a selection change is programmatic rather
    // than a real user click -- ClearSearchAndReveal restoring post-rebuild, or SyncSelectionToActiveFile
    // following the active tab (#910). Without it, either would re-fire FileSelected and re-navigate.
    private bool _isProgrammaticSelectionChange;

    public ObservableCollection<AchxTreeNodeVm> TreeRoots { get; } = new();

    /// <summary>The currently selected file entry, or null when no file row is selected.</summary>
    public AchxFileEntry? SelectedEntry => (ProjectTree.SelectedItem as AchxTreeNodeVm)?.Entry;

    /// <summary>Raised when the user clicks a file row.</summary>
    public event Action<AchxFileEntry>? FileSelected;

    /// <summary>
    /// Raised when the user double-clicks a file row (issue #841) -- promotes an existing
    /// preview tab for that file to a permanent one.
    /// </summary>
    public event Action<AchxFileEntry>? FileDoubleClicked;

    /// <summary>
    /// True when the host can reveal a folder in the OS shell (desktop only -- issue #654 dropped
    /// the equivalent "Open Containing Folder" on the browser build since there's no real
    /// filesystem to reveal). Desktop's <c>MainWindow</c> sets this after construction; left false
    /// (the default) the tree's context menu never shows "View in Explorer" for a folder row.
    /// </summary>
    public bool SupportsRevealInExplorer { get; set; }

    /// <summary>
    /// Raised when the user picks "View in Explorer" for a folder row (issue #841 follow-up).
    /// Carries the folder's <see cref="AchxTreeNodeVm.RelativePath"/> -- this control has no
    /// absolute path for a folder node, only the host (which knows the project root) can resolve
    /// one.
    /// </summary>
    public event Action<string>? FolderRevealRequested;

    /// <summary>
    /// Raised when the user picks "Open Containing Folder" for a file row (issue #886). Carries
    /// the file's <see cref="AchxTreeNodeVm.RelativePath"/> for the host to resolve, same as
    /// <see cref="FolderRevealRequested"/>.
    /// </summary>
    public event Action<string>? FileRevealRequested;

    /// <summary>
    /// Raised when the user picks "Copy Full Path" for a file row (issue #886). Carries the
    /// file's <see cref="AchxTreeNodeVm.RelativePath"/> for the host to resolve, same as
    /// <see cref="FolderRevealRequested"/>.
    /// </summary>
    public event Action<string>? FileCopyPathRequested;

    /// <summary>
    /// Raised when the user picks "Delete" for a file row (issue #919). Carries the file's
    /// <see cref="AchxTreeNodeVm.RelativePath"/> for the host to resolve, same as
    /// <see cref="FolderRevealRequested"/>. The host owns confirming and the actual
    /// recycle-bin move -- this control only reports the request.
    /// </summary>
    public event Action<string>? FileDeleteRequested;

    /// <summary>
    /// Raised when the user picks "New Animation" from the context menu shown on right-clicking
    /// blank space in the tree, i.e. no node under the cursor (issue #908). The host resolves what
    /// "new" means (desktop/browser both currently reuse their existing File → New flow).
    /// </summary>
    public event Action? NewAnimationRequested;

    /// <summary>
    /// Raised when the user commits a name for "New Animation File" on a folder row (issue
    /// #1018). The host creates the file and opens it -- this control only picks the name and the
    /// extension, and has no way to write anything itself. Never raised for a name that collides
    /// with a sibling: the inline editor stays open showing the error instead.
    /// </summary>
    public event Action<NewAnimationFileRequest>? NewAnimationFileRequested;

    /// <summary>
    /// Completes once every thumbnail from the most recent <see cref="Rebuild"/> has finished
    /// loading (or been cancelled by a newer one). Test seam for awaiting the async thumbnail
    /// load -- production code never needs to await this.
    /// </summary>
    public Task ThumbnailLoadTask { get; private set; } = Task.CompletedTask;

    public ProjectPanelControl()
    {
        InitializeComponent();
        DataContext = this;
        ExcludeBinObjCheck.IsCheckedChanged += (_, _) => Rebuild();
        ProjectTree.SelectionChanged += OnTreeSelectionChanged;
        // Tunnel-phase, matching MainWindow.OnTreePointerPressed's ClickCount==2 pattern (#716):
        // TreeViewItem's own pointer handling toggles IsExpanded on the second click before a
        // Bubble-registered DoubleTapped handler on an ancestor would ever see it.
        ProjectTree.AddHandler(InputElement.PointerPressedEvent, OnProjectTreePointerPressed, RoutingStrategies.Tunnel);

        var contextMenu = new ContextMenu();
        contextMenu.Opening += OnTreeContextMenuOpening;
        ProjectTree.ContextMenu = contextMenu;

        Rebuild();
    }

    /// <summary>
    /// Wires the thumbnail generator (issue #839). Not passed to the constructor because
    /// <see cref="AnimationEditor.Views"/> controls are constructed by XAML with no arguments;
    /// call this once, before the first real <see cref="SetEntries"/>.
    /// </summary>
    public void Initialize(ProjectTreeThumbnailService thumbnailService) =>
        _thumbnailService = thumbnailService;

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
        _pendingNode = null;
        _pendingParent = null;
        TreeRoots.Clear();

        var excludeBinObj = ExcludeBinObjCheck.IsChecked == true;
        var files = excludeBinObj
            ? _allEntries.Where(f => !BinObjPathFilter.IsExcluded(f.RelativePath)).ToList()
            : _allEntries.ToList();
        files = AchxSearchFilter.Filter(files, _searchQuery).ToList();

        // ProjectTree stays visible even with zero rows (issue #916): hiding it also hid its
        // right-click "New Animation" context menu, which is exactly what an empty project needs.
        EmptyMessage.IsVisible = files.Count == 0;
        EmptyMessage.Text = _allEntries.Count == 0
            ? "File → Open Project Folder… to browse its .achx files."
            : string.IsNullOrWhiteSpace(_searchQuery)
                ? "No .achx files match the current filter."
                : "No .achx files match your search.";

        foreach (var node in AchxFolderTreeBuilder.Build(files))
            TreeRoots.Add(AchxTreeNodeVm.FromNode(node));

        StartThumbnailLoad();
    }

    /// <summary>
    /// Kicks off thumbnail generation for every file row now in <see cref="TreeRoots"/>, a few at
    /// a time (the throttle lives in <see cref="ProjectTreeThumbnailService"/>) so rows fill in
    /// as they finish instead of blocking the tree. Cancels any load still running from a
    /// previous <see cref="Rebuild"/> (e.g. the user typed in search again) first, so a stale
    /// generation can't overwrite a node from the new tree.
    /// </summary>
    private void StartThumbnailLoad()
    {
        _thumbnailLoadCts?.Cancel();
        _thumbnailLoadCts?.Dispose();
        if (_thumbnailService is null) { ThumbnailLoadTask = Task.CompletedTask; return; }

        var cts = new CancellationTokenSource();
        _thumbnailLoadCts = cts;

        var fileNodes = new List<AchxTreeNodeVm>();
        CollectFileNodes(TreeRoots, fileNodes);

        ThumbnailLoadTask = Task.WhenAll(fileNodes.Select(node => LoadOneThumbnailAsync(node, cts.Token)));
    }

    private static void CollectFileNodes(IEnumerable<AchxTreeNodeVm> nodes, List<AchxTreeNodeVm> results)
    {
        foreach (var node in nodes)
        {
            if (node.IsFile) results.Add(node);
            else CollectFileNodes(node.Children, results);
        }
    }

    private async Task LoadOneThumbnailAsync(AchxTreeNodeVm node, CancellationToken cancellationToken)
    {
        Bitmap? thumbnail;
        try
        {
            thumbnail = await _thumbnailService!.GetThumbnailAsync(
                node.Entry!, ThumbnailSize, ThumbnailSize, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!cancellationToken.IsCancellationRequested)
            node.Thumbnail = thumbnail;
    }

    /// <summary>
    /// Re-generates the thumbnail for a single already-visible node after its file changed on
    /// disk (issue #839 follow-up: a save wasn't reflected in the tree at all). No-op if
    /// <paramref name="entry"/> isn't currently in the tree, or before <see cref="Initialize"/>.
    /// Callers fire-and-forget this; it's <c>async Task</c> only so tests can await it.
    /// </summary>
    public async Task InvalidateThumbnail(AchxFileEntry entry)
    {
        if (_thumbnailService is null) return;

        var node = FindNode(TreeRoots, entry);
        if (node is null) return;

        _thumbnailService.InvalidateEntry(entry);
        await LoadOneThumbnailAsync(node, CancellationToken.None);
    }

    private void OnSearchQueryChanged(object? sender, string query)
    {
        _searchQuery = query;
        Rebuild();
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isProgrammaticSelectionChange) return;
        if (ProjectTree.SelectedItem is not AchxTreeNodeVm { IsFile: true, Entry: { } entry }) return;

        FileSelected?.Invoke(entry);

        // The pick came from a filtered result -- clear the search so the tree returns to its
        // full contents, then re-select/reveal the same entry in it rather than leaving the
        // user looking at an empty selection in a suddenly-repopulated tree.
        if (!string.IsNullOrEmpty(_searchQuery))
            ClearSearchAndReveal(entry);
    }

    /// <summary>
    /// Selects the tree node for <paramref name="entry"/> (or clears selection when null) without
    /// raising <see cref="FileSelected"/> -- keeps this list in sync when the active tab changes
    /// some other way, e.g. clicking the tab strip (#910), as opposed to a click in this tree,
    /// which is what FileSelected exists for.
    /// </summary>
    public void SyncSelectionToActiveFile(AchxFileEntry? entry)
    {
        var match = entry is null ? null : FindNode(TreeRoots, entry);
        if (ReferenceEquals(ProjectTree.SelectedItem, match)) return;

        _isProgrammaticSelectionChange = true;
        try
        {
            ProjectTree.SelectedItem = match;
            if (match is not null)
                ProjectTree.ScrollIntoView(match);
        }
        finally
        {
            _isProgrammaticSelectionChange = false;
        }
    }

    private void ClearSearchAndReveal(AchxFileEntry entry)
    {
        ProjectSearchBox.Clear(); // synchronously fires QueryChanged("") -> Rebuild()

        var match = FindNode(TreeRoots, entry);
        if (match is null) return;

        _isProgrammaticSelectionChange = true;
        try
        {
            ProjectTree.SelectedItem = match;
            ProjectTree.ScrollIntoView(match);
        }
        finally
        {
            _isProgrammaticSelectionChange = false;
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

    private void OnProjectTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(ProjectTree).Properties;

        if (props.IsRightButtonPressed)
        {
            _contextNode = GetNodeVmFromSource(e.Source);
            return;
        }

        if (!props.IsLeftButtonPressed || e.ClickCount != 2) return;

        if (GetNodeVmFromSource(e.Source) is not { IsFile: true, Entry: { } entry }) return;

        FileDoubleClicked?.Invoke(entry);
    }

    private static AchxTreeNodeVm? GetNodeVmFromSource(object? source)
    {
        if (source is not Control src) return null;
        var tvi = src.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        return tvi?.DataContext as AchxTreeNodeVm;
    }

    private void OnTreeContextMenuOpening(object? sender, CancelEventArgs e)
    {
        if (ProjectTree.ContextMenu is null) return;
        ProjectTree.ContextMenu.Items.Clear();

        // No node under the cursor -- right-clicked blank space below/between rows (issue #908).
        // Independent of SupportsRevealInExplorer: that flag only gates filesystem-reveal items.
        if (_contextNode is null)
        {
            var newAnimationItem = new MenuItem { Header = "New Animation" };
            newAnimationItem.Click += (_, _) => NewAnimationRequested?.Invoke();
            ProjectTree.ContextMenu.Items.Add(newAnimationItem);
            return;
        }

        if (_contextNode is { IsFolder: true } folderNode)
        {
            // Not gated on SupportsRevealInExplorer: creating a file works on both hosts, only
            // the OS-shell reveal below is desktop-only (issue #1018).
            var newFileItem = new MenuItem { Header = "New Animation File" };
            newFileItem.Click += (_, _) => BeginNewAnimationFile(folderNode);
            ProjectTree.ContextMenu.Items.Add(newFileItem);

            if (!SupportsRevealInExplorer) return;

            var revealItem = new MenuItem { Header = "View in Explorer" };
            revealItem.Click += (_, _) => FolderRevealRequested?.Invoke(folderNode.RelativePath);
            ProjectTree.ContextMenu.Items.Add(revealItem);
            return;
        }

        if (!SupportsRevealInExplorer) return;

        if (_contextNode is { IsFile: true } fileNode)
        {
            // Same two items and headers as the document tab strip's context menu
            // (MainWindow.axaml.cs, #881/#884) for the equivalent open file.
            var openFolderItem = new MenuItem { Header = "Open Containing Folder" };
            openFolderItem.Click += (_, _) => FileRevealRequested?.Invoke(fileNode.RelativePath);
            ProjectTree.ContextMenu.Items.Add(openFolderItem);

            var copyPathItem = new MenuItem { Header = "Copy Full Path" };
            copyPathItem.Click += (_, _) => FileCopyPathRequested?.Invoke(fileNode.RelativePath);
            ProjectTree.ContextMenu.Items.Add(copyPathItem);

            ProjectTree.ContextMenu.Items.Add(new Separator());

            var deleteItem = new MenuItem { Header = "Delete" };
            deleteItem.Click += (_, _) => FileDeleteRequested?.Invoke(fileNode.RelativePath);
            ProjectTree.ContextMenu.Items.Add(deleteItem);
        }
    }

    /// <summary>
    /// Adds a placeholder row under <paramref name="folderNode"/> and puts it straight into inline
    /// edit mode (issue #1018). Nothing is written until the name is committed -- an abandoned edit
    /// leaves no file behind, so Escape only has to drop the row.
    /// </summary>
    private void BeginNewAnimationFile(AchxTreeNodeVm folderNode)
    {
        CancelPendingNewAnimationFile();

        var extension = NewAnimationFileNaming.ResolveExtension(
            _allEntries
                .Where(f => ExcludeBinObjCheck.IsChecked != true || !BinObjPathFilter.IsExcluded(f.RelativePath))
                .Select(f => f.FileName));
        var suggested = NewAnimationFileNaming.SuggestFileName(SiblingFileNames(folderNode), extension);

        _pendingNode = AchxTreeNodeVm.CreatePending(suggested, folderNode.RelativePath);
        _pendingParent = folderNode;
        folderNode.IsExpanded = true;
        folderNode.Children.Add(_pendingNode);
    }

    private void OnPendingNameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitPendingNewAnimationFile(); e.Handled = true; }
        else if (e.Key == Key.Escape) { CancelPendingNewAnimationFile(); e.Handled = true; }
    }

    // Clicking away abandons the new file rather than committing it -- a half-typed name is far
    // more likely than a deliberate commit-by-defocus, and the row is trivially re-created.
    private void OnPendingNameLostFocus(object? sender, RoutedEventArgs e) =>
        CancelPendingNewAnimationFile();

    // Every row's template carries this TextBox, collapsed -- an unguarded Focus() here would let
    // any newly-realized row steal focus (from the search box, say), not just the pending one.
    private void OnPendingNameAttached(object? sender, VisualTreeAttachmentEventArgs e)
    {
        if (sender is not TextBox { DataContext: AchxTreeNodeVm { IsEditing: true } } textBox) return;

        textBox.Focus();
        textBox.SelectAll();
    }

    private void CommitPendingNewAnimationFile()
    {
        if (_pendingNode is null || _pendingParent is null) return;

        var extension = new FilePath(_pendingNode.Name).Extension;
        var result = NewAnimationFileNaming.Resolve(
            _pendingNode.EditText, SiblingFileNames(_pendingParent), extension);

        if (result.Error is not null)
        {
            _pendingNode.ErrorMessage = result.Error;
            return;
        }

        var request = new NewAnimationFileRequest(_pendingParent.RelativePath, result.FileName!);
        CancelPendingNewAnimationFile();
        NewAnimationFileRequested?.Invoke(request);
    }

    private void CancelPendingNewAnimationFile()
    {
        if (_pendingNode is not null && _pendingParent is not null)
            _pendingParent.Children.Remove(_pendingNode);

        _pendingNode = null;
        _pendingParent = null;
    }

    // Only files directly inside the folder -- the collision that matters is a name clash on disk,
    // which is per-directory.
    private static IEnumerable<string> SiblingFileNames(AchxTreeNodeVm folderNode) =>
        folderNode.Children.Where(n => n.IsFile).Select(n => n.Name);

    private void OnFolderExpanderToggled(object? sender, EventArgs e)
    {
        if (sender is not Control control) return;
        var item = control.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (item?.DataContext is not AchxTreeNodeVm { IsFolder: true } node) return;

        node.IsExpanded = !node.IsExpanded;
    }
}

/// <summary>
/// A committed "New Animation File" name (issue #1018): the file to create, and the folder to
/// create it in as a project-root-relative, forward-slash-separated path (empty for the root).
/// </summary>
public readonly record struct NewAnimationFileRequest(string FolderRelativePath, string FileName);

/// <summary>Tree node view-model for <see cref="ProjectPanelControl"/>'s <c>TreeView</c>.</summary>
public sealed class AchxTreeNodeVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    private Bitmap? _thumbnail;
    private bool _isEditing;
    private string _editText = string.Empty;
    private string? _errorMessage;

    public string Name { get; }
    public AchxFileEntry? Entry { get; }

    /// <summary>
    /// True for the placeholder row a "New Animation File" edit adds (issue #1018). It has no
    /// <see cref="Entry"/> because nothing exists on disk yet, so it would otherwise read as a
    /// folder -- hence this flag rather than inferring from <see cref="Entry"/> alone.
    /// </summary>
    public bool IsPending { get; private init; }

    public bool IsFolder => Entry is null && !IsPending;
    public bool IsFile => Entry is not null;

    /// <summary>Name shown in the inline editor, without the extension.</summary>
    public string EditText
    {
        get => _editText;
        set
        {
            if (_editText == value) return;
            _editText = value;
            OnPropertyChanged();
        }
    }

    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsNotEditing));
        }
    }

    public bool IsNotEditing => !_isEditing;

    /// <summary>Why the typed name was rejected, shown beside the inline editor; null when valid.</summary>
    public string? ErrorMessage
    {
        get => _errorMessage;
        set
        {
            if (_errorMessage == value) return;
            _errorMessage = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => _errorMessage is not null;

    /// <summary>Path from the project root, forward-slash separated -- see
    /// <see cref="AchxTreeNode.RelativePath"/>. Used to resolve a folder row's absolute path for
    /// "View in Explorer" (issue #841 follow-up), since folder nodes carry no <see cref="Entry"/>.</summary>
    public string RelativePath { get; }

    public ObservableCollection<AchxTreeNodeVm> Children { get; } = new();

    public bool IsFolderOpen => _isExpanded;

    /// <summary>First-frame preview (issue #839), populated asynchronously after the row appears --
    /// null until <see cref="ProjectTreeThumbnailService"/> finishes (or if it can't produce one),
    /// in which case the row keeps showing the generic chain icon.</summary>
    public Bitmap? Thumbnail
    {
        get => _thumbnail;
        set
        {
            if (ReferenceEquals(_thumbnail, value)) return;
            _thumbnail = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasThumbnail));
            OnPropertyChanged(nameof(ShowFallbackIcon));
        }
    }

    public bool HasThumbnail => _thumbnail is not null;
    public bool ShowFallbackIcon => IsFile && _thumbnail is null;

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

    private AchxTreeNodeVm(string name, AchxFileEntry? entry, string relativePath)
    {
        Name = name;
        Entry = entry;
        RelativePath = relativePath;
    }

    /// <summary>
    /// The un-committed row a "New Animation File" edit starts on (issue #1018).
    /// <paramref name="suggestedFileName"/> carries the extension the project's convention
    /// resolved to; the editor itself only shows the stem.
    /// </summary>
    public static AchxTreeNodeVm CreatePending(string suggestedFileName, string folderRelativePath) =>
        new(suggestedFileName, entry: null, folderRelativePath)
        {
            IsPending = true,
            EditText = new FilePath(suggestedFileName).NoPathNoExtension,
            IsEditing = true,
        };

    public static AchxTreeNodeVm FromNode(AchxTreeNode node)
    {
        var vm = new AchxTreeNodeVm(node.Name, node.Entry, node.RelativePath);
        foreach (var child in node.Children)
            vm.Children.Add(FromNode(child));
        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
