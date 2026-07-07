using AnimationEditor.App.Services;
using AnimationEditor.App.Theming;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Data;
using AnimationEditor.Core.Diff;
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
    private readonly IPendingCutState _pendingCutState;
    private readonly Services.ThumbnailService _thumbnailService;
    private readonly IFileAssociationService _fileAssociation;
    private readonly PngFolderWatcher _pngFolderWatcher = new();

    private AppSettingsModel _appSettings = new();
    private readonly TabManager _tabManager = new();

    // ── Tab drag state ────────────────────────────────────────────────────────
    private TabEntry? _dragTab;
    private double _dragStartX;
    private bool _isDragging;
    private Border? _ghostBorder;

    // ── Frame drag-and-drop reorder state (issue #500) ──────────────────────────
    // The DataTransfer only carries a marker token; the actual frames + source chain
    // live in _pendingFrameDrag because the drag is always same-process.
    private static readonly DataFormat<string> FrameDragDataFormat =
        DataFormat.CreateStringApplicationFormat("animationeditor-frame-drag");
    private const string FrameDragToken = "frame";
    private FrameDragSource? _pendingFrameDrag;
    private Avalonia.Point? _frameDragPressPoint;
    private PointerPressedEventArgs? _frameDragPressArgs;
    private AnimationFrameSave? _frameDragCandidate;
    private List<object>? _frameDragSelectionSnapshot;
    private AnimationFrameSave? _pendingSingleSelectFrame;
    private bool _frameDragInProgress;
    private Border? _frameDropLine;
    private Border? _frameDropBox;

    // ── Chain drag-and-drop reorder state (issue #566) ──────────────────────────
    // Mirrors the frame path above, but for top-level animation-chain nodes. The
    // dragged chain(s) live in _pendingChainDrag since the drag is always same-process.
    private static readonly DataFormat<string> ChainDragDataFormat =
        DataFormat.CreateStringApplicationFormat("animationeditor-chain-drag");
    private const string ChainDragToken = "chain";
    private ChainDragSource? _pendingChainDrag;
    private AnimationChainSave? _chainDragCandidate;
    private Avalonia.Point? _chainDragPressPoint;
    private PointerPressedEventArgs? _chainDragPressArgs;
    private List<object>? _chainDragSelectionSnapshot;
    private AnimationChainSave? _pendingSingleSelectChain;
    private bool _chainDragInProgress;
    private int _untitledCounter;
    private bool _suppressPropRefresh;
    private bool _suppressTextureComboChanged;
    private bool _suppressPreviewScrollSync;
    private bool _suppressWireframeScrollSync;
    private bool _suppressPngScrollSync;

    // ── PNG Diff (#606) ─────────────────────────────────────────────────
    private readonly Services.PngBlameService _blameService = new();
    // Pixel-diff tolerance = 0: any inequality is a change. PNG is lossless, so a differing pixel
    // genuinely differs — there's no format noise to absorb, and a fuzz threshold would only hide real
    // changes. The one exposed control (the Grouping slider) tunes region merge distance, which
    // regroups boxes but never hides a change.
    private const int DiffTolerance = 0;
    // Debounces the Grouping slider so dragging re-merges once the user pauses, not per pixel-tick.
    private DispatcherTimer? _diffSliderDebounce;

    private List<AnimationChainSave>? _pendingPastedChains;
    private List<bool>? _pendingPastedChainExpand;

    private ScrollViewer? GetAnimTreeScrollViewer() =>
        AnimTree.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();

    private void WithPreservedAnimTreeScroll(Action action)
    {
        var scroll = GetAnimTreeScrollViewer();
        var offset = scroll?.Offset ?? default;
        action();
        if (scroll is not null)
            scroll.Offset = offset;
    }

    private void QueuePastedChainExpandFromSources(
        IReadOnlyList<AnimationChainSave> sources,
        IReadOnlyList<AnimationChainSave> pasted)
    {
        _pendingPastedChains = pasted.ToList();
        _pendingPastedChainExpand = TreeBuilder
            .ExpandStatesForChainNames(_treeRoots, sources.Select(c => c.Name).ToList())
            .ToList();
    }

    private void ApplyPendingPastedChainExpand()
    {
        if (_pendingPastedChains is null || _pendingPastedChainExpand is null)
            return;
        TreeBuilder.ApplyExpandStates(_treeRoots, _pendingPastedChains, _pendingPastedChainExpand);
        _pendingPastedChains = null;
        _pendingPastedChainExpand = null;
    }

    private bool _suppressTreeSelectionHandling;
    private bool _suppressCompanionSave;
    private bool _suppressInterpolateSync;
    private readonly AltMenuActivationSuppressor _altMenuActivationSuppressor = new();
    private System.Threading.CancellationTokenSource? _toastCts;

    // The platform application-data root under which settings live. Injected (not read from
    // Environment here) so headless tests can redirect it to a temp dir and never touch the
    // developer's real %APPDATA%\AnimationEditor\AESettings.json (see issue #438).
    private readonly string _applicationDataRoot;

    // Shared per-user config dir, NOT the build output — so recent files / tabs / theme survive
    // rebuilds, dotnet clean, and switching git worktrees (see issue #424). AppContext.BaseDirectory
    // resolves to bin/<Config>/<TFM>/, which is per-build / per-checkout.
    private FilePath SettingsFilePath =>
        AppSettingsLocation.ForApplicationDataRoot(_applicationDataRoot);

    public MainWindow(
        IProjectManager projectManager,
        ISelectedState selectedState,
        IAppCommands appCommands,
        IAppState appState,
        IApplicationEvents events,
        IIoManager ioManager,
        IObjectFinder objectFinder,
        IUndoManager undoManager,
        IPendingCutState pendingCutState,
        Services.ThumbnailService thumbnailService,
        IFileAssociationService fileAssociation,
        string applicationDataRoot)
    {
        _applicationDataRoot = applicationDataRoot;

        _projectManager = projectManager;
        _selectedState = selectedState;
        _appCommands = appCommands;
        _appState = appState;
        _events = events;
        _ioManager = ioManager;
        _objectFinder = objectFinder;
        _undoManager = undoManager;
        _pendingCutState = pendingCutState;
        _thumbnailService = thumbnailService;
        _fileAssociation = fileAssociation;

        InitializeComponent();

        if (OperatingSystem.IsMacOS())
            ApplyMacOSWindowChrome();

        InitToast();
        InitErrorBanner();
        PropertyChanged += (_, e) => { if (e.Property == OffScreenMarginProperty) Padding = OffScreenMargin; };

        WireAppCommands();
        LoadSettingsFile();
        ApplyPersistedTheme();
        ApplyPersistedCanvasColors();
        WireMenuEvents();
        WireWireframeToolbar();
        WireWireframeControl();
        WirePngViewport();
        WirePngBlame();
        WirePreviewControls();
        WireTreeView();
        WireWindowFileDrop();
        WirePropertyPanel();
        WirePlaybackControls();
        WireTimelineTransport();
        WireKeyboard();
        WireTabBar();
        WireDefaultHandlerBanner();

        WireframeCtrl.InitializeServices(_selectedState, _appState, _appCommands, _events, _projectManager, _undoManager, _pendingCutState, msg => ShowStatusMessage(msg, isError: true));
        PreviewCtrl.InitializeServices(_selectedState, _appState, _appCommands, _events, _projectManager, _undoManager, _thumbnailService, _pendingCutState, msg => ShowStatusMessage(msg, isError: true));
        FilesPanel.Initialize(_thumbnailService, this,
            msg => ShowStatusMessage(msg, isError: true), OpenPngAsTab);
        // On scope toggle, re-supply the current referenced-texture set so "This File" reflects
        // the live .achx instead of the snapshot cached at the last refresh.
        FilesPanel.ScopeChanged += (_, _) => RefreshFilesPanel();
        _pngFolderWatcher.FolderContentsChanged += changed =>
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                // Evict the changed PNGs so the rebuild re-decodes them (and re-renders their
                // cached Files-panel thumbnails) instead of serving the stale pre-change image.
                foreach (var path in changed)
                    _thumbnailService.InvalidatePath(path);
                RefreshFilesPanel();
            });

        Opened += OnOpened;
        Closed += (_, _) =>
        {
            SaveTabsToSettings();
            _appCommands.HotReloadWatcher.Dispose();
            PreviewCtrl.Playback.FrameIndexChanged -= OnPreviewPlaybackFrameIndexChanged;
            PreviewCtrl.IsPlayingChanged -= UpdatePlayPauseIcon;
            // The thumbnail service owns every cached chain/timeline icon; disposing it here
            // releases them all (and the decoded source sheets) as the window tears down.
            _thumbnailService.Dispose();
            _pngFolderWatcher.Dispose();
        };
    }

    // ── Tab bar ───────────────────────────────────────────────────────────────

    private void WireTabBar()
    {
        _tabManager.ActiveChanged += _ => Dispatcher.UIThread.InvokeAsync(RebuildTabStrip);
        // Persist the open-tab session on every change (open / switch / close / reorder), not just
        // on graceful window close — a debugger Stop or crash never fires Closed and would otherwise
        // lose the session (issue #439). Called synchronously so the write lands before any kill;
        // SaveTabsToSettings only reads tab state and writes a file, touching no UI controls.
        _tabManager.TabsChanged += SaveTabsToSettings;
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

            // Attach a single managed ContextMenu (right-click → Detach / Close).  Letting Avalonia
            // own the menu via the ContextMenu property — rather than constructing and Open()-ing a
            // fresh ContextMenu on every right-click PointerPressed — is what prevents the menus from
            // stacking and failing to dismiss (issue #472): Avalonia reuses this one instance and
            // light-dismisses it on the next click.
            var tabMenu = new ContextMenu();
            // Detaching re-opens the file in a fresh editor window; that path only knows how to
            // load achx, so PNG viewer tabs omit it (issue #604).
            if (captured.Kind == TabKind.Achx)
                tabMenu.Items.Add(new MenuItem
                {
                    Header = "Detach to New Window",
                    Command = new RelayCommand(() => DetachTab(captured)),
                });
            tabMenu.Items.Add(new MenuItem
            {
                Header = "Close Tab",
                Command = new RelayCommand(() => CloseTab(captured)),
            });
            tabBorder.ContextMenu = tabMenu;

            // Pointer handling: immediate pointer-capture on press (so PointerMoved fires even
            // when the cursor moves over other tabs).  Activation is deferred to PointerReleased
            // so that RebuildTabStrip is never called while a drag is in-flight.
            // Close-button presses are excluded from capture by checking args.Source.
            tabBorder.PointerPressed += (_, args) =>
            {
                var pt = args.GetCurrentPoint(tabBorder);
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

    // ── PNG viewer tabs (issue #604) ────────────────────────────────────────────

    /// <summary>
    /// Opens <paramref name="path"/> (a .png) as a viewer tab, or focuses it if already open.
    /// Called from the Files panel on double-click. Mirrors the achx open flow's handling of the
    /// leaving tab, but shows the PNG pane instead of loading an animation model.
    /// </summary>
    public void OpenPngAsTab(string path)
    {
        if (string.IsNullOrEmpty(path)) return;

        // Preserve the leaving achx tab's edit state before the view swaps away from the editor.
        var leavingTab = _tabManager.ActiveTab;
        if (leavingTab is { Kind: TabKind.Achx })
        {
            leavingTab.UndoSnapshot = _undoManager.TakeSnapshot();
            _appCommands.CaptureTabEditorState(leavingTab);
            SaveCompanionFile();
        }

        // Keep the currently-open achx editor content as a background tab, same as File > Open.
        EnsureCurrentEditorContentHasTab();

        _tabManager.OpenOrFocus(new FilePath(path));
        ShowPngPane(_tabManager.ActiveTab!);
        RebuildTabStrip();
    }

    // Latest-wins guard + in-flight handle for the async PNG decode, mirroring the history load.
    private int _pngTextureLoadId;
    private Task _pngTextureLoadInFlight = Task.CompletedTask;

    // Lets tests await both background operations a PNG tab kicks off (history load + texture decode)
    // before deleting the temp dir they read from — otherwise cleanup races the live git/file handles.
    internal Task WhenPngTabLoaded() => Task.WhenAll(_blameLoadInFlight, _pngTextureLoadInFlight);

    /// <summary>Swaps the main pane to the PNG viewer and loads <paramref name="tab"/>'s image.</summary>
    private void ShowPngPane(TabEntry tab)
    {
        AchxEditorPane.IsVisible = false;
        PngPaneGrid.IsVisible = true;
        PngPane.IsVisible = true;
        SetSidebarForPng(true);
        // Decode the image off the UI thread so the tab appears immediately even for a large sheet;
        // a "Loading…" overlay shows until it's ready. The history load is likewise async.
        _pngTextureLoadInFlight = LoadPngTextureAsync(tab.Path.FullPath);
        LoadBlameForPng(tab.Path.FullPath);
    }

    private async Task LoadPngTextureAsync(string absolutePath)
    {
        int id = ++_pngTextureLoadId;
        PngLoadingText.IsVisible = true;
        try
        {
            await PngPane.LoadTextureAsync(absolutePath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PngTab] texture load failed: {ex}");
        }
        finally
        {
            // Only the newest load clears the indicator, so a superseded load can't hide it early.
            if (id == _pngTextureLoadId)
                PngLoadingText.IsVisible = false;
        }
    }

    /// <summary>Swaps back to the achx editor pane and releases any previewed image. No-op if already showing it.</summary>
    private void ShowAchxPane()
    {
        // Restore the editing sidebar unconditionally — it must come back whenever we intend to show
        // the achx editor, including on the paths below that short-circuit before touching the panes.
        SetSidebarForPng(false);
        if (!PngPane.IsVisible) return;
        PngPane.LoadTexture(null);   // clear the previewed texture (Pass null to unload)
        PngPane.IsVisible = false;
        PngPaneGrid.IsVisible = false;
        AchxEditorPane.IsVisible = true;
    }

    // Sidebar state saved when collapsing for a PNG tab, so restoring preserves the user's splitter
    // position rather than snapping back to the XAML default ("2*,4,3*").
    private bool _sidebarCollapsedForPng;
    private GridLength _savedTreeRowHeight = new(2, GridUnitType.Star);
    private GridLength _savedSplitterRowHeight = new(4, GridUnitType.Pixel);

    /// <summary>
    /// Collapses the animation-editing sidebar surfaces for a read-only PNG-preview tab (issue #604)
    /// and restores them for the achx editor. A PNG has no animations, no editable frame/shape
    /// properties, and no undo history — and for now we don't offer file navigation from a PNG either
    /// — so the ANIMATIONS tree and the Inspector, History, and Files tabs are all hidden; only the
    /// Diff tab (#606) remains, and it becomes the selected tab.
    /// </summary>
    private void SetSidebarForPng(bool png)
    {
        if (png == _sidebarCollapsedForPng) return;
        var rows = LeftPanelGrid.RowDefinitions;
        if (png)
        {
            _savedTreeRowHeight = rows[0].Height;
            _savedSplitterRowHeight = rows[1].Height;
            rows[0].Height = new GridLength(0);
            rows[1].Height = new GridLength(0);
            AnimationsBlock.IsVisible = false;
            SidebarSplitter.IsVisible = false;
            InspectorTab.IsVisible = false;
            HistoryTab.IsVisible = false;
            FilesTab.IsVisible = false;
            // Diff is the only PNG surface; select it before hiding Files so the strip never shows
            // a hidden selected tab (blank content).
            DiffBlameTab.IsVisible = true;
            SidebarTabs.SelectedItem = DiffBlameTab;
        }
        else
        {
            rows[0].Height = _savedTreeRowHeight;
            rows[1].Height = _savedSplitterRowHeight;
            AnimationsBlock.IsVisible = true;
            SidebarSplitter.IsVisible = true;
            InspectorTab.IsVisible = true;
            HistoryTab.IsVisible = true;
            FilesTab.IsVisible = true;
            // A now-hidden PNG tab can't stay selected or the strip would show blank content — fall
            // back to the Inspector, the achx editor's default surface.
            if (ReferenceEquals(SidebarTabs.SelectedItem, DiffBlameTab))
                SidebarTabs.SelectedItem = InspectorTab;
            DiffBlameTab.IsVisible = false;
        }
        _sidebarCollapsedForPng = png;
    }

    private async Task ActivateTabAsync(TabEntry tab)
    {
        if (tab == _tabManager.ActiveTab) return;

        ClearPendingCut();

        // Save the leaving tab's undo history and in-memory model before the editor switches.
        // PNG tabs carry no model or undo stack, so only capture when leaving the achx editor.
        var leavingTab = _tabManager.ActiveTab;
        if (leavingTab is { Kind: TabKind.Achx })
        {
            leavingTab.UndoSnapshot = _undoManager.TakeSnapshot();
            _appCommands.CaptureTabEditorState(leavingTab);
        }

        // PNG tabs bypass the animation-editor machinery entirely.
        if (tab.Kind == TabKind.Png)
        {
            if (leavingTab is { Kind: TabKind.Achx })
                SaveCompanionFile();
            _tabManager.Activate(tab.Path);
            ShowPngPane(tab);
            RebuildTabStrip();
            return;
        }

        ShowAchxPane();
        SaveCompanionFile();
        _tabManager.Activate(tab.Path);
        // Bypass LoadAnimationFileAsync so we don't hit the short-circuit that skips
        // the file load when the path is already the active tab.
        if (IsUntitledTab(tab))
        {
            ActivateUntitledTabContent(tab);
        }
        else
        {
            await _appCommands.ActivateTabContentAsync(tab);
            // LoadAnimationChain cleared the stack — restore this tab's saved history.
            if (tab.UndoSnapshot != null)
                _undoManager.RestoreSnapshot(tab.UndoSnapshot);
        }
        RebuildTabStrip();
    }

    private void ActivateUntitledTabContent(TabEntry tab)
    {
        ClearPendingCut();
        _projectManager.AnimationChainListSave =
            tab.CachedEditorModel ?? new AnimationChainListSave();
        _projectManager.FileName = null;
        _selectedState.Reset();
        _undoManager.Clear();
        if (tab.UndoSnapshot != null)
            _undoManager.RestoreSnapshot(tab.UndoSnapshot);
        RefreshTreeView();
        UpdateTitle();
        UpdateStatusBar();
    }

    private void SyncTabCacheFromEditor(string? path)
    {
        TabEntry? tab = path != null
            ? _tabManager.Tabs.FirstOrDefault(t => t.Path == new FilePath(path))
            : _tabManager.ActiveTab;
        if (tab != null)
            _appCommands.CaptureTabEditorState(tab);
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
            if (next.Kind == TabKind.Png)
            {
                ShowPngPane(next);
                RebuildTabStrip();
            }
            // Use OpenAchxWorkflowAsync directly — bypasses EnsureCurrentEditorContentHasTab
            // so the just-closed file is not accidentally re-registered as a background tab.
            else if (!IsUntitledTab(next))
            {
                ShowAchxPane();
                _ = ActivateTabAfterCloseAsync(next);
            }
            else
            {
                ShowAchxPane();
                ActivateUntitledTabContent(next);
                RebuildTabStrip();
            }
        }
        else
        {
            // All tabs closed — start fresh
            ShowAchxPane();
            _projectManager.AnimationChainListSave = new AnimationChainListSave();
            _projectManager.FileName = null;
            _selectedState.Reset();
            _undoManager.Clear();
            RefreshTreeView();
            RefreshFilesPanel();
            UpdateTitle();
            UpdateStatusBar();
        }
    }

    private async Task ActivateTabAfterCloseAsync(TabEntry tab)
    {
        await _appCommands.ActivateTabContentAsync(tab);
        if (tab.UndoSnapshot != null)
            _undoManager.RestoreSnapshot(tab.UndoSnapshot);
        RebuildTabStrip();
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
            if (active.Kind == TabKind.Png)
                ShowPngPane(active);
            else
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

        RefreshFilesPanel();
        ShowDefaultHandlerBannerIfAppropriate();
    }

    // ── Default-handler prompt banner ─────────────────────────────────────────

    private void WireDefaultHandlerBanner()
    {
        MakeDefaultBtn.Click += (_, _) => RegisterAsDefaultAchxHandler(hideBanner: true);

        DismissDefaultHandlerBtn.Click += (_, _) =>
        {
            _appSettings.SuppressDefaultHandlerPrompt = true;
            SaveSettingsFile();
            DefaultHandlerBanner.IsVisible = false;
        };
    }

    private void RegisterAsDefaultAchxHandler(bool hideBanner)
    {
        _fileAssociation.RegisterAsDefault();
        if (hideBanner)
            DefaultHandlerBanner.IsVisible = false;
        ShowStatusMessage("Opened Windows settings — choose Animation Editor for .achx files.");
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

        _appCommands.EditorProjectModelChanged += path =>
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                ClearPendingCut();
                SyncTabCacheFromEditor(path);
            });

        _pendingCutState.Changed += () =>
            Dispatcher.UIThread.InvokeAsync(SyncPendingCutHighlights);

        // Tree events — fully wired (WireTreeView connects these after tree is constructed)
        _appCommands.RefreshTreeViewRequested           += () => Dispatcher.UIThread.InvokeAsync(RefreshTreeView);
        _appCommands.RebuildTreeViewRequested           += expandedChainNames => Dispatcher.UIThread.InvokeAsync(() => RebuildTreeView(expandedChainNames));
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
            RefreshFilesPanel();

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

        _appCommands.ItemsDeleted += label =>
            Dispatcher.UIThread.InvokeAsync(() => ShowItemDeletedToast(label));

        _appCommands.PixiJsExportCompleted += (path, warnings) =>
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                var name = System.IO.Path.GetFileName(path);
                ShowToast(warnings.Count == 0
                    ? $"Exported {name}"
                    : $"Exported {name} — {string.Join(" ", warnings)}");
            });

        ItemDeletedToastUndoBtn.Click += (_, _) =>
        {
            _toastCts?.Cancel();
            ItemDeletedToastPanel.IsVisible = false;
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
        WireframeZoom.Attach(WireframeCtrl);

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

        // Move can't be toggled off directly — clicking it again while it's already
        // the active mode has no effect, matching a radio group's behavior.
        if (MoveModeToggle.IsChecked != true)
        {
            MoveModeToggle.IsChecked = true;
            return;
        }

        _suppressModeToggle = true;
        MagicWandToggle.IsChecked = false;
        _suppressModeToggle = false;
        WireframeCtrl.IsMagicWandMode = false;
    }

    private void OnMagicWandToggled(object? sender, RoutedEventArgs e)
    {
        if (_suppressModeToggle) return;

        // Toggling Magic Wand off falls back to Move mode rather than leaving both
        // toolbar toggles unchecked with IsMagicWandMode stuck on (issue #575).
        if (MagicWandToggle.IsChecked != true)
        {
            MoveModeToggle.IsChecked = true;
            return;
        }

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

    // ── WireframeControl event wiring ─────────────────────────────────────────

    private void WireWireframeControl()
    {
        WireframeCtrl.FrameRegionChanged     += OnFrameRegionChanged;
        WireframeCtrl.ChainRegionChanged     += OnChainRegionChanged;
        WireframeCtrl.FrameLiveUpdated       += OnFrameLiveUpdated;
        WireframeCtrl.FrameCreatedFromRegion += OnFrameCreatedFromRegion;
        // Same apply path the ANIMATIONS tree's PNG drop uses (issue #560).
        WireframeCtrl.HandlePngDrop          = HandlePngDropAsync;
        // WireframeZoom follows the live zoom itself (ZoomControl.Attach subscribes ZoomChanged);
        // this handler only persists the settled state — once the smooth wheel-zoom (#425) stops
        // animating (IsZoomAnimating == false), not on every frame.
        WireframeCtrl.ZoomChanged            += _ =>
        {
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

    // ── PngPreviewControl scrollbar wiring (#604) ─────────────────────────────

    private void WirePngViewport()
    {
        // Two-way sync between the PNG viewer's manual camera pan and its scrollbars, mirroring
        // the wireframe (#422). No companion-file persistence — a PNG tab carries no editor state.
        PngHScroll.ValueChanged += (_, _) => OnPngScrollValueChanged(horizontal: true);
        PngVScroll.ValueChanged += (_, _) => OnPngScrollValueChanged(horizontal: false);
        PngPane.ViewChanged += RefreshPngScrollBars;
        // The PNG bar's zoom widget both shows the live zoom and drives it (type/step to 1:1 for
        // screenshots), sharing the wireframe/preview implementation.
        PngZoom.Attach(PngPane);
    }

    private void OnPngScrollValueChanged(bool horizontal)
    {
        if (_suppressPngScrollSync) return;
        _suppressPngScrollSync = true;
        if (horizontal)
            PngPane.SetPanX((float)PngHScroll.Value);
        else
            PngPane.SetPanY((float)PngVScroll.Value);
        _suppressPngScrollSync = false;
    }

    /// <summary>
    /// Pushes the PNG viewer's current pan/zoom/texture size into its two scrollbars. Fired by
    /// <see cref="TextureViewport.ViewChanged"/>. The suppression flag stops the resulting
    /// <c>ValueChanged</c> from looping back into the pan.
    /// </summary>
    private void RefreshPngScrollBars()
    {
        if (_suppressPngScrollSync) return;
        _suppressPngScrollSync = true;
        var (h, v) = PngPane.GetScrollBarRanges();
        ApplyScrollRange(PngHScroll, h);
        ApplyScrollRange(PngVScroll, v);
        _suppressPngScrollSync = false;
    }

    // ── PNG Diff wiring (#606) ──────────────────────────────────────────

    private void WirePngBlame()
    {
        // A revision select frames + reveals the change; a slider drag just re-merges in place.
        RevisionList.SelectionChanged += (_, _) => UpdateDiffOverlay(frame: true);
        // Re-tapping the already-selected revision doesn't fire SelectionChanged, so replay the reveal
        // here. A tap that *changes* selection also lands here, but its SelectionChanged already bumped
        // the latest-wins token, so this second call's compute is discarded before it paints — no
        // double reveal. handledEventsToo: selection marks the press handled, but the tap still routes.
        RevisionList.AddHandler(InputElement.TappedEvent, OnRevisionTapped, handledEventsToo: true);
        DiffGroupingSlider.PropertyChanged += OnDiffSliderPropertyChanged;
    }

    private void OnRevisionTapped(object? sender, TappedEventArgs e)
    {
        // Only when the tap landed on the currently-selected row (not empty list space).
        if (e.Source is Avalonia.Visual v &&
            v.FindAncestorOfType<ListBoxItem>() is { DataContext: Models.RevisionEntryVm vm } &&
            ReferenceEquals(RevisionList.SelectedItem, vm))
        {
            UpdateDiffOverlay(frame: true);
        }
    }

    private void OnDiffSliderPropertyChanged(object? sender, Avalonia.AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty) return;

        // Reflect the value immediately; debounce the (potentially expensive) re-diff so a drag
        // recomputes once the user settles rather than on every intermediate value.
        DiffGroupingValue.Text = ((int)DiffGroupingSlider.Value).ToString();

        _diffSliderDebounce ??= CreateDiffSliderDebounceTimer();
        _diffSliderDebounce.Stop();
        _diffSliderDebounce.Start();
    }

    private DispatcherTimer CreateDiffSliderDebounceTimer()
    {
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(120) };
        timer.Tick += (_, _) => { timer.Stop(); UpdateDiffOverlay(frame: false); };
        return timer;
    }

    /// <summary>
    /// Loads <paramref name="absolutePath"/>'s git history into the Diff revision list, or
    /// shows a fallback message (not a repo / untracked / LFS / git missing). Clears any prior overlay.
    /// </summary>
    // True while PngPane is displaying a historical revision's image rather than the current on-disk
    // file, so a later deselect knows to restore the current image.
    private bool _pngShowingRevision;

    // Latest-wins guard: only the newest requested history load populates the list, so a slow load
    // for a tab the user already left never clobbers the current one. Read/written on the UI thread.
    private int _blameLoadId;

    // The most recent history load, awaited by the next one so the loads run in issue order — that
    // keeps the service's state on the most-recently-requested file even under fast tab switches.
    private Task _blameLoadInFlight = Task.CompletedTask;

    /// <summary>
    /// Starts loading <paramref name="absolutePath"/>'s git history into the revision list. The git
    /// calls (dominated by <c>git log --follow</c>) run off the UI thread so switching to a PNG tab
    /// doesn't freeze the app; the list fills in when the load completes. Fire-and-forget by design.
    /// </summary>
    private async void LoadBlameForPng(string absolutePath)
    {
        // The tab just (re)loaded the current on-disk image into PngPane, so we're no longer showing a
        // historical revision.
        _pngShowingRevision = false;

        int loadId = ++_blameLoadId;

        // Clear the previous file's revisions immediately and show a transient loading state so the
        // stale list never lingers while the (potentially slow) git log runs.
        RevisionList.ItemsSource = null;
        RevisionList.SelectedItem = null;
        PngPane.SetDiffRegions(Array.Empty<PixelRegion>(), frame: false);
        DiffBlameStatus.Text = "Loading history…";
        DiffBlameStatus.IsVisible = true;

        var previous = _blameLoadInFlight;
        _blameLoadInFlight = LoadBlameCoreAsync(absolutePath, loadId, previous);
        await _blameLoadInFlight;
    }

    private async Task LoadBlameCoreAsync(string absolutePath, int loadId, Task previous)
    {
        // Serialize behind any in-flight load so the service's state ends on the most-recently-
        // requested file; a prior load's failure must not block this one.
        try { await previous; } catch { }

        PngBlameResult result;
        try
        {
            result = await Task.Run(() => _blameService.Load(absolutePath));
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PngBlame] history load failed: {ex}");
            if (loadId == _blameLoadId)
            {
                DiffBlameStatus.Text = "History could not be loaded.";
                DiffBlameStatus.IsVisible = true;
            }
            return;
        }

        // Only the newest requested load populates the list (latest-wins).
        if (loadId == _blameLoadId)
            PopulateRevisionList(result);
    }

    private void PopulateRevisionList(PngBlameResult result)
    {
        var rows = new List<Models.RevisionEntryVm>(result.Entries.Count);
        for (int i = 0; i < result.Entries.Count; i++)
        {
            var rev = result.Entries[i];
            string meta = rev.IsWorkingTree ? "uncommitted" : $"{rev.ShortHash} · {rev.Date:yyyy-MM-dd}";
            rows.Add(new Models.RevisionEntryVm(i, rev.Subject, meta));
        }

        RevisionList.ItemsSource = rows;
        RevisionList.SelectedItem = null;
        PngPane.SetDiffRegions(Array.Empty<PixelRegion>(), frame: false);
        ShowDiffBlameStatus(result.Status, rows.Count);
    }

    private void ShowDiffBlameStatus(Core.Git.GitHistoryStatus status, int revisionCount)
    {
        string? message = status switch
        {
            Core.Git.GitHistoryStatus.Ok when revisionCount == 0 => "No git history was found for this file.",
            Core.Git.GitHistoryStatus.Ok => null,
            Core.Git.GitHistoryStatus.NotARepository =>
                "This file isn't inside a git repository, so there's no history to compare.",
            Core.Git.GitHistoryStatus.Untracked =>
                "This file isn't committed to git yet — commit it to compare revisions.",
            Core.Git.GitHistoryStatus.LfsPointer =>
                "This file is stored with Git LFS; committed versions are pointers, not images, so pixel diffing isn't available.",
            Core.Git.GitHistoryStatus.GitUnavailable =>
                "git isn't available on your PATH, so history can't be loaded.",
            _ => null,
        };

        DiffBlameStatus.Text = message ?? "";
        DiffBlameStatus.IsVisible = message is not null;
    }

    // Bumped per diff request; a completed background compute only paints if it's still the latest,
    // so rapid revision clicks / slider drags don't stack or paint stale boxes.
    private int _diffRequestId;

    // Computes and shows the changed-region boxes for the selected revision at the current slider
    // settings. The compute runs off the UI thread (#606) — the service caches decoded blobs and the
    // change mask, so an already-computed revision (or a slider drag) returns near-instantly.
    private async void UpdateDiffOverlay(bool frame)
    {
        if (RevisionList.SelectedItem is not Models.RevisionEntryVm vm)
        {
            PngPane.SetDiffRegions(Array.Empty<PixelRegion>(), frame: false);
            if (_pngShowingRevision)
            {
                _pngShowingRevision = false;
                PngPane.ForceReloadTexture();   // back to the current on-disk image
            }
            return;
        }

        int tolerance = DiffTolerance;
        int distance = (int)DiffGroupingSlider.Value;
        int requestId = ++_diffRequestId;

        IReadOnlyList<PixelRegion> regions;
        ImageData? revisionImage;
        try
        {
            (regions, revisionImage) = await Task.Run(() =>
            {
                var r = _blameService.ComputeRegions(vm.Index, tolerance, distance);
                // Fetch the revision's image only when we're swapping it (a revision select, not a
                // same-revision slider drag) — keeps the merge-distance drag from reloading the image.
                var img = frame ? _blameService.GetRevisionImage(vm.Index) : null;
                return (r, img);
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PngBlame] compute failed: {ex}");
            return;
        }

        // Resumes on the UI thread; ignore if a newer request has since been issued.
        if (requestId != _diffRequestId)
            return;

        // Show the selected revision's actual pixels so the boxes overlay what changed then, not the
        // current art (the boxes are framed against this image, so this must precede SetDiffRegions).
        if (frame && revisionImage is not null)
        {
            PngPane.ShowRevisionImage(revisionImage);
            _pngShowingRevision = true;
        }
        PngPane.SetDiffRegions(regions, frame);
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
        // The status counts are selection-aware (they show "N chains selected" for a
        // multi-select), so re-run them when the selection changes (#623).
        Dispatcher.UIThread.InvokeAsync(UpdateStatusBar);
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
        int selectedChainCount = _selectedState.SelectedChains.Count;
        if (acls == null || acls.AnimationChains.Count == 0)
        {
            StatusCounts.Text = string.Empty;
        }
        else if (selectedChainCount >= 2)
        {
            // Multiple chains selected: a whole-file total or a per-chain breakdown is noise, so
            // just report the selection count (#623).
            StatusCounts.Text = $"{selectedChainCount} chains selected";
        }
        else
        {
            int totalFrames = acls.AnimationChains.Sum(c => c.Frames.Count);
            string totalTime = TimelineBuilder.FormatSeconds(TimelineBuilder.TotalSeconds(acls));
            StatusCounts.Text = $"{acls.AnimationChains.Count} chains · {totalFrames} frames · {totalTime}";
        }
    }



    private void RefreshHistoryPanel()
    {
        var undoHistory = _undoManager.UndoHistory;
        var redoHistory = _undoManager.RedoHistory;
        var items = new List<Models.HistoryEntryVm>();
        // Applied (undo) rows use full-strength ink; redo rows are muted. All brushes come from
        // theme tokens so the panel stays legible in both light and dark (this method re-runs on
        // ActualThemeVariantChanged). The current entry gets an accent fill with on-accent text —
        // reusing the body ink there would paint dark-on-red and fail contrast in light mode.
        bool dark = ActualThemeVariant != ThemeVariant.Light;
        IBrush appliedInk = ThemedBrush("Ink");
        IBrush redoInk    = new SolidColorBrush(Color.Parse(dark ? "#6a6e76" : "#9aa1ad"));
        IBrush accentFill = ThemedBrush("Accent");
        IBrush onAccent   = ThemedBrush("OnAccent");
        // Photoshop order: oldest applied at top, newest applied at bottom, redo items below.
        foreach (var cmd in undoHistory)
            items.Add(new Models.HistoryEntryVm(cmd.Description, appliedInk, Brushes.Transparent));
        // Mark the most recently applied command as "you are here".
        if (items.Count > 0)
            items[^1] = items[^1] with { IsCurrent = true, Foreground = onAccent, Background = accentFill };
        // Redo items follow: next-to-redo first, furthest future last.
        foreach (var cmd in redoHistory)
            items.Add(new Models.HistoryEntryVm(cmd.Description, redoInk, Brushes.Transparent));
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

    // History is a tab in the sidebar tab strip (#544), so "show history" is just selecting it.
    private void SelectHistoryTab() => SidebarTabs.SelectedItem = HistoryTab;

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

        // Borrow the first texture referenced anywhere in the project ONLY when no frame is selected
        // (an empty chain), so the combo + wireframe show something Ctrl-clickable to seed the first
        // frame (issue #618). A selected frame with no texture is left un-assigned rather than
        // borrowing another frame's. Mirrors WireframeControl.DetermineTexturePath's fallback (#616).
        string? textureName = frame?.TextureName;
        if (string.IsNullOrEmpty(textureName) && _selectedState.SelectedFrame is null)
            textureName = TextureListBuilder.GetFirstTextureName(_projectManager.AnimationChainListSave);

        if (!string.IsNullOrEmpty(textureName) && !string.IsNullOrEmpty(_projectManager.FileName))
        {
            string achxFolder = (Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty);
            var abs = System.IO.Path.IsPathRooted(textureName)
                ? textureName
                : Path.Combine(achxFolder, textureName);
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
        else if (_selectedState.SelectedFrame is { } selectedFrame && string.IsNullOrEmpty(selectedFrame.TextureName))
        {
            // A frame is selected but has no texture: clear the combo so it doesn't imply one is
            // assigned. Suppress so the clear doesn't fire OnTextureComboChanged. The wireframe is
            // blanked separately by DetermineTexturePath's RefreshAll (#616).
            _suppressTextureComboChanged = true;
            try { TextureCombo.SelectedItem = null; }
            finally { _suppressTextureComboChanged = false; }
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
        MenuExportPixiJs.Click += OnExportPixiJsClick;
        MenuAbout.Click  += OnAboutClick;
        MenuViewLog.Click += OnViewLogClick;
        // ToggleType="CheckBox" flips IsChecked before Click fires, so just apply the new state.
        // F3 is handled separately in the global KeyDown handler (InputGesture on a MenuItem is
        // display-only here — the same reason Ctrl+Z has its own KeyDown branch).
        MenuShowDiagnostics.Click += (_, _) => ApplyDiagnostics(MenuShowDiagnostics.IsChecked == true);
        MenuSettings.Click += OnSettingsClick;
        MenuCopy.Click          += (_, _) => _ = HandleCopyAsync();
        MenuCut.Click           += (_, _) => _ = HandleCutAsync();
        MenuPaste.Click         += (_, _) => _ = HandlePasteAsync();
        MenuDuplicate.Click     += (_, _) => HandleDuplicate();
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

        HistoryUndoButton.Click += (_, _) => _undoManager.Undo();
        HistoryRedoButton.Click += (_, _) => _undoManager.Redo();
        MenuShowHistory.Click   += (_, _) => SelectHistoryTab();

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
        Cut:             () => _ = HandleCutAsync(),
        Paste:           () => _ = HandlePasteAsync(),
        Duplicate:       () => HandleDuplicate(),
        ReloadFromDisk:  () => { if (!string.IsNullOrEmpty(_projectManager.FileName)) _appCommands.ReloadAchxFromDisk(_projectManager.FileName); },
        ToggleHotReload: () => { _appCommands.HotReloadWatcher.IsEnabled = !_appCommands.HotReloadWatcher.IsEnabled; },
        ResizeTexture:   () => _ = DoResizeTextureAsync(),
        ShowHistory:     () => SelectHistoryTab(),
        ViewLog:         () => OnViewLogClick(null, null!),
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

    private void OnExportPixiJsClick(object? sender, RoutedEventArgs e) =>
        _ = _appCommands.ExportToPixiJsAsync();

    internal const string GitHubUrl = "https://github.com/vchelaru/FlatRedBall2";

    private void OnAboutClick(object? sender, RoutedEventArgs e)
        => _ = BuildAboutWindow().ShowDialog(this);

    private void OnSettingsClick(object? sender, RoutedEventArgs e)
    {
        var themedPalette = CanvasPalette.For(ActualThemeVariant != ThemeVariant.Light);
        var dialog = Settings.SettingsWindowBuilder.Build(
            new Settings.SettingsWindowModel
            {
                FileAssociationSupported = _fileAssociation.IsSupported,
                FileAssociationStatus = _fileAssociation.GetStatus(),
                SuppressDefaultHandlerPrompt = _appSettings.SuppressDefaultHandlerPrompt,
                CanvasBackgroundArgb = _appSettings.CanvasBackgroundArgb,
                ThemeDefaultBackgroundArgb = ToArgb(themedPalette.Background),
                GuideLineArgb = _appSettings.GuideLineArgb,
                ThemeDefaultGuideLineArgb = ToArgb(themedPalette.GuideLine),
            },
            new Settings.SettingsWindowCallbacks
            {
                OnSetDefaultAchx = () => RegisterAsDefaultAchxHandler(hideBanner: false),
                OnSuppressDefaultHandlerPromptChanged = suppressed =>
                {
                    _appSettings.SuppressDefaultHandlerPrompt = suppressed;
                    SaveSettingsFile();
                    ShowDefaultHandlerBannerIfAppropriate();
                },
                OnCanvasBackgroundChanged = SetCanvasBackground,
                OnPickCustomCanvasBackground = PickCustomCanvasBackgroundAsync,
                OnGuideLineChanged = SetGuideLineColor,
                OnPickCustomGuideLine = PickCustomGuideLineColorAsync,
            });
        _ = dialog.ShowDialog(this);
    }

    /// <summary>Flips the render-diagnostics overlay on both canvas panels and syncs the menu
    /// checkmark. Called by the F3 accelerator; the menu Click applies its own already-toggled state.</summary>
    private void ToggleDiagnostics()
    {
        bool on = MenuShowDiagnostics.IsChecked != true;
        MenuShowDiagnostics.IsChecked = on;   // setting IsChecked does not raise Click, so no re-entry
        ApplyDiagnostics(on);
    }

    private void ApplyDiagnostics(bool on)
    {
        WireframeCtrl.DiagnosticsEnabled = on;
        PreviewCtrl.DiagnosticsEnabled   = on;
    }

    private void OnViewLogClick(object? sender, RoutedEventArgs e)
    {
        var path = Services.CrashLogging.LogFilePath;
        if (path == null || !File.Exists(path))
        {
            ShowStatusMessage("No log yet.");
            return;
        }
        try
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"⚠ Could not open log: {ex.Message}", isError: true);
        }
    }

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
        GroupTimelineTracks.ItemsSource = _groupTimelineTracks;

        PreviewZoom.Attach(PreviewCtrl);

        // PreviewZoom tracks the live zoom itself (ZoomControl.Attach subscribes ZoomChanged); this
        // handler only persists the settled state — once the smooth zoom stops animating
        // (IsZoomAnimating == false), not on every animation tick (#451).
        PreviewCtrl.ZoomChanged += _ =>
        {
            if (!PreviewCtrl.IsZoomAnimating) SaveCompanionFile();
        };
        PreviewCtrl.PanChanged  += (_, _) => SaveCompanionFile();
        PreviewCtrl.Playback.FrameIndexChanged += OnPreviewPlaybackFrameIndexChanged;
        PreviewCtrl.Playback.PlaybackTicked += OnPlaybackTicked;
        PreviewCtrl.GroupTracksChanged += RefreshGroupTimelineTracks;
        PreviewCtrl.GroupPlaybackTicked += RefreshGroupTimelineScrubbers;

        // ── Preview scrollbars (#415) ──
        // Two-way sync between the manual pan and the scrollbars, using the same suppression-flag
        // pattern that breaks feedback loops elsewhere. The scroll axis runs
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

    // ── Tree view ─────────────────────────────────────────────────────────────

    private readonly ObservableCollection<TreeNodeVm> _treeRoots = new();
    private readonly ObservableCollection<TimelineFrameVm> _timelineFrames = new();
    private double _timelineEffectivePps = TimelineBuilder.PixelsPerSecond;
    private int _currentTimelineFrameIndex = -1;
    // Structure of the strip the cells were last built from; compared on each refresh so a
    // pure selection change (scrub) skips the clear-and-rebuild (#452).
    private TimelineStripSignature? _timelineSignature;

    // ── Multi-select group preview timeline (#576) ──────────────────────────────
    // One row per selected chain, rebuilt whenever PreviewCtrl.GroupTracksChanged fires
    // (chain added/removed from the group) — never on every render or unrelated selection change.
    private readonly ObservableCollection<ChainTimelineTrackVm> _groupTimelineTracks = new();
    private bool _isGroupTimelineScrubbing;
    private ChainTimelineTrackVm? _groupScrubTrack;
    private ItemsControl? _groupScrubFramesList;

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

        // Frame drag-and-drop reorder: a press on a frame row arms a drag candidate, a
        // pointer move past the threshold starts the Avalonia drag (issue #500).
        AnimTree.AddHandler(
            InputElement.PointerMovedEvent,
            OnTreeFrameDragPointerMoved,
            RoutingStrategies.Bubble);
        AnimTree.AddHandler(
            InputElement.PointerReleasedEvent,
            OnTreeFrameDragPointerReleased,
            RoutingStrategies.Bubble);

        // Chain drag-and-drop reorder: a press on a chain row arms a chain-drag candidate, a
        // pointer move past the threshold starts the Avalonia drag (parallel to the frame path).
        AnimTree.AddHandler(
            InputElement.PointerMovedEvent,
            OnTreeChainDragPointerMoved,
            RoutingStrategies.Bubble);
        AnimTree.AddHandler(
            InputElement.PointerReleasedEvent,
            OnTreeChainDragPointerReleased,
            RoutingStrategies.Bubble);

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

        // Search box: icon toggles the inline box; typing filters the tree by chain name.
        WireTreeSearch();

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

    // Current ANIMATIONS tree filter text. Empty/whitespace = no filter (full tree).
    // The filter is *sticky*: it survives selection and model edits. Two triggers apply
    // it differently — ApplyQueryFilter (typing) may hide rows; the model-change paths
    // (RefreshTreeView/RefreshChainNode) are grow-only and never hide a visible row.
    private string _treeFilterQuery = string.Empty;

    private void WireTreeSearch()
    {
        SearchToggleBtn.Click += (_, _) => ToggleSearchBox();

        // Typing recomputes visibility from scratch — this is the only path allowed to hide.
        SearchBox.TextChanged += (_, _) =>
        {
            _treeFilterQuery = SearchBox.Text ?? string.Empty;
            ApplyQueryFilter();
        };

        // Two-stage ✕: with text, clear it (box stays open); when already empty, collapse.
        SearchClearBtn.Click += (_, _) =>
        {
            if (TreeSearchBoxLogic.ClearShouldCollapse(SearchBox.Text))
                CollapseSearchBox();
            else
            {
                SearchBox.Text = string.Empty; // fires TextChanged → ApplyQueryFilter restores all
                SearchBox.Focus();
            }
        };

        // Escape collapses the box (and clears the filter); handled tunnel-phase so it
        // doesn't reach the TreeView (which would otherwise steal the key).
        SearchBox.AddHandler(
            InputElement.KeyDownEvent,
            (object? _, KeyEventArgs e) =>
            {
                if (e.Key == Key.Escape)
                {
                    CollapseSearchBox();
                    e.Handled = true;
                }
            },
            RoutingStrategies.Tunnel);

        // Click-away collapses the box — EXCEPT when focus moves into the tree, so the
        // sticky-filter workflow (filter, click a result, edit, click another) keeps the
        // box and filter alive. Deferred to Background so the new focus target has settled.
        SearchBox.LostFocus += (_, _) =>
            Dispatcher.UIThread.Post(CollapseSearchBoxOnClickAway, DispatcherPriority.Background);
    }

    // Query-change path: the only place allowed to HIDE a chain (typing/refining shrinks
    // the set); an empty query shows all. The selected row stays visible via its IsVisible
    // binding. Logic lives in the pure, unit-tested TreeBuilder.ApplyQueryFilter.
    private void ApplyQueryFilter() =>
        TreeBuilder.ApplyQueryFilter(_treeRoots, _treeFilterQuery);

    private void ToggleSearchBox()
    {
        if (SearchBox.IsVisible) CollapseSearchBox();
        else ExpandSearchBox();
    }

    // Pattern B: the box replaces the 🔍 icon (they are never both visible) and takes focus.
    private void ExpandSearchBox()
    {
        SearchToggleBtn.IsVisible = false;
        SearchBox.IsVisible = true;
        Dispatcher.UIThread.Post(() => SearchBox.Focus(), DispatcherPriority.Background);
    }

    // Hides the box, restores the 🔍 icon, and clears the query (restoring the full tree).
    // Clearing the text fires TextChanged, which re-applies the empty filter.
    private void CollapseSearchBox()
    {
        SearchBox.IsVisible = false;
        SearchToggleBtn.IsVisible = true;
        SearchBox.Text = string.Empty;
    }

    // Collapses the box when focus has left both the box and the tree. Keeping the box open
    // while focus is in the tree is what preserves the sticky click-a-result workflow.
    private void CollapseSearchBoxOnClickAway()
    {
        if (!SearchBox.IsVisible) return;
        var focused = FocusManager?.GetFocusedElement() as Avalonia.Visual;
        bool focusInBox  = focused is not null &&
            (ReferenceEquals(focused, SearchBox) || focused.GetVisualAncestors().Contains(SearchBox));
        bool focusInTree = focused is not null &&
            (ReferenceEquals(focused, AnimTree) || focused.GetVisualAncestors().Contains(AnimTree));
        if (!focusInBox && !focusInTree)
            CollapseSearchBox();
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
        // Internal frame reorder drag — distinct from the external .png file drag below.
        if (e.DataTransfer.Contains(FrameDragDataFormat) && _pendingFrameDrag is { IsValid: true } drag)
        {
            var target = ResolveFrameDrop(e, drag);
            if (target.IsValid)
            {
                e.DragEffects = DragDropEffects.Move;
                ShowFrameDropIndicator(e);
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
                RemoveFrameDropIndicators();
            }
            e.Handled = true;
            return;
        }

        // Internal chain reorder drag.
        if (e.DataTransfer.Contains(ChainDragDataFormat) && _pendingChainDrag is { IsValid: true } chainDrag)
        {
            var target = ResolveChainDrop(e, chainDrag.Chains);
            if (target.IsValid)
            {
                e.DragEffects = DragDropEffects.Move;
                ShowChainDropIndicator(e);
            }
            else
            {
                e.DragEffects = DragDropEffects.None;
                RemoveFrameDropIndicators();
            }
            e.Handled = true;
            return;
        }

        string? firstFile = DragDropFileResolver.GetFirstDroppedFilePath(e);

        if (string.IsNullOrEmpty(firstFile) ||
            !string.Equals(Path.GetExtension(firstFile), ".png", StringComparison.OrdinalIgnoreCase))
        {
            e.DragEffects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var (targetChain, targetFrame) = ResolveTreePngDropTarget(e);
        var wouldApply = TextureDropProcessor.ComputePngDrop(
            targetChain,
            targetFrame,
            firstFile,
            _projectManager.FileName,
            e.KeyModifiers.HasFlag(KeyModifiers.Control)).Result
            != TextureDropResult.NotApplied;

        e.DragEffects = wouldApply ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnTreeDrop(object? sender, DragEventArgs e)
    {
        // Internal frame reorder drop — perform the move and let the external .png path below
        // stay untouched (external file drags never carry the frame marker format).
        if (e.DataTransfer.Contains(FrameDragDataFormat) && _pendingFrameDrag is { IsValid: true } drag)
        {
            RemoveFrameDropIndicators();
            var target = ResolveFrameDrop(e, drag);
            if (target is { IsValid: true, Chain: not null } && drag.SourceChain is not null)
                _appCommands.MoveFrames(drag.Frames, drag.SourceChain, target.Chain, target.InsertIndex);
            e.Handled = true;
            return;
        }

        // Internal chain reorder drop.
        if (e.DataTransfer.Contains(ChainDragDataFormat) && _pendingChainDrag is { IsValid: true } chainDrag)
        {
            RemoveFrameDropIndicators();
            var target = ResolveChainDrop(e, chainDrag.Chains);
            if (target.IsValid)
                _appCommands.MoveChainsToIndex(chainDrag.Chains, target.InsertIndex);
            e.Handled = true;
            return;
        }

        var firstFile = DragDropFileResolver.GetFirstDroppedFilePath(e);
        Trace.WriteLine($"[DragDrop] OnTreeDrop: firstFile={firstFile ?? "(null)"}, FileName={_projectManager.FileName ?? "(null)"}");

        if (string.IsNullOrEmpty(firstFile))
        {
            Trace.WriteLine("[DragDrop] Aborted: no file found in drop data");
            return;
        }

        var (targetChain, targetFrame) = ResolveTreePngDropTarget(e);

        Trace.WriteLine($"[DragDrop] targetChain={targetChain?.Name ?? "(null)"}, targetFrame={targetFrame?.TextureName ?? "(null)"}, ctrl={e.KeyModifiers.HasFlag(KeyModifiers.Control)}");

        bool applied = await HandlePngDropAsync(targetChain, targetFrame, firstFile, e.KeyModifiers.HasFlag(KeyModifiers.Control));
        if (applied)
            e.Handled = true;
    }

    /// <summary>
    /// Single apply path for every PNG-drop entry point (ANIMATIONS tree — including PNGs
    /// dragged in from the Files panel, which already ride the same OS drag/drop payload —
    /// and the wireframe canvas, see issue #560). Prompts to copy the file alongside the .achx
    /// when it lives outside the achx/project folder (<see cref="TextureCopyDecider"/>), computes
    /// the drop via <see cref="TextureDropProcessor.ComputePngDrop"/>, and applies + refreshes.
    /// Returns <see langword="true"/> when the drop actually changed something.
    /// </summary>
    private async Task<bool> HandlePngDropAsync(
        AnimationChainSave? targetChain, AnimationFrameSave? targetFrame,
        string droppedFilePath, bool createFrameOnCtrl)
    {
        if (!string.Equals(Path.GetExtension(droppedFilePath).TrimStart('.'), "png", StringComparison.OrdinalIgnoreCase))
            return false;

        string resolvedFilePath = droppedFilePath;
        string achxFolder = string.IsNullOrEmpty(_projectManager.FileName)
            ? string.Empty
            : (Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty);

        if (!string.IsNullOrEmpty(achxFolder) &&
            TextureCopyDecider.ShouldPromptToCopy(droppedFilePath, achxFolder, _appState.ProjectFolder))
        {
            var choice = await ShowTextureCopyDialogAsync(droppedFilePath);
            if (choice == TextureCopyChoice.Cancel) return false;

            if (choice == TextureCopyChoice.Copy)
            {
                string destination = Path.Combine(achxFolder, Path.GetFileName(droppedFilePath));
                try
                {
                    File.Copy(droppedFilePath, destination, overwrite: true);
                    resolvedFilePath = destination;
                }
                catch (Exception ex)
                {
                    ShowToast($"Could not copy: {ex.Message}");
                    return false;
                }
            }
        }

        var (result, relPath) = TextureDropProcessor.ComputePngDrop(
            targetChain, targetFrame, resolvedFilePath, _projectManager.FileName, createFrameOnCtrl);

        Trace.WriteLine($"[DragDrop] Result={result}");

        bool applied = TextureDropApplier.Apply(_appCommands, _selectedState, targetChain, targetFrame, result, relPath);
        if (!applied)
        {
            Trace.WriteLine("[DragDrop] NotApplied — no chain or frame targeted, or non-PNG dropped");
            return false;
        }

        RefreshTextureCombo();
        _appCommands.RefreshWireframe();
        _events.RaiseAnimationChainsChanged();
        _appCommands.SyncHotReloadWatcher();  // watch the newly-referenced PNG directory
        return true;
    }

    private TreeNodeVm? GetTreeNodeAtDropPosition(DragEventArgs e)
    {
        var position = e.GetPosition(AnimTree);
        var hit = AnimTree.InputHitTest(position);
        if (hit is not Control src)
            return null;

        var tvi = src.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        return tvi?.DataContext as TreeNodeVm;
    }

    private (AnimationChainSave? Chain, AnimationFrameSave? Frame) ResolveTreePngDropTarget(DragEventArgs e)
    {
        var targetNode = GetTreeNodeAtDropPosition(e);
        return TreePngDropTarget.FromNodeData(
            targetNode?.Data,
            frame => _objectFinder.GetAnimationChainContaining(frame));
    }

    // ── Internal frame drag-and-drop reorder (issue #500) ──────────────────────

    private void ClearFrameDragCandidate()
    {
        _frameDragCandidate = null;
        _frameDragPressPoint = null;
        _frameDragPressArgs = null;
        _frameDragSelectionSnapshot = null;
        _pendingSingleSelectFrame = null;
    }

    private void SelectSingleFrame(AnimationFrameSave frame)
    {
        var vm = TreeBuilder.FindNodeForData(_treeRoots, frame);
        if (vm is null) return;

        // The tree still visually holds the whole multi-selection (the select-on-press was
        // suppressed), and SyncTreeSelection won't collapse it because the clicked frame is
        // already among SelectedItems. Drive the tree directly: clear the others silently,
        // then set the single item so the normal selection cascade updates SelectedState.
        bool prior = _suppressTreeSelectionHandling;
        _suppressTreeSelectionHandling = true;
        try { AnimTree.SelectedItems?.Clear(); }
        finally { _suppressTreeSelectionHandling = prior; }
        AnimTree.SelectedItem = vm;
    }

    private async void OnTreeFrameDragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_frameDragInProgress || _frameDragCandidate is null ||
            _frameDragPressPoint is null || _frameDragPressArgs is null)
            return;

        if (!e.GetCurrentPoint(AnimTree).Properties.IsLeftButtonPressed)
        {
            ClearFrameDragCandidate();
            return;
        }

        var pos = e.GetPosition(AnimTree);
        if (Math.Abs(pos.X - _frameDragPressPoint.Value.X) <= 4 &&
            Math.Abs(pos.Y - _frameDragPressPoint.Value.Y) <= 4)
            return;

        if (!TryBuildFrameDragSource(out var dragSource))
        {
            ClearFrameDragCandidate();
            return;
        }

        // A drag is happening, so the deferred single-select must not fire on release.
        _pendingSingleSelectFrame = null;
        e.Pointer.Capture(null); // release our press-capture so the drag system can take over
        _pendingFrameDrag = dragSource;
        _frameDragInProgress = true;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(FrameDragDataFormat, FrameDragToken));
        try
        {
            // DoDragDropAsync needs the originating press args; the move past the threshold
            // is what gates when we begin, so the platform drag never starts on a plain click.
            await DragDrop.DoDragDropAsync(_frameDragPressArgs, data, DragDropEffects.Move);
        }
        finally
        {
            _pendingFrameDrag = null;
            _frameDragInProgress = false;
            RemoveFrameDropIndicators();
            ClearFrameDragCandidate();
        }
    }

    private void OnTreeFrameDragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_frameDragInProgress) return;

        // A press on an already-multi-selected frame that did not turn into a drag collapses
        // the selection to that single frame now (the select-on-press was suppressed so the
        // multi-selection could survive a potential drag).
        if (_pendingSingleSelectFrame is { } frame)
        {
            e.Pointer.Capture(null);
            SelectSingleFrame(frame);
        }
        ClearFrameDragCandidate();
    }

    /// <summary>
    /// Decides which frames a drag moves. Dragging a frame that is part of a valid frame
    /// multi-selection moves the whole set; a mixed or multi-chain selection that includes
    /// the dragged frame is rejected with a toast; otherwise just the dragged frame moves.
    /// </summary>
    private bool TryBuildFrameDragSource(out FrameDragSource dragSource)
    {
        dragSource = default;
        var candidate = _frameDragCandidate;
        if (candidate is null) return false;

        var snapshot = _frameDragSelectionSnapshot ?? new List<object>();
        bool candidateInSnapshot = snapshot.Any(n => ReferenceEquals(n, candidate));
        var classified = FrameDropResolver.ClassifySelection(
            snapshot, f => _objectFinder.GetAnimationChainContaining(f));

        if (candidateInSnapshot)
        {
            if (classified.IsValid)
            {
                dragSource = classified;
                return true;
            }
            if (classified.Validity is FrameDragValidity.MixedTypes
                or FrameDragValidity.MultipleSourceChains)
            {
                ShowFrameDragRejectedToast(classified.Validity);
                return false;
            }
        }

        // Drag just the single pressed frame.
        var chain = _objectFinder.GetAnimationChainContaining(candidate);
        if (chain is null) return false;
        dragSource = new FrameDragSource(new[] { candidate }, chain, FrameDragValidity.Valid);
        return true;
    }

    private void ShowFrameDragRejectedToast(FrameDragValidity validity)
    {
        string message = validity == FrameDragValidity.MultipleSourceChains
            ? "Can't drag frames from multiple animations yet — select frames from one animation."
            : "Can't reorder a mixed selection — select only frames.";
        ShowStatusMessage(message, isError: true);
    }

    private FrameDropTarget ResolveFrameDrop(DragEventArgs e, FrameDragSource drag)
    {
        if (drag.SourceChain is null) return FrameDropTarget.None;
        var (nodeData, half, _) = HitTestFrameRow(e.GetPosition(AnimTree));
        return FrameDropResolver.Resolve(
            nodeData, half, drag.Frames, drag.SourceChain,
            f => _objectFinder.GetAnimationChainContaining(f));
    }

    /// <summary>
    /// Maps a pointer position over the tree to the node under it, which half of that row
    /// the pointer is in, and the realized container (used to place the drop indicator).
    /// </summary>
    private (object? NodeData, FrameRowHalf Half, TreeViewItem? Item) HitTestFrameRow(Avalonia.Point posInTree)
    {
        if (AnimTree.InputHitTest(posInTree) is not Control hit)
            return (null, FrameRowHalf.Upper, null);

        var tvi = hit.FindAncestorOfType<TreeViewItem>(includeSelf: true);
        if (tvi?.DataContext is not TreeNodeVm vm)
            return (null, FrameRowHalf.Upper, null);

        // Frame rows are leaves, so the container height is the row height. The half only
        // matters for frame targets; chain targets always append regardless.
        var topLeft = Avalonia.VisualExtensions.TranslatePoint(tvi, new Avalonia.Point(0, 0), AnimTree) ?? posInTree;
        double rowHeight = tvi.Bounds.Height;
        var half = (posInTree.Y - topLeft.Y) < rowHeight / 2 ? FrameRowHalf.Upper : FrameRowHalf.Lower;
        return (vm.Data, half, tvi);
    }

    private void ShowFrameDropIndicator(DragEventArgs e)
    {
        var (nodeData, half, tvi) = HitTestFrameRow(e.GetPosition(AnimTree));
        if (tvi is null)
        {
            RemoveFrameDropIndicators();
            return;
        }

        var topLeft = Avalonia.VisualExtensions.TranslatePoint(tvi, new Avalonia.Point(0, 0), DragOverlayCanvas);
        var treeOrigin = Avalonia.VisualExtensions.TranslatePoint(AnimTree, new Avalonia.Point(0, 0), DragOverlayCanvas);
        if (topLeft is null || treeOrigin is null)
        {
            RemoveFrameDropIndicators();
            return;
        }

        double treeRight = treeOrigin.Value.X + AnimTree.Bounds.Width;

        if (nodeData is AnimationChainSave)
        {
            // Appending into an animation: outline the whole animation row/subtree so it reads as
            // "drop inside this animation" — a box conveys containment in a way a line can't,
            // which matters most for a collapsed chain where a line would look top-level.
            RemoveDropLine();
            ShowDropBox(topLeft.Value.X, topLeft.Value.Y, treeRight, tvi.Bounds.Height);
            return;
        }

        // Reordering at a specific frame: a thin line at the precise insert position, indented to
        // the frame content so it clearly belongs inside the animation rather than at the top level.
        RemoveDropBox();
        double y = topLeft.Value.Y + (half == FrameRowHalf.Upper ? 0 : tvi.Bounds.Height);
        double left = HeaderContentLeft(tvi);
        ShowDropLine(left, treeRight, y);
    }

    private void ShowDropLine(double left, double treeRight, double y)
    {
        _frameDropLine ??= new Border
        {
            Height = 2,
            Background = new SolidColorBrush(Color.Parse("#4a90d9")),
            IsHitTestVisible = false,
        };
        if (!DragOverlayCanvas.Children.Contains(_frameDropLine))
            DragOverlayCanvas.Children.Add(_frameDropLine);

        _frameDropLine.Width = Math.Max(0, treeRight - left);
        Canvas.SetLeft(_frameDropLine, left);
        Canvas.SetTop(_frameDropLine, y - 1);
    }

    private void ShowDropBox(double left, double top, double treeRight, double height)
    {
        _frameDropBox ??= new Border
        {
            BorderBrush = new SolidColorBrush(Color.Parse("#4a90d9")),
            BorderThickness = new Avalonia.Thickness(2),
            CornerRadius = new Avalonia.CornerRadius(3),
            Background = new SolidColorBrush(Color.Parse("#334a90d9")), // faint fill to suggest the target area
            IsHitTestVisible = false,
        };
        if (!DragOverlayCanvas.Children.Contains(_frameDropBox))
            DragOverlayCanvas.Children.Add(_frameDropBox);

        _frameDropBox.Width = Math.Max(0, treeRight - left - 2);
        _frameDropBox.Height = Math.Max(0, height);
        Canvas.SetLeft(_frameDropBox, left);
        Canvas.SetTop(_frameDropBox, top);
    }

    /// <summary>X of a tree row's header content (after the indent + chevron) in canvas coords.</summary>
    private double HeaderContentLeft(TreeViewItem tvi)
    {
        Control anchor = tvi.GetVisualDescendants().OfType<Control>()
            .FirstOrDefault(c => c.Name == "PART_HeaderPresenter") ?? tvi;
        return Avalonia.VisualExtensions.TranslatePoint(anchor, new Avalonia.Point(0, 0), DragOverlayCanvas)?.X ?? 0;
    }

    private void RemoveDropLine()
    {
        if (_frameDropLine is not null)
            DragOverlayCanvas.Children.Remove(_frameDropLine);
    }

    private void RemoveDropBox()
    {
        if (_frameDropBox is not null)
            DragOverlayCanvas.Children.Remove(_frameDropBox);
    }

    private void RemoveFrameDropIndicators()
    {
        RemoveDropLine();
        RemoveDropBox();
    }

    // ── Internal chain drag-and-drop reorder ───────────────────────────────────

    private void ClearChainDragCandidate()
    {
        _chainDragCandidate = null;
        _chainDragPressPoint = null;
        _chainDragPressArgs = null;
        _chainDragSelectionSnapshot = null;
        _pendingSingleSelectChain = null;
    }

    private void SelectSingleChain(AnimationChainSave chain)
    {
        var vm = TreeBuilder.FindNodeForData(_treeRoots, chain);
        if (vm is null) return;

        bool prior = _suppressTreeSelectionHandling;
        _suppressTreeSelectionHandling = true;
        try { AnimTree.SelectedItems?.Clear(); }
        finally { _suppressTreeSelectionHandling = prior; }
        AnimTree.SelectedItem = vm;
    }

    private async void OnTreeChainDragPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_chainDragInProgress || _chainDragCandidate is null ||
            _chainDragPressPoint is null || _chainDragPressArgs is null)
            return;

        if (!e.GetCurrentPoint(AnimTree).Properties.IsLeftButtonPressed)
        {
            ClearChainDragCandidate();
            return;
        }

        var pos = e.GetPosition(AnimTree);
        if (Math.Abs(pos.X - _chainDragPressPoint.Value.X) <= 4 &&
            Math.Abs(pos.Y - _chainDragPressPoint.Value.Y) <= 4)
            return;

        if (!TryBuildChainDragSource(out var dragSource))
        {
            ClearChainDragCandidate();
            return;
        }

        // A drag is happening, so the deferred single-select must not fire on release.
        _pendingSingleSelectChain = null;
        e.Pointer.Capture(null); // release press-capture so the drag system can take over
        _pendingChainDrag = dragSource;
        _chainDragInProgress = true;

        var data = new DataTransfer();
        data.Add(DataTransferItem.Create(ChainDragDataFormat, ChainDragToken));
        try
        {
            await DragDrop.DoDragDropAsync(_chainDragPressArgs, data, DragDropEffects.Move);
        }
        finally
        {
            _pendingChainDrag = null;
            _chainDragInProgress = false;
            RemoveFrameDropIndicators();
            ClearChainDragCandidate();
        }
    }

    private void OnTreeChainDragPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_chainDragInProgress) return;

        // A press on an already-multi-selected chain that did not turn into a drag collapses
        // the selection to that single chain now (the select-on-press was suppressed so the
        // multi-selection could survive a potential drag).
        if (_pendingSingleSelectChain is { } chain)
        {
            e.Pointer.Capture(null);
            SelectSingleChain(chain);
        }
        ClearChainDragCandidate();
    }

    /// <summary>
    /// Decides which chains a drag moves. Dragging a chain that is part of a valid chain
    /// multi-selection moves the whole set; a mixed selection that includes the dragged chain
    /// is rejected with a toast; otherwise just the dragged chain moves. Mirrors
    /// <see cref="TryBuildFrameDragSource"/>.
    /// </summary>
    private bool TryBuildChainDragSource(out ChainDragSource dragSource)
    {
        dragSource = default;
        var candidate = _chainDragCandidate;
        if (candidate is null) return false;

        var snapshot = _chainDragSelectionSnapshot ?? new List<object>();
        bool candidateInSnapshot = snapshot.Any(n => ReferenceEquals(n, candidate));
        var classified = ChainDropResolver.ClassifySelection(snapshot);

        if (candidateInSnapshot)
        {
            if (classified.IsValid)
            {
                dragSource = classified;
                return true;
            }
            if (classified.Validity is ChainDragValidity.MixedTypes)
            {
                ShowStatusMessage(
                    "Can't reorder a mixed selection — select only animations.", isError: true);
                return false;
            }
        }

        // Drag just the single pressed chain.
        dragSource = new ChainDragSource(new[] { candidate }, ChainDragValidity.Valid);
        return true;
    }

    private ChainDropTarget ResolveChainDrop(DragEventArgs e, IReadOnlyList<AnimationChainSave> draggedChains)
    {
        var chains = _projectManager.AnimationChainListSave?.AnimationChains;
        if (chains is null) return ChainDropTarget.None;
        var (nodeData, half, _) = HitTestFrameRow(e.GetPosition(AnimTree));
        return ChainDropResolver.Resolve(
            nodeData, half, draggedChains, chains,
            f => _objectFinder.GetAnimationChainContaining(f));
    }

    /// <summary>
    /// Draws a thin insert line at the resolved chain boundary. Chains are root nodes, so the
    /// line spans the full tree width (no box — a box reads as "drop inside", which is the
    /// frame-into-chain affordance, not a sibling reorder).
    /// </summary>
    private void ShowChainDropIndicator(DragEventArgs e)
    {
        var (_, half, tvi) = HitTestFrameRow(e.GetPosition(AnimTree));
        if (tvi is null)
        {
            RemoveFrameDropIndicators();
            return;
        }

        var topLeft = Avalonia.VisualExtensions.TranslatePoint(tvi, new Avalonia.Point(0, 0), DragOverlayCanvas);
        var treeOrigin = Avalonia.VisualExtensions.TranslatePoint(AnimTree, new Avalonia.Point(0, 0), DragOverlayCanvas);
        if (topLeft is null || treeOrigin is null)
        {
            RemoveFrameDropIndicators();
            return;
        }

        double treeRight = treeOrigin.Value.X + AnimTree.Bounds.Width;
        double y = topLeft.Value.Y + (half == FrameRowHalf.Upper ? 0 : tvi.Bounds.Height);
        RemoveDropBox();
        ShowDropLine(treeOrigin.Value.X, treeRight, y);
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

    // ── Files panel ───────────────────────────────────────────────────────────

    private void RefreshFilesPanel()
    {
        string? filesRoot = _projectManager.ResolveFilesPanelRoot();
        var referenced = TextureListBuilder.GetAvailableTextures(_projectManager.AnimationChainListSave);
        string? achxFolder = string.IsNullOrEmpty(_projectManager.FileName)
            ? null
            : new FilePath(_projectManager.FileName).GetDirectoryContainingThis().FullPath;
        FilesPanel.Refresh(filesRoot, referenced, achxFolder);
        _pngFolderWatcher.Watch(filesRoot);
    }

    // ── Tree refresh ──────────────────────────────────────────────────────────

    private void RefreshTreeView()
    {
        _suppressTreeSelectionHandling = true;
        try
        {
            var acls = _projectManager.AnimationChainListSave;
            if (acls is null) { _treeRoots.Clear(); RefreshFilesPanel(); return; }

            // Capture filter state BEFORE the diff mutates the tree: which chains are
            // currently visible, and which existing chains have nodes. Used for the
            // grow-only visibility recompute below.
            // NOTE: this reads PinnedVisible only — a chain visible *solely* via the
            // IsSelected half of the row's `PinnedVisible || IsSelected` binding is not
            // captured here. That's benign: SyncTreeSelection re-applies the selection
            // after the diff, so the selected row is re-shown regardless of PinnedVisible.
            var chains = acls.AnimationChains;
            var previouslyVisible = _treeRoots
                .Where(n => n.Data is AnimationChainSave && n.PinnedVisible)
                .Select(n => (AnimationChainSave)n.Data!)
                .ToList();
            var existingChains = new HashSet<AnimationChainSave>(
                _treeRoots.Where(n => n.Data is AnimationChainSave).Select(n => (AnimationChainSave)n.Data!),
                ReferenceEqualityComparer.Instance);
            var brandNew = chains.Where(c => !existingChains.Contains(c)).ToList();

            // Diff-update the root nodes instead of clearing and rebuilding, so each
            // chain's collapse state (and selection) survives copy/paste and reorder.
            TreeBuilder.SyncChainsInto(_treeRoots, chains);
            ApplyPendingPastedChainExpand();

            // Grow-only: never hide a chain that was visible; add newly-relevant ones.
            var visible = TreeBuilder.ComputeVisibleAfterModelChange(
                previouslyVisible, chains, _treeFilterQuery, brandNew);
            foreach (var node in _treeRoots)
                if (node.Data is AnimationChainSave c)
                    node.PinnedVisible = visible.Contains(c);

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
    /// Fully rebuilds the tree from scratch, expanding only the chains named in
    /// <paramref name="expandedChainNames"/> (empty for a fresh, never-before-seen file,
    /// so it presents a scannable, collapsed overview). Used on .achx load (File &gt; Open,
    /// recent files, drag-drop, startup reopen, tab switch). Building the correct expand
    /// state up front — rather than collapsing here and correcting it once companion-file
    /// settings load — avoids a collapse-then-expand flicker on tab switch, since those two
    /// steps land in separate dispatcher jobs. Contrast with <see cref="RefreshTreeView"/>,
    /// which diff-updates and preserves each chain's collapse state across edits.
    /// </summary>
    private void RebuildTreeView(IReadOnlyList<string> expandedChainNames)
    {
        _suppressTreeSelectionHandling = true;
        try
        {
            _treeRoots.Clear();

            var acls = _projectManager.AnimationChainListSave;
            if (acls is null)
            {
                RefreshFilesPanel();
                return;
            }

            // All nodes are added (membership always mirrors the model); an active filter
            // is applied as per-node visibility so it persists across a full rebuild
            // without dropping nodes from the tree.
            foreach (var node in TreeBuilder.BuildTree(acls, expandedChainNames))
            {
                node.PinnedVisible = TreeBuilder.MatchesFilter(node.Header, _treeFilterQuery);
                _treeRoots.Add(node);
            }
            RefreshFilesPanel();

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
                // Brand-new node defaults to visible (never hide something just created).
                _treeRoots.Add(TreeBuilder.BuildChainNode(chain));
            }
            else
            {
                node.Header = chain.Name;
                node.Meta   = TreeBuilder.BuildChainMeta(chain);
                TreeBuilder.SyncFramesInto(node, chain.Frames);
                // Grow-only: keep it visible if it already was, or if it now matches.
                node.PinnedVisible = node.PinnedVisible
                    || TreeBuilder.MatchesFilter(chain.Name, _treeFilterQuery);
            }
            // Striping is positional and cascades — recompute after a chain add/frame sync.
            TreeBuilder.RestripeRoots(_treeRoots);
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
        // The chain node's Meta carries the total play time (#623), which a frame-length edit
        // changes — keep it in sync whenever a frame under it is refreshed.
        chainNode.Meta = TreeBuilder.BuildChainMeta(chain);
        // A new frame/shape node must inherit its chain group's zebra parity.
        TreeBuilder.RestripeRoots(_treeRoots);
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
                // Force thumbnail regeneration regardless of source equality. The cached bitmap
                // was already dropped by InvalidatePath above; clearing the node fields makes the
                // change-detection in RefreshTreeThumbnails re-render from the reloaded sheet.
                node.Thumbnail = null;
                node.ThumbnailSource = null;
            }
        }
        RefreshTreeThumbnails();
        // A PNG changing on disk alters the thumbnail content without changing any frame field, so
        // the strip signature is unchanged. Force the next refresh to rebuild so stale crops are
        // regenerated from the invalidated cache (mirrors the tree-thumbnail reset above).
        _timelineSignature = null;
        RefreshTimelineStrip();
        _appCommands.RefreshAnimationFrameDisplay();
        RefreshFilesPanel();
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

            // The previous bitmap (if any) is cache-owned — drop the reference, don't dispose it.
            // Tint by frame 0's effective color (nothing precedes it, so this is just its own set
            // channels) so the tree icon renders the frame the same way the preview/timeline does.
            node.Thumbnail = source is null
                ? null
                : _thumbnailService.GetFrameThumbnail(
                    chain.Frames[0], EffectiveFrameColor.Resolve(chain.Frames, 0),
                    TreeChainThumbnailPixelSize, TreeChainThumbnailPixelSize);
            node.ThumbnailSource = source;
        }
    }

    private void SyncTreeSelection()
    {
        if (_selectedState.SelectedNodes.Count > 1)
        {
            SyncTreeMultiSelection(_selectedState.SelectedNodes);
            return;
        }

        // Shapes are more specific than frames — prefer them so clicking a circle or
        // rect in the tree (or preview panel) keeps the shape node highlighted.
        object? sel = (object?)_selectedState.SelectedCircle
                   ?? _selectedState.SelectedRectangle
                   ?? _selectedState.SelectedFrame
                   ?? (object?)_selectedState.SelectedChain;

        var target = sel is not null ? TreeBuilder.FindNodeForData(_treeRoots, sel) : null;

        // Expand the selected node's ancestors so its row is visible even if its chain (or, for a
        // shape, its frame) was collapsed — e.g. a frame selected by scrubbing the timeline.
        // Avalonia does not auto-expand parents.
        if (sel is not null)
            TreeBuilder.ExpandAncestorsOf(_treeRoots, sel);

        if (target is not null && !(AnimTree.SelectedItems?.Contains(target) ?? false))
        {
            // This is a one-way push of model selection into the tree. Suppress OnTreeSelectionChanged
            // so the assignment doesn't loop back through SelectedNodes/RouteNodeSelection and re-fire
            // the whole SelectionChanged cascade (a second timeline + inspector rebuild) — and so it
            // can't clobber SelectedChain when selecting a frame under a collapsed chain (#452).
            // Save/restore rather than bare reset so nesting under another suppressed refresh is safe.
            bool prior = _suppressTreeSelectionHandling;
            _suppressTreeSelectionHandling = true;
            try
            {
                WithPreservedAnimTreeScroll(() => AnimTree.SelectedItem = target);
            }
            finally { _suppressTreeSelectionHandling = prior; }
        }
    }

    // The single-row timeline's fixed height (14px ruler + one ~38px frame-cell row).
    private const double SingleTimelineAreaHeight = 52;
    // Group-preview timeline area (#576): tall enough for ~4 track rows at once; more chains
    // scroll within GroupTimelineScrubHost's ScrollViewer rather than growing the row further.
    private const double GroupTimelineAreaHeight = 160;

    private void RefreshTimelineStrip()
    {
        // Multi-select group preview (#576): 2+ whole chains selected swaps the single-row strip
        // for a per-chain track stack. TimelineScrubSurface/GroupTimelineScrubHost occupy the same
        // grid cell, so only one is ever visible. The host row is also grown so the extra track
        // rows aren't clipped to the single-row strip's original fixed height.
        bool groupActive = _selectedState.SelectedChains.Count >= 2;
        TimelineScrubSurface.IsVisible = !groupActive;
        GroupTimelineScrubHost.IsVisible = groupActive;
        PreviewBlockGrid.RowDefinitions[2].Height =
            new GridLength(groupActive ? GroupTimelineAreaHeight : SingleTimelineAreaHeight);
        if (groupActive)
        {
            // Multiple chains have no single duration — keep the plain label (#623).
            TimelineHeaderLabel.Text = "TIMELINE";
            RefreshGroupTimelineTracks();
            return;
        }

        var chain = GetTimelineChain();

        // Show the active chain's total play time next to the ruler label so the duration is
        // visible without adding up per-frame times (#623).
        TimelineHeaderLabel.Text = chain is { Frames.Count: > 0 }
            ? $"TIMELINE · {TimelineBuilder.FormatSeconds(TimelineBuilder.TotalSeconds(chain))}"
            : "TIMELINE";

        // Only clear-and-rebuild the cells when the frame structure (chain identity, count,
        // durations, or any thumbnail-affecting field) actually changed. A scrub that crosses a
        // frame boundary changes only the selection, so the signature stays equal and we keep the
        // existing cell VMs alive — skipping the per-frame Skia thumbnail regeneration and the
        // playhead-VM teardown that caused the visible pop/fall-behind (#452). The highlight and
        // playhead offset below run on every call regardless.
        var signature = TimelineStripSignature.From(chain);
        if (!signature.Equals(_timelineSignature))
        {
            RebuildTimelineStripCells(chain);
            _timelineSignature = signature;
        }

        int preferred = GetPreferredTimelineFrameIndex(chain);
        UpdateTimelineScrubber(preferred);
        // Drive the playhead from the live playback position so a paused/scrubbed frame keeps its
        // sub-frame offset instead of snapping to the cell's left edge (#432).
        ApplyScrubberOffsetFromPlayback(preferred);
    }

    private void RebuildTimelineStripCells(AnimationChainSave? chain)
    {
        // Timeline thumbnails are cache-owned by the ThumbnailService, so just drop the cells —
        // no per-cell dispose (that would invalidate a bitmap a later cache hit returns).
        _timelineFrames.Clear();

        foreach (var item in TimelineBuilder.BuildFrameItems(chain))
            _timelineFrames.Add(item);
        _timelineEffectivePps = TimelineBuilder.ComputeEffectivePixelsPerSecond(chain);

        // Populate frame thumbnails (texture crop tinted by the frame's effective color, no shapes).
        // Resolve all effective colors in one O(n) pass here — on data change, never in the playback
        // loop. Cells whose effective color is unchanged hit the ThumbnailService cache and skip re-render.
        if (chain is not null)
        {
            var colors = EffectiveFrameColor.ResolveAll(chain.Frames);
            for (int i = 0; i < chain.Frames.Count && i < _timelineFrames.Count; i++)
                _timelineFrames[i].Thumbnail = _thumbnailService.GetFrameThumbnail(chain.Frames[i], colors[i], 22, 18);
        }

        // Cells were just recreated, so no frame is current until UpdateTimelineScrubber runs.
        _currentTimelineFrameIndex = -1;
    }

    private void ApplyScrubberOffsetFromPlayback(int frameIndex)
    {
        if (frameIndex < 0 || frameIndex >= _timelineFrames.Count) return;
        double elapsed = PreviewCtrl.Playback.FrameElapsed;
        double travelWidth = Math.Max(0, _timelineFrames[frameIndex].Width - TimelineFrameVm.PlayheadWidth);
        _timelineFrames[frameIndex].ScrubberOffset = Math.Min(elapsed * _timelineEffectivePps, travelWidth);
    }

    // ── Multi-select group preview timeline (#576) ──────────────────────────────

    /// <summary>
    /// Rebuilds the per-chain track rows from <see cref="PreviewControl.GroupTracks"/>. Fired by
    /// <see cref="PreviewControl.GroupTracksChanged"/> — only when the group's chain membership
    /// actually changes, not on every render or unrelated selection change.
    /// </summary>
    private void RefreshGroupTimelineTracks()
    {
        _groupTimelineTracks.Clear();

        foreach (var (chain, _) in PreviewCtrl.GroupTracks)
        {
            var track = new ChainTimelineTrackVm(chain, TimelineBuilder.BuildFrameItems(chain));
            if (chain.Frames.Count > 0)
            {
                var colors = EffectiveFrameColor.ResolveAll(chain.Frames);
                for (int i = 0; i < chain.Frames.Count && i < track.Frames.Count; i++)
                    track.Frames[i].Thumbnail = _thumbnailService.GetFrameThumbnail(chain.Frames[i], colors[i], 22, 18);
            }
            _groupTimelineTracks.Add(track);
        }

        RefreshGroupTimelineScrubbers();
    }

    /// <summary>
    /// Updates every track's current-frame highlight and sub-frame playhead offset from its own
    /// PlaybackController. Fired by <see cref="PreviewControl.GroupPlaybackTicked"/> on every
    /// group-mode timer tick and after a per-track scrub.
    /// </summary>
    private void RefreshGroupTimelineScrubbers()
    {
        foreach (var (chain, playback) in PreviewCtrl.GroupTracks)
        {
            var track = _groupTimelineTracks.FirstOrDefault(t => ReferenceEquals(t.Chain, chain));
            if (track is null || track.Frames.Count == 0) continue;

            int idx = Math.Clamp(playback.CurrentFrameIndex, 0, track.Frames.Count - 1);
            for (int i = 0; i < track.Frames.Count; i++)
                track.Frames[i].IsCurrent = i == idx;

            double pps = TimelineBuilder.ComputeEffectivePixelsPerSecond(chain);
            double travelWidth = Math.Max(0, track.Frames[idx].Width - TimelineFrameVm.PlayheadWidth);
            track.Frames[idx].ScrubberOffset = Math.Min(playback.FrameElapsed * pps, travelWidth);
        }
    }

    private (ChainTimelineTrackVm Track, ItemsControl FramesList)? FindGroupTrackAndFramesList(PointerEventArgs e)
    {
        if (e.Source is not Avalonia.Visual source) return null;
        var self = new[] { source }.Concat(source.GetVisualAncestors());
        var framesList = self.OfType<ItemsControl>().FirstOrDefault(ic => ic.Name == "TrackFramesList");
        return framesList?.DataContext is ChainTimelineTrackVm track ? (track, framesList) : null;
    }

    private void OnGroupTimelinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(GroupTimelineTracks).Properties.IsLeftButtonPressed) return;
        var hit = FindGroupTrackAndFramesList(e);
        if (hit is null) return;

        _isGroupTimelineScrubbing = true;
        _groupScrubTrack = hit.Value.Track;
        _groupScrubFramesList = hit.Value.FramesList;
        e.Pointer.Capture(GroupTimelineTracks);
        ScrubGroupTimelineToPointer(e);
    }

    private void OnGroupTimelinePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isGroupTimelineScrubbing) ScrubGroupTimelineToPointer(e);
    }

    private void OnGroupTimelinePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isGroupTimelineScrubbing) return;
        _isGroupTimelineScrubbing = false;
        _groupScrubTrack = null;
        _groupScrubFramesList = null;
        e.Pointer.Capture(null);
    }

    /// <summary>
    /// Scrubs the track captured at press time to the pointer's position within its own frames
    /// list. Position is resolved against the captured <see cref="_groupScrubFramesList"/>, not
    /// <c>e.Source</c>, since pointer capture keeps routing move/release events to
    /// <c>GroupTimelineTracks</c> regardless of which row is physically under the cursor.
    /// </summary>
    private void ScrubGroupTimelineToPointer(PointerEventArgs e)
    {
        var track = _groupScrubTrack;
        var framesList = _groupScrubFramesList;
        if (track is null || framesList is null || track.Frames.Count == 0) return;

        double contentX = e.GetPosition(framesList).X;
        var widths = new double[track.Frames.Count];
        for (int i = 0; i < widths.Length; i++) widths[i] = track.Frames[i].Width;

        var result = TimelineScrubMapper.Resolve(contentX, widths);
        // Fires GroupPlaybackTicked synchronously, which refreshes every track's playhead.
        PreviewCtrl.ScrubGroupTrack(track.Chain, result.FrameIndex, result.Fraction);
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
            // Right-clicking a node that's already part of the current multi-selection must
            // leave the whole selection intact (Explorer-style) so the context menu acts on the
            // whole group. Only collapse to just this node when it isn't already selected.
            if (tvi?.DataContext is TreeNodeVm vm && !AnimTree.SelectedItems.Contains(vm))
                AnimTree.SelectedItem = vm;
        }
        else if (props.IsLeftButtonPressed && e.ClickCount == 1)
        {
            // Arm a frame-drag candidate. Snapshot the selection BEFORE the TreeView mutates
            // it on press, so dragging a frame that is part of a multi-selection can move the
            // whole set. Tunnel phase runs ahead of the TreeView's own selection handling.
            if (e.Source is Control src &&
                src.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext
                    is TreeNodeVm { Data: AnimationFrameSave frame })
            {
                ClearChainDragCandidate();
                _frameDragCandidate = frame;
                _frameDragPressPoint = e.GetPosition(AnimTree);
                _frameDragPressArgs = e;
                _frameDragSelectionSnapshot = new List<object>(_selectedState.SelectedNodes);

                // Pressing a frame that's part of a frame multi-selection (no modifiers) must
                // not collapse the selection — otherwise a drag would only move one frame. Mark
                // the press handled to suppress the TreeView's select-on-press, capture so the
                // move/release still arrive here, and defer the single-select to release if no
                // drag happens. Ctrl/Shift presses fall through to normal selection editing.
                bool noModifiers = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) == 0;
                if (noModifiers &&
                    FrameDropResolver.IsFrameMultiSelectionContaining(_frameDragSelectionSnapshot, frame))
                {
                    _pendingSingleSelectFrame = frame;
                    e.Pointer.Capture(AnimTree);
                    e.Handled = true;
                }
                else
                {
                    _pendingSingleSelectFrame = null;
                }
            }
            else if (e.Source is Control chainSrc &&
                chainSrc.FindAncestorOfType<TreeViewItem>(includeSelf: true)?.DataContext
                    is TreeNodeVm { Data: AnimationChainSave chain })
            {
                // Arm a chain-drag candidate. Snapshot the selection BEFORE the TreeView mutates
                // it on press, so dragging a chain that is part of a multi-selection can move
                // the whole set. Tunnel phase runs ahead of the TreeView's own selection handling.
                ClearFrameDragCandidate();
                _chainDragCandidate = chain;
                _chainDragPressPoint = e.GetPosition(AnimTree);
                _chainDragPressArgs = e;
                _chainDragSelectionSnapshot = new List<object>(_selectedState.SelectedNodes);

                // Pressing a chain that's part of a chain multi-selection (no modifiers) must
                // not collapse the selection — otherwise a drag would only move one chain. Mark
                // the press handled to suppress the TreeView's select-on-press, capture so the
                // move/release still arrive here, and defer the single-select to release if no
                // drag happens. Ctrl/Shift presses fall through to normal selection editing.
                bool noModifiers = (e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Shift)) == 0;
                if (noModifiers &&
                    ChainDropResolver.IsChainMultiSelectionContaining(_chainDragSelectionSnapshot, chain))
                {
                    _pendingSingleSelectChain = chain;
                    e.Pointer.Capture(AnimTree);
                    e.Handled = true;
                }
                else
                {
                    _pendingSingleSelectChain = null;
                }
            }
            else
            {
                ClearFrameDragCandidate();
                ClearChainDragCandidate();
            }
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
            AddShapeReorderItems(rect, _objectFinder.GetAnimationFrameContaining(rect));
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
            AddSeparator();
            AddMenuItem("Copy",  () => _ = HandleCopyAsync());
            AddMenuItem("Cut",   () => _ = HandleCutAsync());
            AddMenuItem("Paste", () => _ = HandlePasteAsync());
            AddMenuItem("Duplicate", HandleDuplicate);
            AddSeparator();
            AddMenuItem("Rename…", () => BeginInlineRename(vm!, rect.Name));
            AddSeparator();
            AddMenuItem("Delete Rectangle", HandleDelete);
        }
        else if (vm?.Data is CircleSave circle)
        {
            AddShapeReorderItems(circle, _objectFinder.GetAnimationFrameContaining(circle));
            AddMenuItem("Copy",  () => _ = HandleCopyAsync());
            AddMenuItem("Cut",   () => _ = HandleCutAsync());
            AddMenuItem("Paste", () => _ = HandlePasteAsync());
            AddMenuItem("Duplicate", HandleDuplicate);
            AddSeparator();
            AddMenuItem("Rename…", () => BeginInlineRename(vm!, circle.Name));
            AddSeparator();
            AddMenuItem("Delete Circle", HandleDelete);
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
            AddMenuItem("Cut",   () => _ = HandleCutAsync());
            AddMenuItem("Paste", () => _ = HandlePasteAsync());
            if (chain2 is not null)
                AddMenuItem("Duplicate", HandleDuplicate);
            AddSeparator();
            AddMenuItem("View Texture in Explorer", () => ViewTextureInExplorer(frame2));
            AddSeparator();
            AddMenuItem("Delete Frame", HandleDelete);
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
            AddMenuItem("Copy",  () => _ = HandleCopyAsync());
            AddMenuItem("Cut",   () => _ = HandleCutAsync());
            AddMenuItem("Paste", () => _ = HandlePasteAsync());
            AddSubMenu("Duplicate",
                ("Original",        HandleDuplicate),
                ("Flip Horizontal", () => HandleDuplicateChainsFlip(chain, flipH: true, flipV: false)),
                ("Flip Vertical",   () => HandleDuplicateChainsFlip(chain, flipH: false, flipV: true)));
            AddSeparator();
            AddMenuItem("Adjust Offsets…", () => _ = AskAdjustOffsetsAsync(chain));
            AddMenuItem("Rename…",          () => BeginInlineRenameSelected(chain));
            AddSeparator();
            AddMenuItem("Delete Animation", HandleDelete);
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

    // Adds the four reorder items (To Top / Up / Down / To Bottom) for a shape,
    // guarded by the shape's position within its frame's combined shape list — the
    // same convention the frame menu uses. No-op when the frame has one shape or less.
    private void AddShapeReorderItems(object shape, AnimationFrameSave? frame)
    {
        var shapes = frame?.ShapesSave?.Shapes;
        if (shapes is null || shapes.Count <= 1) return;
        int  index   = shapes.IndexOf(shape);
        bool isFirst = index == 0;
        bool isLast  = index == shapes.Count - 1;
        if (!isFirst) AddMenuItem("^^ Move To Top",   () => _appCommands.MoveShapeToTop(shape, frame!));
        if (!isFirst) AddMenuItem("^  Move Up",        () => _appCommands.MoveShape(shape, frame!, -1));
        if (!isLast)  AddMenuItem("v  Move Down",      () => _appCommands.MoveShape(shape, frame!, +1));
        if (!isLast)  AddMenuItem("vv Move To Bottom", () => _appCommands.MoveShapeToBottom(shape, frame!));
        AddSeparator();
    }

    private void AddMenuItem(string header, Action onClick)
    {
        var item = new MenuItem { Header = header };
        item.Click += (_, _) => onClick();
        AnimTree.ContextMenu!.Items.Add(item);
    }

    private void AddSeparator() =>
        AnimTree.ContextMenu!.Items.Add(new Separator());

    private void AddSubMenu(string header, params (string Header, Action OnClick)[] children)
    {
        var parent = new MenuItem { Header = header };
        foreach (var (childHeader, onClick) in children)
        {
            var child = new MenuItem { Header = childHeader };
            child.Click += (_, _) => onClick();
            parent.Items.Add(child);
        }
        AnimTree.ContextMenu!.Items.Add(parent);
    }

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
        PropFlipD.IsCheckedChanged += (_, _) => ApplyFrameFlip();
        PropFrameLen.ValueChanged  += (_, _) => ApplyFrameLen();
        PropRelX.ValueChanged      += (_, _) => ApplyFrameRelative();
        PropRelY.ValueChanged      += (_, _) => ApplyFrameRelative();
        // Color/alpha channels commit one undo entry on edit completion (focus loss / Enter), not
        // per keystroke — NumericUpDown raises ValueChanged on every keypress while typing (#445).
        PropRed.LostFocus          += (_, _) => ApplyFrameColor();
        PropGreen.LostFocus        += (_, _) => ApplyFrameColor();
        PropBlue.LostFocus         += (_, _) => ApplyFrameColor();
        PropAlpha.LostFocus        += (_, _) => ApplyFrameAlpha();
        PropRed.KeyDown            += (_, e) => CommitColorChannelOnEnter(e);
        PropGreen.KeyDown          += (_, e) => CommitColorChannelOnEnter(e);
        PropBlue.KeyDown           += (_, e) => CommitColorChannelOnEnter(e);
        PropAlpha.KeyDown          += (_, e) => { if (e.Key == Key.Enter) ApplyFrameAlpha(); };
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
        PropTextureName.KeyDown    += (_, e) => { if (e.Key == Key.Enter) ApplyTextureName(); };
        PropTextureBrowseBtn.Click += async (_, _) => await BrowseForFrameTexture();
    }

    private void ApplyTextureName()
    {
        if (_suppressPropRefresh) return;
        var frame = _selectedState.SelectedFrame;
        if (frame is null) return;

        var inputText = PropTextureName.Text?.Trim() ?? string.Empty;

        // Clearing the field clears the frame's texture and blanks the wireframe, rather than being
        // ignored (which left the old texture showing). Only act when there's a texture to clear, so
        // re-committing an already-empty field adds no spurious undo entry. SetFrameTextureName doesn't
        // refresh the wireframe on its own, so blank it explicitly (DetermineTexturePath now resolves
        // to no texture for the selected frame).
        if (string.IsNullOrEmpty(inputText))
        {
            if (!string.IsNullOrEmpty(frame.TextureName))
            {
                _appCommands.SetFrameTextureName(frame, string.Empty);
                WireframeCtrl.RefreshAll();
                RefreshPropertyPanel();
            }
            return;
        }

        var currentDisplay = TexturePathHelper.ComputeDisplayPath(frame.TextureName, _projectManager.FileName);
        if (inputText == currentDisplay) return;

        string achxFolder = string.IsNullOrEmpty(_projectManager.FileName)
            ? string.Empty
            : (Path.GetDirectoryName(_projectManager.FileName) ?? string.Empty);

        string absolutePath = TexturePathHelper.ResolveDisplayPath(inputText, achxFolder);
        string storePath    = TexturePathHelper.ComputeStorePath(absolutePath, achxFolder);

        CommitFrameTexture(frame, storePath, absolutePath);
    }

    /// <summary>
    /// Displays <paramref name="absolutePath"/> and, only if it decodes, commits
    /// <paramref name="storePath"/> as the frame's texture name. If the image can't be loaded
    /// (corrupt/undecodable/missing — see issue #479), the name is left untouched so no broken
    /// reference reaches the undo stack or the saved .achx, the wireframe is restored to the
    /// frame's current texture, and a non-fatal status message is shown.
    /// </summary>
    private void CommitFrameTexture(AnimationFrameSave frame, string storePath, string absolutePath)
    {
        if (!WireframeCtrl.LoadTexture(absolutePath))
        {
            ShowStatusMessage($"⚠ Could not load image: {absolutePath}", isError: true);
            WireframeCtrl.RefreshAll();   // restore the display to the frame's current texture
            return;
        }

        _appCommands.SetFrameTextureName(frame, storePath);
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
            if (TextureCopyDecider.ShouldPromptToCopy(pickedPath, achxFolder, _appState.ProjectFolder))
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
                                CommitFrameTexture(frame, TexturePathHelper.ComputeStorePath(capturedDest, achxFolder), capturedDest);
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

        CommitFrameTexture(frame, storePath, resolvedAbsPath);
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

    /// <summary>
    /// Shows <paramref name="values"/>'s shared value in <paramref name="control"/>, or blanks it
    /// with a "(mixed)" placeholder when the selected frames disagree on that property.
    /// </summary>
    private static void SetValueOrMixed(NumericUpDown control, IReadOnlyList<decimal> values)
    {
        if (values.Distinct().Count() == 1)
        {
            control.Value = values[0];
            control.PlaceholderText = string.Empty;
        }
        else
        {
            control.Value = null;
            control.PlaceholderText = "(mixed)";
        }
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
                // When multiple frames are selected and disagree on a property, show that field
                // blank with a "(mixed)" placeholder instead of one frame's value (issue #571) —
                // editing it then applies the new value to every selected frame; leaving it blank
                // applies nothing (see the `!PropXxx.Value.HasValue` guards in the Apply* methods).
                var frames = _selectedState.SelectedFrames;

                bool flipHMixed = frames.Select(f => f.FlipHorizontal).Distinct().Count() > 1;
                bool flipVMixed = frames.Select(f => f.FlipVertical).Distinct().Count() > 1;
                bool flipDMixed = frames.Select(f => f.FlipDiagonal).Distinct().Count() > 1;
                PropFlipH.IsChecked = flipHMixed ? null : frame.FlipHorizontal;
                PropFlipV.IsChecked = flipVMixed ? null : frame.FlipVertical;
                PropFlipD.IsChecked = flipDMixed ? null : frame.FlipDiagonal;

                SetValueOrMixed(PropFrameLen, frames.Select(f => (decimal)f.FrameLength).ToList());
                SetValueOrMixed(PropRelX, frames.Select(f => (decimal)f.RelativeX).ToList());
                SetValueOrMixed(PropRelY, frames.Select(f => (decimal)f.RelativeY).ToList());

                bool redMixed   = frames.Select(f => f.Red).Distinct().Count() > 1;
                bool greenMixed = frames.Select(f => f.Green).Distinct().Count() > 1;
                bool blueMixed  = frames.Select(f => f.Blue).Distinct().Count() > 1;
                bool alphaMixed = frames.Select(f => f.Alpha).Distinct().Count() > 1;
                bool opMixed    = frames.Select(f => f.ColorOperation).Distinct().Count() > 1;

                PropRed.Value   = redMixed   ? null : (frame.Red.HasValue   ? frame.Red.Value   : (decimal?)null);
                PropGreen.Value = greenMixed ? null : (frame.Green.HasValue ? frame.Green.Value : (decimal?)null);
                PropBlue.Value  = blueMixed  ? null : (frame.Blue.HasValue  ? frame.Blue.Value  : (decimal?)null);
                PropAlpha.Value = alphaMixed ? null : (frame.Alpha.HasValue ? frame.Alpha.Value : (decimal?)null);

                // Ghost the sticky effective value in each blank field: an omitted channel holds
                // whatever an earlier frame last set (climbing back), or the operation's identity
                // (Add → 0, else 255) when nothing ever set it. This makes a blank field read as the
                // value a runtime actually applies, instead of implying "reset to default". Mixed
                // takes priority — it means the selection disagrees, not that a value is inherited.
                var chain = _selectedState.SelectedChain;
                int frameIndex = chain?.Frames.IndexOf(frame) ?? -1;
                var effective = frameIndex >= 0
                    ? EffectiveFrameColor.Resolve(chain!.Frames, frameIndex)
                    : default;
                int rgbDefault = EffectiveFrameColor.ChannelDefault(effective.Operation);
                PropRed.PlaceholderText   = redMixed   ? "(mixed)" : (effective.Red   ?? rgbDefault).ToString();
                PropGreen.PlaceholderText = greenMixed ? "(mixed)" : (effective.Green ?? rgbDefault).ToString();
                PropBlue.PlaceholderText  = blueMixed  ? "(mixed)" : (effective.Blue  ?? rgbDefault).ToString();
                PropAlpha.PlaceholderText = alphaMixed ? "(mixed)" : (effective.Alpha ?? 255).ToString();

                if (opMixed)
                {
                    PropColorMode.SelectedIndex = -1;
                    PropColorMode.PlaceholderText = "(mixed)";
                }
                else if (frame.ColorOperation is ColorOperation op)
                {
                    PropColorMode.SelectedIndex = op == ColorOperation.Multiply ? 1 : 2;
                }
                else if (effective.Operation is ColorOperation inherited)
                {
                    // Unset here but inherited from an earlier frame — ghost it as a combo placeholder
                    // (SelectedIndex -1 shows PlaceholderText) so the combo matches the sticky preview.
                    PropColorMode.SelectedIndex = -1;
                    PropColorMode.PlaceholderText = $"{inherited} (inherited)";
                }
                else
                {
                    PropColorMode.SelectedIndex = 0; // None
                }
                PropTextureName.Text = TexturePathHelper.ComputeDisplayPath(
                    frame.TextureName, _projectManager.FileName);

                var (bmpW, bmpH) = WireframeCtrl.BitmapSize;
                if (bmpW > 0 && bmpH > 0)
                {
                    SetValueOrMixed(PropPixelX, frames.Select(f => (decimal)FrameDisplayValues.GetPixelX(f, bmpW)).ToList());
                    SetValueOrMixed(PropPixelY, frames.Select(f => (decimal)FrameDisplayValues.GetPixelY(f, bmpH)).ToList());
                    SetValueOrMixed(PropPixelW, frames.Select(f => (decimal)FrameDisplayValues.GetPixelWidth(f, bmpW)).ToList());
                    SetValueOrMixed(PropPixelH, frames.Select(f => (decimal)FrameDisplayValues.GetPixelHeight(f, bmpH)).ToList());
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
        var frames = _selectedState.SelectedFrames;
        if (frames.Count == 0) return;
        // ToggleButton.IsChecked is already nullable: null means the checkbox is showing the
        // mixed/indeterminate state (the selection disagrees and the user didn't touch it), so
        // it passes straight through as "leave this axis untouched".
        _appCommands.SetFrameFlip(frames, PropFlipH.IsChecked, PropFlipV.IsChecked, PropFlipD.IsChecked);
    }

    private void ApplyFrameLen()
    {
        if (_suppressPropRefresh) return;
        var frames = _selectedState.SelectedFrames;
        if (frames.Count == 0 || !PropFrameLen.Value.HasValue) return;
        _appCommands.SetFrameLength(frames, (float)PropFrameLen.Value.Value);
    }

    private void ApplyFrameRelative()
    {
        if (_suppressPropRefresh) return;
        var frames = _selectedState.SelectedFrames;
        if (frames.Count == 0) return;
        // A null axis here only ever means "still showing (mixed), not edited" — RelativeX/Y have no
        // legitimate null/cleared state — so it's safe to apply just the axis the user touched and
        // leave the other axis alone per-frame (see SetFrameRelative for why this is unambiguous).
        float? relX = PropRelX.Value.HasValue ? (float)PropRelX.Value.Value : null;
        float? relY = PropRelY.Value.HasValue ? (float)PropRelY.Value.Value : null;
        if (relX is null && relY is null) return;
        _appCommands.SetFrameRelative(frames, relX, relY);
    }

    private void ApplyFrameColor()
    {
        if (_suppressPropRefresh) return;
        var frames = _selectedState.SelectedFrames;
        if (frames.Count == 0) return;
        // A blank NumericUpDown (null Value) means the channel is unset and is omitted from the .achx.
        // Note: with a multi-selection, a channel that is still showing its "(mixed)" placeholder
        // also reads as null here, so it gets applied (cleared) to every selected frame just like an
        // explicit clear would — there's no way to tell "never touched" apart from "cleared on purpose"
        // from the control's Value alone. Prefer not leaving a mixed color panel blank across an edit
        // if that distinction matters; see PR notes for the known limitation.
        static int? ToChannel(decimal? v) => v.HasValue ? (int)v.Value : null;
        _appCommands.SetFrameColor(frames, ToChannel(PropRed.Value), ToChannel(PropGreen.Value), ToChannel(PropBlue.Value));
    }

    private void CommitColorChannelOnEnter(KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyFrameColor();
    }

    private void ApplyFrameAlpha()
    {
        if (_suppressPropRefresh) return;
        var frames = _selectedState.SelectedFrames;
        if (frames.Count == 0) return;
        // A blank NumericUpDown (null Value) means alpha is unset and is omitted from the .achx.
        _appCommands.SetFrameAlpha(frames, PropAlpha.Value.HasValue ? (int)PropAlpha.Value.Value : null);
    }

    private void ApplyFrameColorOperation()
    {
        if (_suppressPropRefresh) return;
        var frames = _selectedState.SelectedFrames;
        if (frames.Count == 0) return;
        // ComboBox order: 0 = None (null), 1 = Multiply, 2 = Add.
        ColorOperation? operation = PropColorMode.SelectedIndex switch
        {
            1 => ColorOperation.Multiply,
            2 => ColorOperation.Add,
            _ => null,
        };
        _appCommands.SetFrameColorOperation(frames, operation);
    }

    private void ApplyFramePixelCoords()
    {
        if (_suppressPropRefresh) return;
        var frames = _selectedState.SelectedFrames;
        if (frames.Count == 0) return;
        var (bmpW, bmpH) = WireframeCtrl.BitmapSize;
        if (bmpW <= 0 || bmpH <= 0) return;
        // A null component here only ever means "still showing (mixed), not edited" — the pixel
        // region has no legitimate null/cleared state — so it's safe to apply just the component(s)
        // the user touched and leave the rest alone per-frame (see SetFramePixelRegion).
        int? x = PropPixelX.Value.HasValue ? (int)PropPixelX.Value.Value : null;
        int? y = PropPixelY.Value.HasValue ? (int)PropPixelY.Value.Value : null;
        int? w = PropPixelW.Value.HasValue ? (int)PropPixelW.Value.Value : null;
        int? h = PropPixelH.Value.HasValue ? (int)PropPixelH.Value.Value : null;
        if (x is null && y is null && w is null && h is null) return;
        _appCommands.SetFramePixelRegion(frames, x, y, w, h, bmpW, bmpH);
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

    // ── Timeline transport: Play/Pause button + click-drag scrubbing (#432) ───

    private bool _isTimelineScrubbing;

    private void WireTimelineTransport()
    {
        PlayPauseBtn.Click += (_, _) => PreviewCtrl.TogglePlayPause();
        PreviewCtrl.IsPlayingChanged += UpdatePlayPauseIcon;
        UpdatePlayPauseIcon(PreviewCtrl.IsPlaying);

        // Handle on the scrub surface in both phases (handledEventsToo) so the gesture works
        // even if a child inside the timeline marks the pointer event handled first.
        TimelineScrubSurface.AddHandler(InputElement.PointerPressedEvent, OnTimelinePointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        TimelineScrubSurface.AddHandler(InputElement.PointerMovedEvent, OnTimelinePointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        TimelineScrubSurface.AddHandler(InputElement.PointerReleasedEvent, OnTimelinePointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);

        // Multi-select group preview timeline (#576) — same tunnel+bubble pattern, scoped to
        // whichever track row's frames list is under the pointer at press time.
        GroupTimelineTracks.AddHandler(InputElement.PointerPressedEvent, OnGroupTimelinePointerPressed,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        GroupTimelineTracks.AddHandler(InputElement.PointerMovedEvent, OnGroupTimelinePointerMoved,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
        GroupTimelineTracks.AddHandler(InputElement.PointerReleasedEvent, OnGroupTimelinePointerReleased,
            RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void UpdatePlayPauseIcon(bool isPlaying)
    {
        PlayPauseIcon.Path = isPlaying
            ? "avares://AnimationEditor/Assets/icons/svg/IconPause.svg"
            : "avares://AnimationEditor/Assets/icons/svg/IconPlay.svg";
        ToolTip.SetTip(PlayPauseBtn, isPlaying ? "Pause (Space)" : "Play (Space)");
    }

    private void OnTimelinePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(TimelineScrubSurface).Properties.IsLeftButtonPressed) return;
        _isTimelineScrubbing = true;
        e.Pointer.Capture(TimelineScrubSurface);
        ScrubTimelineToPointer(e);
    }

    private void OnTimelinePointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isTimelineScrubbing) ScrubTimelineToPointer(e);
    }

    private void OnTimelinePointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_isTimelineScrubbing) return;
        _isTimelineScrubbing = false;
        e.Pointer.Capture(null);
    }

    private void ScrubTimelineToPointer(PointerEventArgs e)
    {
        if (_timelineFrames.Count == 0) return;

        // Position relative to the strip's content (the ItemsControl) already accounts for scroll.
        double contentX = e.GetPosition(TimelineStrip).X;
        var widths = new double[_timelineFrames.Count];
        for (int i = 0; i < widths.Length; i++)
            widths[i] = _timelineFrames[i].Width;

        var result = TimelineScrubMapper.Resolve(contentX, widths);
        PreviewCtrl.ScrubToFrame(result.FrameIndex, result.Fraction);

        UpdateTimelineScrubber(result.FrameIndex);
        if (result.FrameIndex >= 0 && result.FrameIndex < _timelineFrames.Count)
        {
            double travelWidth = Math.Max(0, _timelineFrames[result.FrameIndex].Width - TimelineFrameVm.PlayheadWidth);
            _timelineFrames[result.FrameIndex].ScrubberOffset = Math.Min(result.LocalX, travelWidth);
        }
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

        // Opening an achx always returns to the editor pane if a PNG tab was showing.
        ShowAchxPane();

        // Save the leaving tab's undo history and in-memory model before a different file takes over.
        // PNG tabs carry neither, so only capture when leaving the achx editor.
        var leavingTab = _tabManager.ActiveTab;
        if (leavingTab is { Kind: TabKind.Achx })
        {
            leavingTab.UndoSnapshot = _undoManager.TakeSnapshot();
            _appCommands.CaptureTabEditorState(leavingTab);
        }

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
                await _appCommands.ActivateTabContentAsync(arrivedTab!);
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

    // ── Canvas colors (background + guide line) ────────────────────────────────
    // Both live in the Settings → Canvas Colors section; see SettingsWindowBuilder.

    /// <summary>Pushes the persisted canvas-color overrides onto the canvases affected by each.</summary>
    private void ApplyPersistedCanvasColors()
    {
        WireframeCtrl.CanvasBackgroundOverride = _appSettings.CanvasBackgroundArgb;
        PreviewCtrl.CanvasBackgroundOverride   = _appSettings.CanvasBackgroundArgb;
        PreviewCtrl.GuideLineOverride          = _appSettings.GuideLineArgb;
    }

    private void SetCanvasBackground(uint? argb)
    {
        _appSettings.CanvasBackgroundArgb = argb;
        WireframeCtrl.CanvasBackgroundOverride = argb;
        PreviewCtrl.CanvasBackgroundOverride   = argb;
        SaveSettingsFile();
    }

    private void SetGuideLineColor(uint? argb)
    {
        _appSettings.GuideLineArgb = argb;
        PreviewCtrl.GuideLineOverride = argb;
        SaveSettingsFile();
    }

    private static uint ToArgb(SKColor color) =>
        (uint)((color.Alpha << 24) | (color.Red << 16) | (color.Green << 8) | color.Blue);

    /// <summary>
    /// Opens a color picker seeded with <paramref name="seed"/> and returns the chosen color
    /// (forced opaque) as a packed <c>0xAARRGGBB</c> value, or <c>null</c> if cancelled.
    /// </summary>
    private async Task<uint?> PickCustomColorAsync(string title, Color seed)
    {
        var colorView = new ColorView { Color = seed };

        var okBtn     = new Button { Content = "OK",     MinWidth = 80 };
        var cancelBtn = new Button { Content = "Cancel", MinWidth = 80 };
        var buttons = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Horizontal,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Spacing = 8,
            Margin = new Avalonia.Thickness(0, 12, 0, 0),
            Children = { okBtn, cancelBtn },
        };

        var root = new DockPanel { Margin = new Avalonia.Thickness(12) };
        DockPanel.SetDock(buttons, Dock.Bottom);
        root.Children.Add(buttons);
        root.Children.Add(colorView);

        var dialog = new Window
        {
            Title = title,
            SizeToContent = SizeToContent.WidthAndHeight,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Content = root,
        };
        okBtn.Click     += (_, _) => dialog.Close(true);
        cancelBtn.Click += (_, _) => dialog.Close(false);

        if (!await dialog.ShowDialog<bool>(this))
            return null;

        // Force opaque — a translucent canvas fill/guide would composite unpredictably.
        return 0xFF000000u | (colorView.Color.ToUInt32() & 0x00FFFFFFu);
    }

    private Task<uint?> PickCustomCanvasBackgroundAsync()
    {
        var themed = CanvasPalette.For(ActualThemeVariant != ThemeVariant.Light).Background;
        var seed = _appSettings.CanvasBackgroundArgb is uint v
            ? Color.FromUInt32(v)
            : Color.FromArgb(themed.Alpha, themed.Red, themed.Green, themed.Blue);
        return PickCustomColorAsync("Canvas Background", seed);
    }

    private Task<uint?> PickCustomGuideLineColorAsync()
    {
        var themed = CanvasPalette.For(ActualThemeVariant != ThemeVariant.Light).GuideLine;
        var seed = _appSettings.GuideLineArgb is uint v
            ? Color.FromUInt32(v)
            : Color.FromArgb(255, themed.Red, themed.Green, themed.Blue);
        return PickCustomColorAsync("Guide Line Color", seed);
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
        // Errors route to the prominent top-centre banner so they can't be missed; the thin
        // bottom status bar (low-contrast, easy to overlook) is reserved for informational text.
        if (isError)
        {
            ShowErrorBanner(text);
            return;
        }

        StatusMessage.Text = text;
        StatusMessage.Foreground = ThemedBrush("InkMid");
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

    // ── Error banner ──────────────────────────────────────────────────────────

    private DispatcherTimer? _errorBannerTimer;

    private void InitErrorBanner()
    {
        ErrorBannerDismissBtn.Click += (_, _) => HideErrorBanner();
    }

    /// <summary>
    /// Shows the prominent top-centre error banner. Auto-dismisses after 8s (longer than the
    /// informational status bar's 5s — errors deserve more dwell time) or on manual dismiss.
    /// </summary>
    private void ShowErrorBanner(string text)
    {
        // The banner draws its own ⚠ icon, so drop a leading warning glyph that callers prepend
        // (many ShowStatusMessage sites use "⚠ ..."), otherwise the icon shows twice.
        ErrorBannerText.Text = text.TrimStart('⚠', ' ');
        ErrorBanner.IsVisible = true;

        _errorBannerTimer?.Stop();
        _errorBannerTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(8) };
        _errorBannerTimer.Tick += (_, _) => HideErrorBanner();
        _errorBannerTimer.Start();
    }

    private void HideErrorBanner()
    {
        _errorBannerTimer?.Stop();
        ErrorBanner.IsVisible = false;
        ErrorBannerText.Text = string.Empty;
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
            else if (e.Key == Key.X && HasCommandModifier(e.KeyModifiers))
            {
                if (IsTextInputFocused()) return;
                e.Handled = true;
                _ = HandleCutAsync();
            }
            else if (e.Key == Key.V && HasCommandModifier(e.KeyModifiers))
            {
                if (IsTextInputFocused()) return;
                e.Handled = true;
                _ = HandlePasteAsync();
            }
            else if (e.Key == Key.D && HasCommandModifier(e.KeyModifiers))
            {
                if (IsTextInputFocused()) return;
                e.Handled = true;
                HandleDuplicate();
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
                      ?? (_selectedState.SelectedShape is { } ss
                              ? TreeBuilder.FindNodeForData(_treeRoots, ss) : null)
                      ?? (_selectedState.SelectedFrame is { } sf
                              ? TreeBuilder.FindNodeForData(_treeRoots, sf) : null)
                      ?? (_selectedState.SelectedChain is { } sc
                              ? TreeBuilder.FindNodeForData(_treeRoots, sc) : null);

                if (vm is not null)
                {
                    // Frame nodes are intentionally not renameable: a frame's identity is its
                    // index, so its label is the computed positional "Frame N" (see TreeBuilder).
                    if (vm.Data is AnimationChainSave chain)
                        BeginInlineRename(vm, chain.Name);
                    else if (vm.Data is AARectSave rect)
                        BeginInlineRename(vm, rect.Name);
                    else if (vm.Data is CircleSave circle)
                        BeginInlineRename(vm, circle.Name);
                }
                // F2 is rename-only. Render diagnostics moved to F3 / Help ▸ Show Render Diagnostics
                // (the old F2 fallback was unreachable — a tree node is essentially always selected).
            }
            else if (e.Key == Key.F3)
            {
                e.Handled = true;
                ToggleDiagnostics();
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
            else if (e.Key == Key.Space)
            {
                if (IsTextInputFocused()) return;
                // Let a focused button receive Space to activate itself rather than hijacking it.
                if (FocusManager?.GetFocusedElement() is Button) return;
                e.Handled = true;
                PreviewCtrl.TogglePlayPause();
            }
            else if ((e.Key == Key.Up || e.Key == Key.Down) &&
                     e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                e.Handled = true;
                _altMenuActivationSuppressor.ArmFromAltArrowReorder();
                int delta = e.Key == Key.Up ? -1 : +1;
                _appCommands.HandleReorder(delta);
                // Restore focus to the tree — reorder can cause Avalonia to shift focus away, which
                // would let F2 hit the wrong target for a subsequent rename.
                Dispatcher.UIThread.Post(() => AnimTree.Focus(), DispatcherPriority.Background);
                if (_selectedState.SelectedFrame is not null)
                    ShowStatusMessage("Frame labels updated to reflect new positions");
            }
        }), RoutingStrategies.Tunnel);

        // Avalonia activates the title-bar menu on Alt KeyUp. Alt+Arrow reorder handles only
        // the arrow KeyDown, so consume the matching Alt release when we armed suppression.
        AddHandler(KeyUpEvent, (EventHandler<KeyEventArgs>)((_, e) =>
        {
            if (e.Handled) return;
            if (e.Key is Key.LeftAlt or Key.RightAlt &&
                _altMenuActivationSuppressor.TryConsumeIfArmed())
            {
                e.Handled = true;
            }
        }), RoutingStrategies.Tunnel);
    }

    // Returns true when a text-editing control (TextBox) owns keyboard focus.
    // Used to gate frame/shape copy-paste and Delete so those keys still reach
    // the text control instead of being swallowed by the window-level handler.
    private bool IsTextInputFocused()
        => FocusManager?.GetFocusedElement() is TextBox;

    // ── Copy / Paste ──────────────────────────────────────────────────────────

    // The selected domain object, resolved from the selection model (the source of
    // truth) with the tree node as a fast path. AnimTree.SelectedItem alone is null
    // whenever the selected node isn't realized — e.g. a frame is selected while its
    // chain row is collapsed — even though _selectedState still holds it. Mirrors the
    // shape→frame→chain priority in SyncTreeSelection.
    private object? SelectedData =>
        (AnimTree.SelectedItem as TreeNodeVm)?.Data
        ?? (object?)_selectedState.SelectedCircle
        ?? _selectedState.SelectedRectangle
        ?? _selectedState.SelectedFrame
        ?? (object?)_selectedState.SelectedChain;

    // Copy/Paste are invoked fire-and-forget (_ = HandleCopyAsync()), so an exception
    // inside them would otherwise vanish as an unobserved task exception — which is how
    // a clipboard-serialization failure silently produced "nothing happened". Route both
    // through this guard so any failure surfaces as a visible error instead.
    internal async Task RunGuardedAsync(Func<Task> action, string actionName)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ShowStatusMessage($"⚠ {actionName} failed: {ex.Message}", isError: true);
        }
    }

    private Task HandleCopyAsync()  => RunGuardedAsync(HandleCopyCoreAsync,  "Copy");
    private Task HandleCutAsync()   => RunGuardedAsync(HandleCutCoreAsync,   "Cut");
    private Task HandlePasteAsync() => RunGuardedAsync(HandlePasteCoreAsync, "Paste");

    private async Task HandleCopyCoreAsync()
    {
        if (IsTextInputFocused()) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        if (!SelectionCopyContext.TryGet(
                _selectedState, _objectFinder, _projectManager.AnimationChainListSave,
                out var payload, out var failureMessage))
        {
            if (failureMessage is not null)
            {
                ShowStatusMessage(failureMessage, isError: true);
                await clipboard.SetTextAsync(string.Empty);
            }
            return;
        }

        await clipboard.SetTextAsync(ClipboardPayload.SerializeFromPayload(payload));
        _pendingCutState.Clear();
        SyncPendingCutHighlights();
    }

    private async Task HandleCutCoreAsync()
    {
        if (IsTextInputFocused()) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        if (!SelectionCopyContext.TryGet(
                _selectedState, _objectFinder, _projectManager.AnimationChainListSave,
                out var payload, out var failureMessage))
        {
            if (failureMessage is not null)
            {
                ShowStatusMessage(failureMessage, isError: true);
                await clipboard.SetTextAsync(string.Empty);
            }
            return;
        }

        await clipboard.SetTextAsync(ClipboardPayload.SerializeFromPayload(payload));
        _pendingCutState.Set(payload);
        SyncPendingCutHighlights();
    }

    private async Task HandlePasteCoreAsync()
    {
        if (IsTextInputFocused()) return;
        var clipboard = TopLevel.GetTopLevel(this)?.Clipboard;
        if (clipboard is null) return;

        var text = await clipboard.TryGetTextAsync();
        if (string.IsNullOrWhiteSpace(text)) return;

        bool ok = ClipboardPayload.TryDeserialize(text,
            out var chains, out var frames, out var rectangles, out var circles);
        if (!ok) return;

        var acls = _projectManager.AnimationChainListSave;
        if (acls is null) return;

        var selectedData = SelectedData;
        bool completingCut = _pendingCutState.IsActive;
        if (completingCut && !_pendingCutState.SourcesBelongToProject(acls, _objectFinder))
        {
            _pendingCutState.Clear();
            SyncPendingCutHighlights();
            completingCut = false;
        }

        if (chains is { Count: > 0 })
        {
            if (completingCut && _pendingCutState.Kind != CopySelectionKind.Chain) return;
            QueuePastedChainExpandFromSources(chains, chains);
            if (completingCut)
                _appCommands.PasteChainsCut(chains, _pendingCutState.Chains);
            else
                _appCommands.PasteChains(chains);
        }
        else if (frames is { Count: > 0 })
        {
            if (completingCut && _pendingCutState.Kind != CopySelectionKind.Frame) return;
            var (targetChain, insertIndex) = PastePlacementLogic.ResolveFramePasteTarget(
                acls, selectedData, _objectFinder, _selectedState);
            if (targetChain is null) return;

            foreach (var pasted in frames)
                pasted.ShapesSave ??= new ShapesSave();

            if (completingCut)
                _appCommands.PasteFramesCut(targetChain, frames, insertIndex, _pendingCutState.Frames);
            else
                _appCommands.PasteFrames(targetChain, frames, insertIndex);
            RefreshChainNode(targetChain);
            _appCommands.RefreshWireframe();
            SyncTreeSelection();
        }
        else if (rectangles is { Count: > 0 } || circles is { Count: > 0 })
        {
            if (completingCut && _pendingCutState.Kind != CopySelectionKind.Shape) return;
            var frame = _selectedState.SelectedFrame;
            if (frame is null) return;

            if (completingCut)
            {
                var sourceFrame = _pendingCutState.Shapes[0] switch
                {
                    AARectSave r => _objectFinder.GetAnimationFrameContaining(r),
                    CircleSave c => _objectFinder.GetAnimationFrameContaining(c),
                    _ => null,
                };
                if (sourceFrame is null) return;
                _appCommands.PasteShapesCut(
                    frame, rectangles ?? [], circles ?? [], _pendingCutState.Shapes, sourceFrame);
            }
            else
            {
                _appCommands.PasteShapes(frame, rectangles ?? [], circles ?? []);
            }
            RefreshFrameNode(frame);
            SyncTreeSelection();
        }

        if (completingCut)
        {
            _pendingCutState.Clear();
            SyncPendingCutHighlights();
        }
    }

    private void SyncPendingCutHighlights()
    {
        void Walk(TreeNodeVm node)
        {
            node.IsPendingCut = node.Data is not null && _pendingCutState.Contains(node.Data);
            foreach (var child in node.Children)
                Walk(child);
        }
        foreach (var root in _treeRoots)
            Walk(root);
        WireframeCtrl.InvalidateVisual();
        PreviewCtrl.InvalidateVisual();
    }

    private void ClearPendingCut()
    {
        if (!_pendingCutState.IsActive) return;
        _pendingCutState.Clear();
        SyncPendingCutHighlights();
    }

    private bool TreeMultiSelectionAlreadySynced(IReadOnlyList<object> dataObjects)
    {
        if (AnimTree.SelectedItems is null) return dataObjects.Count == 0;
        var selected = AnimTree.SelectedItems.OfType<TreeNodeVm>().ToList();
        if (selected.Count != dataObjects.Count) return false;
        var dataSet = new HashSet<object>(dataObjects);
        return selected.All(n => n.Data is not null && dataSet.Contains(n.Data));
    }

    /// <summary>
    /// One-way push of model multi-selection into the tree. Must not write back to
    /// <see cref="ISelectedState.SelectedNodes"/> or call <see cref="TreeBuilder.RouteNodeSelection"/> —
    /// that is <see cref="OnTreeSelectionChanged"/>'s job and causes a SelectionChanged loop.
    /// </summary>
    private void SyncTreeMultiSelection(IReadOnlyList<object> dataObjects)
    {
        if (TreeMultiSelectionAlreadySynced(dataObjects))
            return;

        var nodes = new List<TreeNodeVm>();
        bool chainsOnly = dataObjects.All(d => d is AnimationChainSave);
        foreach (var data in dataObjects)
        {
            if (!chainsOnly)
                TreeBuilder.ExpandAncestorsOf(_treeRoots, data);
            var node = TreeBuilder.FindNodeForData(_treeRoots, data);
            if (node is not null)
                nodes.Add(node);
        }

        if (nodes.Count == 0 && dataObjects.Count > 0)
            return;

        bool prior = _suppressTreeSelectionHandling;
        _suppressTreeSelectionHandling = true;
        try
        {
            WithPreservedAnimTreeScroll(() =>
            {
                AnimTree.SelectedItems!.Clear();
                foreach (var node in nodes)
                    AnimTree.SelectedItems.Add(node);
            });
        }
        finally
        {
            _suppressTreeSelectionHandling = prior;
        }
    }

    // ── Duplicate ─────────────────────────────────────────────────────────────

    // Mirrors HandleCopyAsync's selection dispatch exactly (chain/frame/rect/circle) so
    // every type that can be copied can also be duplicated. Each duplicate places the copy
    // adjacent to its source and selects it; flip-H/flip-V chain variants stay menu-only.
    private void HandleDuplicate()
    {
        if (IsTextInputFocused()) return;

        if (!SelectionCopyContext.TryGet(
                _selectedState, _objectFinder, _projectManager.AnimationChainListSave,
                out var payload, out var failureMessage))
        {
            if (failureMessage is not null)
                ShowStatusMessage(failureMessage, isError: true);
            return;
        }

        _appCommands.DuplicateSelection(payload);
        if (payload.Kind == CopySelectionKind.Chain)
        {
            var copies = _selectedState.SelectedChains;
            QueuePastedChainExpandFromSources(payload.Chains, copies);
            RefreshTreeView();
        }
        else if (payload.Kind == CopySelectionKind.Frame && payload.Frames.Count > 0)
        {
            var chain = _objectFinder.GetAnimationChainContaining(payload.Frames[0]);
            if (chain is not null) RefreshChainNode(chain);
            SyncTreeSelection();
        }
        else if (payload.Kind == CopySelectionKind.Shape && payload.Shapes.Count > 0
                 && _selectedState.SelectedFrame is { } shapeFrame)
        {
            RefreshFrameNode(shapeFrame);
            SyncTreeSelection();
        }
    }

    // Flip variants aren't part of the Ctrl+D path (HandleDuplicate has no flip concept),
    // so this mirrors its multi-select dispatch for chains only: duplicate every selected
    // chain (falling back to the right-clicked one when nothing is selected) with the flip
    // flag applied, via the same AppCommands.DuplicateChains batch HandleDuplicate uses.
    private void HandleDuplicateChainsFlip(AnimationChainSave rightClicked, bool flipH, bool flipV)
    {
        var selected = _selectedState.SelectedChains;
        var sources = selected.Count > 0 ? selected : new List<AnimationChainSave> { rightClicked };
        var copies = _appCommands.DuplicateChains(sources, flipH, flipV);
        QueuePastedChainExpandFromSources(sources, copies);
        RefreshTreeView();
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    private void HandleDelete()
    {
        // Delete the whole multi-selection of the focused node's kind, not just the
        // focused node — the delete commands batch them into a single undo step.
        // All kinds are fully undoable, so they delete immediately and surface an
        // undo toast rather than a confirmation dialog.
        switch (SelectedData)
        {
            case AnimationChainSave chainToDel:
            {
                var chains = _selectedState.SelectedChains;
                _appCommands.DeleteAnimationChains(chains.Count > 0 ? chains : new List<AnimationChainSave> { chainToDel });
                break;
            }
            case AnimationFrameSave frameToDel:
            {
                var frames = _selectedState.SelectedFrames;
                _appCommands.DeleteFrames(frames.Count > 0 ? frames : new() { frameToDel });
                break;
            }
            case AARectSave rectToDel:
            {
                var frame   = _selectedState.SelectedFrame!;
                var rects   = _selectedState.SelectedRectangles;
                var circles = _selectedState.SelectedCircles;
                _appCommands.DeleteShapes(frame, rects.Count > 0 ? rects : new() { rectToDel }, circles);
                break;
            }
            case CircleSave circleToDel:
            {
                var frame   = _selectedState.SelectedFrame!;
                var circles = _selectedState.SelectedCircles;
                var rects   = _selectedState.SelectedRectangles;
                _appCommands.DeleteShapes(frame, rects, circles.Count > 0 ? circles : new() { circleToDel });
                break;
            }
        }
    }

    private async void ShowItemDeletedToast(string label)
    {
        _toastCts?.Cancel();
        _toastCts = new System.Threading.CancellationTokenSource();
        System.Threading.CancellationToken token = _toastCts.Token;

        ItemDeletedToastLabel.Text = $"\"{label}\" deleted";
        ItemDeletedToastPanel.IsVisible = true;

        try
        {
            await System.Threading.Tasks.Task.Delay(4000, token);
            ItemDeletedToastPanel.IsVisible = false;
        }
        catch (System.Threading.Tasks.TaskCanceledException) { }
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
        {
            // The file decoded fine at the top of this method, but the user has since been in a
            // modal dialog — it could have been deleted, truncated, or locked in the meantime.
            // SKBitmap.Decode returns null (it does not throw); guard before DrawBitmap so a
            // race doesn't crash the app on the dispatcher (issue #479).
            if (src is null)
            {
                ShowStatusMessage("⚠ Could not read texture file.", isError: true);
                return;
            }

            using var resized = new SKBitmap(newW, newH);
            using var canvas  = new SKCanvas(resized);
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
            FocusTreeAfterRename(vm);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            vm.CancelEdit();
            FocusTreeAfterRename(vm);
        }
        else if (e.Key is Key.Left or Key.Right)
        {
            // Same Tunnel-interception point as Enter/Escape above (issue #591): this runs
            // before TreeViewItem.OnKeyDown, which otherwise treats Left/Right as
            // expand/collapse and steals keyboard focus off the TextBox (ending the rename
            // via OnInlineRenameLostFocus) instead of just moving the caret. We move the
            // caret ourselves rather than re-dispatching the key, since re-raising it on the
            // TextBox would route back through this same Tunnel handler.
            e.Handled = true;
            var min = Math.Min(tb.SelectionStart, tb.SelectionEnd);
            var max = Math.Max(tb.SelectionStart, tb.SelectionEnd);
            var hasSelection = min != max;
            var newCaret = e.Key == Key.Left
                ? (hasSelection ? min : Math.Max(0, tb.CaretIndex - 1))
                : (hasSelection ? max : Math.Min((tb.Text ?? string.Empty).Length, tb.CaretIndex + 1));
            tb.SelectionStart = newCaret;
            tb.SelectionEnd = newCaret;
        }
        else if (e.Key is Key.Up or Key.Down)
        {
            // Single-line rename box: Up/Down have no caret meaning. Swallow them here so
            // TreeViewItem doesn't navigate to a sibling row and end the rename via focus loss.
            e.Handled = true;
        }
    }

    // AnimTree (the TreeView itself) has Focusable=false — only its TreeViewItem containers
    // are focusable — so AnimTree.Focus() is always a no-op. Committing/cancelling a rename
    // also flips the TextBox's IsVisible binding off, and Avalonia's own focus-fallback (moving
    // focus off a now-invisible control) runs on a later dispatcher tick than this handler.
    // Without an explicit refocus posted after that fallback, keyboard focus ends up on
    // whatever window chrome is next in tab order (e.g. the minimize button) instead of back
    // on the row that was being renamed.
    private void FocusTreeAfterRename(TreeNodeVm vm)
        => Dispatcher.UIThread.Post(() =>
        {
            AnimTree.GetVisualDescendants()
                .OfType<TreeViewItem>()
                .FirstOrDefault(t => t.DataContext == vm)
                ?.Focus();
        }, DispatcherPriority.Render);

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
        else if (vm.Data is AARectSave rect)
        {
            if (!string.IsNullOrEmpty(newName) && newName != rect.Name)
                _appCommands.SetRectProps(
                    _objectFinder.GetAnimationFrameContaining(rect),
                    rect, newName, rect.X, rect.Y, rect.ScaleX, rect.ScaleY);
        }
        else if (vm.Data is CircleSave circle)
        {
            if (!string.IsNullOrEmpty(newName) && newName != circle.Name)
                _appCommands.SetCircleProps(
                    _objectFinder.GetAnimationFrameContaining(circle),
                    circle, newName, circle.X, circle.Y, circle.Radius);
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

        var error = Services.ShellExplorer.RevealFile(absPath);
        if (error is not null)
            ShowStatusMessage($"⚠ {error}", isError: true);
    }
}

