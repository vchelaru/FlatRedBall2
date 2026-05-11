using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.DragDrop;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using FlatRedBall.Content.AnimationChain;
using FlatRedBall.Content.Math.Geometry;
using Newtonsoft.Json;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FilePath = FlatRedBall.IO.FilePath;
using StringFunctions = FlatRedBall.Utilities.StringFunctions;

namespace AnimationEditor.App;

public partial class MainWindow : Window
{
    private AppSettingsModel _appSettings = new();
    private bool _suppressPropRefresh;
    private bool _suppressTextureComboChanged;
    private bool _suppressZoomComboChanged;
    private bool _suppressPreviewZoomComboChanged;

    private FilePath SettingsFilePath =>
        (FilePath)(Path.GetDirectoryName(
            System.Reflection.Assembly.GetExecutingAssembly().Location) + "\\AESettings.json");

    public MainWindow()
    {
        InitializeComponent();
        WireframeCtrl.AttachScrollViewer(WireframeScrollViewer);

        WireAppCommands();
        LoadSettingsFile();
        WireMenuEvents();
        WireWireframeToolbar();
        WireWireframeControl();
        WirePreviewControls();
        WireTreeView();
        WirePropertyPanel();
        WirePlaybackControls();
        WireKeyboard();

        Opened += OnOpened;
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private void OnOpened(object? sender, EventArgs e)
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length >= 2 && File.Exists(args[1]))
        {
            LoadAnimationFile(args[1]);
        }
        else
        {
            ProjectManager.Self.AnimationChainListSave =
                new FlatRedBall.Content.AnimationChain.AnimationChainListSave();
        }
    }

    // ── AppCommands wiring ────────────────────────────────────────────────────

    private void WireAppCommands()
    {
        AppCommands.Self.DoOnUiThread = action => Dispatcher.UIThread.InvokeAsync(action);
        AppCommands.Self.ConfirmAsync = ShowConfirmDialogAsync;
        AppCommands.Self.PromptStringAsync = ShowStringInputDialogAsync;

        // File dialog service
        AppCommands.Self.FileDialogService = new Services.AvaloniaFileDialogService(this);
        AppCommands.Self.SaveAsCompleted  += path =>
        {
            _appSettings.AddFile(new FilePath(path));
            SaveSettingsFile();
            RefreshRecentFiles();
            UpdateTitle();
        };

        // Tree events — fully wired (WireTreeView connects these after tree is constructed)
        AppCommands.Self.RefreshTreeViewRequested           += () => Dispatcher.UIThread.InvokeAsync(RefreshTreeView);
        AppCommands.Self.RefreshChainNodeRequested          += c  => Dispatcher.UIThread.InvokeAsync(() => RefreshChainNode(c));
        AppCommands.Self.RefreshFrameNodeRequested          += f  => Dispatcher.UIThread.InvokeAsync(() => RefreshFrameNode(f));
        AppCommands.Self.RefreshAnimationFrameDisplayRequested += () => { };
        // RefreshWireframeRequested is handled by WireframeControl directly

        ApplicationEvents.Self.AchxLoaded               += HandleAchxLoaded;
        ApplicationEvents.Self.AnimationChainsChanged    += HandleAnimationChainsChanged;
        SelectedState.Self.SelectionChanged              += HandleSelectionChanged;
    }

    // ── Wireframe toolbar wiring ──────────────────────────────────────────────

    private void WireWireframeToolbar()
    {
        TextureCombo.SelectionChanged += OnTextureComboChanged;
        MagicWandToggle.IsCheckedChanged += OnMagicWandToggled;
        SnapToGridCheck.IsCheckedChanged += OnSnapToGridChanged;
        GridSizeInput.LostFocus += OnGridSizeInputLostFocus;
        GridSizePlusBtn.Click  += OnGridSizePlusBtnClick;
        GridSizeMinusBtn.Click += OnGridSizeMinusBtnClick;
        ZoomCombo.ItemsSource = _zoomPresetTexts;
        ZoomCombo.KeyDown += OnZoomComboKeyDown;
        ZoomCombo.LostFocus += OnZoomComboLostFocus;
        ZoomCombo.SelectionChanged += OnZoomComboSelectionChanged;
        ZoomPlusBtn.Click  += (_, _) => StepZoomPreset(WireframeCtrl.Zoom * 100f, _zoomPresets, +1, p => WireframeCtrl.SetZoomPercent(p));
        ZoomMinusBtn.Click += (_, _) => StepZoomPreset(WireframeCtrl.Zoom * 100f, _zoomPresets, -1, p => WireframeCtrl.SetZoomPercent(p));
        UnitTypeCombo.SelectionChanged += OnUnitTypeComboChanged;

        // Apply initial grid state
        WireframeCtrl.SetGrid(false, 16);

        // Sync UnitTypeCombo to current AppState
        UnitTypeCombo.SelectedIndex = (int)AppState.Self.UnitType;
    }

    private void OnTextureComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressTextureComboChanged) return;
        if (TextureCombo.SelectedItem is not string absolutePath) return;

        WireframeCtrl.LoadTexture(absolutePath);

        var frame = SelectedState.Self.SelectedFrame;
        if (frame == null) return;

        string achxFolder = string.IsNullOrEmpty(ProjectManager.Self.FileName)
            ? string.Empty
            : FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName);
        string storePath = TexturePathHelper.ComputeStorePath(absolutePath, achxFolder);
        AppCommands.Self.SetFrameTextureName(frame, storePath);
        RefreshPropertyPanel();
    }

    private void OnMagicWandToggled(object? sender, RoutedEventArgs e)
    {
        WireframeCtrl.IsMagicWandMode = MagicWandToggle.IsChecked == true;
    }

    private void OnSnapToGridChanged(object? sender, RoutedEventArgs e)
    {
        WireframeCtrl.SetGrid(
            SnapToGridCheck.IsChecked == true,
            GetGridSizeFromInput());
    }

    private int GetGridSizeFromInput()
    {
        return int.TryParse(GridSizeInput.Text, out int v) && v >= 1 ? Math.Min(v, 512) : 16;
    }

    private void OnGridSizeInputLostFocus(object? sender, RoutedEventArgs e) => ApplyGridSize();

    private void OnGridSizeInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            ApplyGridSize();
            e.Handled = true;
        }
    }

    private void ApplyGridSize()
    {
        int size = GetGridSizeFromInput();
        GridSizeInput.Text = size.ToString();
        if (SnapToGridCheck.IsChecked == true)
            WireframeCtrl.SetGrid(true, size);
        if (PropTileSection.IsVisible) UpdateCellPxDisplays();
    }

    private void OnGridSizePlusBtnClick(object? sender, RoutedEventArgs e)
    {
        int size = Math.Min(GetGridSizeFromInput() + 1, 512);
        GridSizeInput.Text = size.ToString();
        if (SnapToGridCheck.IsChecked == true)
            WireframeCtrl.SetGrid(true, size);
        if (PropTileSection.IsVisible) UpdateCellPxDisplays();
    }

    private void OnGridSizeMinusBtnClick(object? sender, RoutedEventArgs e)
    {
        int size = Math.Max(GetGridSizeFromInput() - 1, 1);
        GridSizeInput.Text = size.ToString();
        if (SnapToGridCheck.IsChecked == true)
            WireframeCtrl.SetGrid(true, size);
        if (PropTileSection.IsVisible) UpdateCellPxDisplays();
    }

    // ── Editable zoom combo (top wireframe) ──────────────────────────────────
    //
    // AutoCompleteBox is used instead of ComboBox because it accepts arbitrary
    // text. Wheel-zooming the wireframe can land on values that aren't in the
    // preset list (e.g. 156%); the combo must display the live percent rather
    // than snap to the nearest preset.
    //
    // Commit boundaries: Enter and LostFocus. SelectionChanged covers the case
    // where the user picks a preset from the suggestion dropdown. The
    // suppression flag breaks the feedback loop when ZoomChanged → SyncZoomCombo
    // writes Text back into the control.

    private static readonly int[] _zoomPresets =
        { 10, 25, 50, 100, 200, 400, 800 };
    private static readonly string[] _zoomPresetTexts =
        _zoomPresets.Select(p => $"{p}%").ToArray();

    private void OnZoomComboKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitZoomComboText(); e.Handled = true; }
    }

    private void OnZoomComboLostFocus(object? sender, RoutedEventArgs e) => CommitZoomComboText();

    private void OnZoomComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressZoomComboChanged) return;
        if (ZoomCombo.SelectedItem is string s) ApplyZoomComboText(s);
    }

    private void CommitZoomComboText()
    {
        if (_suppressZoomComboChanged) return;
        ApplyZoomComboText(ZoomCombo.Text ?? string.Empty);
    }

    private void ApplyZoomComboText(string text)
    {
        if (TryParsePercent(text, out int pct)) WireframeCtrl.SetZoomPercent(pct);
    }

    /// <summary>
    /// Updates the ZoomCombo display to the live zoom percent (rounded). Called
    /// from <see cref="WireframeControl.ZoomChanged"/>; the suppression flag
    /// stops this write from triggering CommitZoomComboText via LostFocus or
    /// SelectionChanged feedback.
    /// </summary>
    private void SyncZoomCombo(float zoomPercent)
    {
        _suppressZoomComboChanged = true;
        ZoomCombo.Text = $"{(int)MathF.Round(zoomPercent)}%";
        _suppressZoomComboChanged = false;
    }

    private static bool TryParsePercent(string text, out int pct)
    {
        var trimmed = text.Trim().TrimEnd('%').Trim();
        return int.TryParse(trimmed, out pct);
    }

    private void OnUnitTypeComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (UnitTypeCombo.SelectedIndex >= 0)
        {
            AppState.Self.UnitType = (UnitType)UnitTypeCombo.SelectedIndex;
            Dispatcher.UIThread.InvokeAsync(RefreshPropertyPanel);
        }
    }

    // ── WireframeControl event wiring ─────────────────────────────────────────

    private void WireWireframeControl()
    {
        WireframeCtrl.FrameRegionChanged     += OnFrameRegionChanged;
        WireframeCtrl.ChainRegionChanged     += OnChainRegionChanged;
        WireframeCtrl.FrameLiveUpdated       += OnFrameLiveUpdated;
        WireframeCtrl.FrameCreatedFromRegion += OnFrameCreatedFromRegion;
        WireframeCtrl.ZoomChanged            += SyncZoomCombo;
    }

    private void OnChainRegionChanged(AnimationChainSave chain)
    {
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
    }

    private void OnFrameLiveUpdated(AnimationFrameSave frame)
    {
        // Called on UI thread during drag — refresh property panel and preview without saving
        RefreshPropertyPanel();
        AppCommands.Self.RefreshAnimationFrameDisplay();
    }

    private void OnFrameRegionChanged(AnimationFrameSave frame)
    {
        AppCommands.Self.RefreshTreeNode(frame);
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
    }

    private void OnFrameCreatedFromRegion(int minX, int minY, int maxX, int maxY)
    {
        var chain = SelectedState.Self.SelectedChain;
        if (chain is null) return;

        var texPath = WireframeCtrl.LoadedTexturePath;
        if (string.IsNullOrEmpty(texPath)) return;

        var (bitmapW, bitmapH) = WireframeCtrl.BitmapSize;
        if (bitmapW == 0 || bitmapH == 0) return;

        // When an .achx project file is open, make the path relative to it.
        // When no file exists yet (unsaved project), keep the absolute path so
        // WireframeControl.DetermineTexturePath can still resolve it for display.
        string relPath = !string.IsNullOrEmpty(ProjectManager.Self.FileName)
            ? FlatRedBall.IO.FileManager.MakeRelative(
                texPath,
                FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName))
            : texPath;

        var frame = new AnimationFrameSave
        {
            TextureName        = relPath,
            LeftCoordinate     = minX / (float)bitmapW,
            RightCoordinate    = maxX / (float)bitmapW,
            TopCoordinate      = minY / (float)bitmapH,
            BottomCoordinate   = maxY / (float)bitmapH,
            FrameLength        = 0.1f,
            ShapeCollectionSave = new FlatRedBall.Content.Math.Geometry.ShapeCollectionSave()
        };

        chain.Frames.Add(frame);
        AppCommands.Self.RefreshTreeNode(chain);
        SelectedState.Self.SelectedFrame = frame;
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
    }

    // ── Core event handlers ───────────────────────────────────────────────────

    private void HandleAchxLoaded(string fileName)
    {
        AppCommands.Self.LoadAnimationChain(fileName);   // triggers RefreshTreeViewRequested

        _appSettings.AddFile(new FilePath(fileName));
        SaveSettingsFile();
        RefreshRecentFiles();
        UpdateTitle();
        RefreshTextureCombo();
    }

    private void HandleAnimationChainsChanged()
    {
        if (!string.IsNullOrEmpty(ProjectManager.Self.FileName))
        {
            AppCommands.Self.SaveCurrentAnimationChainList();
            UpdateTitle();
        }
    }

    private void HandleSelectionChanged()
    {
        // Sync the texture combo to the texture of the currently selected frame/chain
        Dispatcher.UIThread.InvokeAsync(SyncTextureCombo);
        // Sync tree selection
        Dispatcher.UIThread.InvokeAsync(SyncTreeSelection);
        // Refresh property inspector
        Dispatcher.UIThread.InvokeAsync(RefreshPropertyPanel);
    }

    // ── Texture combo helpers ─────────────────────────────────────────────────

    /// <summary>Rebuild the texture dropdown from all frames in the loaded .achx.</summary>
    private void RefreshTextureCombo()
    {
        _suppressTextureComboChanged = true;
        try
        {
            TextureCombo.Items.Clear();

            var acls = ProjectManager.Self.AnimationChainListSave;
            if (acls is null || string.IsNullOrEmpty(ProjectManager.Self.FileName)) return;

            string achxFolder = FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName);

            var paths = acls.AnimationChains
                .SelectMany(c => c.Frames)
                .Where(f => !string.IsNullOrEmpty(f.TextureName))
                .Select(f =>
                {
                    var abs = System.IO.Path.IsPathRooted(f.TextureName)
                        ? f.TextureName
                        : achxFolder + f.TextureName;
                    return new FilePath(abs).Standardized;
                })
                .Union(ProjectManager.Self.ReferencedPngs.Select(p => p.Standardized))
                .Distinct()
                .ToList();

            foreach (var p in paths)
                TextureCombo.Items.Add(p);

            if (paths.Count > 0)
            {
                TextureCombo.SelectedIndex = 0;
                WireframeCtrl.LoadTexture(paths[0]);
            }
        }
        finally
        {
            _suppressTextureComboChanged = false;
        }
    }

    /// <summary>Sync the combo selection to whichever texture the selected frame uses.</summary>
    private void SyncTextureCombo()
    {
        string? texPath = null;

        var frame = SelectedState.Self.SelectedFrame;
        if (frame != null && !string.IsNullOrEmpty(frame.TextureName) &&
            !string.IsNullOrEmpty(ProjectManager.Self.FileName))
        {
            string achxFolder = FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName);
            var abs = System.IO.Path.IsPathRooted(frame.TextureName)
                ? frame.TextureName
                : achxFolder + frame.TextureName;
            texPath = new FilePath(abs).Standardized;
        }

        if (texPath != null && TextureCombo.Items.Contains(texPath))
        {
            if (TextureCombo.SelectedItem as string != texPath)
            {
                _suppressTextureComboChanged = true;
                try { TextureCombo.SelectedItem = texPath; }
                finally { _suppressTextureComboChanged = false; }
                WireframeCtrl.LoadTexture(texPath);
            }
        }
        else if (texPath != null)
        {
            WireframeCtrl.LoadTexture(texPath);
        }
    }

    // ── Menu wiring ───────────────────────────────────────────────────────────

    private void WireMenuEvents()
    {
        MenuNew.Click    += OnNewClick;
        MenuLoad.Click   += OnLoadClick;
        MenuSave.Click   += OnSaveClick;
        MenuSaveAs.Click += OnSaveAsClick;
        MenuAbout.Click  += OnAboutClick;
        MenuCopy.Click          += (_, _) => _ = HandleCopyAsync();
        MenuPaste.Click         += (_, _) => _ = HandlePasteAsync();
        MenuResizeTexture.Click += (_, _) => _ = DoResizeTextureAsync();

        MenuUndo.IsEnabled = UndoManager.Self.CanUndo;
        MenuRedo.IsEnabled = UndoManager.Self.CanRedo;
        MenuUndo.Click += (_, _) => UndoManager.Self.Undo();
        MenuRedo.Click += (_, _) => UndoManager.Self.Redo();
        UndoManager.Self.StackChanged += () =>
        {
            MenuUndo.IsEnabled = UndoManager.Self.CanUndo;
            MenuRedo.IsEnabled = UndoManager.Self.CanRedo;
        };

        RefreshRecentFiles();
    }

    private void RefreshRecentFiles()
    {
        MenuLoadRecent.Items.Clear();
        foreach (var file in _appSettings.RecentFiles)
        {
            var item = new MenuItem { Header = file };
            var captured = file;
            item.Click += (_, _) => LoadAnimationFile(captured);
            MenuLoadRecent.Items.Add(item);
        }
    }

    // ── File menu handlers ────────────────────────────────────────────────────

    private void OnNewClick(object? sender, RoutedEventArgs e)
    {
        ProjectManager.Self.AnimationChainListSave =
            new FlatRedBall.Content.AnimationChain.AnimationChainListSave();
        ProjectManager.Self.FileName = null;
        _ = AppCommands.Self.SaveCurrentAnimationChainListAsync();
    }

    private void OnLoadClick(object? sender, RoutedEventArgs e) => _ = LoadAsync();

    private async Task LoadAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Animation Chain",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Animation Chain") { Patterns = new[] { "*.achx" } }
            }
        });

        if (files.Count > 0)
            LoadAnimationFile(files[0].Path.LocalPath);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (ProjectManager.Self.AnimationChainListSave is null) return;

        if (string.IsNullOrEmpty(ProjectManager.Self.FileName))
            _ = AppCommands.Self.SaveCurrentAnimationChainListAsync();
        else
        {
            AppCommands.Self.SaveCurrentAnimationChainList();
            UpdateTitle();
        }
    }

    private void OnSaveAsClick(object? sender, RoutedEventArgs e) =>
        _ = AppCommands.Self.SaveCurrentAnimationChainListAsync();

    private void OnAboutClick(object? sender, RoutedEventArgs e)
    {
        _ = new Window
        {
            Title = "About AnimationEditor",
            Width = 320,
            Height = 130,
            Content = new TextBlock
            {
                Text = "AnimationEditor: Avalonia Port\n© FlatRedBall Contributors",
                Margin = new Avalonia.Thickness(16),
                TextWrapping = Avalonia.Media.TextWrapping.Wrap
            }
        }.ShowDialog(this);
    }

    // ── Preview controls wiring ───────────────────────────────────────────────

    private void WirePreviewControls()
    {
        OnionSkinToggle.IsCheckedChanged += (_, _) =>
            PreviewCtrl.ShowOnionSkin = OnionSkinToggle.IsChecked == true;

        ShowGuidesCheck.IsCheckedChanged += (_, _) =>
            PreviewCtrl.ShowGuides = ShowGuidesCheck.IsChecked == true;

        PreviewZoomCombo.ItemsSource = _previewZoomPresetTexts;
        PreviewZoomCombo.KeyDown += OnPreviewZoomComboKeyDown;
        PreviewZoomCombo.LostFocus += OnPreviewZoomComboLostFocus;
        PreviewZoomCombo.SelectionChanged += OnPreviewZoomComboSelectionChanged;
        PreviewZoomPlusBtn.Click  += (_, _) => StepZoomPreset(PreviewCtrl.Zoom * 100f, _previewZoomPresets, +1, p => PreviewCtrl.SetZoomPercent(p));
        PreviewZoomMinusBtn.Click += (_, _) => StepZoomPreset(PreviewCtrl.Zoom * 100f, _previewZoomPresets, -1, p => PreviewCtrl.SetZoomPercent(p));

        PreviewCtrl.ZoomChanged += SyncPreviewZoomCombo;
    }

    // ── Editable preview-zoom combo (bottom preview) ─────────────────────────
    //
    // Same editable-AutoCompleteBox pattern as ZoomCombo above. The bottom
    // preview's wheel zoom uses a 1.25 / 0.8 multiplier so it almost always
    // lands on a non-preset value — making the preset-snap display from the
    // pre-fix code straight-up wrong.

    private static readonly int[] _previewZoomPresets =
        { 10, 25, 50, 100, 200, 400 };
    private static readonly string[] _previewZoomPresetTexts =
        _previewZoomPresets.Select(p => $"{p}%").ToArray();

    /// <summary>
    /// Steps to the next or previous preset relative to the current zoom percent.
    /// + button: smallest preset strictly greater than current.
    /// - button: largest preset strictly less than current.
    /// If the current value is outside the preset range, clamps to the nearest end.
    /// </summary>
    private static void StepZoomPreset(float currentPct, int[] presets, int direction, Action<int> apply)
    {
        if (direction > 0)
        {
            for (int i = 0; i < presets.Length; i++)
                if (presets[i] > currentPct) { apply(presets[i]); return; }
            apply(presets[^1]);
        }
        else
        {
            for (int i = presets.Length - 1; i >= 0; i--)
                if (presets[i] < currentPct) { apply(presets[i]); return; }
            apply(presets[0]);
        }
    }

    private void OnPreviewZoomComboKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { CommitPreviewZoomComboText(); e.Handled = true; }
    }

    private void OnPreviewZoomComboLostFocus(object? sender, RoutedEventArgs e)
        => CommitPreviewZoomComboText();

    private void OnPreviewZoomComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressPreviewZoomComboChanged) return;
        if (PreviewZoomCombo.SelectedItem is string s) ApplyPreviewZoomComboText(s);
    }

    private void CommitPreviewZoomComboText()
    {
        if (_suppressPreviewZoomComboChanged) return;
        ApplyPreviewZoomComboText(PreviewZoomCombo.Text ?? string.Empty);
    }

    private void ApplyPreviewZoomComboText(string text)
    {
        if (TryParsePercent(text, out int pct)) PreviewCtrl.SetZoomPercent(pct);
    }

    private void SyncPreviewZoomCombo(float zoomPercent)
    {
        _suppressPreviewZoomComboChanged = true;
        PreviewZoomCombo.Text = $"{(int)MathF.Round(zoomPercent)}%";
        _suppressPreviewZoomComboChanged = false;
    }

    // ── Tree view ─────────────────────────────────────────────────────────────

    private readonly ObservableCollection<TreeNodeVm> _treeRoots = new();

    private void WireTreeView()
    {
        AnimTree.ItemsSource = _treeRoots;
        DragDrop.SetAllowDrop(AnimTree, true);

        // Selection changes in the tree → SelectedState
        AnimTree.SelectionChanged += OnTreeSelectionChanged;
        DragDrop.AddDragOverHandler(AnimTree, OnTreeDragOver);
        DragDrop.AddDropHandler(AnimTree, OnTreeDrop);

        // Context menu
        var cm = new ContextMenu();
        cm.Opening += OnTreeContextMenuOpening;
        AnimTree.ContextMenu = cm;

        // Tunnel-phase PointerPressed: select the right-clicked node BEFORE the context menu opens,
        // so OnTreeContextMenuOpening always sees the item under the pointer, not the previous selection.
        AnimTree.AddHandler(
            InputElement.PointerPressedEvent,
            OnTreePointerPressed,
            RoutingStrategies.Tunnel);

        // "Add Chain" button under the tree
        AddChainBtn.Click += (_, _) =>
        {
            if (ProjectManager.Self.AnimationChainListSave is null)
                ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
            _ = AppCommands.Self.AddAnimationChain();
        };

        // Expand/Collapse toolbar buttons
        ExpandAllBtn.Click  += (_, _) => SetAllExpanded(true);
        CollapseAllBtn.Click += (_, _) => SetAllExpanded(false);

        // Inline rename: double-tap a chain node
        AnimTree.DoubleTapped += OnAnimTreeDoubleTapped;

        // Bubble-phase KeyDown from the inline TextBox (Enter=commit, Escape=cancel)
        AnimTree.AddHandler(
            InputElement.KeyDownEvent,
            OnInlineRenameKeyDown,
            RoutingStrategies.Bubble);

        // Bubble-phase LostFocus from the inline TextBox: commit
        AnimTree.AddHandler(
            InputElement.LostFocusEvent,
            OnInlineRenameLostFocus,
            RoutingStrategies.Bubble);
    }

    private void SetAllExpanded(bool expanded)
    {
        foreach (var node in _treeRoots)
            node.IsExpanded = expanded;
    }

    /// <summary>
    /// Click handler for the inline "Add Frame" button shown on each chain node in the tree.
    /// Adds a new frame to the animation chain that owns the clicked button.
    /// </summary>
    private void OnAddFrameBtnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not TreeNodeVm vm) return;
        if (vm.Data is not AnimationChainSave chain) return;
        AppCommands.Self.AddFrame(chain);
        e.Handled = true;
    }

    private void OnTreeDragOver(object? sender, DragEventArgs e)
    {
        // Use TryGetFiles() — the correct Avalonia 12 API for OS file drops
        string? firstFile = e.DataTransfer.TryGetFiles()?                  
            .FirstOrDefault()?.Path.LocalPath;

        // Fallback for internal drag sources that use the Items API
        if (firstFile is null)
        {
            firstFile = e.DataTransfer.Items?
                .Select(i => i.TryGetFile())
                .FirstOrDefault(f => f is not null)?.Path.LocalPath;
        }

        if (!string.IsNullOrEmpty(firstFile) &&
            string.Equals(Path.GetExtension(firstFile), ".png", StringComparison.OrdinalIgnoreCase))
        {
            e.DragEffects = DragDropEffects.Copy;
        }
        else
        {
            e.DragEffects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void OnTreeDrop(object? sender, DragEventArgs e)
    {
        var firstFile = GetFirstDroppedFilePath(e);
        Console.WriteLine($"[DragDrop] OnTreeDrop: firstFile={firstFile ?? "(null)"}, FileName={ProjectManager.Self.FileName ?? "(null)"}");

        if (string.IsNullOrEmpty(firstFile))
        {
            Console.WriteLine("[DragDrop] Aborted: no file found in drop data");
            return;
        }

        // If no ACHX is saved yet, allow the drop but use an absolute texture path.
        // Relative-path conversion requires a base directory; without one we fall back to absolute.
        if (string.IsNullOrEmpty(ProjectManager.Self.FileName))
        {
            Console.WriteLine("[DragDrop] Warning: no ACHX file saved yet — texture path will be absolute");
        }

        var targetNode = AnimTree.SelectedItem as TreeNodeVm;

        var targetFrame = targetNode?.Data as AnimationFrameSave;
        var targetChain = targetNode?.Data as AnimationChainSave;

        if (targetFrame is not null)
        {
            targetChain = ObjectFinder.Self.GetAnimationChainContaining(targetFrame);
        }

        Console.WriteLine($"[DragDrop] targetChain={targetChain?.Name ?? "(null)"}, targetFrame={targetFrame?.TextureName ?? "(null)"}, ctrl={e.KeyModifiers.HasFlag(KeyModifiers.Control)}");

        var result = TextureDropProcessor.ApplyPngDrop(
            targetChain,
            targetFrame,
            firstFile,
            ProjectManager.Self.FileName,
            e.KeyModifiers.HasFlag(KeyModifiers.Control));

        Console.WriteLine($"[DragDrop] Result={result}");

        if (result == TextureDropResult.NotApplied)
        {
            Console.WriteLine("[DragDrop] NotApplied — no chain or frame targeted, or non-PNG dropped");
            return;
        }

        if (targetFrame is not null)
        {
            AppCommands.Self.RefreshTreeNode(targetFrame);
            SelectedState.Self.SelectedFrame = targetFrame;
        }
        else if (targetChain is not null)
        {
            AppCommands.Self.RefreshTreeNode(targetChain);

            if (result == TextureDropResult.CreatedFrame)
            {
                var createdFrame = targetChain.Frames.LastOrDefault();
                if (createdFrame is not null)
                    SelectedState.Self.SelectedFrame = createdFrame;
            }
            else
            {
                SelectedState.Self.SelectedChain = targetChain;
            }
        }

        RefreshTextureCombo();
        AppCommands.Self.RefreshWireframe();
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        e.Handled = true;
    }

    private static string? GetFirstDroppedFilePath(DragEventArgs e)
    {
        // Log item formats so we can see exactly what the OS provides
        var itemFormats = e.DataTransfer.Items?
            .Select(i => "[" + string.Join(",", i.Formats) + "]")
            .ToList();
        Console.WriteLine($"[DragDrop] Items and their formats: {(itemFormats == null ? "(null)" : string.Join(" ", itemFormats))}");
        Console.WriteLine($"[DragDrop] Contains(DataFormat.File)={e.DataTransfer.Contains(DataFormat.File)}");

        // Correct Avalonia 12 API for OS file drops
        var files = e.DataTransfer.TryGetFiles()?.ToList();
        Console.WriteLine($"[DragDrop] TryGetFiles() count={files?.Count ?? -1}");
        if (files?.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            Console.WriteLine($"[DragDrop] resolved path={path}");
            return path;
        }

        // Fallback: per-item TryGetFile()
        var items = e.DataTransfer.Items?.ToList();
        Console.WriteLine($"[DragDrop] Items count={items?.Count ?? -1}");
        foreach (var item in items ?? new())
            Console.WriteLine($"[DragDrop] Item: Formats=[{string.Join(",", item.Formats)}] TryGetFile={item.TryGetFile()?.Path?.LocalPath ?? "(null)"}");

        var fallback = items?
            .Select(item => item.TryGetFile())
            .FirstOrDefault(f => f is not null);
        Console.WriteLine($"[DragDrop] Items fallback resolved={fallback?.Path.LocalPath ?? "(null)"}");
        return fallback?.Path.LocalPath;
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (AnimTree.SelectedItem is not TreeNodeVm vm) return;

        // Sync multi-select into SelectedState
        SelectedState.Self.SelectedNodes = AnimTree.SelectedItems
            .OfType<TreeNodeVm>()
            .Select(n => n.Data)
            .OfType<object>()
            .ToList();

        TreeBuilder.RouteNodeSelection(vm);
    }

    // ── Tree refresh ──────────────────────────────────────────────────────────

    private void RefreshTreeView()
    {
        var acls = ProjectManager.Self.AnimationChainListSave;

        // Preserve expanded chain names before clearing
        var expanded = TreeBuilder.GetExpandedChainNames(_treeRoots).ToHashSet();

        _treeRoots.Clear();

        if (acls is null) return;

        foreach (var chain in acls.AnimationChains)
        {
            var node = TreeBuilder.BuildChainNode(chain);
            // Restore expand state — keep true if no prior state recorded yet
            node.IsExpanded = expanded.Count == 0 || expanded.Contains(chain.Name);
            _treeRoots.Add(node);
        }

        // Re-select to keep visual state
        SyncTreeSelection();
    }

    private void RefreshChainNode(AnimationChainSave chain)
    {
        var node = FindChainNode(chain);
        if (node is null)
        {
            _treeRoots.Add(TreeBuilder.BuildChainNode(chain));
        }
        else
        {
            node.Header = chain.Name;
            node.Children.Clear();
            foreach (var frame in chain.Frames)
                node.Children.Add(TreeBuilder.BuildFrameNode(frame));
        }
    }

    private void RefreshFrameNode(AnimationFrameSave frame)
    {
        var chain    = AnimationEditor.Core.ObjectFinder.Self.GetAnimationChainContaining(frame);
        var chainNode = chain is null ? null : FindChainNode(chain);
        if (chainNode is null) return;

        var frameNode = chainNode.Children
            .FirstOrDefault(n => n.Data is AnimationFrameSave f && f == frame);

        if (frameNode is null)
        {
            chainNode.Children.Add(TreeBuilder.BuildFrameNode(frame));
        }
        else
        {
            frameNode.Header = TreeBuilder.BuildFrameHeader(frame);
            // Rebuild shape children via TreeBuilder
            frameNode.Children.Clear();
            if (frame.ShapeCollectionSave is not null)
            {
                foreach (var r in frame.ShapeCollectionSave.AxisAlignedRectangleSaves)
                    frameNode.Children.Add(new TreeNodeVm { Header = r.Name, Data = r });
                foreach (var c in frame.ShapeCollectionSave.CircleSaves)
                    frameNode.Children.Add(new TreeNodeVm { Header = c.Name, Data = c });
            }
        }
    }

    private TreeNodeVm? FindChainNode(AnimationChainSave chain) =>
        _treeRoots.FirstOrDefault(n => n.Data is AnimationChainSave c && c == chain);

    private void SyncTreeSelection()
    {
        var selFrame = SelectedState.Self.SelectedFrame;
        var selChain = SelectedState.Self.SelectedChain;

        TreeNodeVm? target = selFrame is not null
            ? TreeBuilder.FindNodeForData(_treeRoots, selFrame)
            : selChain is not null
                ? TreeBuilder.FindNodeForData(_treeRoots, selChain)
                : null;

        if (target is not null && AnimTree.SelectedItem != target)
            AnimTree.SelectedItem = target;
    }

    // ── Context menu ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tunnel-phase PointerPressed: selects the tree node under the pointer on right-click so the
    /// context menu always acts on the item the user actually right-clicked, not the previous selection.
    /// On left-button double-click over a frame node, centres the wireframe on that frame.
    /// We do NOT set e.Handled so normal selection and context-menu logic continues afterward.
    /// </summary>
    private void OnTreePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var props = e.GetCurrentPoint(AnimTree).Properties;

        if (props.IsRightButtonPressed)
        {
            // e.Source is the innermost visual under the pointer (e.g. the TextBlock in the DataTemplate).
            // Walk up the visual tree to find the containing TreeViewItem.
            if (e.Source is not Control src) return;
            var tvi = src.FindAncestorOfType<TreeViewItem>(includeSelf: true);
            if (tvi?.DataContext is TreeNodeVm vm && !ReferenceEquals(AnimTree.SelectedItem, vm))
                AnimTree.SelectedItem = vm;
        }
        else if (props.IsLeftButtonPressed && e.ClickCount == 2)
        {
            if (e.Source is not Control src) return;
            var tvi = src.FindAncestorOfType<TreeViewItem>(includeSelf: true);
            if (tvi?.DataContext is TreeNodeVm { Data: AnimationFrameSave frame })
            {
                // Post at Background priority so we run after the higher-priority
                // SelectionChanged → RefreshAll dispatch has completed.  This prevents
                // a same-texture RefreshAll from overwriting our queued scroll.
                Dispatcher.UIThread.Post(
                    () => WireframeCtrl.CenterOnFrame(frame),
                    DispatcherPriority.Background);
            }
        }
    }

    private void OnTreeContextMenuOpening(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (AnimTree.ContextMenu is null) return;
        AnimTree.ContextMenu.Items.Clear();

        var vm = AnimTree.SelectedItem as TreeNodeVm;

        if (vm?.Data is AxisAlignedRectangleSave rect)
        {
            AddMenuItem("Match Frame Size", () =>
            {
                var frame = SelectedState.Self.SelectedFrame;
                if (frame is not null)
                {
                    AppCommands.Self.MatchRectangleToFrame(rect, frame);
                    AppCommands.Self.RefreshAnimationFrameDisplay();
                    AppCommands.Self.SaveCurrentAnimationChainList();
                }
            });
            AddMenuItem("Delete Rectangle", () =>
                _ = AppCommands.Self.AskToDeleteRectangles(new() { rect }));
        }
        else if (vm?.Data is CircleSave circle)
        {
            AddMenuItem("Delete Circle", () =>
                _ = AppCommands.Self.AskToDeleteCircles(new() { circle }));
        }
        else if (vm?.Data is AnimationFrameSave frame2)
        {
            var chain2 = AnimationEditor.Core.ObjectFinder.Self.GetAnimationChainContaining(frame2);
            if (chain2 is not null)
            {
                AddMenuItem("^^ Move To Top",    () => AppCommands.Self.MoveFrameToTop(frame2, chain2));
                AddMenuItem("^  Move Up",         () => AppCommands.Self.MoveFrame(frame2, chain2, -1));
                AddMenuItem("v  Move Down",        () => AppCommands.Self.MoveFrame(frame2, chain2, +1));
                AddMenuItem("vv Move To Bottom",  () => AppCommands.Self.MoveFrameToBottom(frame2, chain2));
                AddSeparator();
            }
            AddMenuItem("Add AxisAlignedRectangle", () => AppCommands.Self.AddAxisAlignedRectangle(frame2));
            AddMenuItem("Add Circle",               () => AppCommands.Self.AddCircle(frame2));
            AddSeparator();
            AddMenuItem("Copy",  () => _ = HandleCopyAsync());
            AddMenuItem("Paste", () => _ = HandlePasteAsync());
            AddSeparator();
            AddMenuItem("Rename (texture path)…", () => _ = AskRenameFrameAsync(frame2));
            AddMenuItem("View Texture in Explorer", () => ViewTextureInExplorer(frame2));
            AddSeparator();
            AddMenuItem("Delete Frame", () =>
                _ = AppCommands.Self.AskToDeleteFrames(new() { frame2 }));
        }
        else if (vm?.Data is AnimationChainSave chain)
        {
            AddMenuItem("^^ Move To Top",    () => AppCommands.Self.MoveChainToTop(chain));
            AddMenuItem("^  Move Up",         () => AppCommands.Self.MoveChain(chain, -1));
            AddMenuItem("v  Move Down",        () => AppCommands.Self.MoveChain(chain, +1));
            AddMenuItem("vv Move To Bottom",  () => AppCommands.Self.MoveChainToBottom(chain));
            AddSeparator();
            AddMenuItem("Adjust Frame Time…", () => AskAdjustFrameTime(chain));
            AddMenuItem("Flip Horizontally",  () => AppCommands.Self.FlipChainHorizontally(chain));
            AddMenuItem("Flip Vertically",    () => AppCommands.Self.FlipChainVertically(chain));
            AddMenuItem("Invert Frame Order", () => AppCommands.Self.InvertFrameOrder(chain));
            AddSeparator();
            AddMenuItem("Add AnimationChain", () => _ = AppCommands.Self.AddAnimationChain());
            AddMenuItem("Add Frame",          () => AppCommands.Self.AddFrame(chain));
            AddMenuItem("Add Multiple Frames…", () => _ = AskAddMultipleFramesAsync(chain));
            AddSeparator();
            AddMenuItem("Duplicate (original)",         () => AppCommands.Self.DuplicateChain(chain));
            AddMenuItem("Duplicate (flip horizontally)",() => AppCommands.Self.DuplicateChain(chain, flipH: true));
            AddMenuItem("Duplicate (flip vertically)",  () => AppCommands.Self.DuplicateChain(chain, flipV: true));
            AddSeparator();
            AddMenuItem("Copy",  () => _ = HandleCopyAsync());
            AddMenuItem("Paste", () => _ = HandlePasteAsync());
            AddSeparator();
            AddMenuItem("Adjust Offsets…", () => _ = AskAdjustOffsetsAsync(chain));
            AddMenuItem("Rename…",          () => BeginInlineRenameSelected(chain));
            AddSeparator();
            AddMenuItem("Delete AnimationChain",
                () => _ = AppCommands.Self.AskToDeleteAnimationChains(new() { chain }));
        }
        else
        {
            AddMenuItem("Add AnimationChain", () =>
            {
                if (ProjectManager.Self.AnimationChainListSave is null)
                    ProjectManager.Self.AnimationChainListSave = new AnimationChainListSave();
                _ = AppCommands.Self.AddAnimationChain();
            });
        }

        AddSeparator();
        AddMenuItem("Sort Animations Alphabetically",
            () => AppCommands.Self.SortAnimationsAlphabetically());
    }

    private void AddMenuItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        AnimTree.ContextMenu!.Items.Add(item);
    }

    private void AddSeparator() =>
        AnimTree.ContextMenu!.Items.Add(new Separator());

    private void AskAdjustFrameTime(AnimationChainSave chain)
    {
        if (chain.Frames.Count == 0) return;

        int   frameCount    = chain.Frames.Count;
        float totalDuration = chain.Frames.Sum(f => f.FrameLength);
        bool  canProportional = totalDuration > 0f;

        var dialog = new Window
        {
            Title  = "Adjust All Frame Time",
            Width  = 360,
            Height = 240,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false
        };

        var durationInput = new NumericUpDown
        {
            Value        = (decimal)totalDuration,
            Minimum      = 0m,
            Maximum      = 3600m,
            Increment    = 0.1m,
            FormatString = "0.000",
            Width        = 160
        };

        var radioProportional = new RadioButton
        {
            Content   = "Keep Proportional",
            IsChecked = canProportional,
            IsEnabled = canProportional
        };
        var radioSetAll = new RadioButton
        {
            Content   = "Set All Frames Same",
            IsChecked = !canProportional
        };

        var perFrameLabel = new TextBlock
        {
            FontSize   = 11,
            Foreground = Avalonia.Media.Brushes.Gray
        };

        void UpdateLabel()
        {
            float val = (float)(durationInput.Value ?? 0m);
            perFrameLabel.Text = radioSetAll.IsChecked == true
                ? $"Each frame: {val / frameCount:F3} seconds"
                : string.Empty;
        }

        durationInput.ValueChanged       += (_, _) => UpdateLabel();
        radioSetAll.IsCheckedChanged     += (_, _) => UpdateLabel();
        radioProportional.IsCheckedChanged += (_, _) => UpdateLabel();
        UpdateLabel();

        var panel = new StackPanel { Margin = new Avalonia.Thickness(12), Spacing = 8 };
        panel.Children.Add(new TextBlock { Text = "Total animation duration (seconds):" });
        panel.Children.Add(durationInput);
        panel.Children.Add(radioProportional);
        panel.Children.Add(radioSetAll);
        panel.Children.Add(perFrameLabel);

        var okBtn = new Button
        {
            Content = "OK",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        panel.Children.Add(okBtn);
        dialog.Content = panel;

        okBtn.Click += (_, _) =>
        {
            if (durationInput.Value.HasValue)
            {
                float newTotal = (float)durationInput.Value.Value;
                if (radioProportional.IsChecked == true)
                    AppCommands.Self.ScaleFrameTimesProportional(chain, newTotal);
                else
                    AppCommands.Self.ScaleFrameTimesSetAllSame(chain, newTotal);
            }
            dialog.Close();
        };

        _ = dialog.ShowDialog(this);
    }

    // ── Property panel wiring ─────────────────────────────────────────────────

    private void WirePropertyPanel()
    {
        PropFlipH.IsCheckedChanged += (_, _) => ApplyFrameFlip();
        PropFlipV.IsCheckedChanged += (_, _) => ApplyFrameFlip();
        PropFrameLen.ValueChanged  += (_, _) => ApplyFrameLen();
        PropRelX.ValueChanged      += (_, _) => ApplyFrameRelative();
        PropRelY.ValueChanged      += (_, _) => ApplyFrameRelative();
        PropPixelX.ValueChanged    += (_, _) => ApplyFramePixelCoords();
        PropPixelY.ValueChanged    += (_, _) => ApplyFramePixelCoords();
        PropPixelW.ValueChanged    += (_, _) => ApplyFramePixelCoords();
        PropPixelH.ValueChanged    += (_, _) => ApplyFramePixelCoords();
        PropTcLeft.ValueChanged    += (_, _) => ApplyFrameTcCoords();
        PropTcRight.ValueChanged   += (_, _) => ApplyFrameTcCoords();
        PropTcTop.ValueChanged     += (_, _) => ApplyFrameTcCoords();
        PropTcBottom.ValueChanged  += (_, _) => ApplyFrameTcCoords();
        PropSpanW.ValueChanged     += (_, _) => { UpdateCellPxDisplays(); ApplyFrameCellSize(); };
        PropSpanH.ValueChanged     += (_, _) => { UpdateCellPxDisplays(); ApplyFrameCellSize(); };
        PropTileX.ValueChanged     += (_, _) => ApplyFrameTileCoords();
        PropTileY.ValueChanged     += (_, _) => ApplyFrameTileCoords();

        PropRectName.LostFocus     += (_, _) => ApplyRectProps();
        PropRectX.ValueChanged     += (_, _) => ApplyRectProps();
        PropRectY.ValueChanged     += (_, _) => ApplyRectProps();
        PropRectScaleX.ValueChanged += (_, _) => ApplyRectProps();
        PropRectScaleY.ValueChanged += (_, _) => ApplyRectProps();

        PropCircleName.LostFocus   += (_, _) => ApplyCircleProps();
        PropCircleX.ValueChanged   += (_, _) => ApplyCircleProps();
        PropCircleY.ValueChanged   += (_, _) => ApplyCircleProps();
        PropCircleRadius.ValueChanged += (_, _) => ApplyCircleProps();

        PropTextureBrowseBtn.Click += async (_, _) => await BrowseForFrameTexture();
    }

    private async Task BrowseForFrameTexture()
    {
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is null) return;

        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Texture",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("Images") { Patterns = new[] { "*.png", "*.bmp", "*.gif", "*.jpg", "*.jpeg" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        var pickedPath = files?.FirstOrDefault()?.TryGetLocalPath();
        if (string.IsNullOrEmpty(pickedPath)) return;

        string achxFolder = string.IsNullOrEmpty(ProjectManager.Self.FileName)
            ? string.Empty
            : (Path.GetDirectoryName(ProjectManager.Self.FileName) ?? string.Empty);

        // resolvedAbsPath tracks the actual file we will use (may change if user copies it)
        string resolvedAbsPath = pickedPath;

        if (!string.IsNullOrEmpty(achxFolder))
        {
            bool isInAchxFolder    = IsPathUnder(pickedPath, achxFolder);
            bool isInProjectFolder = !string.IsNullOrEmpty(AppState.Self.ProjectFolder)
                                     && IsPathUnder(pickedPath, AppState.Self.ProjectFolder);

            if (!isInAchxFolder && !isInProjectFolder)
            {
                var choice = await ShowTextureCopyDialogAsync(pickedPath);
                if (choice == TextureCopyChoice.Cancel) return;

                if (choice == TextureCopyChoice.Copy)
                {
                    string destination = Path.Combine(achxFolder, Path.GetFileName(pickedPath));
                    try
                    {
                        File.Copy(pickedPath, destination, overwrite: true);
                        resolvedAbsPath = destination;
                    }
                    catch (Exception ex)
                    {
                        await ShowConfirmDialogAsync($"Could not copy the file:\n{ex.Message}", "Copy Failed");
                    }
                }
            }
        }

        // Store relative path when possible; ../relative paths are allowed for textures
        // outside the .achx folder so they round-trip correctly.
        string storePath = string.IsNullOrEmpty(achxFolder)
            ? resolvedAbsPath
            : TexturePathHelper.ComputeStorePath(
                resolvedAbsPath,
                FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName));

        AppCommands.Self.SetFrameTextureName(frame, storePath);
        WireframeCtrl.LoadTexture(resolvedAbsPath);
        RefreshPropertyPanel();
    }

    private enum TextureCopyChoice { Copy, Keep, Cancel }

    private async Task<TextureCopyChoice> ShowTextureCopyDialogAsync(string absoluteTexturePath)
    {
        var tcs = new TaskCompletionSource<TextureCopyChoice>();

        var dialog = new Window
        {
            Title = "This frame does not share a folder",
            Width = 560,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 10 };
        panel.Children.Add(new TextBlock
        {
            Text = $"The selected file:\n\n{absoluteTexturePath}\n\nis not relative to the Animation Chain file.  What would you like to do?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var copyBtn = new Button
        {
            Content = "Copy the file to the same folder as the Animation Chain",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        var keepBtn = new Button
        {
            Content = "Keep the file where it is (this may limit the portability of the Animation Chain file)",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };

        copyBtn.Click += (_, _) => { tcs.TrySetResult(TextureCopyChoice.Copy);   dialog.Close(); };
        keepBtn.Click += (_, _) => { tcs.TrySetResult(TextureCopyChoice.Keep);   dialog.Close(); };
        panel.Children.Add(copyBtn);
        panel.Children.Add(keepBtn);

        dialog.Content = panel;
        dialog.Closed += (_, _) => tcs.TrySetResult(TextureCopyChoice.Cancel);

        await dialog.ShowDialog(this);
        return await tcs.Task;
    }

    private static bool IsPathUnder(string path, string folder)
    {
        char sep = Path.DirectorySeparatorChar;
        string normPath   = Path.GetFullPath(path).TrimEnd(sep);
        string normFolder = Path.GetFullPath(folder).TrimEnd(sep) + sep;
        return normPath.StartsWith(normFolder, StringComparison.OrdinalIgnoreCase);
    }

    private void RefreshPropertyPanel()
    {
        _suppressPropRefresh = true;
        try
        {
            var frame = SelectedState.Self.SelectedFrame;
            var rect  = SelectedState.Self.SelectedRectangle;
            var circ  = SelectedState.Self.SelectedCircle;

            PropNoneLabel.IsVisible   = frame is null && rect is null && circ is null;
            PropFramePanel.IsVisible  = frame is not null;
            PropRectPanel.IsVisible   = rect  is not null;
            PropCirclePanel.IsVisible = circ  is not null;

            if (frame is not null)
            {
                PropFlipH.IsChecked  = frame.FlipHorizontal;
                PropFlipV.IsChecked  = frame.FlipVertical;
                PropFrameLen.Value   = (decimal)frame.FrameLength;
                PropRelX.Value       = (decimal)frame.RelativeX;
                PropRelY.Value       = (decimal)frame.RelativeY;
                PropTextureName.Text = TexturePathHelper.ComputeDisplayPath(
                    frame.TextureName, ProjectManager.Self.FileName);

                var unitType = AppState.Self.UnitType;
                PropPixelSection.IsVisible = unitType != UnitType.TextureCoordinate;
                PropTcSection.IsVisible    = unitType == UnitType.TextureCoordinate;
                PropTileSection.IsVisible  = unitType == UnitType.SpriteSheet;

                if (unitType == UnitType.TextureCoordinate)
                {
                    PropTcLeft.Value   = (decimal)frame.LeftCoordinate;
                    PropTcRight.Value  = (decimal)frame.RightCoordinate;
                    PropTcTop.Value    = (decimal)frame.TopCoordinate;
                    PropTcBottom.Value = (decimal)frame.BottomCoordinate;
                }
                else
                {
                    var (bmpW, bmpH) = WireframeCtrl.BitmapSize;
                    if (bmpW > 0 && bmpH > 0)
                    {
                        PropPixelX.Value = FrameDisplayValues.GetPixelX(frame, bmpW);
                        PropPixelY.Value = FrameDisplayValues.GetPixelY(frame, bmpH);
                        PropPixelW.Value = FrameDisplayValues.GetPixelWidth(frame, bmpW);
                        PropPixelH.Value = FrameDisplayValues.GetPixelHeight(frame, bmpH);
                    }

                    if (unitType == UnitType.SpriteSheet)
                    {
                        var tmi = SelectedState.Self.SelectedTileMapInformation;
                        int cellW = tmi?.TileWidth  > 0 ? tmi.TileWidth  : 16;
                        int cellH = tmi?.TileHeight > 0 ? tmi.TileHeight : 16;

                        int gridSize = GetGridSizeFromInput();
                        if (gridSize < 1) gridSize = 1;
                        PropSpanW.Value = Math.Max(1, (int)Math.Round(cellW / (float)gridSize));
                        PropSpanH.Value = Math.Max(1, (int)Math.Round(cellH / (float)gridSize));
                        UpdateCellPxDisplays();

                        var (bmpW2, bmpH2) = WireframeCtrl.BitmapSize;
                        if (bmpW2 > 0 && bmpH2 > 0 && cellW > 0 && cellH > 0)
                        {
                            PropTileX.Value = FrameDisplayValues.GetTileX(frame, cellW, bmpW2);
                            PropTileY.Value = FrameDisplayValues.GetTileY(frame, cellH, bmpH2);
                        }
                    }
                }
            }

            if (rect is not null)
            {
                PropRectName.Text    = rect.Name   ?? "";
                PropRectX.Value      = (decimal)rect.X;
                PropRectY.Value      = (decimal)rect.Y;
                PropRectScaleX.Value = (decimal)rect.ScaleX;
                PropRectScaleY.Value = (decimal)rect.ScaleY;
            }

            if (circ is not null)
            {
                PropCircleName.Text    = circ.Name   ?? "";
                PropCircleX.Value      = (decimal)circ.X;
                PropCircleY.Value      = (decimal)circ.Y;
                PropCircleRadius.Value = (decimal)circ.Radius;
            }
        }
        finally
        {
            _suppressPropRefresh = false;
        }
    }

    // ── Property apply methods ────────────────────────────────────────────────

    private void ApplyFrameFlip()
    {
        if (_suppressPropRefresh) return;
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is null) return;
        frame.FlipHorizontal = PropFlipH.IsChecked == true;
        frame.FlipVertical   = PropFlipV.IsChecked == true;
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        AppCommands.Self.RefreshWireframe();
    }

    private void ApplyFrameLen()
    {
        if (_suppressPropRefresh) return;
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is null || !PropFrameLen.Value.HasValue) return;
        frame.FrameLength = (float)PropFrameLen.Value.Value;
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
    }

    private void ApplyFrameRelative()
    {
        if (_suppressPropRefresh) return;
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is null) return;
        if (PropRelX.Value.HasValue) frame.RelativeX = (float)PropRelX.Value.Value;
        if (PropRelY.Value.HasValue) frame.RelativeY = (float)PropRelY.Value.Value;
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        AppCommands.Self.RefreshWireframe();
    }

    private void ApplyFramePixelCoords()
    {
        if (_suppressPropRefresh) return;
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is null) return;
        var (bmpW, bmpH) = WireframeCtrl.BitmapSize;
        if (bmpW <= 0 || bmpH <= 0) return;
        if (!PropPixelX.Value.HasValue || !PropPixelY.Value.HasValue ||
            !PropPixelW.Value.HasValue || !PropPixelH.Value.HasValue) return;

        PixelFrameEditor.SetX(frame,      (int)PropPixelX.Value.Value, bmpW);
        PixelFrameEditor.SetY(frame,      (int)PropPixelY.Value.Value, bmpH);
        PixelFrameEditor.SetWidth(frame,  (int)PropPixelW.Value.Value, bmpW);
        PixelFrameEditor.SetHeight(frame, (int)PropPixelH.Value.Value, bmpH);
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        WireframeCtrl.RefreshFrames();
    }

    private void ApplyFrameTcCoords()
    {
        if (_suppressPropRefresh) return;
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is null) return;
        if (PropTcLeft.Value.HasValue)   frame.LeftCoordinate   = (float)PropTcLeft.Value.Value;
        if (PropTcRight.Value.HasValue)  frame.RightCoordinate  = (float)PropTcRight.Value.Value;
        if (PropTcTop.Value.HasValue)    frame.TopCoordinate    = (float)PropTcTop.Value.Value;
        if (PropTcBottom.Value.HasValue) frame.BottomCoordinate = (float)PropTcBottom.Value.Value;
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        WireframeCtrl.RefreshFrames();
    }

    private void UpdateCellPxDisplays()
    {
        int gridSize = GetGridSizeFromInput();
        if (gridSize < 1) gridSize = 1;
        int spanW = PropSpanW.Value.HasValue ? (int)PropSpanW.Value.Value : 1;
        int spanH = PropSpanH.Value.HasValue ? (int)PropSpanH.Value.Value : 1;
        PropCellWPx.Text = $"{spanW * gridSize} px";
        PropCellHPx.Text = $"{spanH * gridSize} px";
    }

    private void ApplyFrameCellSize()
    {
        if (_suppressPropRefresh) return;
        if (!PropSpanW.Value.HasValue || !PropSpanH.Value.HasValue) return;
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is null || string.IsNullOrEmpty(frame.TextureName)) return;

        int gridSize = GetGridSizeFromInput();
        if (gridSize < 1) gridSize = 1;

        var tmi = SelectedState.Self.SelectedTileMapInformation;
        if (tmi is null)
        {
            tmi = new TileMapInformation { Name = frame.TextureName };
            ProjectManager.Self.TileMapInformationList.TileMapInfos.Add(tmi);
        }
        tmi.TileWidth  = (int)PropSpanW.Value.Value * gridSize;
        tmi.TileHeight = (int)PropSpanH.Value.Value * gridSize;
        // Recompute frame UV bounds so the selection box reflects the new cell size.
        ApplyFrameTileCoords();
    }

    private void ApplyFrameTileCoords()
    {
        if (_suppressPropRefresh) return;
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is null) return;
        if (!PropTileX.Value.HasValue || !PropTileY.Value.HasValue) return;
        var (bmpW, bmpH) = WireframeCtrl.BitmapSize;
        if (bmpW <= 0 || bmpH <= 0) return;

        int gridSize = GetGridSizeFromInput();
        if (gridSize < 1) gridSize = 1;
        int cellW = PropSpanW.Value.HasValue ? (int)PropSpanW.Value.Value * gridSize : 16;
        int cellH = PropSpanH.Value.HasValue ? (int)PropSpanH.Value.Value * gridSize : 16;
        if (cellW <= 0 || cellH <= 0) return;

        var (left, right) = TileCoordinateCalculator.GetLeftRight((int)PropTileX.Value.Value, cellW, bmpW);
        var (top,  bot)   = TileCoordinateCalculator.GetTopBottom((int)PropTileY.Value.Value, cellH, bmpH);
        frame.LeftCoordinate   = left;
        frame.RightCoordinate  = right;
        frame.TopCoordinate    = top;
        frame.BottomCoordinate = bot;
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        WireframeCtrl.RefreshFrames();
    }

    private void ApplyRectProps()
    {
        if (_suppressPropRefresh) return;
        var rect = SelectedState.Self.SelectedRectangle;
        if (rect is null) return;
        rect.Name = PropRectName.Text ?? "";
        if (PropRectX.Value.HasValue)      rect.X      = (float)PropRectX.Value.Value;
        if (PropRectY.Value.HasValue)      rect.Y      = (float)PropRectY.Value.Value;
        if (PropRectScaleX.Value.HasValue) rect.ScaleX = (float)PropRectScaleX.Value.Value;
        if (PropRectScaleY.Value.HasValue) rect.ScaleY = (float)PropRectScaleY.Value.Value;
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        AppCommands.Self.RefreshWireframe();
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is not null) AppCommands.Self.RefreshTreeNode(frame);
    }

    private void ApplyCircleProps()
    {
        if (_suppressPropRefresh) return;
        var circ = SelectedState.Self.SelectedCircle;
        if (circ is null) return;
        circ.Name = PropCircleName.Text ?? "";
        if (PropCircleX.Value.HasValue)      circ.X      = (float)PropCircleX.Value.Value;
        if (PropCircleY.Value.HasValue)      circ.Y      = (float)PropCircleY.Value.Value;
        if (PropCircleRadius.Value.HasValue) circ.Radius = (float)PropCircleRadius.Value.Value;
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        AppCommands.Self.RefreshWireframe();
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is not null) AppCommands.Self.RefreshTreeNode(frame);
    }

    // ── Playback controls wiring ──────────────────────────────────────────────

    private void WirePlaybackControls()
    {
        SpeedInput.LostFocus += (_, _) => ApplySpeedFromInput();
        SpeedUpBtn.Click   += (_, _) =>
        {
            double s = Math.Min(Math.Round(GetSpeedFromInput() + 0.1, 1), 10.0);
            SpeedInput.Text = s.ToString("0.0#");
            PreviewCtrl.SpeedMultiplier = s;
        };
        SpeedDownBtn.Click += (_, _) =>
        {
            double s = Math.Max(Math.Round(GetSpeedFromInput() - 0.1, 1), 0.1);
            SpeedInput.Text = s.ToString("0.0#");
            PreviewCtrl.SpeedMultiplier = s;
        };
    }

    private double GetSpeedFromInput() =>
        double.TryParse(SpeedInput.Text, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out double v)
            ? Math.Clamp(v, 0.1, 10.0)
            : 1.0;

    private void ApplySpeedFromInput()
    {
        double s = GetSpeedFromInput();
        SpeedInput.Text = s.ToString("0.0#");
        PreviewCtrl.SpeedMultiplier = s;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void LoadAnimationFile(string fileName)
    {
        if (!string.IsNullOrEmpty(fileName))
            ApplicationEvents.Self.CallAchxLoaded(fileName);
    }

    private void UpdateTitle()
    {
        Title = string.IsNullOrEmpty(ProjectManager.Self.FileName)
            ? "AnimationEditor"
            : $"AnimationEditor - {ProjectManager.Self.FileName}";
    }

    private void LoadSettingsFile()
    {
        try
        {
            if (SettingsFilePath.Exists())
            {
                var contents = File.ReadAllText(SettingsFilePath.FullPath);
                _appSettings = JsonConvert.DeserializeObject<AppSettingsModel>(contents)
                               ?? new AppSettingsModel();
            }
        }
        catch
        {
            _appSettings = new AppSettingsModel();
        }
    }

    private void SaveSettingsFile()
    {
        try
        {
            File.WriteAllText(SettingsFilePath.FullPath,
                JsonConvert.SerializeObject(_appSettings, Formatting.Indented));
        }
        catch (IOException)
        {
            // File in use — ignore
        }
    }

    private async Task<bool> ShowConfirmDialogAsync(string message, string title)
    {
        var tcs = new TaskCompletionSource<bool>();

        var dialog = new Window
        {
            Title = title,
            Width = 420,
            Height = 160,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        var yesBtn = new Button { Content = "Yes" };
        var noBtn  = new Button { Content = "No" };
        yesBtn.Click += (_, _) => { tcs.TrySetResult(true);  dialog.Close(); };
        noBtn.Click  += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };
        buttons.Children.Add(yesBtn);
        buttons.Children.Add(noBtn);
        panel.Children.Add(buttons);

        dialog.Content = panel;
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        await dialog.ShowDialog(this);
        return await tcs.Task;
    }

    // ── String-input dialog helper ────────────────────────────────────────────

    private async Task<string?> ShowStringInputDialogAsync(string title, string prompt, string initial = "")
    {
        var tcs = new TaskCompletionSource<string?>();

        var dialog = new Window
        {
            Title = title,
            Width = 380,
            Height = 155,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var tb = new TextBox { Text = initial };
        var ok = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel" };

        ok.Click     += (_, _) => { tcs.TrySetResult(tb.Text); dialog.Close(); };
        cancel.Click += (_, _) => { tcs.TrySetResult(null);    dialog.Close(); };
        dialog.Closed += (_, _) => tcs.TrySetResult(null);

        var btns = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Avalonia.Thickness(14), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = prompt, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(tb);
        panel.Children.Add(btns);

        dialog.Content = panel;
        dialog.Opened += (_, _) =>
        {
            tb.Focus();
            tb.SelectAll();
        };

        tb.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)  { tcs.TrySetResult(tb.Text); dialog.Close(); }
            if (e.Key == Key.Escape) { tcs.TrySetResult(null);    dialog.Close(); }
        };

        await dialog.ShowDialog(this);
        return await tcs.Task;
    }

    // ── Message dialog helper ─────────────────────────────────────────────────

    private async Task ShowMessageAsync(string message, string title = "Animation Editor")
    {
        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 145,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var ok = new Button { Content = "OK", IsDefault = true, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 12 };
        panel.Children.Add(new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap });
        panel.Children.Add(ok);
        dialog.Content = panel;
        ok.Click += (_, _) => dialog.Close();
        await dialog.ShowDialog(this);
    }

    // ── Keyboard wiring ───────────────────────────────────────────────────────

    private void WireKeyboard()
    {
        // Use Tunnel routing so we intercept keys before child controls (e.g. the TreeView,
        // which handles Up/Down for navigation and would mark the event Handled before the
        // default Bubble-phase KeyDown fires).
        AddHandler(KeyDownEvent, (EventHandler<KeyEventArgs>)((_, e) =>
        {
            if (e.Handled) return;

            if (e.Key == Key.C && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                _ = HandleCopyAsync();
            }
            else if (e.Key == Key.V && e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                e.Handled = true;
                _ = HandlePasteAsync();
            }
            else if (e.Key == Key.Delete)
            {
                e.Handled = true;
                HandleDelete();
            }
            else if (e.Key == Key.F2)
            {
                e.Handled = true;
                if (AnimTree.IsKeyboardFocusWithin &&
                    AnimTree.SelectedItem is TreeNodeVm vm &&
                    vm.Data is AnimationChainSave chain)
                    BeginInlineRename(vm, chain);
                else
                    WireframeCtrl.ToggleDebugMode();
            }
            else if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control) &&
                     !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                UndoManager.Self.Undo();
            }
            else if ((e.Key == Key.Y && e.KeyModifiers.HasFlag(KeyModifiers.Control)) ||
                     (e.Key == Key.Z && e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift)))
            {
                e.Handled = true;
                UndoManager.Self.Redo();
            }
            else if ((e.Key == Key.Up || e.Key == Key.Down) &&
                     e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                e.Handled = true;
                int delta = e.Key == Key.Up ? -1 : +1;
                AppCommands.Self.HandleReorder(delta);
            }
        }), RoutingStrategies.Tunnel);
    }

    // ── Copy / Paste ──────────────────────────────────────────────────────────

    private async Task HandleCopyAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        string? xml = null;

        var selectedVm = AnimTree.SelectedItem as TreeNodeVm;
        if (selectedVm?.Data is AnimationChainSave chainToCopy)
            xml = ClipboardPayload.Serialize(new List<AnimationChainSave> { chainToCopy });
        else if (selectedVm?.Data is AnimationFrameSave frameToCopy)
            xml = ClipboardPayload.Serialize(new List<AnimationFrameSave> { frameToCopy });
        else if (selectedVm?.Data is AxisAlignedRectangleSave rectToCopy)
            xml = ClipboardPayload.Serialize(rectToCopy);
        else if (selectedVm?.Data is CircleSave circleToCopy)
            xml = ClipboardPayload.Serialize(circleToCopy);

        if (xml is not null)
            await clipboard.SetTextAsync(xml);
    }

    private async Task HandlePasteAsync()
    {
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(text)) return;

        bool ok = ClipboardPayload.TryDeserialize(text,
            out var chains, out var frames, out var rectangle, out var circle);
        if (!ok) return;

        var acls = ProjectManager.Self.AnimationChainListSave;
        if (acls is null) return;

        var selectedVm = AnimTree.SelectedItem as TreeNodeVm;

        if (chains is { Count: > 0 })
        {
            var existingNames = acls.AnimationChains.Select(c => c.Name).ToList();
            foreach (var chain in chains)
            {
                chain.Name = StringFunctions.MakeStringUnique(chain.Name, existingNames, 2);
                existingNames.Add(chain.Name);
                acls.AnimationChains.Add(chain);
            }
            SelectedState.Self.SelectedChain = chains[^1];
            RefreshTreeView();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
        }
        else if (frames is { Count: > 0 })
        {
            AnimationChainSave? targetChain = null;
            if (selectedVm?.Data is AnimationChainSave c) targetChain = c;
            else if (selectedVm?.Data is AnimationFrameSave f)
                targetChain = AnimationEditor.Core.ObjectFinder.Self.GetAnimationChainContaining(f);

            if (targetChain is null && acls.AnimationChains.Count > 0)
                targetChain = acls.AnimationChains[^1];

            if (targetChain is null) return;

            foreach (var pasted in frames)
            {
                pasted.ShapeCollectionSave ??= new FlatRedBall.Content.Math.Geometry.ShapeCollectionSave();
                targetChain.Frames.Add(pasted);
            }
            SelectedState.Self.SelectedFrame = frames[^1];
            AppCommands.Self.RefreshTreeNode(targetChain);
            AppCommands.Self.RefreshWireframe();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
        }
        else if (rectangle is not null)
        {
            var frame = SelectedState.Self.SelectedFrame;
            if (frame is null) return;
            frame.ShapeCollectionSave ??= new FlatRedBall.Content.Math.Geometry.ShapeCollectionSave();
            var existingNames = frame.ShapeCollectionSave.AxisAlignedRectangleSaves
                .Select(r => r.Name)
                .Concat(frame.ShapeCollectionSave.CircleSaves.Select(c => c.Name))
                .ToList();
            rectangle.Name = StringFunctions.MakeStringUnique(
                rectangle.Name, existingNames, 2);
            frame.ShapeCollectionSave.AxisAlignedRectangleSaves.Add(rectangle);
            AppCommands.Self.RefreshTreeNode(frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
        }
        else if (circle is not null)
        {
            var frame = SelectedState.Self.SelectedFrame;
            if (frame is null) return;
            frame.ShapeCollectionSave ??= new FlatRedBall.Content.Math.Geometry.ShapeCollectionSave();
            var existingNames = frame.ShapeCollectionSave.AxisAlignedRectangleSaves
                .Select(r => r.Name)
                .Concat(frame.ShapeCollectionSave.CircleSaves.Select(c => c.Name))
                .ToList();
            circle.Name = StringFunctions.MakeStringUnique(
                circle.Name, existingNames, 2);
            frame.ShapeCollectionSave.CircleSaves.Add(circle);
            AppCommands.Self.RefreshTreeNode(frame);
            AppCommands.Self.RefreshAnimationFrameDisplay();
            ApplicationEvents.Self.RaiseAnimationChainsChanged();
        }

        AppCommands.Self.SaveCurrentAnimationChainList();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    private void HandleDelete()
    {
        var selectedVm = AnimTree.SelectedItem as TreeNodeVm;
        if (selectedVm is null) return;

        if (selectedVm.Data is AnimationChainSave chainToDel)
            _ = AppCommands.Self.AskToDeleteAnimationChains(new() { chainToDel });
        else if (selectedVm.Data is AnimationFrameSave frameToDel)
            _ = AppCommands.Self.AskToDeleteFrames(new() { frameToDel });
        else if (selectedVm.Data is AxisAlignedRectangleSave rectToDel)
            _ = AppCommands.Self.AskToDeleteRectangles(new() { rectToDel });
        else if (selectedVm.Data is CircleSave circleToDel)
            _ = AppCommands.Self.AskToDeleteCircles(new() { circleToDel });
    }

    // ── Add Multiple Frames ───────────────────────────────────────────────────

    private async Task AskAddMultipleFramesAsync(AnimationChainSave chain)
    {
        var dialog = new Window
        {
            Title = "Add Multiple Frames",
            Width = 320,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var countInput = new NumericUpDown
        {
            Value = 1, Minimum = 1, Maximum = 1000, Increment = 1,
            FormatString = "0", Width = 100
        };
        var incrToggle = new CheckBox { Content = "Increment UV", IsChecked = true };

        var ok     = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel" };
        ok.Click     += (_, _) => dialog.Close();
        cancel.Click += (_, _) => { countInput.Value = 0; dialog.Close(); };
        dialog.Closed += (_, _) => { };

        var btns = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Avalonia.Thickness(14), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = "Number of frames to add:" });
        panel.Children.Add(countInput);
        panel.Children.Add(incrToggle);
        panel.Children.Add(btns);
        dialog.Content = panel;

        await dialog.ShowDialog(this);

        int count = (int)(countInput.Value ?? 0);
        if (count <= 0) return;

        bool exceededBounds = AppCommands.Self.AddMultipleFrames(
            chain, count, incrToggle.IsChecked == true);

        if (exceededBounds)
            await ShowMessageAsync("Some frames were clipped because they exceeded the texture bounds.");

        AppCommands.Self.RefreshTreeNode(chain);
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        AppCommands.Self.SaveCurrentAnimationChainList();
    }

    // ── Adjust Offsets ────────────────────────────────────────────────────────

    private async Task AskAdjustOffsetsAsync(AnimationChainSave chain)
    {
        var dialog = new Window
        {
            Title = "Adjust Offsets",
            Width = 340,
            Height = 265,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var justifyBottomRb = new RadioButton { Content = "Justify Bottom", IsChecked = true, GroupName = "mode" };
        var adjustAllRb     = new RadioButton { Content = "Adjust All (enter values)", GroupName = "mode" };
        var (adjustAllRow, relXInput, relYInput) = BuildAdjustAllRow();

        var absoluteRb = new RadioButton { Content = "Absolute", IsChecked = true, GroupName = "offsetMode" };
        var relativeRb = new RadioButton { Content = "Relative", GroupName = "offsetMode" };

        var offsetModeRow = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 16
        };
        offsetModeRow.Children.Add(absoluteRb);
        offsetModeRow.Children.Add(relativeRb);

        adjustAllRb.IsCheckedChanged += (_, _) =>
        {
            adjustAllRow.IsVisible   = adjustAllRb.IsChecked == true;
            offsetModeRow.IsVisible  = adjustAllRb.IsChecked == true;
        };
        adjustAllRow.IsVisible  = false;
        offsetModeRow.IsVisible = false;

        bool confirmed = false;
        var ok = new Button { Content = "OK", IsDefault = true };
        ok.Click += (_, _) => { confirmed = true; dialog.Close(); };
        var cancel = new Button { Content = "Cancel" };
        cancel.Click += (_, _) => dialog.Close();

        var btns = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);

        var panel = new StackPanel { Margin = new Avalonia.Thickness(14), Spacing = 8 };
        panel.Children.Add(justifyBottomRb);
        panel.Children.Add(adjustAllRb);
        panel.Children.Add(adjustAllRow);
        panel.Children.Add(offsetModeRow);
        panel.Children.Add(btns);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
        if (!confirmed) return;

        var (bmpW, bmpH) = WireframeCtrl.BitmapSize;

        if (justifyBottomRb.IsChecked == true)
        {
            AppCommands.Self.AdjustOffsetsJustifyBottom(chain, frame =>
            {
                if (bmpH > 0 && !string.IsNullOrEmpty(frame.TextureName))
                    return (float)bmpH;
                return null;
            });
        }
        else
        {
            AppCommands.Self.AdjustOffsetsAdjustAll(chain,
                (float)(relXInput.Value ?? 0),
                (float)(relYInput.Value ?? 0),
                relative: relativeRb.IsChecked == true);
        }

        AppCommands.Self.RefreshAnimationFrameDisplay();
        AppCommands.Self.SaveCurrentAnimationChainList();
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
    }

    /// <summary>
    /// Builds the X/Y input row for the Adjust Offsets dialog using a Grid so both
    /// inputs receive proportional space rather than being squashed inside a StackPanel.
    /// </summary>
    public static (Grid AdjustAllRow, NumericUpDown RelXInput, NumericUpDown RelYInput) BuildAdjustAllRow()
    {
        var relXInput = new NumericUpDown { Value = 0, FormatString = "0.###", Minimum = -9999, Maximum = 9999 };
        var relYInput = new NumericUpDown { Value = 0, FormatString = "0.###", Minimum = -9999, Maximum = 9999 };

        var xLabel = new TextBlock
        {
            Text = "X:",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(0, 0, 4, 0)
        };
        var yLabel = new TextBlock
        {
            Text = "Y:",
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(8, 0, 4, 0)
        };

        Grid.SetColumn(xLabel, 0);
        Grid.SetColumn(relXInput, 1);
        Grid.SetColumn(yLabel, 2);
        Grid.SetColumn(relYInput, 3);

        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*,Auto,*") };
        grid.Children.Add(xLabel);
        grid.Children.Add(relXInput);
        grid.Children.Add(yLabel);
        grid.Children.Add(relYInput);

        return (grid, relXInput, relYInput);
    }

    // ── Resize Texture ────────────────────────────────────────────────────────

    private async Task DoResizeTextureAsync()
    {
        var frame = SelectedState.Self.SelectedFrame;
        if (frame is null || string.IsNullOrEmpty(frame.TextureName))
        {
            await ShowMessageAsync("Select a frame with a texture before resizing.");
            return;
        }

        string? achxDir = null;
        if (!string.IsNullOrEmpty(ProjectManager.Self.FileName))
            achxDir = FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName);

        var absTexPath = achxDir is not null
            ? Path.GetFullPath(Path.Combine(achxDir, frame.TextureName))
            : frame.TextureName;

        if (!File.Exists(absTexPath))
        {
            await ShowMessageAsync($"Texture file not found:\n{absTexPath}");
            return;
        }

        // Read current dimensions
        int oldW, oldH;
        using (var bmp = SKBitmap.Decode(absTexPath))
        {
            if (bmp is null)
            {
                await ShowMessageAsync("Could not read texture file.");
                return;
            }
            oldW = bmp.Width;
            oldH = bmp.Height;
        }

        // Dialog: enter new size
        var dialog = new Window
        {
            Title = "Resize Texture",
            Width = 300,
            Height = 195,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var wInput = new NumericUpDown { Value = oldW, Minimum = 1, Maximum = 65536, FormatString = "0", Width = 90 };
        var hInput = new NumericUpDown { Value = oldH, Minimum = 1, Maximum = 65536, FormatString = "0", Width = 90 };

        bool confirmed = false;
        var ok     = new Button { Content = "OK", IsDefault = true };
        var cancel = new Button { Content = "Cancel" };
        ok.Click     += (_, _) => { confirmed = true; dialog.Close(); };
        cancel.Click += (_, _) => dialog.Close();

        var btns = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        btns.Children.Add(ok);
        btns.Children.Add(cancel);

        var wRow = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        wRow.Children.Add(new TextBlock { Text = "Width:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Width = 50 });
        wRow.Children.Add(wInput);

        var hRow = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6 };
        hRow.Children.Add(new TextBlock { Text = "Height:", VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Width = 50 });
        hRow.Children.Add(hInput);

        var panel = new StackPanel { Margin = new Avalonia.Thickness(14), Spacing = 10 };
        panel.Children.Add(new TextBlock { Text = $"Current size: {oldW} × {oldH}" });
        panel.Children.Add(wRow);
        panel.Children.Add(hRow);
        panel.Children.Add(btns);
        dialog.Content = panel;

        await dialog.ShowDialog(this);
        if (!confirmed) return;

        int newW = (int)(wInput.Value ?? oldW);
        int newH = (int)(hInput.Value ?? oldH);

        if (newW == oldW && newH == oldH)
        {
            await ShowMessageAsync("New size is the same as current size. No changes made.");
            return;
        }

        // Save resized copy as <name>Resize.png
        string dir        = Path.GetDirectoryName(absTexPath)!;
        string baseName   = Path.GetFileNameWithoutExtension(absTexPath);
        string newAbsPath = Path.Combine(dir, baseName + "Resize.png");

        using (var src = SKBitmap.Decode(absTexPath))
        using (var resized = new SKBitmap(newW, newH))
        using (var canvas = new SKCanvas(resized))
        {
            canvas.DrawBitmap(src, new SKRect(0, 0, newW, newH));
            canvas.Flush();
            using var stream = File.OpenWrite(newAbsPath);
            resized.Encode(stream, SKEncodedImageFormat.Png, 100);
        }

        // Adjust UV coordinates in all chains
        var acls = ProjectManager.Self.AnimationChainListSave;
        if (acls is not null)
        {
            var modifiedFrames = AnimationEditor.Core.IO.TextureResizeAdjuster.AdjustAll(
                acls, achxDir ?? "", absTexPath, oldW, oldH, newW, newH);

            // Re-reference all modified frames to the new texture file
            string newRelPath = achxDir is not null
                ? FlatRedBall.IO.FileManager.MakeRelative(newAbsPath, achxDir)
                : newAbsPath;

            foreach (var f in modifiedFrames)
                f.TextureName = newRelPath;
        }

        RefreshTreeView();
        AppCommands.Self.RefreshWireframe();
        RefreshTextureCombo();
        AppCommands.Self.SaveCurrentAnimationChainList();
        ApplicationEvents.Self.RaiseAnimationChainsChanged();

        await ShowMessageAsync($"Resized texture saved to:\n{newAbsPath}");
    }

    // ── Rename Chain / Frame ──────────────────────────────────────────────────

    // ── Inline rename helpers ─────────────────────────────────────────────────

    private void OnAnimTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Control src) return;
        var tvi = src.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (tvi?.DataContext is not TreeNodeVm vm) return;
        if (vm.Data is not AnimationChainSave chain) return;
        e.Handled = true;
        BeginInlineRename(vm, chain);
    }

    private void OnInlineRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not TextBox tb) return;
        if (tb.DataContext is not TreeNodeVm vm) return;
        if (vm.Data is not AnimationChainSave chain) return;

        if (e.Key == Key.Return)
        {
            e.Handled = true;
            CommitInlineRename(vm, chain, tb.Text ?? string.Empty);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            vm.CancelEdit();
            AnimTree.Focus();
        }
    }

    private void OnInlineRenameLostFocus(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not TextBox tb) return;
        if (tb.DataContext is not TreeNodeVm vm) return;
        if (!vm.IsEditing) return;
        if (vm.Data is not AnimationChainSave chain) return;
        CommitInlineRename(vm, chain, tb.Text ?? string.Empty);
    }

    private void BeginInlineRename(TreeNodeVm vm, AnimationChainSave chain)
    {
        vm.BeginEdit();
        // After the visual tree updates, focus and select-all in the TextBox
        Dispatcher.UIThread.Post(() =>
        {
            var tb = AnimTree.GetVisualDescendants()
                .OfType<TextBox>()
                .FirstOrDefault(t => t.DataContext == vm);
            if (tb is null) return;
            tb.Focus();
            tb.SelectAll();
        }, DispatcherPriority.Render);
    }

    private void BeginInlineRenameSelected(AnimationChainSave chain)
    {
        var vm = TreeBuilder.FindNodeForData(_treeRoots, chain);
        if (vm is null) return;
        BeginInlineRename(vm, chain);
    }

    private void CommitInlineRename(TreeNodeVm vm, AnimationChainSave chain, string newName)
    {
        newName = newName.Trim();
        vm.IsEditing = false;
        if (!string.IsNullOrEmpty(newName) && newName != chain.Name)
            AppCommands.Self.RenameChain(chain, newName);
        AnimTree.Focus();
    }

    private async Task AskRenameChainAsync(AnimationChainSave chain)
    {
        var name = await ShowStringInputDialogAsync("Rename Animation Chain", "New name:", chain.Name);
        if (name is null) return;
        name = name.Trim();
        if (string.IsNullOrEmpty(name))
        {
            await ShowMessageAsync("Chain name cannot be empty.");
            return;
        }
        AppCommands.Self.RenameChain(chain, name);
        AppCommands.Self.RefreshTreeNode(chain);
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        AppCommands.Self.SaveCurrentAnimationChainList();
    }

    private async Task AskRenameFrameAsync(AnimationFrameSave frame)
    {
        var name = await ShowStringInputDialogAsync(
            "Change Texture Path",
            "New texture path (relative to ACHX):",
            frame.TextureName ?? "");
        if (name is null) return;
        AppCommands.Self.RenameFrame(frame, name);
        var chain = AnimationEditor.Core.ObjectFinder.Self.GetAnimationChainContaining(frame);
        if (chain is not null) AppCommands.Self.RefreshTreeNode(chain);
        AppCommands.Self.RefreshWireframe();
        RefreshTextureCombo();
        ApplicationEvents.Self.RaiseAnimationChainsChanged();
        AppCommands.Self.SaveCurrentAnimationChainList();
    }

    // ── View Texture in Explorer ──────────────────────────────────────────────

    private void ViewTextureInExplorer(AnimationFrameSave frame)
    {
        if (string.IsNullOrEmpty(frame.TextureName))
        {
            _ = ShowMessageAsync("This frame has no texture path set.");
            return;
        }

        string? achxDir = null;
        if (!string.IsNullOrEmpty(ProjectManager.Self.FileName))
            achxDir = FlatRedBall.IO.FileManager.GetDirectory(ProjectManager.Self.FileName);

        var absPath = achxDir is not null
            ? Path.GetFullPath(Path.Combine(achxDir, frame.TextureName))
            : frame.TextureName;

        if (!File.Exists(absPath))
        {
            _ = ShowMessageAsync($"Texture file not found:\n{absPath}");
            return;
        }

        try
        {
            Process.Start("explorer.exe", $"/select,\"{absPath}\"");
        }
        catch (Exception ex)
        {
            _ = ShowMessageAsync($"Could not open Explorer:\n{ex.Message}");
        }
    }
}

