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
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AnimationEditor.App.Controls;

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
    private PointerPressedEventArgs? _dragPressEvent;
    private string? _dragPath;
    private PngFilesTreeNodeVm? _dragSourceNode;
    private PngFilesTreeNodeVm? _contextNode;

    public ObservableCollection<PngFilesTreeNodeVm> TreeRoots { get; } = new();

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

        var contextMenu = new ContextMenu();
        contextMenu.Opening += OnTreeContextMenuOpening;
        FilesTree.ContextMenu = contextMenu;
    }

    public void Initialize(ThumbnailService thumbnailService, Window ownerWindow, Action<string>? showError = null)
    {
        _thumbnailService = thumbnailService;
        _ownerWindow = ownerWindow;
        _showError = showError;
    }

    public void Refresh(string? achxFolder)
    {
        TreeRoots.Clear();

        if (_thumbnailService is null)
        {
            SetEmptyMessage(null, visible: false);
            return;
        }

        var files = PngFolderScanner.ListFiles(achxFolder);
        if (files.Count == 0)
        {
            SetEmptyMessage(
                string.IsNullOrEmpty(achxFolder)
                    ? "Save the .achx to browse folder PNGs."
                    : "No PNG files in this folder.",
                visible: true);
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

    private void OnFolderExpanderPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(sender as Control).Properties.IsLeftButtonPressed)
            return;

        if (GetNodeVmFromSource(sender) is not { IsFolder: true } node)
            return;

        node.IsExpanded = !node.IsExpanded;
        e.Handled = true;
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
    public bool IsFolderClosed => !_isExpanded;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded == value) return;
            _isExpanded = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsFolderOpen));
            OnPropertyChanged(nameof(IsFolderClosed));
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
