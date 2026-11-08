using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Paths;
using AnimationEditor.Core.ViewModels;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
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
/// Everything it renders comes from <see cref="AchxFolderScanner"/> /
/// <see cref="AchxFolderTreeBuilder"/> over <see cref="IEditorFolder"/>, so tests can drive it
/// with in-memory folders -- including first-frame thumbnails (issue #839), generated lazily via
/// <see cref="ProjectTreeThumbnailService"/> after <see cref="SetEntries"/>.
/// </summary>
public partial class ProjectPanelControl : UserControl
{
    private const int ThumbnailSize = 28;

    private IReadOnlyList<AchxFileEntry> _allEntries = Array.Empty<AchxFileEntry>();
    private IReadOnlyList<string> _allFolderPaths = Array.Empty<string>();
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

    /// <summary>
    /// Folders the user collapsed, keyed by <see cref="AchxTreeNodeVm.RelativePath"/>, kept across
    /// rebuilds (#1207). The host loads and persists it per project folder.
    /// </summary>
    public CollapsedFolderSet CollapsedFolders { get; } = new();

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
    /// Raised when the user picks "Reveal in File Manager" for a folder row (issue #841 follow-up).
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
    /// Raised when the user picks "Duplicate" for a file row (issue #1208). Carries the file's
    /// <see cref="AchxTreeNodeVm.RelativePath"/>; the host picks the copy's name and writes it.
    /// </summary>
    public event Action<string>? FileDuplicateRequested;

    /// <summary>
    /// Raised when the user picks "Convert to .achj" on an <c>.achx</c> file row (issue #1376).
    /// Carries the file's <see cref="AchxTreeNodeVm.RelativePath"/>; the host confirms and converts.
    /// </summary>
    public event Action<string>? FileConvertToAchjRequested;

    /// <summary>
    /// Raised when the user picks "Convert Animations to .achj" on a folder row (issue #1376).
    /// Carries the folder's <see cref="AchxTreeNodeVm.RelativePath"/>; the host converts every
    /// <c>.achx</c> under it.
    /// </summary>
    public event Action<string>? FolderConvertToAchjRequested;

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
    /// "new" means (<c>MainWindow</c> reuses its File → New flow).
    /// </summary>
    public event Action? NewAnimationRequested;

    /// <summary>
    /// Raised when the user commits a name for "New Animation File" or "New Sequence File" on a
    /// folder row (issue #1018); the file name's extension says which. The host creates the file and opens it -- this control only picks the name and the
    /// extension, and has no way to write anything itself. Never raised for a name that collides
    /// with a sibling: the inline editor stays open showing the error instead.
    /// </summary>
    public event Action<NewAnimationFileRequest>? NewAnimationFileRequested;

    /// <summary>
    /// True when the tree also lists folders with no animation files (#1332). The host restores
    /// it from settings; <see cref="ShowAllFoldersChanged"/> reports the user toggling it.
    /// </summary>
    public bool ShowAllFolders
    {
        get => ShowAllFoldersCheck.IsChecked == true;
        set => ShowAllFoldersCheck.IsChecked = value;
    }

    /// <summary>Raised when <see cref="ShowAllFolders"/> changes, with its new value.</summary>
    public event Action<bool>? ShowAllFoldersChanged;

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
        ShowAllFoldersCheck.IsCheckedChanged += (_, _) =>
        {
            Rebuild();
            ShowAllFoldersChanged?.Invoke(ShowAllFolders);
        };
        ProjectTree.SelectionChanged += OnTreeSelectionChanged;
        // Tunnel-phase, matching MainWindow.OnTreePointerPressed's ClickCount==2 pattern (#716):
        // TreeViewItem's own pointer handling toggles IsExpanded on the second click before a
        // Bubble-registered DoubleTapped handler on an ancestor would ever see it.
        ProjectTree.AddHandler(InputElement.PointerPressedEvent, OnProjectTreePointerPressed, RoutingStrategies.Tunnel);
        ProjectTree.AddHandler(InputElement.PointerMovedEvent, OnProjectTreePointerMoved, RoutingStrategies.Tunnel);
        ProjectTree.AddHandler(InputElement.PointerReleasedEvent, (_, _) => _dragPress = null, RoutingStrategies.Tunnel);

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
    /// <paramref name="folderPaths"/> is every scanned subfolder, shown only while
    /// <see cref="ShowAllFolders"/> is on (#1332).
    /// </summary>
    public void SetEntries(IReadOnlyList<AchxFileEntry> entries, IReadOnlyList<string>? folderPaths = null)
    {
        _allEntries = entries;
        _allFolderPaths = folderPaths ?? Array.Empty<string>();
        Rebuild();
    }

    /// <summary>Shows a <see cref="AchxFolderScanner.ScanProjectAsync"/> result.</summary>
    public void SetEntries(ProjectFolderScan scan) => SetEntries(scan.Files, scan.FolderPaths);

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
        files = RelativePathSearchFilter.Filter(files, f => f.RelativePath, _searchQuery).ToList();
        var extraFolders = ProjectTreeFolderFilter.Select(
            _allFolderPaths, ShowAllFolders, excludeBinObj, _searchQuery);

        // ProjectTree stays visible even with zero rows (issue #916): hiding it also hid its
        // right-click "New Animation" context menu, which is exactly what an empty project needs.
        EmptyMessage.IsVisible = files.Count == 0 && extraFolders.Count == 0;
        EmptyMessage.Text = _allEntries.Count == 0
            ? "File → Open Project Folder… to browse its .achx files."
            : string.IsNullOrWhiteSpace(_searchQuery)
                ? "No .achx files match the current filter."
                : "No .achx files match your search.";

        foreach (var node in AchxFolderTreeBuilder.Build(files, extraFolders))
            TreeRoots.Add(AchxTreeNodeVm.FromNode(node));

        // Search results show fully expanded -- a remembered collapse would hide the matches.
        CollapsedFolders.Track(string.IsNullOrWhiteSpace(_searchQuery) ? TreeRoots : Array.Empty<AchxTreeNodeVm>());

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
            // A .tsx or .seqj has no animation frames to preview -- it always shows its own icon,
            // so there's nothing for ProjectTreeThumbnailService to generate.
            if (node.IsFile && node.Entry!.HasAnimationFrames) results.Add(node);
            else if (!node.IsFile) CollectFileNodes(node.Children, results);
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
        if (_thumbnailService is null || !entry.HasAnimationFrames) return;

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

        _dragPress = null;
        if (props.IsLeftButtonPressed && e.ClickCount == 1
            && GetNodeVmFromSource(e.Source) is { IsFile: true, Entry: { } pressed })
            _dragPress = (e, pressed);

        if (!props.IsLeftButtonPressed || e.ClickCount != 2) return;

        if (GetNodeVmFromSource(e.Source) is not { IsFile: true, Entry: { } entry }) return;

        FileDoubleClicked?.Invoke(entry);
    }

    private const double DragThreshold = 4;

    // The press that may become a drag of a file row out of the tree, until the pointer moves far enough.
    private (PointerPressedEventArgs Event, AchxFileEntry Entry)? _dragPress;

    /// <summary>
    /// Maps a file row to its path on disk so it can be dragged out of the tree (into a sequence's library, say).
    /// Rows it returns null for, or every row while it is unset, don't drag.
    /// </summary>
    public Func<AchxFileEntry, string?>? DragPathFor { get; set; }

    // OS drag-drop, which a headless test can't drive: the drop side is tested instead.
    private async void OnProjectTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragPress is not var (press, entry)) return;
        if (!e.GetCurrentPoint(ProjectTree).Properties.IsLeftButtonPressed)
        {
            _dragPress = null;
            return;
        }
        var delta = e.GetPosition(ProjectTree) - press.GetPosition(ProjectTree);
        if (Math.Abs(delta.X) < DragThreshold && Math.Abs(delta.Y) < DragThreshold) return;
        _dragPress = null;
        if (DragPathFor?.Invoke(entry) is not { } path || TopLevel.GetTopLevel(this) is not { } top) return;
        var file = await top.StorageProvider.TryGetFileFromPathAsync(path);
        if (file is null) return;
        var data = new DataTransfer();
        data.Add(DataTransferItem.CreateFile(file));
        await DragDrop.DoDragDropAsync(press, data, DragDropEffects.Copy);
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
        if (_contextNode is null)
        {
            var newAnimationItem = new MenuItem { Header = "New Animation" };
            newAnimationItem.Click += (_, _) => NewAnimationRequested?.Invoke();
            ProjectTree.ContextMenu.Items.Add(newAnimationItem);
            return;
        }

        if (_contextNode is { IsFolder: true } folderNode)
        {
            // Convert only has something to act on when an .achx sits somewhere under the folder.
            // Checked against the scan the tree was built from, so opening the menu reads no disk.
            var hasConvertible = ProjectFolderConvertRule.HasConvertibleAchx(
                _allEntries.Select(f => f.RelativePath), folderNode.RelativePath);
            AddMenuGroups(ProjectTreeMenuLayout.ForFolder(hasConvertible), folderNode);
            return;
        }

        if (_contextNode is { IsFile: true } fileNode)
            AddMenuGroups(ProjectTreeMenuLayout.ForFile(fileNode.Entry!.FileName), fileNode);
    }

    private void AddMenuGroups(IReadOnlyList<IReadOnlyList<ProjectTreeMenuCommand>> groups, AchxTreeNodeVm node)
    {
        var items = ProjectTree.ContextMenu!.Items;
        for (int i = 0; i < groups.Count; i++)
        {
            if (i > 0) items.Add(new Separator());
            foreach (var command in groups[i])
            {
                var (header, onClick) = DescribeMenuCommand(command, node);
                var item = new MenuItem { Header = header };
                item.Click += (_, _) => onClick();
                items.Add(item);
            }
        }
    }

    private (string Header, Action OnClick) DescribeMenuCommand(ProjectTreeMenuCommand command, AchxTreeNodeVm node) =>
        command switch
        {
            // Issue #1018: create a new animation file inline in this folder.
            ProjectTreeMenuCommand.NewAnimationFile => ("New Animation File", () => BeginNewFile(node, sequence: false)),
            ProjectTreeMenuCommand.NewSequenceFile => ("New Sequence File", () => BeginNewFile(node, sequence: true)),
            ProjectTreeMenuCommand.ConvertFolderToAchj => ("Convert Animations to .achj", () => FolderConvertToAchjRequested?.Invoke(node.RelativePath)),
            ProjectTreeMenuCommand.RevealFolder => ("Reveal in File Manager", () => FolderRevealRequested?.Invoke(node.RelativePath)),
            // Same two items and headers as the document tab strip's context menu
            // (MainWindow.axaml.cs, #881/#884) for the equivalent open file.
            ProjectTreeMenuCommand.OpenContainingFolder => ("Open Containing Folder", () => FileRevealRequested?.Invoke(node.RelativePath)),
            ProjectTreeMenuCommand.CopyFullPath => ("Copy Full Path", () => FileCopyPathRequested?.Invoke(node.RelativePath)),
            ProjectTreeMenuCommand.Duplicate => ("Duplicate", () => FileDuplicateRequested?.Invoke(node.RelativePath)),
            ProjectTreeMenuCommand.ConvertFileToAchj => ("Convert to .achj", () => FileConvertToAchjRequested?.Invoke(node.RelativePath)),
            ProjectTreeMenuCommand.Delete => ("Delete", () => FileDeleteRequested?.Invoke(node.RelativePath)),
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, null),
        };

    /// <summary>
    /// Adds a placeholder row under <paramref name="folderNode"/> and puts it straight into inline
    /// edit mode (issue #1018). Nothing is written until the name is committed -- an abandoned edit
    /// leaves no file behind, so Escape only has to drop the row.
    /// </summary>
    private void BeginNewFile(AchxTreeNodeVm folderNode, bool sequence)
    {
        CancelPendingNewAnimationFile();

        string suggested;
        if (sequence)
        {
            suggested = NewAnimationFileNaming.SuggestSequenceFileName(SiblingFileNames(folderNode));
        }
        else
        {
            var extension = NewAnimationFileNaming.ResolveExtension(
                _allEntries
                    .Where(f => ExcludeBinObjCheck.IsChecked != true || !BinObjPathFilter.IsExcluded(f.RelativePath))
                    .Select(f => f.FileName));
            suggested = NewAnimationFileNaming.SuggestFileName(SiblingFileNames(folderNode), extension);
        }

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
        var result = string.Equals(extension, NewAnimationFileNaming.SequenceExtension, StringComparison.OrdinalIgnoreCase)
            ? NewAnimationFileNaming.ResolveSequence(_pendingNode.EditText, SiblingFileNames(_pendingParent))
            : NewAnimationFileNaming.Resolve(_pendingNode.EditText, SiblingFileNames(_pendingParent), extension);

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

/// <summary>An SVG icon a project tree row shows in place of a thumbnail (shared, one per kind).</summary>
public sealed class ProjectRowSvgIcon
{
    private ProjectRowSvgIcon(string path) => Path = path;

    public string Path { get; }

    public static ProjectRowSvgIcon Chain { get; } =
        new("avares://AnimationEditor.Views/Assets/icons/svg/IconChain.svg");

    public static ProjectRowSvgIcon Sequence { get; } =
        new("avares://AnimationEditor.Views/Assets/icons/svg/IconSequence.svg");
}

/// <summary>Marker for the Tiled icon on a <c>.tsx</c> project tree row.</summary>
public sealed class ProjectRowTsxIcon
{
    private ProjectRowTsxIcon() { }

    public static ProjectRowTsxIcon Instance { get; } = new();
}

/// <summary>Tree node view-model for <see cref="ProjectPanelControl"/>'s <c>TreeView</c>.</summary>
public sealed class AchxTreeNodeVm : ICollapsibleFolderNode
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
    /// "Reveal in File Manager" (issue #841 follow-up), since folder nodes carry no <see cref="Entry"/>.</summary>
    public string RelativePath { get; }

    public ObservableCollection<AchxTreeNodeVm> Children { get; } = new();

    string? ICollapsibleFolderNode.FolderKey => IsFolder ? RelativePath : null;
    IEnumerable<ICollapsibleFolderNode> ICollapsibleFolderNode.ChildNodes => Children;

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
            OnPropertyChanged(nameof(FileIconContent));
        }
    }

    /// <summary>
    /// What the folder-toggle slot of the row shows: the node itself for a folder (the row template
    /// maps it to a <see cref="FolderExpanderIcon"/>), null for every other row so it builds no control.
    /// </summary>
    public object? FolderToggleContent => IsFolder ? this : null;

    /// <summary>
    /// What the icon slot of the row shows, or null when it shows nothing (a thumbnail replaces the
    /// chain icon once it arrives). Each kind maps to one template, so a row builds only the icon
    /// control it needs: a hidden <c>Svg</c> still parses its file, which cost ~0.8 s per 130 rows.
    /// </summary>
    public object? FileIconContent =>
        Entry is null ? null
        : IsTsx ? ProjectRowTsxIcon.Instance
        : IsSequence ? ProjectRowSvgIcon.Sequence
        : ShowFallbackIcon ? ProjectRowSvgIcon.Chain
        : null;

    public bool HasThumbnail => _thumbnail is not null;

    /// <summary>True for a <c>.tsx</c> row -- shown with the Tiled icon instead of the
    /// chain icon/thumbnail, since a tileset has no animation frames to preview.</summary>
    public bool IsTsx => Entry?.IsTsx == true;

    /// <summary>True for a <c>.seqj</c> row, shown with the sequence icon and never a thumbnail.</summary>
    public bool IsSequence => Entry?.IsSequence == true;

    public bool ShowFallbackIcon => Entry?.HasAnimationFrames == true && _thumbnail is null;
    public bool ShowTsxIcon => IsTsx;
    public bool ShowSequenceIcon => IsSequence;

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
