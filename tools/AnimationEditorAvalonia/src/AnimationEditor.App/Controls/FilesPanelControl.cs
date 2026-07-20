using AnimationEditor.App.Services;
using AnimationEditor.Core.IO;
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
using System.Runtime.CompilerServices;

namespace AnimationEditor.App.Controls;

/// <summary>Which PNGs the Files panel lists (issue #615).</summary>
public enum FilesPanelScope
{
    /// <summary>Every PNG under the browse root (the historical behavior).</summary>
    Project,
    /// <summary>Only the PNGs referenced by the open .achx.</summary>
    ThisFile,
}

/// <summary>
/// Tree of PNG thumbnails from the .achx folder for drag-to-assign and reveal-in-explorer.
/// </summary>
public partial class FilesPanelControl : UserControl
{
    private const int ThumbnailSize = 28;
    private const double DragThreshold = 4;

    private ThumbnailService? _thumbnailService;
    private Window? _ownerWindow;
    private Action<string>? _showError;
    private Action<string>? _openPng;
    private PointerPressedEventArgs? _dragPressEvent;
    private string? _dragPath;
    private PngFilesTreeNodeVm? _dragSourceNode;
    private PngFilesTreeNodeVm? _contextNode;

    // Last inputs from Refresh, cached so the scope toggle can rebuild without a round-trip.
    private string? _filesRoot;
    private IReadOnlyList<string> _referencedTextureNames = Array.Empty<string>();
    private string? _achxFolder;

    public ObservableCollection<PngFilesTreeNodeVm> TreeRoots { get; } = new();

    /// <summary>The active scope. The owner refreshes referenced-texture data when this changes.</summary>
    public FilesPanelScope Scope { get; private set; } = FilesPanelScope.Project;

    /// <summary>
    /// Raised when the user flips the scope toggle. The owner (MainWindow) handles this by
    /// re-invoking <see cref="Refresh"/> with the current referenced textures, so "This File"
    /// always reflects the live .achx rather than a stale snapshot.
    /// </summary>
    public event EventHandler? ScopeChanged;

    public FilesPanelControl()
    {
        InitializeComponent();
        DataContext = this;

        FilesTree.AddHandler(InputElement.PointerPressedEvent, OnTreePointerPressed,
            RoutingStrategies.Tunnel);
        FilesTree.AddHandler(InputElement.PointerMovedEvent, OnTreePointerMoved,
            RoutingStrategies.Tunnel);
        FilesTree.AddHandler(InputElement.PointerReleasedEvent, OnTreePointerReleased,
            RoutingStrategies.Tunnel);
        FilesTree.SelectionChanged += (_, _) => ClearTreeSelection();

        ScopeProjectRadio.IsCheckedChanged += OnScopeRadioChanged;
        ScopeThisFileRadio.IsCheckedChanged += OnScopeRadioChanged;

        var contextMenu = new ContextMenu();
        contextMenu.Opening += OnTreeContextMenuOpening;
        FilesTree.ContextMenu = contextMenu;
    }

    private void OnScopeRadioChanged(object? sender, RoutedEventArgs e)
    {
        var scope = ScopeThisFileRadio.IsChecked == true
            ? FilesPanelScope.ThisFile
            : FilesPanelScope.Project;
        if (scope == Scope)
            return;

        Scope = scope;
        // The owner (MainWindow) handles ScopeChanged synchronously by re-invoking Refresh with
        // the current referenced textures, which rebuilds the tree — so no rebuild here.
        ScopeChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Initialize(ThumbnailService thumbnailService, Window ownerWindow,
        Action<string>? showError = null, Action<string>? openPng = null)
    {
        _thumbnailService = thumbnailService;
        _ownerWindow = ownerWindow;
        _showError = showError;
        _openPng = openPng;
    }

    /// <param name="filesRoot">Root folder to browse (from <c>ProjectManager.ResolveFilesPanelRoot</c>).</param>
    /// <param name="referencedTextureNames">
    /// Texture names referenced by the open .achx (from <c>TextureListBuilder.GetAvailableTextures</c>),
    /// used only in <see cref="FilesPanelScope.ThisFile"/> scope. Pass fresh values on every call so
    /// the scoped list stays current.
    /// </param>
    /// <param name="achxFolder">The .achx's directory, used to resolve the relative texture names.</param>
    public void Refresh(string? filesRoot, IReadOnlyList<string> referencedTextureNames, string? achxFolder)
    {
        _filesRoot = filesRoot;
        _referencedTextureNames = referencedTextureNames;
        _achxFolder = achxFolder;
        Rebuild();
    }

    private void Rebuild()
    {
        TreeRoots.Clear();

        if (_thumbnailService is null)
        {
            SetEmptyMessage(null, visible: false);
            return;
        }

        var allFiles = PngFolderScanner.ListFiles(_filesRoot);
        if (allFiles.Count == 0)
        {
            SetEmptyMessage(
                string.IsNullOrEmpty(_filesRoot)
                    ? "Save the .achx to browse folder PNGs."
                    : "No PNG files in this folder.",
                visible: true);
            return;
        }

        var files = Scope == FilesPanelScope.ThisFile
            ? FilesPanelScopeFilter.FilterToReferenced(allFiles, _referencedTextureNames, _achxFolder)
            : allFiles;

        if (files.Count == 0)
        {
            // Only reachable in ThisFile scope — Project scope shows every scanned file.
            SetEmptyMessage("This .achx references no PNGs in this folder.", visible: true);
            return;
        }

        SetEmptyMessage(null, visible: false);
        foreach (var node in PngFolderTreeBuilder.Build(files))
            TreeRoots.Add(PngFilesTreeNodeVm.FromNode(node, _thumbnailService, ThumbnailSize));
    }

    private void SetEmptyMessage(string? text, bool visible)
    {
        EmptyMessage.Text = text ?? string.Empty;
        EmptyMessage.IsVisible = visible;
        FilesTree.IsVisible = !visible;
    }

    private void ClearTreeSelection()
    {
        if (FilesTree.SelectedItems.Count == 0)
            return;

        FilesTree.SelectedItems.Clear();
    }

    private void OnFolderExpanderToggled(object? sender, EventArgs e)
    {
        if (GetNodeVmFromSource(sender) is not { IsFolder: true } node)
            return;

        node.IsExpanded = !node.IsExpanded;
        ClearTreeSelection();
    }

    private void OnTreeContextMenuOpening(object? sender, CancelEventArgs e)
    {
        if (FilesTree.ContextMenu is null)
            return;

        FilesTree.ContextMenu.Items.Clear();

        if (_contextNode is not { IsFile: true, AbsolutePath: { } path })
            return;

        var revealItem = new MenuItem { Header = "View in Explorer" };
        revealItem.Click += (_, _) => RevealInExplorer(path);
        FilesTree.ContextMenu.Items.Add(revealItem);
    }

    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(FilesTree).Properties;

        if (props.IsRightButtonPressed)
        {
            _contextNode = GetNodeVmFromSource(e.Source);
            return;
        }

        if (!props.IsLeftButtonPressed)
            return;

        if (GetNodeVmFromSource(e.Source) is not { IsFile: true, AbsolutePath: { } path } sourceNode)
            return;

        // Double-click opens the PNG in a tab. Detected here via ClickCount rather than the
        // DoubleTapped event: the pointer capture below (for drag-to-assign) cancels Avalonia's
        // tap-gesture recognizer, so DoubleTapped never fires on this tree.
        if (e.ClickCount >= 2)
        {
            _dragSourceNode = null;
            ClearPendingDrag();
            _openPng?.Invoke(path);
            e.Handled = true;
            return;
        }

        _dragSourceNode = sourceNode;
        _dragPressEvent = e;
        _dragPath = path;
        e.Pointer.Capture(FilesTree);
    }

    private async void OnTreePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_dragPressEvent is null || _dragPath is null || _dragSourceNode is null)
            return;
        if (!e.GetCurrentPoint(FilesTree).Properties.IsLeftButtonPressed)
            return;

        var start = _dragPressEvent.GetPosition(FilesTree);
        var current = e.GetPosition(FilesTree);
        if (Math.Abs(current.X - start.X) < DragThreshold &&
            Math.Abs(current.Y - start.Y) < DragThreshold)
            return;

        var path = _dragPath;
        var pressEvent = _dragPressEvent;
        var sourceNode = _dragSourceNode;
        ClearPendingDrag();
        e.Pointer.Capture(null);

        if (_ownerWindow is null)
            return;

        sourceNode.IsDragging = true;
        try
        {
            var storageItem = await _ownerWindow.StorageProvider.TryGetFileFromPathAsync(path);
            if (storageItem is null)
                return;

            var data = new DataTransfer();
            data.Add(DataTransferItem.CreateFile(storageItem));
            await DragDrop.DoDragDropAsync(pressEvent, data, DragDropEffects.Copy);
        }
        finally
        {
            sourceNode.IsDragging = false;
            if (ReferenceEquals(_dragSourceNode, sourceNode))
                _dragSourceNode = null;
        }
    }

    private void OnTreePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_dragSourceNode?.IsDragging == true)
            return;

        _dragSourceNode = null;
        ClearPendingDrag();
    }

    private static PngFilesTreeNodeVm? GetNodeVmFromSource(object? source)
    {
        if (source is not Control control)
            return null;

        var item = control.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        return item?.DataContext as PngFilesTreeNodeVm;
    }

    private void ClearPendingDrag()
    {
        _dragPressEvent = null;
        _dragPath = null;
    }

    private void RevealInExplorer(string absolutePath)
    {
        var error = ShellExplorer.RevealFile(absolutePath);
        if (error is not null)
            _showError?.Invoke(error);
    }
}

public sealed class PngFilesTreeNodeVm : INotifyPropertyChanged
{
    private bool _isExpanded = true;
    private bool _isDragging;

    public string Name { get; }
    public string? AbsolutePath { get; }
    public string? PathHint { get; }
    public bool ShowPathHint => !string.IsNullOrEmpty(PathHint);
    public bool IsFolder => AbsolutePath is null;
    public bool IsFile => AbsolutePath is not null;
    public Bitmap? Thumbnail { get; }
    public ObservableCollection<PngFilesTreeNodeVm> Children { get; } = new();

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

    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (_isDragging == value) return;
            _isDragging = value;
            OnPropertyChanged();
        }
    }

    private PngFilesTreeNodeVm(string name, string? absolutePath, string? pathHint, Bitmap? thumbnail)
    {
        Name = name;
        AbsolutePath = absolutePath;
        PathHint = pathHint;
        Thumbnail = thumbnail;
    }

    public static PngFilesTreeNodeVm FromNode(PngFilesTreeNode node, ThumbnailService thumbnails, int thumbSize)
    {
        Bitmap? bitmap = node.IsFolder
            ? null
            : thumbnails.GetFullImageThumbnail(node.AbsolutePath, thumbSize, thumbSize);

        string? pathHint = null;
        if (!node.IsFolder && !string.IsNullOrEmpty(node.RelativePath))
        {
            int slash = node.RelativePath.LastIndexOf('/');
            if (slash >= 0)
                pathHint = node.RelativePath[..slash].Replace("/", " › ");
        }

        var vm = new PngFilesTreeNodeVm(node.Name, node.AbsolutePath, pathHint, bitmap);
        foreach (var child in node.Children)
            vm.Children.Add(FromNode(child, thumbnails, thumbSize));

        return vm;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
