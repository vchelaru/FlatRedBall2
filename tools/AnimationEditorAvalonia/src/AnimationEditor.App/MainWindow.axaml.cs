using AnimationEditor.App.Theming;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.DragDrop;
using AnimationEditor.Core.HotReload;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Rendering;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.VisualTree;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Styling;
using Avalonia.Threading;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using StringFunctions = AnimationEditor.Core.Utilities.StringFunctions;

namespace AnimationEditor.App;

public partial class MainWindow : Window
{
    private readonly IProjectManager _projectManager;
    private readonly ISelectedState _selectedState;
    private readonly IAppCommands _appCommands;
    private readonly IAppState _appState;
    private readonly IApplicationEvents _events;
    private readonly IIoManager _ioManager;
    private readonly IObjectFinder _objectFinder;
    private readonly IUndoManager _undoManager;
    private readonly Services.ThumbnailService _thumbnailService;
    private readonly IFileAssociationService _fileAssociation;

    private AppSettingsModel _appSettings = new();
    private readonly TabManager _tabManager = new();

    // ── Tab drag state ────────────────────────────────────────────────────────
    private TabEntry? _dragTab;
    private double _dragStartX;
    private bool _isDragging;
    private Border? _ghostBorder;
    private int _untitledCounter;
    private bool _suppressPropRefresh;
    private bool _suppressTextureComboChanged;
    private bool _suppressZoomComboChanged;
    private bool _suppressPreviewZoomComboChanged;
    private bool _suppressPreviewScrollSync;
    private bool _suppressWireframeScrollSync;
    private bool _suppressTreeSelectionHandling;
    private bool _suppressCompanionSave;
    private bool _suppressInterpolateSync;
    private System.Threading.CancellationTokenSource? _toastCts;

    // Shared per-user config dir, NOT the build output — so recent files / tabs / theme survive
    // rebuilds, dotnet clean, and switching git worktrees (see issue #424). AppContext.BaseDirectory
    // resolves to bin/<Config>/<TFM>/, which is per-build / per-checkout.
    private FilePath SettingsFilePath =>
        AppSettingsLocation.ForApplicationDataRoot(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData));

    public MainWindow(
        IProjectManager projectManager,
        ISelectedState selectedState,
        IAppCommands appCommands,
        IAppState appState,
        IApplicationEvents events,
        IIoManager ioManager,
        IObjectFinder objectFinder,
        IUndoManager undoManager,
        Services.ThumbnailService thumbnailService,
        IFileAssociationService fileAssociation)
    {
        _projectManager = projectManager;
        _selectedState = selectedState;
        _appCommands = appCommands;
        _appState = appState;
        _events = events;
        _ioManager = ioManager;
        _objectFinder = objectFinder;
        _undoManager = undoManager;
        _thumbnailService = thumbnailService;
        _fileAssociation = fileAssociation;

        InitializeComponent();

        if (OperatingSystem.IsMacOS())
            ApplyMacOSWindowChrome();

        InitToast();
        PropertyChanged += (_, e) => { if (e.Property == OffScreenMarginProperty) Padding = OffScreenMargin; };

        WireAppCommands();
        LoadSettingsFile();
        ApplyPersistedTheme();
        WireMenuEvents();
        WireWireframeToolbar();
        WireWireframeControl();
        WirePreviewControls();
        WireTreeView();
        WireWindowFileDrop();
        WirePropertyPanel();
        WirePlaybackControls();
        WireKeyboard();
        WireTabBar();
        WireDefaultHandlerBanner();

        WireframeCtrl.InitializeServices(_selectedState, _appState, _appCommands, _events, _projectManager, _undoManager);
        PreviewCtrl.InitializeServices(_selectedState, _appState, _appCommands, _events, _projectManager, _undoManager, _thumbnailService);

        Opened += OnOpened;
        Closed += (_, _) =>
        {
            SaveTabsToSettings();
            _appCommands.HotReloadWatcher.Dispose();
            PreviewCtrl.Playback.FrameIndexChanged -= OnPreviewPlaybackFrameIndexChanged;
            foreach (var vm in _timelineFrames)
                (vm.Thumbnail as IDisposable)?.Dispose();
            DisposeTreeThumbnails();
        };
    }

    // ── Tab bar ───────────────────────────────────────────────────────────────

    private void WireTabBar()
    {
        _tabManager.ActiveChanged += _ => Dispatcher.UIThread.InvokeAsync(RebuildTabStrip);
    }

    private void RebuildTabStrip()
    {
        TabStrip.Children.Clear();

        var tabs = _tabManager.Tabs;
        TabBarBorder.IsVisible = tabs.Count > 1;

        foreach (var tab in tabs)
        {
            bool isActive = tab == _tabManager.ActiveTab;
            var captured = tab;

            // Tab container
            var tabBorder = new Border
            {
                Background = isActive ? ThemedBrush("BgActive") : Avalonia.Media.Brushes.Transparent,
                BorderBrush = ThemedBrush("LineBrush"),
                BorderThickness = new Avalonia.Thickness(0, 0, 1, 0),
                Padding = new Avalonia.Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
            };

            ToolTip.SetTip(tabBorder, tab.Path.FullPath);

            var row = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto"),
                Margin = new Avalonia.Thickness(8, 0, 0, 0),
            };

            var label = new TextBlock
            {
                Text = tab.DisplayName,
                FontSize = 11,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Foreground = isActive ? ThemedBrush("Ink") : ThemedBrush("InkMid"),
            };
            Grid.SetColumn(label, 0);

            var closeBtn = new Button
            {
                Content = "✕",
                FontSize = 9,
                Width = 20,
                Height = 20,
                Padding = new Avalonia.Thickness(0),
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Avalonia.Thickness(0),
                Foreground = ThemedBrush("InkMid"),
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Avalonia.Thickness(2, 0, 2, 0),
            };
            Grid.SetColumn(closeBtn, 1);
            closeBtn.Click += (_, _) => CloseTab(captured);

            row.Children.Add(label);
            row.Children.Add(closeBtn);
            tabBorder.Child = row;

            // Pointer handling: immediate pointer-capture on press (so PointerMoved fires even
            // when the cursor moves over other tabs).  Activation is deferred to PointerReleased
            // so that RebuildTabStrip is never called while a drag is in-flight.
            // Close-button presses are excluded from capture by checking args.Source.
            tabBorder.PointerPressed += (_, args) =>
            {
                var pt = args.GetCurrentPoint(tabBorder);
                if (pt.Properties.IsRightButtonPressed)
                {
                    var menu = new ContextMenu
                    {
                        Items =
                        {
                            new MenuItem
                            {
                                Header = "Detach to New Window",
                                Command = new RelayCommand(() => DetachTab(captured)),
                            },
                            new MenuItem
                            {
                                Header = "Close Tab",
                                Command = new RelayCommand(() => CloseTab(captured)),
                            },
                        },
                    };
                    menu.Open(tabBorder);
                    args.Handled = true;
                    return;
                }

                if (pt.Properties.IsLeftButtonPressed)
                {
                    // Skip close-button presses so Button.Click still fires normally.
                    if (args.Source is Button) return;

                    // Capture immediately so PointerMoved always arrives here even when the
                    // cursor moves across other tabs.  Activation happens on release.
                    args.Pointer.Capture(tabBorder);
                    _dragTab = captured;
                    _dragStartX = args.GetPosition(TabStrip).X;
                    _isDragging = false;
                    args.Handled = true;
                }
            };

            tabBorder.PointerMoved += (_, args) =>
            {
                if (_dragTab != captured) return;
                double x = args.GetPosition(TabStrip).X;
                if (!_isDragging && Math.Abs(x - _dragStartX) > 5)
                {
                    _isDragging = true;
                    tabBorder.Cursor = new Cursor(StandardCursorType.DragMove);
                    tabBorder.Opacity = 0.4;

                    // Create a floating ghost label that follows the pointer
                    _ghostBorder = new Border
                    {
                        Background = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#3a4150")),
                        BorderBrush = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#4a90d9")),
                        BorderThickness = new Avalonia.Thickness(1),
                        CornerRadius = new Avalonia.CornerRadius(3),
                        Padding = new Avalonia.Thickness(10, 5),
                        Opacity = 0.92,
                        IsHitTestVisible = false,
                        Child = new TextBlock
                        {
                            Text = captured.DisplayName,
                            FontSize = 11,
                            Foreground = new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.Parse("#d4d8de")),
                        },
                    };
                    var initPos = args.GetPosition(DragOverlayCanvas);
                    Canvas.SetLeft(_ghostBorder, initPos.X + 10);
                    Canvas.SetTop(_ghostBorder, initPos.Y - 18);
                    DragOverlayCanvas.Children.Add(_ghostBorder);
                }

                if (_isDragging && _ghostBorder != null)
                {
                    var pos = args.GetPosition(DragOverlayCanvas);
                    Canvas.SetLeft(_ghostBorder, pos.X + 10);
                    Canvas.SetTop(_ghostBorder, pos.Y - 18);
                }
            };

            tabBorder.PointerReleased += (_, args) =>
            {
                var uk = args.GetCurrentPoint(tabBorder).Properties.PointerUpdateKind;

                if (uk == PointerUpdateKind.MiddleButtonReleased)
                {
                    CloseTab(captured);
                    args.Handled = true;
                    return;
                }

                if (_dragTab != captured || uk != PointerUpdateKind.LeftButtonReleased)
                    return;

                args.Pointer.Capture(null);
                tabBorder.Cursor = new Cursor(StandardCursorType.Hand);
                tabBorder.Opacity = 1.0;

                // Remove ghost overlay
                if (_ghostBorder != null)
                {
                    DragOverlayCanvas.Children.Remove(_ghostBorder);
                    _ghostBorder = null;
                }

                if (_isDragging)
                {
                    int targetIdx = ComputeTabIndexAt(args.GetPosition(TabStrip).X);
                    _tabManager.Move(captured.Path, targetIdx);
                    RebuildTabStrip();
                }
                else
                {
                    _ = ActivateTabAsync(captured);
                }

                _dragTab = null;
                _isDragging = false;
            };

            TabStrip.Children.Add(tabBorder);
        }
    }

    private async Task ActivateTabAsync(TabEntry tab)
    {
        if (tab == _tabManager.ActiveTab) return;

        // Save the leaving tab's undo history before the editor reloads a different file.
        var leavingTab = _tabManager.ActiveTab;
        if (leavingTab != null)
            leavingTab.UndoSnapshot = _undoManager.TakeSnapshot();

        SaveCompanionFile();
        _tabManager.Activate(tab.Path);
        // Bypass LoadAnimationFileAsync so we don't hit the short-circuit that skips
        // the file load when the path is already the active tab.
        if (IsUntitledTab(tab))
        {
            // Untitled tab — reset to a blank editor; there is no on-disk file to reload.
            _projectManager.AnimationChainListSave = new AnimationChainListSave();
            _projectManager.FileName = null;
            _selectedState.Reset();
            _undoManager.Clear();
            if (tab.UndoSnapshot != null)
                _undoManager.RestoreSnapshot(tab.UndoSnapshot);
            RefreshTreeView();
            UpdateTitle();
            UpdateStatusBar();
        }
        else
        {
            await _appCommands.OpenAchxWorkflowAsync(tab.Path.FullPath);
            // LoadAnimationChain cleared the stack — restore this tab's saved history.
            if (tab.UndoSnapshot != null)
                _undoManager.RestoreSnapshot(tab.UndoSnapshot);
        }
        RebuildTabStrip();
    }

    /// <summary>
    /// Opens <paramref name="filePath"/> as a new tab (or focuses it if already open).
    /// Called from <see cref="App"/> when the single-instance server receives a path from
    /// a second process.
    /// </summary>
    public async Task OpenFileAsTab(string filePath) => await LoadAnimationFileAsync(filePath);

    private void CloseTab(TabEntry tab)
    {
        _tabManager.Close(tab.Path);
        var next = _tabManager.ActiveTab;
        if (next != null)
        {
            // Use OpenAchxWorkflowAsync directly — bypasses EnsureCurrentEditorContentHasTab
            // so the just-closed file is not accidentally re-registered as a background tab.
            if (!IsUntitledTab(next))
                _ = _appCommands.OpenAchxWorkflowAsync(next.Path.FullPath)
                    .ContinueWith(_ => Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        if (next.UndoSnapshot != null)
                            _undoManager.RestoreSnapshot(next.UndoSnapshot);
                        RebuildTabStrip();
                    }));
            else
            {
                // Switching to an Untitled tab — reset to a blank editor.
                _projectManager.AnimationChainListSave = new AnimationChainListSave();
                _projectManager.FileName = null;
                _selectedState.Reset();
                _undoManager.Clear();
                if (next.UndoSnapshot != null)
                    _undoManager.RestoreSnapshot(next.UndoSnapshot);
                RefreshTreeView();
                UpdateTitle();
                UpdateStatusBar();
                RebuildTabStrip();
            }
        }
        else
        {
            // All tabs closed — start fresh
            _projectManager.AnimationChainListSave = new AnimationChainListSave();
            _projectManager.FileName = null;
            _selectedState.Reset();
            _undoManager.Clear();
            RefreshTreeView();
            UpdateTitle();
            UpdateStatusBar();
        }
    }

    private void SaveTabsToSettings()
    {
        // Exclude unsaved (sentinel) Untitled tabs — they have no on-disk path to restore.
        _appSettings.OpenTabPaths = _tabManager.OpenTabPaths
            .Where(p => !IsUntitledSentinel(p))
            .ToList();
        _appSettings.ActiveTabPath = IsUntitledTab(_tabManager.ActiveTab)
            ? null
            : _tabManager.ActiveTab?.Path.FullPath;
        SaveSettingsFile();
    }

    private async Task RestoreTabsAsync()
    {
        if (_appSettings.OpenTabPaths.Count == 0) return;

        // Filter to paths that still exist on disk
        var valid = _appSettings.OpenTabPaths
            .Where(p => File.Exists(p))
            .ToList();
        if (valid.Count == 0) return;

        _tabManager.RestoreFrom(valid, _appSettings.ActiveTabPath);
        RebuildTabStrip();

        // Load the active tab's file directly — bypassing LoadAnimationFileAsync avoids
        // the early-return in that method (OpenOrFocus would return Focused for a tab
        // that RestoreFrom already registered, skipping the actual file load).
        var active = _tabManager.ActiveTab;
        if (active != null)
        {
            await _appCommands.OpenAchxWorkflowAsync(active.Path.FullPath);
            RebuildTabStrip();
        }
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private void OnOpened(object? sender, EventArgs e)
    {
        var args = Environment.GetCommandLineArgs();
        if (args.Length >= 2 && File.Exists(args[1]))
        {
            _ = LoadAnimationFileAsync(args[1]);
        }
        else if (_appSettings.OpenTabPaths.Count > 0)
        {
            _ = RestoreTabsAsync();
        }
        else
        {
            _projectManager.AnimationChainListSave =
                new AnimationChainListSave();
        }

        ShowDefaultHandlerBannerIfAppropriate();
    }

    // ── Default-handler prompt banner ─────────────────────────────────────────

    private void WireDefaultHandlerBanner()
    {
        MakeDefaultBtn.Click += (_, _) =>
        {
            _fileAssociation.RegisterAsDefault();
            DefaultHandlerBanner.IsVisible = false;
            ShowStatusMessage("Opened Windows settings — choose Animation Editor for .achx files.");
        };

        DismissDefaultHandlerBtn.Click += (_, _) =>
        {
            _appSettings.SuppressDefaultHandlerPrompt = true;
            SaveSettingsFile();
            DefaultHandlerBanner.IsVisible = false;
        };
    }

    private void ShowDefaultHandlerBannerIfAppropriate()
    {
        bool isDefault = _fileAssociation.IsSupported && _fileAssociation.IsDefault();
        if (DefaultHandlerPromptDecider.ShouldPrompt(
                _fileAssociation.IsSupported, isDefault, _appSettings.SuppressDefaultHandlerPrompt))
        {
            DefaultHandlerBanner.IsVisible = true;
        }
    }

    // ── AppCommands wiring ────────────────────────────────────────────────────

    private void WireAppCommands()
    {
        _appCommands.DoOnUiThread = action => Dispatcher.UIThread.InvokeAsync(action);
        _appCommands.ConfirmAsync = ShowConfirmDialogAsync;
        _appCommands.PromptStringAsync = ShowStringInputDialogAsync;

        // File dialog service
        _appCommands.FileDialogService = new Services.AvaloniaFileDialogService(this);
        _appCommands.LoadFailed += (path, ex) =>
            Dispatcher.UIThread.InvokeAsync(() => ShowLoadFailedDialogAsync(path, ex));

        _appCommands.HotReloadFailed += (path, reason) =>
            Dispatcher.UIThread.InvokeAsync(() =>
                ShowStatusMessage($"⚠ Reload skipped for '{Path.GetFileName(path)}': {reason}", isError: true));

        // Tree events — fully wired (WireTreeView connects these after tree is constructed)
        _appCommands.RefreshTreeViewRequested           += () => Dispatcher.UIThread.InvokeAsync(RefreshTreeView);
        _appCommands.RebuildTreeViewRequested           += () => Dispatcher.UIThread.InvokeAsync(RebuildTreeView);
        _appCommands.RefreshChainNodeRequested          += c  => Dispatcher.UIThread.InvokeAsync(() => RefreshChainNode(c));
        _appCommands.RefreshFrameNodeRequested          += f  => Dispatcher.UIThread.InvokeAsync(() => RefreshFrameNode(f));
        _appCommands.RefreshAnimationFrameDisplayRequested += () => PreviewCtrl.InvalidateVisual();
        // RefreshWireframeRequested is handled by WireframeControl directly

        _events.CurrentFileChanged     += path => Dispatcher.UIThread.InvokeAsync(() =>
        {
            _appSettings.AddFile(new FilePath(path));
            SaveSettingsFile();
            RefreshRecentFiles();
            UpdateTitle();
            UpdateStatusBar();

            // If the active tab was an Untitled sentinel, promote it to the real file path.
            var active = _tabManager.ActiveTab;
            if (active != null && IsUntitledTab(active))
            {
                _tabManager.Rename(active.Path, new FilePath(path));
                RebuildTabStrip();
            }
        });
        _events.AvailableTexturesChanged += () => Dispatcher.UIThread.InvokeAsync(RefreshTextureCombo);

        _undoManager.StackChanged         += () => Dispatcher.UIThread.InvokeAsync(UpdateStatusBar);
        _events.AnimationChainsChanged    += HandleAnimationChainsChanged;
        _selectedState.SelectionChanged   += HandleSelectionChanged;

        _appCommands.FramesDeleted += label =>
            Dispatcher.UIThread.InvokeAsync(() => ShowFrameDeletedToast(label));

        FrameDeletedToastUndoBtn.Click += (_, _) =>
        {
            _toastCts?.Cancel();
            FrameDeletedToastPanel.IsVisible = false;
            _undoManager.Undo();
        };

        // Wire hot reload watcher
        _appCommands.HotReloadWatcher = new HotReloadWatcher();
        _appCommands.WireHotReloadWatcher();

        _events.PngChangedOnDisk += path =>
            Dispatcher.UIThread.InvokeAsync(() => OnPngChangedOnDisk(path));
        _events.AchxDeletedOnDisk += path =>
            Dispatcher.UIThread.InvokeAsync(() =>
                ShowToast($"'{System.IO.Path.GetFileName(path)}' was deleted from disk."));
        _events.AchxReloadedFromDisk += path =>
            Dispatcher.UIThread.InvokeAsync(() =>
                ShowToast($"Reloaded {System.IO.Path.GetFileName(path)}"));

        _ioManager.SettingsLoaded += s => Dispatcher.UIThread.InvokeAsync(() => ApplyCompanionSettings(s));
    }

    // ── Wireframe toolbar wiring ──────────────────────────────────────────────

    private bool _suppressModeToggle;

    private void WireWireframeToolbar()
    {
        TextureCombo.SelectionChanged += OnTextureComboChanged;
        MoveModeToggle.IsCheckedChanged += OnMoveModeToggled;
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
        WireframeCtrl.WheelZoomPresets = _zoomPresets;

        // Default to Move mode
        MoveModeToggle.IsChecked = true;
        WireframeCtrl.IsMagicWandMode = false;

        // Apply initial grid state
        WireframeCtrl.SetGrid(false, 16);
    }

    private void OnTextureComboChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressTextureComboChanged) return;
        if (TextureCombo.SelectedItem is not string absolutePath) return;

        WireframeCtrl.LoadTexture(absolutePath);

        var frame = _selectedState.SelectedFrame;
        if (frame == null) return;

        string achxFolder = string.IsNullOrEmpty(_projectManager.FileName)
            ? string.Empty
            : (Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty);
        string storePath = TexturePathHelper.ComputeStorePath(absolutePath, achxFolder);
        _appCommands.SetFrameTextureName(frame, storePath);
        RefreshPropertyPanel();
    }

    private void OnMoveModeToggled(object? sender, RoutedEventArgs e)
    {
        if (_suppressModeToggle) return;
        if (MoveModeToggle.IsChecked != true) return;
        _suppressModeToggle = true;
        MagicWandToggle.IsChecked = false;
        _suppressModeToggle = false;
        WireframeCtrl.IsMagicWandMode = false;
    }

    private void OnMagicWandToggled(object? sender, RoutedEventArgs e)
    {
        if (_suppressModeToggle) return;
        if (MagicWandToggle.IsChecked != true) return;
        _suppressModeToggle = true;
        MoveModeToggle.IsChecked = false;
        _suppressModeToggle = false;
        WireframeCtrl.IsMagicWandMode = true;
    }

    private void OnSnapToGridChanged(object? sender, RoutedEventArgs e)
    {
        WireframeCtrl.SetGrid(
            SnapToGridCheck.IsChecked == true,
            GetGridSizeFromInput());
        SaveCompanionFile();
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
        SaveCompanionFile();
    }

    private void OnGridSizePlusBtnClick(object? sender, RoutedEventArgs e)
    {
        int size = Math.Min(GetGridSizeFromInput() + 1, 512);
        GridSizeInput.Text = size.ToString();
        if (SnapToGridCheck.IsChecked == true)
            WireframeCtrl.SetGrid(true, size);
        SaveCompanionFile();
    }

    private void OnGridSizeMinusBtnClick(object? sender, RoutedEventArgs e)
    {
        int size = Math.Max(GetGridSizeFromInput() - 1, 1);
        GridSizeInput.Text = size.ToString();
        if (SnapToGridCheck.IsChecked == true)
            WireframeCtrl.SetGrid(true, size);
        SaveCompanionFile();
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
        { 5, 10, 16, 25, 33, 50, 66, 75, 100, 150, 200, 300, 400, 800, 1600, 3200 };
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

    // ── WireframeControl event wiring ─────────────────────────────────────────

    private void WireWireframeControl()
    {
        WireframeCtrl.FrameRegionChanged     += OnFrameRegionChanged;
        WireframeCtrl.ChainRegionChanged     += OnChainRegionChanged;
        WireframeCtrl.FrameLiveUpdated       += OnFrameLiveUpdated;
        WireframeCtrl.FrameCreatedFromRegion += OnFrameCreatedFromRegion;
        // The combo follows every tick of a smooth wheel-zoom (#425); the companion file is only
        // persisted once the animation settles (IsZoomAnimating == false), not on every frame.
        WireframeCtrl.ZoomChanged            += zoomPct =>
        {
            SyncZoomCombo(zoomPct);
            if (!WireframeCtrl.IsZoomAnimating) SaveCompanionFile();
        };
        WireframeCtrl.PanChanged             += (_, _) => SaveCompanionFile();

        // ── Wireframe scrollbars (#422) ──
        // Two-way sync between the manual camera pan and the scrollbars, mirroring the Preview
        // panel (#415). The scroll axis runs opposite the pan axis (PanScrollBar inverts it).
        WireframeHScroll.ValueChanged += (_, _) => OnWireframeScrollValueChanged(horizontal: true);
        WireframeVScroll.ValueChanged += (_, _) => OnWireframeScrollValueChanged(horizontal: false);
        // Persist on scroll-end only (not per tick), matching the pan-drag save semantics.
        WireframeHScroll.Scroll += OnPreviewScrollEnded;
        WireframeVScroll.Scroll += OnPreviewScrollEnded;
        WireframeCtrl.ViewChanged += RefreshWireframeScrollBars;
    }

    private void OnWireframeScrollValueChanged(bool horizontal)
    {
        if (_suppressWireframeScrollSync) return;
        _suppressWireframeScrollSync = true;
        if (horizontal)
            WireframeCtrl.SetPanX((float)WireframeHScroll.Value);
        else
            WireframeCtrl.SetPanY((float)WireframeVScroll.Value);
        _suppressWireframeScrollSync = false;
    }

    /// <summary>
    /// Pushes the wireframe's current pan/zoom/texture size into its two scrollbars. Fired by
    /// <see cref="WireframeControl.ViewChanged"/>. The suppression flag stops the resulting
    /// <c>ValueChanged</c> from looping back into the pan.
    /// </summary>
    private void RefreshWireframeScrollBars()
    {
        if (_suppressWireframeScrollSync) return;
        _suppressWireframeScrollSync = true;
        var (h, v) = WireframeCtrl.GetScrollBarRanges();
        ApplyScrollRange(WireframeHScroll, h);
        ApplyScrollRange(WireframeVScroll, v);
        _suppressWireframeScrollSync = false;
    }

    private void OnChainRegionChanged(AnimationChainSave chain)
    {
        _events.RaiseAnimationChainsChanged();
    }

    private void OnFrameLiveUpdated(AnimationFrameSave frame)
    {
        // Called on UI thread during drag — refresh property panel and preview without saving
        RefreshPropertyPanel();
        _appCommands.RefreshAnimationFrameDisplay();
    }

    private void OnFrameRegionChanged(AnimationFrameSave frame)
    {
        _appCommands.RefreshTreeNode(frame);
        _events.RaiseAnimationChainsChanged();
    }

    private void OnFrameCreatedFromRegion(int minX, int minY, int maxX, int maxY)
    {
        var selectedChains = _selectedState.SelectedChains;
        var primaryChain = selectedChains.Count > 0 ? selectedChains[0] : _selectedState.SelectedChain;
        if (primaryChain is null) return;

        var texPath = WireframeCtrl.LoadedTexturePath;
        if (string.IsNullOrEmpty(texPath)) return;

        var (bitmapW, bitmapH) = WireframeCtrl.BitmapSize;
        if (bitmapW == 0 || bitmapH == 0) return;

        string relPath = !string.IsNullOrEmpty(_projectManager.FileName)
            ? Path.GetRelativePath(
                Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty,
                texPath).Replace('\\', '/')
            : texPath;

        var chainsToAddTo = selectedChains.Count > 1 ? selectedChains : new List<AnimationChainSave> { primaryChain };

        if (chainsToAddTo.Count == 1)
        {
            // AddFrameFromPixelBounds selects the new frame — desired behavior for single-chain.
            _appCommands.AddFrameFromPixelBounds(primaryChain, relPath, minX, minY, maxX, maxY, bitmapW, bitmapH);
        }
        else
        {
            // Multi-chain: add to each chain but preserve the current selection.
            var priorFrame = _selectedState.SelectedFrame;
            foreach (var chain in chainsToAddTo)
                _appCommands.AddFrameFromPixelBounds(chain, relPath, minX, minY, maxX, maxY, bitmapW, bitmapH);
            _selectedState.SelectedFrame = priorFrame;
        }
    }

    // ── Core event handlers ───────────────────────────────────────────────────

    private void HandleAnimationChainsChanged()
    {
        if (!string.IsNullOrEmpty(_projectManager.FileName))
        {
            _appCommands.SaveCurrentAnimationChainList();
            SaveCompanionFile();
            UpdateTitle();
        }

        Dispatcher.UIThread.InvokeAsync(RefreshTimelineStrip);
        Dispatcher.UIThread.InvokeAsync(RefreshTreeThumbnails);
        // Re-sync the property inspector so its values (flip toggles, frame length,
        // offsets, …) reflect the model after any mutation — including undo/redo.
        Dispatcher.UIThread.InvokeAsync(RefreshPropertyPanel);
        Dispatcher.UIThread.InvokeAsync(UpdateStatusBar);
    }

    private void HandleSelectionChanged()
    {
        // Sync the texture combo to the texture of the currently selected frame/chain
        Dispatcher.UIThread.InvokeAsync(SyncTextureCombo);
        // Sync tree selection
        Dispatcher.UIThread.InvokeAsync(SyncTreeSelection);
        // Refresh property inspector
        Dispatcher.UIThread.InvokeAsync(RefreshPropertyPanel);
        // Refresh timeline strip
        Dispatcher.UIThread.InvokeAsync(RefreshTimelineStrip);
    }

    // ── Companion file (.aeproperties) ────────────────────────────────────────

    private AESettingsSave BuildCompanionSettings() => new AESettingsSave
    {
        SnapToGrid           = SnapToGridCheck.IsChecked == true,
        GridSize             = GetGridSizeFromInput(),
        WireframeZoomPercent = (int)MathF.Round(WireframeCtrl.Zoom * 100f),
        PreviewZoomPercent   = (int)MathF.Round(PreviewCtrl.Zoom * 100f),
        WireframePanX        = WireframeCtrl.CameraState.PanX,
        WireframePanY        = WireframeCtrl.CameraState.PanY,
        PreviewPanX          = PreviewCtrl.PanOffset.X,
        PreviewPanY          = PreviewCtrl.PanOffset.Y,
        OffsetMultiplier     = _appState.OffsetMultiplier,
        ExpandedNodes        = TreeBuilder.GetExpandedChainNames(_treeRoots).ToList(),
        HorizontalGuides     = PreviewCtrl.HGuides.ToList(),
        VerticalGuides       = PreviewCtrl.VGuides.ToList(),
    };

    private void SaveCompanionFile()
    {
        if (_suppressCompanionSave) return;
        if (string.IsNullOrEmpty(_projectManager.FileName)) return;
        _ioManager.SaveCompanionFileFor(new FilePath(_projectManager.FileName), BuildCompanionSettings());
    }

    private void ApplyCompanionSettings(AESettingsSave settings)
    {
        _suppressCompanionSave = true;
        try
        {
            SnapToGridCheck.IsChecked = settings.SnapToGrid;
            GridSizeInput.Text        = settings.GridSize.ToString();
            WireframeCtrl.SetGrid(settings.SnapToGrid, settings.GridSize);

            WireframeCtrl.SetZoomPercent(settings.WireframeZoomPercent);
            PreviewCtrl.SetZoomPercent(settings.PreviewZoomPercent);

            WireframeCtrl.SetCamera(settings.WireframePanX, settings.WireframePanY, WireframeCtrl.CameraState.Zoom);
            PreviewCtrl.SetPan(settings.PreviewPanX, settings.PreviewPanY);

            var expandedSet = settings.ExpandedNodes.ToHashSet();
            foreach (var node in _treeRoots)
            {
                if (node.Data is AnimationChainSave chain)
                    node.IsExpanded = expandedSet.Contains(chain.Name);
            }

            PreviewCtrl.SetGuides(settings.HorizontalGuides, settings.VerticalGuides);
        }
        finally
        {
            _suppressCompanionSave = false;
        }
    }

    private void WireTreeRootsCompanionSave()
    {
        _treeRoots.CollectionChanged += (_, args) =>
        {
            if (args.NewItems != null)
                foreach (TreeNodeVm vm in args.NewItems)
                    vm.PropertyChanged += OnTreeNodeIsExpandedChanged;
            if (args.OldItems != null)
                foreach (TreeNodeVm vm in args.OldItems)
                    vm.PropertyChanged -= OnTreeNodeIsExpandedChanged;
        };
    }

    private void OnTreeNodeIsExpandedChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TreeNodeVm.IsExpanded))
            SaveCompanionFile();
    }

    // ── Status bar ────────────────────────────────────────────────────────────

    private static readonly Avalonia.Media.SolidColorBrush _autoSaveBrush =
        new(Avalonia.Media.Color.FromRgb(0x6d, 0xd2, 0x8d));
    private static readonly Avalonia.Media.SolidColorBrush _unsavedBrush =
        new(Avalonia.Media.Color.FromRgb(0xf0, 0xc6, 0x74));
    private static readonly Avalonia.Media.SolidColorBrush _failedBrush =
        new(Avalonia.Media.Color.FromRgb(0xe0, 0x55, 0x55));

    private void UpdateStatusBar()
    {
        var (label, brush) = _undoManager.SaveState switch
        {
            AnimationEditor.Core.CommandsAndState.Commands.SaveState.AutoSaveOn => ("Auto Save On", _autoSaveBrush),
            AnimationEditor.Core.CommandsAndState.Commands.SaveState.Failed     => ("Auto Save Failed", _failedBrush),
            _                                                                    => ("Not saved", _unsavedBrush),
        };
        StatusSaveLabel.Text = label;
        StatusDot.Fill       = brush;
        StatusFilename.Text  = Path.GetFileName(_projectManager.FileName ?? string.Empty);
        var acls = _projectManager.AnimationChainListSave;
        if (acls == null || acls.AnimationChains.Count == 0)
        {
            StatusCounts.Text = string.Empty;
        }
        else
        {
            int totalFrames = acls.AnimationChains.Sum(c => c.Frames.Count);
            StatusCounts.Text = $"{acls.AnimationChains.Count} chains · {totalFrames} frames";
        }
    }



    private void RefreshHistoryPanel()
    {
        var undoHistory = _undoManager.UndoHistory;
        var redoHistory = _undoManager.RedoHistory;
        var items = new List<Models.HistoryEntryVm>();
        // Applied (undo) rows use full-strength ink; redo rows are muted. Pick per theme.
        bool dark = ActualThemeVariant != ThemeVariant.Light;
        string appliedInk = dark ? "#e6e8ec" : "#1a1d22";
        string redoInk     = dark ? "#6a6e76" : "#9aa1ad";
        // Photoshop order: oldest applied at top, newest applied at bottom, redo items below.
        foreach (var cmd in undoHistory)
            items.Add(new Models.HistoryEntryVm(cmd.Description, appliedInk));
        // Mark the most recently applied command as "you are here".
        if (items.Count > 0)
            items[^1] = items[^1] with { IsCurrent = true };
        // Redo items follow: next-to-redo first, furthest future last.
        foreach (var cmd in redoHistory)
            items.Add(new Models.HistoryEntryVm(cmd.Description, redoInk));
        HistoryList.ItemsSource = items;
        int currentIndex = undoHistory.Count - 1;
        ScrollHistoryToCurrent(currentIndex, items.Count);

        HistoryUndoButton.IsEnabled = _undoManager.CanUndo;
        HistoryRedoButton.IsEnabled = _undoManager.CanRedo;
    }

    private void ScrollHistoryToCurrent(int currentIndex, int totalCount)
    {
        if (totalCount == 0 || currentIndex < 0) return;
        Dispatcher.UIThread.Post(() =>
        {
            double extent   = HistoryScrollViewer.Extent.Height;
            double viewport = HistoryScrollViewer.Viewport.Height;
            double newOffset = Helpers.HistoryScrollHelper.ComputeScrollOffset(
                currentIndex, totalCount, extent, viewport,
                HistoryScrollViewer.Offset.Y) ?? HistoryScrollViewer.Offset.Y;
            HistoryScrollViewer.Offset = new Avalonia.Vector(0, newOffset);
        }, Avalonia.Threading.DispatcherPriority.Render);
    }

    private void SetHistoryVisible(bool visible)
    {
        HistorySplitter.IsVisible = visible;
        HistorySectionGrid.IsVisible = visible;
        if (visible)
        {
            // Share space: inspector content gets 2/3, history list gets 1/3
            InspectorColumnGrid.RowDefinitions[1].Height = new GridLength(2, GridUnitType.Star);
            InspectorColumnGrid.RowDefinitions[2].Height = new GridLength(4);
            InspectorColumnGrid.RowDefinitions[3].Height = new GridLength(1, GridUnitType.Star);
        }
        else
        {
            InspectorColumnGrid.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            InspectorColumnGrid.RowDefinitions[2].Height = new GridLength(0);
            InspectorColumnGrid.RowDefinitions[3].Height = new GridLength(0);
        }
        MenuShowHistory.IsEnabled = !visible;
    }

    // ── Texture combo helpers ─────────────────────────────────────────────────

    /// <summary>Rebuild the texture dropdown from all frames in the loaded .achx.</summary>
    private void RefreshTextureCombo()
    {
        _suppressTextureComboChanged = true;
        try
        {
            TextureCombo.Items.Clear();

            var acls = _projectManager.AnimationChainListSave;
            if (acls is null || string.IsNullOrEmpty(_projectManager.FileName)) return;

            string achxFolder = (Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty);

            var paths = acls.AnimationChains
                .SelectMany(c => c.Frames)
                .Where(f => !string.IsNullOrEmpty(f.TextureName))
                .Select(f =>
                {
                    var abs = System.IO.Path.IsPathRooted(f.TextureName)
                        ? f.TextureName
                        : Path.Combine(achxFolder, f.TextureName);
                    return new FilePath(abs).Standardized;
                })
                .Union(_projectManager.ReferencedPngs.Select(p => p.Standardized))
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

        // Prefer selected frame, then fall back to selected chains/chain for texture lookup.
        var frame = _selectedState.SelectedFrame
            ?? _selectedState.SelectedChains.SelectMany(c => c.Frames).FirstOrDefault()
            ?? _selectedState.SelectedChain?.Frames?.FirstOrDefault();

        if (frame != null && !string.IsNullOrEmpty(frame.TextureName) &&
            !string.IsNullOrEmpty(_projectManager.FileName))
        {
            string achxFolder = (Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty);
            var abs = System.IO.Path.IsPathRooted(frame.TextureName)
                ? frame.TextureName
                : Path.Combine(achxFolder, frame.TextureName);
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

        RefreshTextureNameLabel();
    }

    private void RefreshTextureNameLabel()
    {
        string? name = _selectedState.SelectedTextureName;
        string label = string.IsNullOrEmpty(name) ? string.Empty : Path.GetFileName(name);
        TextureNameLabel.Text = label;
        TextureNameLabel.IsVisible = label.Length > 0;
    }

    // ── Custom title bar ─────────────────────────────────────────────────────

    /// <summary>
    /// Switches to native macOS window decorations (traffic-light buttons on the left)
    /// and hides the custom title bar and resize grips, which are only needed on
    /// Windows/Linux where <c>WindowDecorations="None"</c> is in effect.
    /// macOS handles window resizing natively when <c>WindowDecorations.Full</c> is active.
    /// </summary>
    private void ApplyMacOSWindowChrome()
    {
        WindowDecorations = WindowDecorations.Full;
        TitleBarBorder.IsVisible = false;
        GripN.IsVisible  = false;
        GripS.IsVisible  = false;
        GripW.IsVisible  = false;
        GripE.IsVisible  = false;
        GripNW.IsVisible = false;
        GripNE.IsVisible = false;
        GripSW.IsVisible = false;
        GripSE.IsVisible = false;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    private void OnTitleBarDoubleTapped(object? sender, TappedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnTitleFileOpenFolderClick(object? sender, RoutedEventArgs e)
    {
        var folder = Path.GetDirectoryName(_projectManager.FileName);
        if (!string.IsNullOrEmpty(folder))
            Process.Start(new ProcessStartInfo { FileName = folder, UseShellExecute = true });
    }

    private void OnTitleFileCopyPathClick(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_projectManager.FileName))
            _ = TopLevel.GetTopLevel(this)?.Clipboard?.SetTextAsync(_projectManager.FileName);
    }

    private void OnMinimizeBtnClick(object? sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void OnMaximizeBtnClick(object? sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void OnCloseBtnClick(object? sender, RoutedEventArgs e) => Close();

    private void OnResizeGrip(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        var edge = (sender as Control)?.Name switch
        {
            "GripN"  => WindowEdge.North,
            "GripS"  => WindowEdge.South,
            "GripE"  => WindowEdge.East,
            "GripW"  => WindowEdge.West,
            "GripNE" => WindowEdge.NorthEast,
            "GripNW" => WindowEdge.NorthWest,
            "GripSE" => WindowEdge.SouthEast,
            "GripSW" => WindowEdge.SouthWest,
            _        => (WindowEdge?)null,
        };
        if (edge.HasValue) BeginResizeDrag(edge.Value, e);
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

        MenuReloadFromDisk.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(_projectManager.FileName))
                _appCommands.ReloadAchxFromDisk(_projectManager.FileName);
        };
        MenuEnableHotReload.Click += (_, _) =>
        {
            _appCommands.HotReloadWatcher.IsEnabled = MenuEnableHotReload.IsChecked == true;
        };

        MenuUndo.IsEnabled = _undoManager.CanUndo;
        MenuRedo.IsEnabled = _undoManager.CanRedo;
        MenuUndo.Click += (_, _) => _undoManager.Undo();
        MenuRedo.Click += (_, _) => _undoManager.Redo();
        _undoManager.StackChanged += () =>
        {
            MenuUndo.IsEnabled = _undoManager.CanUndo;
            MenuRedo.IsEnabled = _undoManager.CanRedo;
            RefreshHistoryPanel();
        };
        RefreshHistoryPanel();

        HistoryUndoButton.Click  += (_, _) => _undoManager.Undo();
        HistoryRedoButton.Click  += (_, _) => _undoManager.Redo();
        HistoryCloseButton.Click += (_, _) => SetHistoryVisible(false);
        MenuShowHistory.Click    += (_, _) => SetHistoryVisible(true);
        MenuShowHistory.IsEnabled = false;

        MenuThemeLight.Click  += (_, _) => SetTheme(AppTheme.Light);
        MenuThemeDark.Click   += (_, _) => SetTheme(AppTheme.Dark);
        MenuThemeSystem.Click += (_, _) => SetTheme(AppTheme.System);
        // C#-built surfaces (tab strip, history rows) hold static brush snapshots, so
        // rebuild them when the variant changes. XAML surfaces follow via DynamicResource.
        ActualThemeVariantChanged += (_, _) => { RebuildTabStrip(); RefreshHistoryPanel(); };

        RefreshRecentFiles();

        // On macOS the menus live in the system menu bar (NativeMenu); hide the duplicate in-window copy.
        if (OperatingSystem.IsMacOS())
            MainMenu.IsVisible = false;
    }

    /// <summary>
    /// Returns delegates for every actionable menu item so that the macOS NativeMenu
    /// can be wired from <see cref="App"/> without reaching into private window state.
    /// </summary>
    internal NativeMenuActions CreateNativeMenuActions() => new(
        New:             () => OnNewClick(null, null!),
        Load:            () => _ = LoadAsync(),
        RecentFiles:     () => _appSettings.RecentFiles
                                    .Take(5)
                                    .Select(f => (System.IO.Path.GetFileName(f), (Action)(() => _ = LoadAnimationFileAsync(f))))
                                    .ToList(),
        Save:            () => OnSaveClick(null, null!),
        SaveAs:          () => _ = _appCommands.SaveCurrentAnimationChainListAsync(),
        Undo:            () => _undoManager.Undo(),
        Redo:            () => _undoManager.Redo(),
        Copy:            () => _ = HandleCopyAsync(),
        Paste:           () => _ = HandlePasteAsync(),
        ReloadFromDisk:  () => { if (!string.IsNullOrEmpty(_projectManager.FileName)) _appCommands.ReloadAchxFromDisk(_projectManager.FileName); },
        ToggleHotReload: () => { _appCommands.HotReloadWatcher.IsEnabled = !_appCommands.HotReloadWatcher.IsEnabled; },
        ResizeTexture:   () => _ = DoResizeTextureAsync(),
        ShowHistory:     () => SetHistoryVisible(true),
        About:           () => _ = BuildAboutWindow().ShowDialog(this));

    private void RefreshRecentFiles()
    {
        MenuLoadRecent.Items.Clear();
        foreach (var file in _appSettings.RecentFiles.Take(5))
        {
            var item = new MenuItem { Header = System.IO.Path.GetFileName(file) };
            ToolTip.SetTip(item, file);
            var captured = file;
            item.Click += async (_, _) => await LoadAnimationFileAsync(captured);
            MenuLoadRecent.Items.Add(item);
        }
    }

    // ── File menu handlers ────────────────────────────────────────────────────

    private void OnNewClick(object? sender, RoutedEventArgs e)
    {
        // Register the currently-open file (if any) as a tab before we clear it.
        EnsureCurrentEditorContentHasTab();

        _projectManager.AnimationChainListSave = new AnimationChainListSave();
        _projectManager.FileName = null;
        _selectedState.Reset();
        _undoManager.Clear();
        RefreshTreeView();

        // Open a new numbered Untitled tab and activate it.
        var displayName = TabManager.ComputeUntitledDisplayName(
            _tabManager.Tabs.Select(t => t.DisplayName).ToList());
        var sentinelPath = new FilePath(NewUntitledSentinelPath());
        _tabManager.OpenOrFocus(sentinelPath, displayName);
        RebuildTabStrip();

        _ = _appCommands.SaveCurrentAnimationChainListAsync();
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
            await LoadAnimationFileAsync(files[0].Path.LocalPath);
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        if (_projectManager.AnimationChainListSave is null) return;

        if (string.IsNullOrEmpty(_projectManager.FileName))
            _ = _appCommands.SaveCurrentAnimationChainListAsync();
        else
        {
            _appCommands.SaveCurrentAnimationChainList();
            UpdateTitle();
        }
    }

    private void OnSaveAsClick(object? sender, RoutedEventArgs e) =>
        _ = _appCommands.SaveCurrentAnimationChainListAsync();

    internal const string GitHubUrl = "https://github.com/vchelaru/FlatRedBall2";

    private void OnAboutClick(object? sender, RoutedEventArgs e)
        => _ = BuildAboutWindow().ShowDialog(this);

    /// <summary>
    /// Returns a fully-configured About window centered on its owner.
    /// Extracted for testability.
    /// </summary>
    internal static Window BuildAboutWindow() =>
        new Window
        {
            Title = "About AnimationEditor",
            Width = 420,
            Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = BuildAboutContent(),
        };

    /// <summary>
    /// Builds the content panel for the About dialog.
    /// Extracted for testability.
    /// </summary>
    internal static Control BuildAboutContent()
    {
        var ver = typeof(MainWindow).Assembly.GetName().Version;
        var versionText = ver is null ? "unknown" : $"{ver.Major}.{ver.Minor}.{ver.Build}";

        var linkButton = new Button { Content = GitHubUrl };
        linkButton.Click += (_, _) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
            }
            catch { }
        };

        return new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 8,
            Children =
            {
                new TextBlock { Text = "AnimationEditor", FontSize = 16 },
                new TextBlock { Text = $"Version {versionText}" },
                new TextBlock { Text = "© FlatRedBall Contributors" },
                linkButton,
            }
        };
    }

    // ── Preview controls wiring ───────────────────────────────────────────────

    private void WirePreviewControls()
    {
        OnionSkinToggle.IsCheckedChanged += (_, _) =>
            PreviewCtrl.ShowOnionSkin = OnionSkinToggle.IsChecked == true;

        ShowGuidesCheck.IsCheckedChanged += (_, _) =>
            PreviewCtrl.ShowGuides = ShowGuidesCheck.IsChecked == true;

        InterpolateToggle.IsCheckedChanged += (_, _) =>
        {
            if (_suppressInterpolateSync) return;
            PreviewCtrl.InterpolateOffsets = InterpolateToggle.IsChecked == true;
        };
        // PreviewControl auto-resets InterpolateOffsets when the chain changes; resync the toggle.
        PreviewCtrl.InterpolateOffsetsChanged += isOn =>
        {
            _suppressInterpolateSync = true;
            InterpolateToggle.IsChecked = isOn;
            _suppressInterpolateSync = false;
        };

        TimelineStrip.ItemsSource = _timelineFrames;

        PreviewZoomCombo.ItemsSource = _previewZoomPresetTexts;
        PreviewZoomCombo.KeyDown += OnPreviewZoomComboKeyDown;
        PreviewZoomCombo.LostFocus += OnPreviewZoomComboLostFocus;
        PreviewZoomCombo.SelectionChanged += OnPreviewZoomComboSelectionChanged;
        PreviewZoomPlusBtn.Click  += (_, _) => StepZoomPreset(PreviewCtrl.Zoom * 100f, _previewZoomPresets, +1, p => PreviewCtrl.SetZoomPercent(p));
        PreviewZoomMinusBtn.Click += (_, _) => StepZoomPreset(PreviewCtrl.Zoom * 100f, _previewZoomPresets, -1, p => PreviewCtrl.SetZoomPercent(p));
        PreviewCtrl.WheelZoomPresets = _previewZoomPresets;

        PreviewCtrl.ZoomChanged += zoomPct => { SyncPreviewZoomCombo(zoomPct); SaveCompanionFile(); };
        PreviewCtrl.PanChanged  += (_, _) => SaveCompanionFile();
        PreviewCtrl.Playback.FrameIndexChanged += OnPreviewPlaybackFrameIndexChanged;
        PreviewCtrl.Playback.PlaybackTicked += OnPlaybackTicked;

        // ── Preview scrollbars (#415) ──
        // Two-way sync between the manual pan and the scrollbars, mirroring the
        // PreviewZoomCombo ↔ PreviewCtrl suppression pattern above. The scroll axis runs
        // opposite the pan axis (PanScrollBar handles the inversion).
        PreviewHScroll.ValueChanged += (_, _) => OnPreviewScrollValueChanged(horizontal: true);
        PreviewVScroll.ValueChanged += (_, _) => OnPreviewScrollValueChanged(horizontal: false);
        // Persist on scroll-end only (not per tick), matching the pan-drag save semantics.
        PreviewHScroll.Scroll += OnPreviewScrollEnded;
        PreviewVScroll.Scroll += OnPreviewScrollEnded;
        PreviewCtrl.ViewChanged += RefreshPreviewScrollBars;
    }

    private void OnPreviewScrollValueChanged(bool horizontal)
    {
        if (_suppressPreviewScrollSync) return;
        _suppressPreviewScrollSync = true;
        if (horizontal)
            PreviewCtrl.SetPanX(PanScrollBar.PanFromValue((float)PreviewHScroll.Value));
        else
            PreviewCtrl.SetPanY(PanScrollBar.PanFromValue((float)PreviewVScroll.Value));
        _suppressPreviewScrollSync = false;
    }

    private void OnPreviewScrollEnded(object? sender, ScrollEventArgs e)
    {
        if (e.ScrollEventType == ScrollEventType.EndScroll) SaveCompanionFile();
    }

    /// <summary>
    /// Pushes the Preview's current pan/zoom/content extent into the two scrollbars. Fired by
    /// <see cref="PreviewControl.ViewChanged"/>. The suppression flag stops the resulting
    /// <c>ValueChanged</c> from looping back into the pan.
    /// </summary>
    private void RefreshPreviewScrollBars()
    {
        if (_suppressPreviewScrollSync) return;
        _suppressPreviewScrollSync = true;
        var (h, v) = PreviewCtrl.GetScrollBarRanges();
        ApplyScrollRange(PreviewHScroll, h);
        ApplyScrollRange(PreviewVScroll, v);
        _suppressPreviewScrollSync = false;
    }

    // Order matters: set Minimum/Maximum before Value so RangeBase doesn't coerce it.
    private static void ApplyScrollRange(ScrollBar bar, ScrollBarRange r)
    {
        bar.Minimum      = r.Minimum;
        bar.Maximum      = r.Maximum;
        bar.ViewportSize = r.ViewportSize;
        bar.Value        = r.Value;
    }

    private void OnPreviewPlaybackFrameIndexChanged(int index)
    {
        if (_selectedState.SelectedFrame is not null)
            return;

        Dispatcher.UIThread.Post(
            () => UpdateTimelineScrubber(index),
            DispatcherPriority.Background);
    }

    private void OnPlaybackTicked()
    {
        if (_selectedState.SelectedFrame is not null)
            return;

        int idx = PreviewCtrl.Playback.CurrentFrameIndex;
        if (idx < 0 || idx >= _timelineFrames.Count)
            return;

        double elapsed = PreviewCtrl.Playback.FrameElapsed;
        double travelWidth = Math.Max(0, _timelineFrames[idx].Width - TimelineFrameVm.PlayheadWidth);

        // Move the playhead at a constant PixelsPerSecond rate.
        // For clamped cells (shorter than natural proportional width) the playhead parks
        // at the right edge until the frame advances rather than speeding up.
        double offset = Math.Min(elapsed * _timelineEffectivePps, travelWidth);
        _timelineFrames[idx].ScrubberOffset = offset;
    }

    // ── Editable preview-zoom combo (bottom preview) ─────────────────────────
    //
    // Same editable-AutoCompleteBox pattern as ZoomCombo above.

    private static readonly int[] _previewZoomPresets =
        { 5, 10, 16, 25, 33, 50, 66, 75, 100, 150, 200, 300, 400, 800, 1600, 3200 };
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
    private readonly ObservableCollection<TimelineFrameVm> _timelineFrames = new();
    private double _timelineEffectivePps = TimelineBuilder.PixelsPerSecond;
    private int _currentTimelineFrameIndex = -1;

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

        // "Add Animation" button under the tree
        AddChainBtn.Click += (_, _) =>
        {
            if (_projectManager.AnimationChainListSave is null)
                _projectManager.AnimationChainListSave = new AnimationChainListSave();
            AddAnimationChainAndBeginInlineRename();
        };

        // Expand/Collapse toolbar buttons
        ExpandAllBtn.Click  += (_, _) => SetAllExpanded(true);
        CollapseAllBtn.Click += (_, _) => SetAllExpanded(false);

        // Blank-space double-tap: expand / collapse the node
        AnimTree.DoubleTapped += OnAnimTreeDoubleTapped;

        // Tunnel-phase KeyDown from the inline TextBox (Enter=commit, Escape=cancel).
        // Must be Tunnel (not Bubble) so we intercept Enter/Escape BEFORE the event
        // reaches TreeViewItem.OnKeyDown, which in Avalonia 12.x handles Key.Enter by
        // toggling IsExpanded — collapsing the chain mid-rename if we arrive too late.
        AnimTree.AddHandler(
            InputElement.KeyDownEvent,
            OnInlineRenameKeyDown,
            RoutingStrategies.Tunnel);

        // Bubble-phase LostFocus from the inline TextBox: commit
        AnimTree.AddHandler(
            InputElement.LostFocusEvent,
            OnInlineRenameLostFocus,
            RoutingStrategies.Bubble);

        WireTreeRootsCompanionSave();
    }

    private void SetAllExpanded(bool expanded)
    {
        foreach (var node in _treeRoots)
            TreeNodeVm.SetExpandedRecursive(node, expanded);
    }

    private void AddAnimationChainAndBeginInlineRename()
    {
        if (_projectManager.AnimationChainListSave is null)
            _projectManager.AnimationChainListSave = new AnimationChainListSave();

        var existingNames = _projectManager.AnimationChainListSave.AnimationChains
            .Select(c => c.Name)
            .ToList();
        var defaultName = StringFunctions.MakeStringUnique("NewAnimation", existingNames);
        var chain = _appCommands.AddAnimationChainWithName(defaultName);
        if (chain is null) return;

        Dispatcher.UIThread.Post(() =>
        {
            SyncTreeSelection();
            BeginInlineRenameSelected(chain);
        }, DispatcherPriority.Background);
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
        _appCommands.AddFrame(chain);
        e.Handled = true;
    }

    // DoubleTapped on the + button must not reach OnAnimTreeDoubleTapped.
    // Marking handled here mirrors how the header TextBlock suppresses the fallback handler.
    private void OnAddFrameBtnDoubleTapped(object? _, TappedEventArgs e) => e.Handled = true;

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
        Trace.WriteLine($"[DragDrop] OnTreeDrop: firstFile={firstFile ?? "(null)"}, FileName={_projectManager.FileName ?? "(null)"}");

        if (string.IsNullOrEmpty(firstFile))
        {
            Trace.WriteLine("[DragDrop] Aborted: no file found in drop data");
            return;
        }

        // If no ACHX is saved yet, allow the drop but use an absolute texture path.
        // Relative-path conversion requires a base directory; without one we fall back to absolute.
        if (string.IsNullOrEmpty(_projectManager.FileName))
        {
            Trace.WriteLine("[DragDrop] Warning: no ACHX file saved yet — texture path will be absolute");
        }

        var targetNode = AnimTree.SelectedItem as TreeNodeVm;

        var targetFrame = targetNode?.Data as AnimationFrameSave;
        var targetChain = targetNode?.Data as AnimationChainSave;

        if (targetFrame is not null)
        {
            targetChain = _objectFinder.GetAnimationChainContaining(targetFrame);
        }

        Trace.WriteLine($"[DragDrop] targetChain={targetChain?.Name ?? "(null)"}, targetFrame={targetFrame?.TextureName ?? "(null)"}, ctrl={e.KeyModifiers.HasFlag(KeyModifiers.Control)}");

        var (result, relPath) = TextureDropProcessor.ComputePngDrop(
            targetChain,
            targetFrame,
            firstFile,
            _projectManager.FileName,
            e.KeyModifiers.HasFlag(KeyModifiers.Control));

        Trace.WriteLine($"[DragDrop] Result={result}");

        if (result == TextureDropResult.NotApplied)
        {
            Trace.WriteLine("[DragDrop] NotApplied — no chain or frame targeted, or non-PNG dropped");
            return;
        }

        switch (result)
        {
            case TextureDropResult.UpdatedFrame:
                _appCommands.SetFrameTextureName(targetFrame!, relPath);
                _appCommands.RefreshTreeNode(targetFrame!);
                _selectedState.SelectedFrame = targetFrame!;
                break;

            case TextureDropResult.CreatedFrame:
                _appCommands.AddFrame(targetChain!, relPath);
                var createdFrame = targetChain!.Frames.LastOrDefault();
                if (createdFrame is not null)
                    _selectedState.SelectedFrame = createdFrame;
                break;

            case TextureDropResult.UpdatedChainFrames:
                _appCommands.SetAllFramesTextureName(targetChain!, relPath);
                _appCommands.RefreshTreeNode(targetChain!);
                _selectedState.SelectedChain = targetChain;
                break;
        }

        RefreshTextureCombo();
        _appCommands.RefreshWireframe();
        _events.RaiseAnimationChainsChanged();
        _appCommands.SyncHotReloadWatcher();  // watch the newly-referenced PNG directory
        e.Handled = true;
    }

    private static string? GetFirstDroppedFilePath(DragEventArgs e)
    {
        // Log item formats so we can see exactly what the OS provides
        var itemFormats = e.DataTransfer.Items?
            .Select(i => "[" + string.Join(",", i.Formats) + "]")
            .ToList();
        Trace.WriteLine($"[DragDrop] Items and their formats: {(itemFormats == null ? "(null)" : string.Join(" ", itemFormats))}");
        Trace.WriteLine($"[DragDrop] Contains(DataFormat.File)={e.DataTransfer.Contains(DataFormat.File)}");

        // Correct Avalonia 12 API for OS file drops
        var files = e.DataTransfer.TryGetFiles()?.ToList();
        Trace.WriteLine($"[DragDrop] TryGetFiles() count={files?.Count ?? -1}");
        if (files?.Count > 0)
        {
            var path = files[0].Path.LocalPath;
            Trace.WriteLine($"[DragDrop] resolved path={path}");
            return path;
        }

        // Fallback: per-item TryGetFile()
        var items = e.DataTransfer.Items?.ToList();
        Trace.WriteLine($"[DragDrop] Items count={items?.Count ?? -1}");
        foreach (var item in items ?? new())
            Trace.WriteLine($"[DragDrop] Item: Formats=[{string.Join(",", item.Formats)}] TryGetFile={item.TryGetFile()?.Path?.LocalPath ?? "(null)"}");

        var fallback = items?
            .Select(item => item.TryGetFile())
            .FirstOrDefault(f => f is not null);
        Trace.WriteLine($"[DragDrop] Items fallback resolved={fallback?.Path.LocalPath ?? "(null)"}");
        return fallback?.Path.LocalPath;
    }

    // ── Window-level OS file drop: open dropped .achx files as tabs ────────────
    //
    // Registered on the whole window (handledEventsToo) so an .achx dropped anywhere —
    // tab strip, editor canvas, or even over the tree — opens as a tab, matching
    // File > Open. These handlers act ONLY when the payload contains at least one
    // .achx; for any other payload they stay passive, leaving the tree's PNG-texture
    // drop (OnTreeDragOver / OnTreeDrop) untouched. handledEventsToo lets the DragOver
    // override the tree's "no drop" affordance when an .achx is dragged over the tree.

    private void WireWindowFileDrop()
    {
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnWindowDragOver, RoutingStrategies.Bubble, handledEventsToo: true);
        AddHandler(DragDrop.DropEvent, OnWindowDrop, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private static IEnumerable<string?>? DroppedFilePaths(DragEventArgs e) =>
        e.DataTransfer.TryGetFiles()?.Select(f => f.Path.LocalPath);

    private void OnWindowDragOver(object? sender, DragEventArgs e)
    {
        if (AchxDropProcessor.ContainsAchx(DroppedFilePaths(e)))
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        }
    }

    private async void OnWindowDrop(object? sender, DragEventArgs e)
    {
        var achxFiles = AchxDropProcessor.SelectAchxFiles(DroppedFilePaths(e));
        if (achxFiles.Count == 0) return;  // not ours — leave the tree's PNG drop to run

        e.Handled = true;
        // LoadAnimationFileAsync de-dupes against already-open tabs (focuses instead of
        // duplicating); awaiting in sequence opens each file and leaves the last active.
        foreach (var path in achxFiles)
            await LoadAnimationFileAsync(path);
    }

    private void OnTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressTreeSelectionHandling) return;
        if (AnimTree.SelectedItem is not TreeNodeVm vm) return;

        // Sync multi-select into SelectedState
        _selectedState.SelectedNodes = AnimTree.SelectedItems
            .OfType<TreeNodeVm>()
            .Select(n => n.Data)
            .OfType<object>()
            .ToList();

        TreeBuilder.RouteNodeSelection(vm.Data, _selectedState, _projectManager.AnimationChainListSave);
    }

    // ── Tree refresh ──────────────────────────────────────────────────────────

    private void RefreshTreeView()
    {
        _suppressTreeSelectionHandling = true;
        try
        {
            var acls = _projectManager.AnimationChainListSave;
            if (acls is null) { DisposeTreeThumbnails(); _treeRoots.Clear(); return; }

            // Diff-update the root nodes instead of clearing and rebuilding, so each
            // chain's collapse state (and selection) survives copy/paste and reorder.
            TreeBuilder.SyncChainsInto(_treeRoots, acls.AnimationChains);
            RefreshTreeThumbnails();

            // Re-select to keep visual state
            SyncTreeSelection();
        }
        finally
        {
            _suppressTreeSelectionHandling = false;
        }
    }

    /// <summary>
    /// Fully rebuilds the tree from scratch with every chain collapsed. Used on
    /// .achx load (File &gt; Open, recent files, drag-drop, startup reopen) so a
    /// freshly-opened file presents a scannable, collapsed overview. Contrast with
    /// <see cref="RefreshTreeView"/>, which diff-updates and preserves each chain's
    /// collapse state across edits.
    /// </summary>
    private void RebuildTreeView()
    {
        _suppressTreeSelectionHandling = true;
        try
        {
            DisposeTreeThumbnails();
            _treeRoots.Clear();

            var acls = _projectManager.AnimationChainListSave;
            if (acls is null) return;

            // Empty expandedChainNames (not null) collapses every chain; null would
            // default them all to expanded.
            foreach (var node in TreeBuilder.BuildTree(acls, System.Array.Empty<string>()))
                _treeRoots.Add(node);

            RefreshTreeThumbnails();
            SyncTreeSelection();
        }
        finally
        {
            _suppressTreeSelectionHandling = false;
        }
    }

    private void RefreshChainNode(AnimationChainSave chain)
    {
        _suppressTreeSelectionHandling = true;
        try
        {
            var node = FindChainNode(chain);
            if (node is null)
            {
                _treeRoots.Add(TreeBuilder.BuildChainNode(chain));
            }
            else
            {
                node.Header = chain.Name;
                node.Meta   = $"{chain.Frames.Count} fr";
                TreeBuilder.SyncFramesInto(node, chain.Frames);
            }
            RefreshTreeThumbnails();
            SyncTreeSelection();
        }
        finally
        {
            _suppressTreeSelectionHandling = false;
        }
    }

    private void RefreshFrameNode(AnimationFrameSave frame)
    {
        var chain    = _objectFinder.GetAnimationChainContaining(frame);
        var chainNode = chain is null ? null : FindChainNode(chain);
        if (chainNode is null) return;
        var frameIndex = chain!.Frames.IndexOf(frame);

        var frameNode = chainNode.Children
            .FirstOrDefault(n => n.Data is AnimationFrameSave f && f == frame);

        var rebuiltFrameNode = TreeBuilder.BuildFrameNode(frame, frameIndex);

        if (frameNode is null)
        {
            chainNode.Children.Add(rebuiltFrameNode);
        }
        else
        {
            frameNode.Header     = rebuiltFrameNode.Header;
            frameNode.Kind       = rebuiltFrameNode.Kind;
            frameNode.IsFrameNode = rebuiltFrameNode.IsFrameNode;
            frameNode.Meta       = rebuiltFrameNode.Meta;
            TreeBuilder.SyncShapesInto(frameNode, frame.ShapesSave);
        }
        RefreshTreeThumbnails();
    }

    private TreeNodeVm? FindChainNode(AnimationChainSave chain) =>
        _treeRoots.FirstOrDefault(n => n.Data is AnimationChainSave c && c == chain);

    // ── Hot reload ────────────────────────────────────────────────────────────

    private void OnPngChangedOnDisk(string absolutePath)
    {
        _thumbnailService.InvalidatePath(absolutePath);

        // Force-reload the wireframe texture if it matches the changed PNG
        if (string.Equals(WireframeCtrl.LoadedTexturePath,
            new FilePath(absolutePath).Standardized,
            StringComparison.OrdinalIgnoreCase))
        {
            WireframeCtrl.ForceReloadTexture();
        }

        // Invalidate all cached thumbnails for this path and rebuild tree icons
        foreach (var node in _treeRoots)
        {
            if (node.Data is AnimationChainSave chain &&
                chain.Frames.Count > 0 &&
                !string.IsNullOrEmpty(chain.Frames[0].TextureName))
            {
                // Force thumbnail regeneration regardless of source equality
                (node.Thumbnail as IDisposable)?.Dispose();
                node.Thumbnail = null;
                node.ThumbnailSource = null;
            }
        }
        RefreshTreeThumbnails();
        RefreshTimelineStrip();
        _appCommands.RefreshAnimationFrameDisplay();
        ShowToast($"Reloaded {System.IO.Path.GetFileName(absolutePath)}");
    }

    /// <summary>
    /// Pixel size the chain first-frame thumbnail bitmap is baked at. Kept at twice the
    /// displayed icon size (the <c>TreeNodeIconSize</c> resource in MainWindow.axaml, 28px)
    /// so the <c>Image</c> control downsamples — which is crisp — instead of upscaling a
    /// too-small bitmap, which looks blurry. The 2× headroom also covers high-DPI displays.
    /// </summary>
    private const int TreeChainThumbnailPixelSize = 56;

    /// <summary>
    /// Regenerates each chain node's first-frame icon when its <see cref="ThumbnailSource"/>
    /// has changed — a frame reorder, first-frame texture swap, first-frame region edit, or
    /// first-frame delete. Chains with no frames fall back to the generic chain icon.
    /// Change-detected, so calling it from every tree-refresh path is cheap when nothing
    /// about a chain's first frame actually changed.
    /// </summary>
    private void RefreshTreeThumbnails()
    {
        foreach (var node in _treeRoots)
        {
            if (node.Data is not AnimationChainSave chain)
                continue;

            var source = ThumbnailSource.FromChain(chain);
            // Regenerate when the first-frame visual changed, or when we have a source
            // but no thumbnail yet (e.g. the texture was unresolvable on a prior pass).
            bool needsRegen = !Equals(source, node.ThumbnailSource)
                           || (source is not null && node.Thumbnail is null);
            if (!needsRegen)
                continue;

            (node.Thumbnail as IDisposable)?.Dispose();
            node.Thumbnail = source is null
                ? null
                : _thumbnailService.GetFrameThumbnail(
                    chain.Frames[0], TreeChainThumbnailPixelSize, TreeChainThumbnailPixelSize);
            node.ThumbnailSource = source;
        }
    }

    /// <summary>Releases every chain node's first-frame thumbnail bitmap. Call before clearing
    /// <c>_treeRoots</c> or on window close so the cropped bitmaps are not leaked.</summary>
    private void DisposeTreeThumbnails()
    {
        foreach (var node in _treeRoots)
            (node.Thumbnail as IDisposable)?.Dispose();
    }

    private void SyncTreeSelection()
    {
        // Shapes are more specific than frames — prefer them so clicking a circle or
        // rect in the tree (or preview panel) keeps the shape node highlighted.
        object? sel = (object?)_selectedState.SelectedCircle
                   ?? _selectedState.SelectedRectangle
                   ?? _selectedState.SelectedFrame
                   ?? (object?)_selectedState.SelectedChain;

        var target = sel is not null ? TreeBuilder.FindNodeForData(_treeRoots, sel) : null;

        // When a shape is selected, ensure its parent frame node is expanded so
        // the shape node is visible in the tree (Avalonia does not auto-expand parents).
        if (sel is AARectSave or CircleSave)
        {
            var frame = _selectedState.SelectedFrame;
            if (frame is not null)
            {
                var frameNode = TreeBuilder.FindNodeForData(_treeRoots, frame);
                if (frameNode is not null)
                    frameNode.IsExpanded = true;
            }
        }

        if (target is not null && !(AnimTree.SelectedItems?.Contains(target) ?? false))
            AnimTree.SelectedItem = target;
    }

    private void RefreshTimelineStrip()
    {
        var chain = GetTimelineChain();

        // Capture old thumbnails before clearing so we dispose after the collection is empty
        // (avoids briefly holding disposed Bitmaps in bound Image controls)
        var oldThumbnails = _timelineFrames.Select(vm => vm.Thumbnail as IDisposable).ToList();
        _timelineFrames.Clear();
        foreach (var d in oldThumbnails)
            d?.Dispose();

        foreach (var item in TimelineBuilder.BuildFrameItems(chain))
            _timelineFrames.Add(item);
        _timelineEffectivePps = TimelineBuilder.ComputeEffectivePixelsPerSecond(chain);

        // Populate frame thumbnails (texture crop, no shapes)
        if (chain is not null)
        {
            for (int i = 0; i < chain.Frames.Count && i < _timelineFrames.Count; i++)
                _timelineFrames[i].Thumbnail = _thumbnailService.GetFrameThumbnail(chain.Frames[i], 22, 18);
        }

        _currentTimelineFrameIndex = -1;
        UpdateTimelineScrubber(GetPreferredTimelineFrameIndex(chain));
    }

    private AnimationChainSave? GetTimelineChain()
    {
        var chain = _selectedState.SelectedChain;
        if (chain is null && _selectedState.SelectedFrame is { } selectedFrame)
            chain = _objectFinder.GetAnimationChainContaining(selectedFrame);
        return chain;
    }

    private int GetPreferredTimelineFrameIndex(AnimationChainSave? chain)
    {
        if (chain is null || chain.Frames.Count == 0)
            return -1;

        if (_selectedState.SelectedFrame is { } selectedFrame)
        {
            var selectedFrameChain = _objectFinder.GetAnimationChainContaining(selectedFrame);
            if (ReferenceEquals(selectedFrameChain, chain))
            {
                var selectedFrameIndex = chain.Frames.IndexOf(selectedFrame);
                if (selectedFrameIndex >= 0)
                    return selectedFrameIndex;
            }
        }

        return PreviewCtrl.Playback.CurrentFrameIndex;
    }

    private void UpdateTimelineScrubber(int frameIndex)
    {
        if (_timelineFrames.Count == 0)
        {
            _currentTimelineFrameIndex = -1;
            return;
        }

        var clampedIndex = Math.Clamp(frameIndex, 0, _timelineFrames.Count - 1);
        if (_currentTimelineFrameIndex == clampedIndex)
            return;

        if (_currentTimelineFrameIndex >= 0 &&
            _currentTimelineFrameIndex < _timelineFrames.Count)
        {
            _timelineFrames[_currentTimelineFrameIndex].IsCurrent = false;
        }

        _timelineFrames[clampedIndex].IsCurrent = true;
        _currentTimelineFrameIndex = clampedIndex;
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

        if (vm?.Data is AARectSave rect)
        {
            AddMenuItem("Match Frame Size", () =>
            {
                var frame = _selectedState.SelectedFrame;
                if (frame is not null)
                {
                    _appCommands.MatchRectangleToFrame(rect, frame);
                    _appCommands.RefreshAnimationFrameDisplay();
                    _appCommands.SaveCurrentAnimationChainList();
                }
            });
            AddMenuItem("Delete Rectangle", () =>
                _ = _appCommands.AskToDeleteRectangles(new() { rect }));
        }
        else if (vm?.Data is CircleSave circle)
        {
            AddMenuItem("Delete Circle", () =>
                _ = _appCommands.AskToDeleteCircles(new() { circle }));
        }
        else if (vm?.Data is AnimationFrameSave frame2)
        {
            var chain2 = _objectFinder.GetAnimationChainContaining(frame2);
            if (chain2 is not null && chain2.Frames.Count > 1)
            {
                var frameIndex = chain2.Frames.IndexOf(frame2);
                var isFirst    = frameIndex == 0;
                var isLast     = frameIndex == chain2.Frames.Count - 1;
                if (!isFirst) AddMenuItem("^^ Move To Top",   () => _appCommands.MoveFrameToTop(frame2, chain2));
                if (!isFirst) AddMenuItem("^  Move Up",        () => _appCommands.MoveFrame(frame2, chain2, -1));
                if (!isLast)  AddMenuItem("v  Move Down",      () => _appCommands.MoveFrame(frame2, chain2, +1));
                if (!isLast)  AddMenuItem("vv Move To Bottom", () => _appCommands.MoveFrameToBottom(frame2, chain2));
                AddSeparator();
            }
            AddMenuItem("Add AxisAlignedRectangle", () => _appCommands.AddAxisAlignedRectangle(frame2));
            AddMenuItem("Add Circle",               () => _appCommands.AddCircle(frame2));
            AddSeparator();
            AddMenuItem("Copy",  () => _ = HandleCopyAsync());
            AddMenuItem("Paste", () => _ = HandlePasteAsync());
            AddSeparator();
            AddMenuItem("View Texture in Explorer", () => ViewTextureInExplorer(frame2));
            AddMenuItem("Rename…", () =>
                BeginInlineRename(vm!, frame2.HasCustomName ? frame2.Name : string.Empty));
            AddSeparator();
            if (chain2 is not null)
                AddMenuItem("Duplicate Frame", () => _appCommands.DuplicateFrame(frame2, chain2));
            AddSeparator();
            AddMenuItem("Delete Frame", () =>
                _appCommands.DeleteFrames(new List<AnimationFrameSave> { frame2 }));
        }
        else if (vm?.Data is AnimationChainSave chain)
        {
            var chains = _projectManager.AnimationChainListSave?.AnimationChains;
            if (chains is not null && chains.Count > 1)
            {
                var chainIndex = chains.IndexOf(chain);
                var isFirst    = chainIndex == 0;
                var isLast     = chainIndex == chains.Count - 1;
                if (!isFirst) AddMenuItem("^^ Move To Top",   () => _appCommands.MoveChainToTop(chain));
                if (!isFirst) AddMenuItem("^  Move Up",        () => _appCommands.MoveChain(chain, -1));
                if (!isLast)  AddMenuItem("v  Move Down",      () => _appCommands.MoveChain(chain, +1));
                if (!isLast)  AddMenuItem("vv Move To Bottom", () => _appCommands.MoveChainToBottom(chain));
                AddSeparator();
            }
            AddMenuItem("Adjust Frame Time…", () => AskAdjustFrameTime(chain));
            AddMenuItem("Flip Horizontally",  () => _appCommands.FlipChainHorizontally(chain));
            AddMenuItem("Flip Vertically",    () => _appCommands.FlipChainVertically(chain));
            AddMenuItem("Invert Frame Order", () => _appCommands.InvertFrameOrder(chain));
            AddSeparator();
            AddMenuItem("Add Animation", AddAnimationChainAndBeginInlineRename);
            AddMenuItem("Add Frame",          () => _appCommands.AddFrame(chain));
            AddMenuItem("Add Multiple Frames…", () => _ = AskAddMultipleFramesAsync(chain));
            AddSeparator();
            AddMenuItem("Duplicate (original)",         () => _appCommands.DuplicateChain(chain));
            AddMenuItem("Duplicate (flip horizontally)",() => _appCommands.DuplicateChain(chain, flipH: true));
            AddMenuItem("Duplicate (flip vertically)",  () => _appCommands.DuplicateChain(chain, flipV: true));
            AddSeparator();
            AddMenuItem("Copy",  () => _ = HandleCopyAsync());
            AddMenuItem("Paste", () => _ = HandlePasteAsync());
            AddSeparator();
            AddMenuItem("Adjust Offsets…", () => _ = AskAdjustOffsetsAsync(chain));
            AddMenuItem("Rename…",          () => BeginInlineRenameSelected(chain));
            AddSeparator();
            AddMenuItem("Delete Animation", () =>
            {
                if (chain.Frames.Count > 0)
                    ShowDeleteChainConfirm(chain);
                else
                    _appCommands.DeleteAnimationChains(new List<AnimationChainSave> { chain });
            });
        }
        else
        {
            AddMenuItem("Add Animation", () =>
            {
                if (_projectManager.AnimationChainListSave is null)
                    _projectManager.AnimationChainListSave = new AnimationChainListSave();
                AddAnimationChainAndBeginInlineRename();
            });
        }

        AddSeparator();
        AddMenuItem("Sort Animations Alphabetically",
            () => _appCommands.SortAnimationsAlphabetically());
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

        var dialog = BuildAdjustFrameTimeWindow();

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
            bool showLabel = radioSetAll.IsChecked == true;
            perFrameLabel.IsVisible = showLabel;
            if (showLabel)
                perFrameLabel.Text = $"Each frame: {val / frameCount:F3} seconds";
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

        void Apply()
        {
            if (durationInput.Value.HasValue)
            {
                float newTotal = (float)durationInput.Value.Value;
                if (radioProportional.IsChecked == true)
                    _appCommands.ScaleFrameTimesProportional(chain, newTotal);
                else
                    _appCommands.ScaleFrameTimesSetAllSame(chain, newTotal);
            }
            dialog.Close();
        }

        var okBtn     = new Button { Content = "OK" };
        var cancelBtn = new Button { Content = "Cancel" };
        okBtn.Click     += (_, _) => Apply();
        cancelBtn.Click += (_, _) => dialog.Close();

        var btns = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };
        btns.Children.Add(okBtn);
        btns.Children.Add(cancelBtn);
        panel.Children.Add(btns);
        dialog.Content = panel;

        WireDialogKeyboard(dialog, onConfirm: Apply, onCancel: dialog.Close);

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
        PropRed.ValueChanged       += (_, _) => ApplyFrameColor();
        PropGreen.ValueChanged     += (_, _) => ApplyFrameColor();
        PropBlue.ValueChanged      += (_, _) => ApplyFrameColor();
        PropColorMode.SelectionChanged += (_, _) => ApplyFrameColorOperation();
        PropPixelX.ValueChanged    += (_, _) => ApplyFramePixelCoords();
        PropPixelY.ValueChanged    += (_, _) => ApplyFramePixelCoords();
        PropPixelW.ValueChanged    += (_, _) => ApplyFramePixelCoords();
        PropPixelH.ValueChanged    += (_, _) => ApplyFramePixelCoords();
        PropRectName.LostFocus     += (_, _) => ApplyRectProps();
        PropRectX.ValueChanged     += (_, _) => ApplyRectProps();
        PropRectY.ValueChanged     += (_, _) => ApplyRectProps();
        PropRectScaleX.ValueChanged += (_, _) => ApplyRectProps();
        PropRectScaleY.ValueChanged += (_, _) => ApplyRectProps();

        PropCircleName.LostFocus   += (_, _) => ApplyCircleProps();
        PropCircleX.ValueChanged   += (_, _) => ApplyCircleProps();
        PropCircleY.ValueChanged   += (_, _) => ApplyCircleProps();
        PropCircleRadius.ValueChanged += (_, _) => ApplyCircleProps();

        PropTextureName.LostFocus  += (_, _) => ApplyTextureName();
        PropTextureBrowseBtn.Click += async (_, _) => await BrowseForFrameTexture();
    }

    private void ApplyTextureName()
    {
        if (_suppressPropRefresh) return;
        var frame = _selectedState.SelectedFrame;
        if (frame is null) return;

        var inputText = PropTextureName.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(inputText)) return;

        var currentDisplay = TexturePathHelper.ComputeDisplayPath(frame.TextureName, _projectManager.FileName);
        if (inputText == currentDisplay) return;

        string achxFolder = string.IsNullOrEmpty(_projectManager.FileName)
            ? string.Empty
            : (Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty);

        string absolutePath = TexturePathHelper.ResolveDisplayPath(inputText, achxFolder);
        string storePath    = TexturePathHelper.ComputeStorePath(absolutePath, achxFolder);

        _appCommands.SetFrameTextureName(frame, storePath);
        WireframeCtrl.LoadTexture(absolutePath);
        RefreshPropertyPanel();
    }

    private async Task BrowseForFrameTexture()
    {
        var frame = _selectedState.SelectedFrame;
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

        string achxFolder = string.IsNullOrEmpty(_projectManager.FileName)
            ? string.Empty
            : (Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty);

        // resolvedAbsPath tracks the actual file we will use (may change if user copies it)
        string resolvedAbsPath = pickedPath;

        if (!string.IsNullOrEmpty(achxFolder))
        {
            bool isInAchxFolder    = IsPathUnder(pickedPath, achxFolder);
            bool isInProjectFolder = !string.IsNullOrEmpty(_appState.ProjectFolder)
                                     && IsPathUnder(pickedPath, _appState.ProjectFolder);

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
                        var capturedSource = pickedPath;
                        var capturedDest   = destination;
                        ShowToast($"Could not copy: {ex.Message}", retryAction: () =>
                        {
                            try
                            {
                                File.Copy(capturedSource, capturedDest, overwrite: true);
                                _appCommands.SetFrameTextureName(frame, TexturePathHelper.ComputeStorePath(capturedDest, achxFolder));
                                WireframeCtrl.LoadTexture(capturedDest);
                                RefreshPropertyPanel();
                            }
                            catch (Exception retryEx)
                            {
                                ShowToast($"Retry failed: {retryEx.Message}");
                            }
                        });
                    }
                }
            }
        }

        // Store relative path when possible; ../relative paths are allowed for textures
        // outside the .achx folder so they round-trip correctly.
        string storePath = string.IsNullOrEmpty(achxFolder)
            ? resolvedAbsPath
            : TexturePathHelper.ComputeStorePath(resolvedAbsPath, achxFolder);

        _appCommands.SetFrameTextureName(frame, storePath);
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
            Text = $"The selected file:\n\n{absoluteTexturePath}\n\nis not relative to the Animation file.  What would you like to do?",
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });

        var copyBtn = new Button
        {
            Content = "Copy the file to the same folder as the Animation",
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch
        };
        var keepBtn = new Button
        {
            Content = "Keep the file where it is (this may limit the portability of the Animation file)",
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
            var frame = _selectedState.SelectedFrame;
            var rect  = _selectedState.SelectedRectangle;
            var circ  = _selectedState.SelectedCircle;
            var hasShapeSelection = rect is not null || circ is not null;

            bool noneVisible = frame is null && rect is null && circ is null;
            PropNoneLabel.IsVisible = noneVisible;
            if (noneVisible)
            {
                PropNoneLabel.Text = _selectedState.SelectedChain is not null
                    ? "Select a frame or shape to edit its properties."
                    : "No selection";
            }
            PropFramePanel.IsVisible  = frame is not null && !hasShapeSelection;
            PropRectPanel.IsVisible   = rect  is not null;
            PropCirclePanel.IsVisible = circ  is not null;

            if (frame is not null && !hasShapeSelection)
            {
                PropFlipH.IsChecked  = frame.FlipHorizontal;
                PropFlipV.IsChecked  = frame.FlipVertical;
                PropFrameLen.Value   = (decimal)frame.FrameLength;
                PropRelX.Value       = (decimal)frame.RelativeX;
                PropRelY.Value       = (decimal)frame.RelativeY;
                PropRed.Value        = frame.Red.HasValue   ? frame.Red.Value   : (decimal?)null;
                PropGreen.Value      = frame.Green.HasValue ? frame.Green.Value : (decimal?)null;
                PropBlue.Value       = frame.Blue.HasValue  ? frame.Blue.Value  : (decimal?)null;
                PropColorMode.SelectedIndex = frame.ColorOperation switch
                {
                    ColorOperation.Multiply => 1,
                    ColorOperation.Add      => 2,
                    _                       => 0, // None / null
                };
                PropTextureName.Text = TexturePathHelper.ComputeDisplayPath(
                    frame.TextureName, _projectManager.FileName);

                var (bmpW, bmpH) = WireframeCtrl.BitmapSize;
                if (bmpW > 0 && bmpH > 0)
                {
                    PropPixelX.Value = FrameDisplayValues.GetPixelX(frame, bmpW);
                    PropPixelY.Value = FrameDisplayValues.GetPixelY(frame, bmpH);
                    PropPixelW.Value = FrameDisplayValues.GetPixelWidth(frame, bmpW);
                    PropPixelH.Value = FrameDisplayValues.GetPixelHeight(frame, bmpH);
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
        var frame = _selectedState.SelectedFrame;
        if (frame is null) return;
        // Route through the undoable flip commands. FlipFrame* toggles, so only call
        // it when the toggle button's state actually differs from the model.
        if (frame.FlipHorizontal != (PropFlipH.IsChecked == true))
            _appCommands.FlipFrameHorizontally(frame);
        if (frame.FlipVertical != (PropFlipV.IsChecked == true))
            _appCommands.FlipFrameVertically(frame);
    }

    private void ApplyFrameLen()
    {
        if (_suppressPropRefresh) return;
        var frame = _selectedState.SelectedFrame;
        if (frame is null || !PropFrameLen.Value.HasValue) return;
        _appCommands.SetFrameLength(frame, (float)PropFrameLen.Value.Value);
    }

    private void ApplyFrameRelative()
    {
        if (_suppressPropRefresh) return;
        var frame = _selectedState.SelectedFrame;
        if (frame is null || !PropRelX.Value.HasValue || !PropRelY.Value.HasValue) return;
        _appCommands.SetFrameRelative(frame, (float)PropRelX.Value.Value, (float)PropRelY.Value.Value);
    }

    private void ApplyFrameColor()
    {
        if (_suppressPropRefresh) return;
        var frame = _selectedState.SelectedFrame;
        if (frame is null) return;
        // A blank NumericUpDown (null Value) means the channel is unset and is omitted from the .achx.
        static int? ToChannel(decimal? v) => v.HasValue ? (int)v.Value : null;
        _appCommands.SetFrameColor(frame, ToChannel(PropRed.Value), ToChannel(PropGreen.Value), ToChannel(PropBlue.Value));
    }

    private void ApplyFrameColorOperation()
    {
        if (_suppressPropRefresh) return;
        var frame = _selectedState.SelectedFrame;
        if (frame is null) return;
        // ComboBox order: 0 = None (null), 1 = Multiply, 2 = Add.
        ColorOperation? operation = PropColorMode.SelectedIndex switch
        {
            1 => ColorOperation.Multiply,
            2 => ColorOperation.Add,
            _ => null,
        };
        _appCommands.SetFrameColorOperation(frame, operation);
    }

    private void ApplyFramePixelCoords()
    {
        if (_suppressPropRefresh) return;
        var frame = _selectedState.SelectedFrame;
        if (frame is null) return;
        var (bmpW, bmpH) = WireframeCtrl.BitmapSize;
        if (bmpW <= 0 || bmpH <= 0) return;
        if (!PropPixelX.Value.HasValue || !PropPixelY.Value.HasValue ||
            !PropPixelW.Value.HasValue || !PropPixelH.Value.HasValue) return;
        _appCommands.SetFramePixelRegion(frame,
            (int)PropPixelX.Value.Value, (int)PropPixelY.Value.Value,
            (int)PropPixelW.Value.Value, (int)PropPixelH.Value.Value,
            bmpW, bmpH);
        WireframeCtrl.RefreshFrames();
    }


    private void ApplyRectProps()
    {
        if (_suppressPropRefresh) return;
        var rect = _selectedState.SelectedRectangle;
        if (rect is null || !PropRectX.Value.HasValue || !PropRectY.Value.HasValue ||
            !PropRectScaleX.Value.HasValue || !PropRectScaleY.Value.HasValue) return;
        var frame = _selectedState.SelectedFrame;
        _appCommands.SetRectProps(frame, rect,
            PropRectName.Text ?? "",
            (float)PropRectX.Value.Value, (float)PropRectY.Value.Value,
            (float)PropRectScaleX.Value.Value, (float)PropRectScaleY.Value.Value);
    }

    private void ApplyCircleProps()
    {
        if (_suppressPropRefresh) return;
        var circ = _selectedState.SelectedCircle;
        if (circ is null || !PropCircleX.Value.HasValue || !PropCircleY.Value.HasValue ||
            !PropCircleRadius.Value.HasValue) return;
        var frame = _selectedState.SelectedFrame;
        _appCommands.SetCircleProps(frame, circ,
            PropCircleName.Text ?? "",
            (float)PropCircleX.Value.Value, (float)PropCircleY.Value.Value,
            (float)PropCircleRadius.Value.Value);
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

    private async Task LoadAnimationFileAsync(string fileName)
    {
        if (string.IsNullOrEmpty(fileName)) return;

        // Save the leaving tab's undo history before a different file takes over the editor.
        var leavingTab = _tabManager.ActiveTab;
        if (leavingTab != null)
            leavingTab.UndoSnapshot = _undoManager.TakeSnapshot();

        // If there is already a file open that hasn't been registered as a tab yet,
        // add it as a background tab so it appears as the first tab when the second
        // file is opened.  This covers the common case of File > Open > Open.
        EnsureCurrentEditorContentHasTab();

        var filePath = new FilePath(fileName);
        var result = _tabManager.OpenOrFocus(filePath);
        var arrivedTab = _tabManager.ActiveTab;

        if (result == TabOpenResult.Focused)
        {
            // The tab is already registered and now active in TabManager, but the editor
            // may still be displaying a different tab's content (e.g. user was on tab 1
            // and re-opened tab 2's file via File > Open).  Load the file to ensure the
            // panels and previews reflect the focused tab.
            bool alreadyShown = string.Equals(_projectManager.FileName, fileName,
                StringComparison.OrdinalIgnoreCase);
            if (!alreadyShown && !string.IsNullOrEmpty(fileName))
            {
                await _appCommands.OpenAchxWorkflowAsync(fileName);
                if (arrivedTab?.UndoSnapshot != null)
                    _undoManager.RestoreSnapshot(arrivedTab.UndoSnapshot);
            }
            RebuildTabStrip();
            return;
        }

        await _appCommands.OpenAchxWorkflowAsync(fileName);
        // Restore this tab's prior history if it was previously open (snapshot normally
        // null on first open; non-null if the tab was closed and re-opened mid-session).
        if (arrivedTab?.UndoSnapshot != null)
            _undoManager.RestoreSnapshot(arrivedTab.UndoSnapshot);
    }

    /// <summary>
    /// Registers the currently-loaded file (or an "Untitled" placeholder when the editor has
    /// content but no saved path) as a background tab so it appears before the next file that
    /// is about to be opened.
    /// </summary>
    private void EnsureCurrentEditorContentHasTab()
    {
        var currentPath = _projectManager.FileName;
        if (!string.IsNullOrEmpty(currentPath))
        {
            // Saved file — add its tab if not already tracked.
            var fp = new FilePath(currentPath);
            if (_tabManager.Tabs.All(t => t.Path != fp))
                _tabManager.RegisterBackground(fp);
        }
        else if (_tabManager.Tabs.Count == 0 &&
                 _projectManager.AnimationChainListSave?.AnimationChains.Count > 0)
        {
            // Unsaved new file with content — register a numbered Untitled placeholder tab.
            var displayName = TabManager.ComputeUntitledDisplayName(
                _tabManager.Tabs.Select(t => t.DisplayName).ToList());
            _tabManager.RegisterBackground(new FilePath(NewUntitledSentinelPath()), displayName);
        }
    }

    /// <summary>
    /// Returns the tab index that corresponds to the given X coordinate (in the TabStrip
    /// StackPanel's local coordinate space).  Finds the first tab whose centre is to the
    /// right of <paramref name="xInTabStrip"/>; if none, returns the last tab index.
    /// </summary>
    private int ComputeTabIndexAt(double xInTabStrip)
    {
        var children = TabStrip.Children;
        for (int i = 0; i < children.Count; i++)
        {
            var b = children[i].Bounds;
            if (xInTabStrip < b.Left + b.Width / 2.0)
                return i;
        }
        return Math.Max(0, children.Count - 1);
    }

    // Sentinel paths use the prefix "__untitled__:" so they are distinguishable from real
    // on-disk paths and are unique per new-file action within this window session.
    private const string UntitledSentinelPrefix = "__untitled__:";

    private static bool IsUntitledSentinel(string? path) =>
        path?.StartsWith(UntitledSentinelPrefix, StringComparison.Ordinal) == true;

    private static bool IsUntitledTab(TabEntry? tab) =>
        tab != null &&
        (string.IsNullOrEmpty(tab.Path.Original) || IsUntitledSentinel(tab.Path.Original));

    private string NewUntitledSentinelPath() =>
        $"{UntitledSentinelPrefix}{++_untitledCounter}";

    /// <summary>
    /// Closes <paramref name="tab"/> in this window and opens it in a brand-new,
    /// fully-independent <see cref="MainWindow"/> instance.
    /// No-op for Untitled (unsaved) tabs — there is no file to move.
    /// </summary>
    private void DetachTab(TabEntry tab)
    {
        if (IsUntitledTab(tab)) return;
        var filePath = tab.Path.FullPath;
        CloseTab(tab);
        var window = App.CreateDetachedWindow();
        window.Show();
        _ = window.OpenFileAsTab(filePath);
    }

    private void UpdateTitle()
    {
        var filePath = _projectManager.FileName;
        Title = TitleBarHelper.BuildWindowTitle(filePath);

        var hasFile = !string.IsNullOrEmpty(filePath);
        TitleSeparator.IsVisible = hasFile;
        TitleFileName.IsVisible  = hasFile;
        if (hasFile)
        {
            TitleFileName.Text = new FilePath(filePath!).NoPath;
            ToolTip.SetTip(TitleFileName, filePath);
        }
    }

    // ── Theme ─────────────────────────────────────────────────────────────────

    /// <summary>Applies the persisted theme to the application and syncs the menu checkmarks.</summary>
    private void ApplyPersistedTheme()
    {
        if (Avalonia.Application.Current is { } app)
            app.RequestedThemeVariant = ThemeManager.ToVariant(_appSettings.Theme);
        SyncThemeMenuChecks();
    }

    private void SetTheme(AppTheme theme)
    {
        _appSettings.Theme = theme;
        if (Avalonia.Application.Current is { } app)
            app.RequestedThemeVariant = ThemeManager.ToVariant(theme);
        SyncThemeMenuChecks();
        SaveSettingsFile();
    }

    private void SyncThemeMenuChecks()
    {
        MenuThemeLight.IsChecked  = _appSettings.Theme == AppTheme.Light;
        MenuThemeDark.IsChecked   = _appSettings.Theme == AppTheme.Dark;
        MenuThemeSystem.IsChecked = _appSettings.Theme == AppTheme.System;
    }

    /// <summary>
    /// Resolves a design-token brush for the application's current theme variant. Used for
    /// C#-built controls that can't bind via DynamicResource in XAML (tab strip, history rows,
    /// dialog builders). All windows inherit the app variant, so this matches their appearance.
    /// </summary>
    private static IBrush ThemedBrush(string key) =>
        Avalonia.Application.Current is { } app
        && app.TryFindResource(key, app.ActualThemeVariant, out var v) && v is IBrush b
            ? b : Brushes.Transparent;

    private void LoadSettingsFile()
    {
        try
        {
            if (SettingsFilePath.Exists())
            {
                var contents = File.ReadAllText(SettingsFilePath.FullPath);
                _appSettings = JsonSerializer.Deserialize<AppSettingsModel>(contents)
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
            var settingsFile = SettingsFilePath;
            // The AnimationEditor subfolder under %APPDATA% won't exist on a fresh install.
            Directory.CreateDirectory(settingsFile.GetDirectoryContainingThis().FullPath);
            File.WriteAllText(settingsFile.FullPath,
                JsonSerializer.Serialize(_appSettings, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (IOException)
        {
            // File in use — ignore
        }
    }

    // ── Load-failed error dialog ──────────────────────────────────────────────

    private Task ShowLoadFailedDialogAsync(string filePath, Exception ex)
    {
        var fileName = Path.GetFileName(filePath);
        ShowStatusMessage($"⚠ Could not load '{fileName}': {ex.Message}", isError: true);
        return Task.CompletedTask;
    }

    private async Task<bool> ShowConfirmDialogAsync(string message, string title)
    {
        var tcs = new TaskCompletionSource<bool>();
        var dialog = BuildConfirmDialog(message, title, tcs);
        await dialog.ShowDialog(this);
        return await tcs.Task;
    }

    /// <summary>
    /// Builds the yes/no confirmation dialog. ENTER confirms (Yes), ESC cancels
    /// (No), and closing the window by any other means resolves
    /// <paramref name="tcs"/> to false. Extracted for testability.
    /// </summary>
    internal static Window BuildConfirmDialog(string message, string title, TaskCompletionSource<bool> tcs)
    {
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

        WireDialogKeyboard(dialog,
            onConfirm: () => { tcs.TrySetResult(true);  dialog.Close(); },
            onCancel:  () => { tcs.TrySetResult(false); dialog.Close(); });

        return dialog;
    }

    /// <summary>
    /// Builds a danger-styled delete confirmation dialog. ENTER confirms (Delete), ESC cancels,
    /// and closing by any other means resolves <paramref name="tcs"/> to false.
    /// </summary>
    internal static Window BuildDeleteConfirmDialog(string message, string title, TaskCompletionSource<bool> tcs)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 380,
            SizeToContent = SizeToContent.Height,
            CanResize = false,
            WindowStartupLocation = WindowStartupLocation.CenterOwner
        };

        var panel = new StackPanel { Margin = new Avalonia.Thickness(20, 16, 20, 16), Spacing = 8 };
        panel.Children.Add(new TextBlock
        {
            Text = message,
            FontSize = 13,
            TextWrapping = Avalonia.Media.TextWrapping.Wrap
        });
        panel.Children.Add(new TextBlock
        {
            Text = "This action cannot be undone.",
            FontSize = 11,
            Foreground = ThemedBrush("InkMid")
        });

        var deleteBtn = new Button
        {
            Content = "Delete",
            Background = ThemedBrush("Accent"),
            Foreground = Avalonia.Media.Brushes.White,
            Padding = new Avalonia.Thickness(16, 6)
        };
        var cancelBtn = new Button
        {
            Content = "Cancel",
            Padding = new Avalonia.Thickness(16, 6)
        };

        deleteBtn.Click += (_, _) => { tcs.TrySetResult(true);  dialog.Close(); };
        cancelBtn.Click += (_, _) => { tcs.TrySetResult(false); dialog.Close(); };

        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            Spacing = 8,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Avalonia.Thickness(0, 4, 0, 0)
        };
        buttons.Children.Add(cancelBtn);
        buttons.Children.Add(deleteBtn);
        panel.Children.Add(buttons);

        dialog.Content = panel;
        dialog.Closed += (_, _) => tcs.TrySetResult(false);

        WireDialogKeyboard(dialog,
            onConfirm: () => { tcs.TrySetResult(true);  dialog.Close(); },
            onCancel:  () => { tcs.TrySetResult(false); dialog.Close(); });

        // Ensure ENTER confirms (Delete) by focusing the delete button last — it overrides
        // WireDialogKeyboard's Opened handler which would otherwise land on Cancel (first child).
        dialog.Opened += (_, _) => deleteBtn.Focus();

        return dialog;
    }

    /// <summary>
    /// Wires ENTER → <paramref name="onConfirm"/> and ESC → <paramref name="onCancel"/>
    /// on a modal dialog. The handler is attached at the window with
    /// <c>handledEventsToo: true</c> so it still fires when a focused input control
    /// (e.g. <see cref="NumericUpDown"/>) has already marked the key event handled —
    /// which is why <see cref="Button.IsDefault"/>/<see cref="Button.IsCancel"/> alone
    /// are unreliable for dialogs that contain text or numeric inputs.
    /// Also moves focus into the dialog on open: a freshly-shown window has no
    /// focused element, and keyboard input is not routed anywhere until something
    /// is focused, so without this ENTER/ESC do nothing until the user clicks.
    /// </summary>
    internal static void WireDialogKeyboard(Window dialog, Action onConfirm, Action onCancel)
    {
        dialog.AddHandler(InputElement.KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Enter)       { onConfirm(); e.Handled = true; }
            else if (e.Key == Key.Escape) { onCancel();  e.Handled = true; }
        }, RoutingStrategies.Bubble, handledEventsToo: true);

        dialog.Opened += (_, _) =>
            dialog.GetVisualDescendants()
                  .OfType<InputElement>()
                  .FirstOrDefault(x => x is { Focusable: true, IsEffectivelyVisible: true, IsEffectivelyEnabled: true })
                  ?.Focus();
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

    // ── Status bar message ────────────────────────────────────────────────────

    private DispatcherTimer? _statusMessageTimer;

    private void ShowStatusMessage(string text, bool isError = false)
    {
        StatusMessage.Text = text;
        StatusMessage.Foreground = isError
            ? new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromRgb(220, 80, 60))
            : new Avalonia.Media.SolidColorBrush(Avalonia.Media.Color.FromArgb(255, 160, 160, 160));
        StatusMessage.IsVisible = true;

        _statusMessageTimer?.Stop();
        _statusMessageTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _statusMessageTimer.Tick += (_, _) =>
        {
            _statusMessageTimer.Stop();
            StatusMessage.IsVisible = false;
            StatusMessage.Text = string.Empty;
        };
        _statusMessageTimer.Start();
    }

    // ── Toast notification ────────────────────────────────────────────────────

    private DispatcherTimer? _toastTimer;
    private Action? _toastRetryAction;

    private void InitToast()
    {
        ToastDismissBtn.Click += (_, _) => HideToast();
        ToastRetryBtn.Click   += (_, _) =>
        {
            HideToast();
            _toastRetryAction?.Invoke();
        };
    }

    private void ShowToast(string message, Action? retryAction = null)
    {
        _toastRetryAction = retryAction;
        ToastMessage.Text = message;
        ToastRetryBtn.IsVisible = retryAction is not null;
        ToastPanel.IsVisible = true;

        _toastTimer?.Stop();
        _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(6) };
        _toastTimer.Tick += (_, _) => HideToast();
        _toastTimer.Start();
    }

    private void HideToast()
    {
        _toastTimer?.Stop();
        ToastPanel.IsVisible = false;
    }

    // ── Keyboard wiring ───────────────────────────────────────────────────────

    // Returns true when the platform command modifier is active.
    // On macOS the Command key (⌘) maps to KeyModifiers.Meta; on Windows/Linux it is Control.
    // Accepting both lets Ctrl+C/V/Z keep working in tests and on Windows/Linux while also
    // handling Cmd+C/V/Z on macOS.
    private static bool HasCommandModifier(KeyModifiers m)
        => m.HasFlag(KeyModifiers.Control) || m.HasFlag(KeyModifiers.Meta);

    private void WireKeyboard()
    {
        // Use Tunnel routing so we intercept keys before child controls (e.g. the TreeView,
        // which handles Up/Down for navigation and would mark the event Handled before the
        // default Bubble-phase KeyDown fires).
        AddHandler(KeyDownEvent, (EventHandler<KeyEventArgs>)((_, e) =>
        {
            if (e.Handled) return;

            if (e.Key == Key.C && HasCommandModifier(e.KeyModifiers))
            {
                if (IsTextInputFocused()) return;
                e.Handled = true;
                _ = HandleCopyAsync();
            }
            else if (e.Key == Key.V && HasCommandModifier(e.KeyModifiers))
            {
                if (IsTextInputFocused()) return;
                e.Handled = true;
                _ = HandlePasteAsync();
            }
            else if (e.Key == Key.Delete)
            {
                if (IsTextInputFocused()) return;
                e.Handled = true;
                HandleDelete();
            }
            else if (e.Key == Key.F2)
            {
                e.Handled = true;
                // Prefer the tree's SelectedItem, fall back to _selectedState when the tree
                // has temporarily lost focus (e.g. immediately after ALT+arrow reorder, where
                // focus shifts before our Background-priority re-focus post runs).
                var vm = AnimTree.SelectedItem as TreeNodeVm
                      ?? (_selectedState.SelectedFrame is { } sf
                              ? TreeBuilder.FindNodeForData(_treeRoots, sf) : null)
                      ?? (_selectedState.SelectedChain is { } sc
                              ? TreeBuilder.FindNodeForData(_treeRoots, sc) : null);

                if (vm is not null)
                {
                    if (vm.Data is AnimationChainSave chain)
                        BeginInlineRename(vm, chain.Name);
                    else if (vm.Data is AnimationFrameSave frame)
                        BeginInlineRename(vm, frame.HasCustomName ? frame.Name : string.Empty);
                }
                else
                    WireframeCtrl.ToggleDebugMode();
            }
            else if (e.Key == Key.Z && HasCommandModifier(e.KeyModifiers) &&
                     !e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                e.Handled = true;
                _undoManager.Undo();
            }
            else if ((e.Key == Key.Y && HasCommandModifier(e.KeyModifiers)) ||
                     (e.Key == Key.Z && (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) ||
                                         e.KeyModifiers == (KeyModifiers.Meta    | KeyModifiers.Shift))))
            {
                e.Handled = true;
                _undoManager.Redo();
            }
            else if ((e.Key == Key.Up || e.Key == Key.Down) &&
                     e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                e.Handled = true;
                int delta = e.Key == Key.Up ? -1 : +1;
                _appCommands.HandleReorder(delta);
                // Restore focus to the tree — reorder can cause Avalonia to shift focus
                // away, which would make F2 fall through to WireframeCtrl.ToggleDebugMode.
                Dispatcher.UIThread.Post(() => AnimTree.Focus(), DispatcherPriority.Background);
                if (_selectedState.SelectedFrame is { HasCustomName: false })
                    ShowStatusMessage("Frame labels updated to reflect new positions  ·  F2 to assign a custom name");
            }
        }), RoutingStrategies.Tunnel);
    }

    // Returns true when a text-editing control (TextBox) owns keyboard focus.
    // Used to gate frame/shape copy-paste and Delete so those keys still reach
    // the text control instead of being swallowed by the window-level handler.
    private bool IsTextInputFocused()
        => FocusManager?.GetFocusedElement() is TextBox;

    // ── Copy / Paste ──────────────────────────────────────────────────────────

    private async Task HandleCopyAsync()
    {
        if (IsTextInputFocused()) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        string? xml = null;

        var selectedVm = AnimTree.SelectedItem as TreeNodeVm;
        if (selectedVm?.Data is AnimationChainSave chainToCopy)
            xml = ClipboardPayload.Serialize(new List<AnimationChainSave> { chainToCopy });
        else if (selectedVm?.Data is AnimationFrameSave frameToCopy)
            xml = ClipboardPayload.Serialize(new List<AnimationFrameSave> { frameToCopy });
        else if (selectedVm?.Data is AARectSave rectToCopy)
            xml = ClipboardPayload.Serialize(rectToCopy);
        else if (selectedVm?.Data is CircleSave circleToCopy)
            xml = ClipboardPayload.Serialize(circleToCopy);

        if (xml is not null)
            await clipboard.SetTextAsync(xml);
    }

    private async Task HandlePasteAsync()
    {
        if (IsTextInputFocused()) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(text)) return;

        bool ok = ClipboardPayload.TryDeserialize(text,
            out var chains, out var frames, out var rectangle, out var circle);
        if (!ok) return;

        var acls = _projectManager.AnimationChainListSave;
        if (acls is null) return;

        var selectedVm = AnimTree.SelectedItem as TreeNodeVm;

        // Each branch hands the mutation to an undoable command via _appCommands;
        // the read-side prep (target resolution, ShapesSave init, name uniquing)
        // stays here. The commands raise refresh/save events themselves.
        if (chains is { Count: > 0 })
        {
            _appCommands.PasteChains(chains);
            _selectedState.SelectedChain = chains[^1];
        }
        else if (frames is { Count: > 0 })
        {
            AnimationChainSave? targetChain = null;
            if (selectedVm?.Data is AnimationChainSave c) targetChain = c;
            else if (selectedVm?.Data is AnimationFrameSave f)
                targetChain = _objectFinder.GetAnimationChainContaining(f);

            if (targetChain is null && acls.AnimationChains.Count > 0)
                targetChain = acls.AnimationChains[^1];

            if (targetChain is null) return;

            foreach (var pasted in frames)
                pasted.ShapesSave ??= new ShapesSave();

            FramePasteLogic.AssignUniqueNames(targetChain.Frames, frames);
            _appCommands.PasteFrames(targetChain, frames);
            _selectedState.SelectedFrame = frames[^1];
            _appCommands.RefreshWireframe();
        }
        else if (rectangle is not null)
        {
            var frame = _selectedState.SelectedFrame;
            if (frame is null) return;
            frame.ShapesSave ??= new ShapesSave();
            var existingNames = frame.ShapesSave.AARectSaves
                .Select(r => r.Name)
                .Concat(frame.ShapesSave.CircleSaves.Select(c => c.Name))
                .ToList();
            rectangle.Name = StringFunctions.MakeStringUnique(
                rectangle.Name, existingNames, 2);
            _appCommands.PasteRectangle(frame, rectangle);
        }
        else if (circle is not null)
        {
            var frame = _selectedState.SelectedFrame;
            if (frame is null) return;
            frame.ShapesSave ??= new ShapesSave();
            var existingNames = frame.ShapesSave.AARectSaves
                .Select(r => r.Name)
                .Concat(frame.ShapesSave.CircleSaves.Select(c => c.Name))
                .ToList();
            circle.Name = StringFunctions.MakeStringUnique(
                circle.Name, existingNames, 2);
            _appCommands.PasteCircle(frame, circle);
        }
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    private void HandleDelete()
    {
        var selectedVm = AnimTree.SelectedItem as TreeNodeVm;
        if (selectedVm is null) return;

        // Delete the whole multi-selection of the focused node's kind, not just the
        // focused node — AskToDelete* batches them into a single undo step.
        if (selectedVm.Data is AnimationChainSave chainToDel)
        {
            var chains = _selectedState.SelectedChains;
            List<AnimationChainSave> toDelete = chains.Count > 0 ? chains : new List<AnimationChainSave> { chainToDel };
            if (toDelete.Any(c => c.Frames.Count > 0))
                ShowDeleteChainConfirm(toDelete);
            else
                _appCommands.DeleteAnimationChains(toDelete);
        }
        else if (selectedVm.Data is AnimationFrameSave frameToDel)
        {
            var frames = _selectedState.SelectedFrames;
            _appCommands.DeleteFrames(frames.Count > 0 ? frames : new() { frameToDel });
        }
        else if (selectedVm.Data is AARectSave rectToDel)
        {
            var frame   = _selectedState.SelectedFrame!;
            var rects   = _selectedState.SelectedRectangles;
            var circles = _selectedState.SelectedCircles;
            ShowDeleteShapeConfirm(
                frame,
                rects.Count > 0 ? rects : new() { rectToDel },
                circles);
        }
        else if (selectedVm.Data is CircleSave circleToDel)
        {
            var frame   = _selectedState.SelectedFrame!;
            var circles = _selectedState.SelectedCircles;
            var rects   = _selectedState.SelectedRectangles;
            ShowDeleteShapeConfirm(
                frame,
                rects,
                circles.Count > 0 ? circles : new() { circleToDel });
        }
    }

    private async void ShowFrameDeletedToast(string label)
    {
        _toastCts?.Cancel();
        _toastCts = new System.Threading.CancellationTokenSource();
        System.Threading.CancellationToken token = _toastCts.Token;

        FrameDeletedToastLabel.Text = $"\"{label}\" deleted";
        FrameDeletedToastPanel.IsVisible = true;

        try
        {
            await System.Threading.Tasks.Task.Delay(4000, token);
            FrameDeletedToastPanel.IsVisible = false;
        }
        catch (System.Threading.Tasks.TaskCanceledException) { }
    }

    private void ShowDeleteChainConfirm(AnimationChainSave chain) =>
        ShowDeleteChainConfirm(new List<AnimationChainSave> { chain });

    private async void ShowDeleteChainConfirm(List<AnimationChainSave> chains)
    {
        string msg = chains.Count == 1
            ? $"Delete animation \"{chains[0].Name}\"? It has {chains[0].Frames.Count} frame(s)."
            : $"Delete {chains.Count} animations?";
        var tcs = new TaskCompletionSource<bool>();
        var dialog = BuildDeleteConfirmDialog(msg, "Delete Animation", tcs);
        await dialog.ShowDialog(this);
        if (await tcs.Task)
            _appCommands.DeleteAnimationChains(chains);
    }

    private async void ShowDeleteShapeConfirm(AnimationFrameSave frame, List<AARectSave> rects, List<CircleSave> circles)
    {
        int total = rects.Count + circles.Count;
        string name = rects.Count > 0 ? rects[0].Name : circles[0].Name;
        string msg = total == 1
            ? $"Delete shape \"{name}\"?"
            : $"Delete {total} shape(s)?";
        var tcs = new TaskCompletionSource<bool>();
        var dialog = BuildDeleteConfirmDialog(msg, "Delete Shape", tcs);
        await dialog.ShowDialog(this);
        if (await tcs.Task)
            _appCommands.DeleteShapes(frame, rects, circles);
    }

    /// <summary>Test hook — invokes <see cref="HandleDelete"/> as if the Delete key were pressed.</summary>
    internal void HandleDeleteForTest() => HandleDelete();

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

        // Cancelling zeroes the count; the post-dialog code treats count <= 0 as "do nothing".
        void Cancel() { countInput.Value = 0; dialog.Close(); }

        var ok     = new Button { Content = "OK" };
        var cancel = new Button { Content = "Cancel" };
        ok.Click     += (_, _) => dialog.Close();
        cancel.Click += (_, _) => Cancel();

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

        WireDialogKeyboard(dialog, onConfirm: dialog.Close, onCancel: Cancel);

        await dialog.ShowDialog(this);

        int count = (int)(countInput.Value ?? 0);
        if (count <= 0) return;

        bool exceededBounds = _appCommands.AddMultipleFrames(
            chain, count, incrToggle.IsChecked == true);

        if (exceededBounds)
            ShowStatusMessage("Some frames were clipped — exceeded texture bounds.");

        _appCommands.RefreshTreeNode(chain);
        _events.RaiseAnimationChainsChanged();
        _appCommands.SaveCurrentAnimationChainList();
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
        void Confirm() { confirmed = true; dialog.Close(); }

        var ok = new Button { Content = "OK" };
        ok.Click += (_, _) => Confirm();
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

        WireDialogKeyboard(dialog, onConfirm: Confirm, onCancel: dialog.Close);

        await dialog.ShowDialog(this);
        if (!confirmed) return;

        var (bmpW, bmpH) = WireframeCtrl.BitmapSize;

        if (justifyBottomRb.IsChecked == true)
        {
            _appCommands.AdjustOffsetsJustifyBottom(chain, frame =>
            {
                if (bmpH > 0 && !string.IsNullOrEmpty(frame.TextureName))
                    return (float)bmpH;
                return null;
            });
        }
        else
        {
            _appCommands.AdjustOffsetsAdjustAll(chain,
                (float)(relXInput.Value ?? 0),
                (float)(relYInput.Value ?? 0),
                relative: relativeRb.IsChecked == true);
        }

        _appCommands.RefreshAnimationFrameDisplay();
        _appCommands.SaveCurrentAnimationChainList();
        _events.RaiseAnimationChainsChanged();
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

    /// <summary>
    /// Creates the shell <see cref="Window"/> for the Adjust Frame Time dialog.
    /// The height is left unset so <see cref="SizeToContent.Height"/> can size
    /// the window to fit whichever radio-option layout is currently shown.
    /// </summary>
    public static Window BuildAdjustFrameTimeWindow() => new Window
    {
        Title  = "Adjust All Frame Time",
        Width  = 360,
        SizeToContent = SizeToContent.Height,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        CanResize = false
    };

    // ── Resize Texture ────────────────────────────────────────────────────────

    private async Task DoResizeTextureAsync()
    {
        var frame = _selectedState.SelectedFrame;
        if (frame is null || string.IsNullOrEmpty(frame.TextureName))
        {
            ShowStatusMessage("Select a frame with a texture before resizing.", isError: true);
            return;
        }

        string? achxDir = null;
        if (!string.IsNullOrEmpty(_projectManager.FileName))
            achxDir = (Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty);

        var absTexPath = achxDir is not null
            ? Path.GetFullPath(Path.Combine(achxDir, frame.TextureName))
            : frame.TextureName;

        if (!File.Exists(absTexPath))
        {
            ShowStatusMessage($"⚠ Texture file not found: {absTexPath}", isError: true);
            return;
        }

        // Read current dimensions
        int oldW, oldH;
        using (var bmp = SKBitmap.Decode(absTexPath))
        {
            if (bmp is null)
            {
                ShowStatusMessage("⚠ Could not read texture file.", isError: true);
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
        void Confirm() { confirmed = true; dialog.Close(); }

        var ok     = new Button { Content = "OK" };
        var cancel = new Button { Content = "Cancel" };
        ok.Click     += (_, _) => Confirm();
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

        WireDialogKeyboard(dialog, onConfirm: Confirm, onCancel: dialog.Close);

        await dialog.ShowDialog(this);
        if (!confirmed) return;

        int newW = (int)(wInput.Value ?? oldW);
        int newH = (int)(hInput.Value ?? oldH);

        if (newW == oldW && newH == oldH)
        {
            ShowStatusMessage("New size is the same as current — no changes made.");
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
        var acls = _projectManager.AnimationChainListSave;
        if (acls is not null)
        {
            var modifiedFrames = AnimationEditor.Core.IO.TextureResizeAdjuster.AdjustAll(
                acls, achxDir ?? "", absTexPath, oldW, oldH, newW, newH);

            // Re-reference all modified frames to the new texture file
            string newRelPath = achxDir is not null
                ? Path.GetRelativePath(achxDir, newAbsPath).Replace('\\', '/')
                : newAbsPath;

            foreach (var f in modifiedFrames)
                f.TextureName = newRelPath;
        }

        RefreshTreeView();
        _appCommands.RefreshWireframe();
        RefreshTextureCombo();
        _appCommands.SaveCurrentAnimationChainList();
        _events.RaiseAnimationChainsChanged();

        ShowStatusMessage($"Texture resized and saved to: {newAbsPath}");
    }

    // ── Inline rename helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Double-tap on the text label of a tree node. Marks the event handled so it does
    /// not bubble to <see cref="OnAnimTreeDoubleTapped"/>, then routes the gesture
    /// through <see cref="HandleHeaderTextDoubleTap"/>.
    /// </summary>
    private void OnHeaderTextDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not Control src) return;
        var tvi = src.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (tvi?.DataContext is not TreeNodeVm vm) return;
        e.Handled = true;
        HandleHeaderTextDoubleTap(vm);
    }

    /// <summary>
    /// Routes a double-tap on a tree node's text <em>label</em>. A chain inline-renames
    /// (its name is meaningful and used to look the chain up); every other node type —
    /// frame, rect, circle — routes to <see cref="HandleAnimTreeNodeDoubleTap"/>, so a
    /// frame centers the wireframe on itself. <see cref="AnimationFrameSave.Name"/> is
    /// only a tree display label and is not referenced anywhere else, so the more useful
    /// center-on-frame gesture wins the text-label real estate over an inline rename.
    /// </summary>
    internal void HandleHeaderTextDoubleTap(TreeNodeVm vm)
        => HandleAnimTreeNodeDoubleTap(vm);

    /// <summary>
    /// Double-tap on blank space in a tree row (not the text label, not a Button) →
    /// toggle expand / collapse.
    /// </summary>
    private void OnAnimTreeDoubleTapped(object? sender, TappedEventArgs e)
    {
        // If the TextBlock's DoubleTapped handler already handled the event (inline rename),
        // or if the + button's DoubleTapped handler consumed it, skip.
        if (e.Handled) return;
        if (e.Source is not Control src) return;
        if (src is TextBlock) return;
        // Belt-and-suspenders: exclude clicks that originated from inside a Button even if
        // the Button's DoubleTapped handler didn't fire (e.g. focus or routing edge cases).
        // The event source is often a visual child (ContentPresenter, SVG icon, etc.),
        // not the Button itself, so a simple `is Button` check is insufficient.
        if (src.FindAncestorOfType<Button>(includeSelf: true) is not null) return;
        var tvi = src.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (tvi?.DataContext is not TreeNodeVm vm) return;
        if (!HandleAnimTreeNodeDoubleTap(vm)) return;
        e.Handled = true;
    }

    /// <summary>
    /// Routes a double-tap on a tree node to the appropriate action.
    /// Returns <c>true</c> when a recognised action was performed.
    /// </summary>
    internal bool HandleAnimTreeNodeDoubleTap(TreeNodeVm vm)
    {
        switch (vm.Data)
        {
            case AnimationChainSave chain:
                BeginInlineRename(vm, chain.Name);
                return true;
            case AnimationFrameSave frame:
                WireframeCtrl.CenterOnFrame(frame);
                // Bring the sprite into view in the preview too — center on the frame's
                // offset, not the entity origin, or a large-offset frame stays off-screen.
                PreviewCtrl.CenterOnEntityPoint(frame.RelativeX, frame.RelativeY);
                return true;
            case AARectSave rect:
                PreviewCtrl.CenterOnEntityPoint(rect.X, rect.Y);
                return true;
            case CircleSave circle:
                PreviewCtrl.CenterOnEntityPoint(circle.X, circle.Y);
                return true;
            default:
                return false;
        }
    }

    private void OnInlineRenameKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is not TextBox tb) return;
        if (tb.DataContext is not TreeNodeVm vm) return;

        if (e.Key == Key.Return)
        {
            e.Handled = true;
            CommitInlineRename(vm, tb.Text ?? string.Empty);
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
        CommitInlineRename(vm, tb.Text ?? string.Empty);
    }

    private void BeginInlineRename(TreeNodeVm vm, string initialText)
    {
        vm.EditingText = initialText;
        vm.IsEditing = true;
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
        BeginInlineRename(vm, chain.Name);
    }

    private void CommitInlineRename(TreeNodeVm vm, string newName)
    {
        newName = newName.Trim();
        vm.IsEditing = false;

        if (vm.Data is AnimationChainSave chain)
        {
            if (string.IsNullOrEmpty(newName))
            {
                ShowStatusMessage("Chain name cannot be empty.", isError: true);
            }
            else if (newName != chain.Name)
            {
                _appCommands.RenameChain(chain, newName);
            }
        }
        else if (vm.Data is AnimationFrameSave frame)
        {
            if (!string.IsNullOrEmpty(newName))
                _appCommands.RenameFrame(frame, newName);
        }

        AnimTree.Focus();
    }

    internal void CommitInlineRenamePublic(TreeNodeVm vm, string newName) =>
        CommitInlineRename(vm, newName);

    internal IReadOnlyList<TreeNodeVm> GetTreeRoots() => _treeRoots;

    // ── View Texture in Explorer ──────────────────────────────────────────────

    private void ViewTextureInExplorer(AnimationFrameSave frame)
    {
        if (string.IsNullOrEmpty(frame.TextureName))
        {
            ShowStatusMessage("This frame has no texture path set.", isError: true);
            return;
        }

        string? achxDir = null;
        if (!string.IsNullOrEmpty(_projectManager.FileName))
            achxDir = (Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty);

        var absPath = achxDir is not null
            ? Path.GetFullPath(Path.Combine(achxDir, frame.TextureName))
            : frame.TextureName;

        if (!File.Exists(absPath))
        {
            ShowStatusMessage($"⚠ Texture file not found: {absPath}", isError: true);
            return;
        }

        try
        {
            Process.Start("explorer.exe", $"/select,\"{absPath}\"");
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"⚠ Could not open Explorer: {ex.Message}", isError: true);
        }
    }
}

