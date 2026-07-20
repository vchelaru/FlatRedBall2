using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices.JavaScript;
using System.Threading.Tasks;
using AnimationEditor.App.Controls;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.App.Theming;
using AnimationEditor.Core.Export;
using AnimationEditor.Core.Hotkeys;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Markup.Xaml.MarkupExtensions;
using Avalonia.Platform.Storage;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using FilePath = AnimationEditor.Core.Paths.FilePath;
using SvgIcon = Avalonia.Svg.Skia.Svg;

namespace AnimationEditor.Browser;

public partial class App : Application
{
    /// <summary>
    /// One row in the history list: <see cref="IsCurrent"/> marks the most recently applied
    /// command ("you are here"), <see cref="IsRedo"/> marks a command available to redo (rendered
    /// muted). Mirrors desktop MainWindow's HistoryEntryVm at a fraction of the ceremony -- no
    /// theme-token brush lookups, just FontWeight/Opacity.
    /// </summary>
    private sealed record HistoryRowVm(string Text, bool IsCurrent, bool IsRedo);

    /// <summary>
    /// Phase 8 (#648): one shared SVG icon, colored via a theme token by key (default
    /// <c>IconInk</c>) instead of a hardcoded brush -- matches desktop's
    /// <c>CurrentColor="{DynamicResource ...}"</c> pattern so icons still track the active theme.
    /// </summary>
    private static SvgIcon Icon(string name, double size = 16, string colorKey = "IconInk")
    {
        var svg = new SvgIcon(baseUri: null!)
        {
            Width = size,
            Height = size,
            Path = $"avares://AnimationEditor.Views/Assets/icons/svg/{name}.svg",
            VerticalAlignment = VerticalAlignment.Center,
        };
        svg.Bind(SvgIcon.CurrentColorProperty, svg.GetResourceObservable(colorKey));
        return svg;
    }

    /// <summary>Icon + label content for a Button/ToggleButton -- desktop's toolbar pattern.</summary>
    private static StackPanel IconLabel(string iconName, string text) => new()
    {
        Orientation = Orientation.Horizontal,
        Spacing = 4,
        Children =
        {
            Icon(iconName),
            new TextBlock { Text = text, VerticalAlignment = VerticalAlignment.Center },
        },
    };

    /// <summary>
    /// Groups controls into a bordered, clipped pill matching desktop's toolbar edit-mode-toggle
    /// style (<c>CornerRadius="4"</c>, 1px <c>LineBrush</c> border, 26px height).
    /// </summary>
    private static Border Pill(params Control[] children)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal };
        stack.Children.AddRange(children);
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Height = 26,
            VerticalAlignment = VerticalAlignment.Center,
            Child = stack,
        };
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("LineBrush"));
        return border;
    }

    /// <summary>Bottom-bordered rail matching desktop's wireframe/preview toolbar chrome.</summary>
    private static Border ToolbarChrome(Control content)
    {
        var border = new Border
        {
            Padding = new Thickness(4, 3),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ClipToBounds = true,
            Child = content,
        };
        border.Bind(Border.BackgroundProperty, border.GetResourceObservable("BgRail"));
        border.Bind(Border.BorderBrushProperty, border.GetResourceObservable("LineBrush"));
        return border;
    }

    private static Border ToolbarDivider()
    {
        var divider = new Border
        {
            Width = 1,
            Height = 18,
            Margin = new Thickness(4, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        divider.Bind(Border.BackgroundProperty, divider.GetResourceObservable("LineBrush"));
        return divider;
    }

    // Translates Avalonia's native modifier flags into the UI-independent HotkeyModifiers used by
    // AnimationEditor.Core.Hotkeys -- mirrors MainWindow.ToHotkeyModifiers (desktop host); kept in
    // sync manually since the two hosts don't share an Avalonia-dependent project to host it in.
    private static HotkeyModifiers ToHotkeyModifiers(KeyModifiers modifiers)
    {
        var result = HotkeyModifiers.None;
        if (modifiers.HasFlag(KeyModifiers.Shift)) result |= HotkeyModifiers.Shift;
        if (modifiers.HasFlag(KeyModifiers.Alt)) result |= HotkeyModifiers.Alt;
        if (modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta))
            result |= HotkeyModifiers.Command;
        return result;
    }

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
            singleView.MainView = BuildView();

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// #535 M2/M3: wires the same service graph <c>MainWindow</c> builds on desktop (minus the
    /// desktop-only bits -- file association, native menu, single-instance IPC), loads the
    /// bundled sample fetched by <see cref="Program.Main"/>, and hands it to the real
    /// <see cref="PreviewControl"/>. A toolbar (Open Folder / Save As) and a window-wide drop
    /// target let the user load/save a different project -- through Avalonia's IStorageProvider
    /// and drag-drop, both of which work with no local filesystem (see BrowserProjectLoader and
    /// docs/BROWSER_SPIKE_FINDINGS.md).
    /// </summary>
    private static Control BuildView()
    {
        var projectManager    = new ProjectManager();
        var applicationEvents = new ApplicationEvents();
        var selectedState     = new SelectedState(projectManager);
        var appState          = new AppState(applicationEvents, selectedState);
        var ioManager         = new IoManager(appState);
        var objectFinder      = new ObjectFinder(projectManager);
        var undoManager       = new UndoManager();
        var pendingCutState   = new PendingCutState();
        var appCommands       = new AppCommands(
            projectManager, selectedState, applicationEvents, ioManager, objectFinder, undoManager);
        var thumbnailService  = new ThumbnailService(projectManager);

        // Phase 15: shared toast/banner overlays (item-deleted undo, generic toast, error banner).
        var notifications = new EditorNotificationOverlay();
        notifications.WireUndo(() => undoManager.Undo());
        appCommands.ItemsDeleted += notifications.ShowItemDeleted;

        // #610: the first browser setting worth persisting -- theme has a real toggle button
        // below, unlike zoom/grid/recent-files, which have no browser UI yet (see
        // docs/BROWSER_SETTINGS_DECISION.md for why those aren't wired up until they do).
        // LocalStorageInterop.InitializeAsync() (Program.cs) must have already completed by the
        // time this runs -- BuildView is only reached after Program.Main's Task.WhenAll finishes.
        var settingsStore = new BrowserSettingsStore(new JsLocalStorage());
        var currentTheme = settingsStore.LoadTheme() ?? AppTheme.Dark; // matches AppSettingsModel's default
        Application.Current!.RequestedThemeVariant = ThemeManager.ToVariant(currentTheme);

        var acls = AnimationChainListSave.FromString(SampleContent.AchxText);
        var bitmap = SKBitmap.Decode(SampleContent.PngBytes);
        thumbnailService.SeedTexture("player.png", bitmap);

        // "sample/player.achx" doesn't exist on disk (no filesystem in the browser); preParsed
        // means LoadAnimationChain never tries to read it, only uses it as a logical identity.
        // knownTextureSizes mirrors BrowserProjectLoader's real load path -- the bundled sample
        // happens to be UV-format today so this isn't load-bearing yet, but a Pixel-format
        // sample would otherwise silently fail the same way BrowserProjectLoader's fix (#535)
        // was needed for.
        var knownTextureSizes = new Dictionary<string, (int Width, int Height)>(StringComparer.OrdinalIgnoreCase)
        {
            ["player.png"] = (bitmap.Width, bitmap.Height),
        };
        projectManager.LoadAnimationChain(new FilePath("sample/player.achx"), acls, knownTextureSizes);

        // Phase 4 (#620): multi-file tabs. TabManager/TabEditorCache are already fully built and
        // tested in Core (pure in-memory, zero disk dependency) -- this is wiring, not new logic.
        // One thing checked before wiring: TabEditorCache.HasFreshCache treats a tab as fresh
        // whenever its cached disk-write-time is null, and TryReadDiskWriteTimeUtc naturally
        // returns null for any path that doesn't exist on disk (every browser tab's path) --
        // so cached tabs are already correctly "always trusted" here with no code changes needed.
        var tabManager = new TabManager();
        tabManager.OpenOrFocus(new FilePath("sample/player.achx"), "player.achx");
        appCommands.CaptureTabEditorState(tabManager.ActiveTab!);

        // Per-tab writable file handle for a direct "Save" (vs. "Save As", which always
        // prompts). This is a browser-only concept (an IEditorFile -- see IEditorFile.cs) --
        // kept local here rather than added to the portable, Avalonia-free Core TabEntry, which
        // desktop also uses. Populated after a successful Open Folder load or drag-drop (both
        // hand back a real writable handle, see BrowserProjectLoader.TryLoadAsync) and after a
        // successful Save As. Absent for a brand-new/Untitled tab or the bundled sample --
        // exactly the tabs where "Save" should fall back to Save As's prompt.
        // Declared here (rather than beside the Save/Save As buttons below) so CloseTab -- a
        // local function defined earlier in BuildView than the natural place to construct those
        // buttons -- sees a definitely-assigned variable (same reasoning as textureListPanel's
        // declaration site, see docs/BROWSER_FILES_PANEL_DECISION.md).
        var tabFileHandles = new Dictionary<TabEntry, IEditorFile>();

        var preview = new PreviewControl();
        preview.InitializeServices(
            selectedState, appState, appCommands, applicationEvents,
            projectManager, undoManager, thumbnailService, pendingCutState);

        // Phase 3 (#614): shape editing on the canvas. WireframeControl has zero
        // Avalonia.Desktop dependency (confirmed during the #535/#588 M1 spike, same as
        // PreviewControl) -- this is wiring an existing, tested control, not new interaction
        // logic. The one real gap found while researching this (TextureViewport.LoadTexture
        // reading straight from disk) was fixed separately; passing thumbnailService here is
        // what makes RefreshAll() use that fix instead of always trying a disk read.
        var wireframe = new WireframeControl();
        wireframe.InitializeServices(
            selectedState, appState, appCommands, applicationEvents,
            projectManager, undoManager, pendingCutState, objectFinder,
            thumbnailService: thumbnailService);

        // Phase 1 (#603): read-only browsing of every chain/frame/shape in the loaded file,
        // replacing the previous hardcoded "always show AnimationChains[0]" behavior. Both
        // controls are independent of each other and of PreviewControl -- any of the three can
        // drive ISelectedState, and the others react via SelectionChanged.
        var animationTree = new AnimationTreeControl();
        // Browser renders the tree with AnimationTreeControl, so the controller reads expand
        // state from there (desktop reads its own _treeRoots collection instead).
        var tabController = new TabController(undoManager, appCommands,
            () => animationTree.CaptureExpandState());
        var inspector = new InspectorControl();
        inspector.InitializeServices(selectedState);
        inspector.EnableEditing(appCommands, textureName =>
        {
            var bmp = thumbnailService.GetBitmap(textureName);
            return bmp is null ? null : (bmp.Width, bmp.Height);
        });
        animationTree.InitializeServices(selectedState, acls);
        animationTree.EnableRename(appCommands);
        // Phase 2 of #754: right-click tree context menu, matching desktop's MainWindow menu
        // (see docs/BROWSER_TREE_CONTEXT_MENU_DECISION.md).
        animationTree.EnableContextMenu(appCommands, objectFinder, projectManager, pendingCutState);

        // Phase 12 (#655): declared here (rather than alongside the Files TabItem UI below) so
        // CloseTab/SwitchToTab -- local functions defined earlier in this method that reference
        // it -- see a definitely-assigned variable; C# local functions may capture a
        // later-declared local, but only once it's assigned before any possible invocation.
        var textureListPanel = new TextureListPanel();
        textureListPanel.InitializeServices(acls, thumbnailService);

        // Selecting a chain with no frame pinned auto-plays it (PreviewControl.OnSelectionChanged).
        selectedState.SelectedChain = acls.AnimationChains[0];

        var status = new TextBlock { Margin = new Thickness(8), Text = "Loaded bundled sample." };
        var statusLeftText = new TextBlock { Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };

        // Phase 8 (#648): branded header bar -- visual only (icon, app name, active filename), per
        // the roadmap's decision #1: no drag-to-move/custom resize/minimize-maximize-close, since
        // the browser tab already has real OS chrome for those. IconChain doubles as the closest
        // thing to an app mark already in the shared icon set (no dedicated logo asset exists).
        var headerFileNameText = new TextBlock { VerticalAlignment = VerticalAlignment.Center };
        headerFileNameText.Bind(TextBlock.ForegroundProperty, headerFileNameText.GetResourceObservable("InkMid"));
        // Phase 11 (#654): "Open Containing Folder" is dropped (no filesystem); "Copy Full Path"
        // stays -- low-value against a synthetic path but harmless, per the roadmap.
        var copyPathItem = new MenuItem { Header = "Copy Full Path" };
        copyPathItem.Click += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(headerFileNameText);
            var path = tabManager.ActiveTab?.Path.FullPath;
            if (topLevel?.Clipboard is { } clipboard && path is not null)
                await clipboard.SetTextAsync(path);
        };
        headerFileNameText.ContextMenu = new ContextMenu { Items = { copyPathItem } };
        var headerBar = new Border
        {
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding = new Thickness(10, 6),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                Children =
                {
                    Icon("IconChain", 18, "Accent"),
                    new TextBlock { Text = "Animation Editor", FontWeight = Avalonia.Media.FontWeight.SemiBold, VerticalAlignment = VerticalAlignment.Center },
                    new TextBlock { Text = "—", Opacity = 0.5, VerticalAlignment = VerticalAlignment.Center },
                    headerFileNameText,
                },
            },
        };
        headerBar.Bind(Border.BackgroundProperty, headerBar.GetResourceObservable("BgRail"));
        headerBar.Bind(Border.BorderBrushProperty, headerBar.GetResourceObservable("LineBrush"));

        // Desktop's TabBarBorder uses BgCanvas (not BgRail) so the strip reads as a distinct
        // tab rail against the menu bar / header above it.
        var tabStrip = new StackPanel { Orientation = Orientation.Horizontal };
        var tabBarBorder = new Border
        {
            Height = 30,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new ScrollViewer
            {
                HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,
                VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
                Content = tabStrip,
            },
        };
        tabBarBorder.Bind(Border.BackgroundProperty, tabBarBorder.GetResourceObservable("BgCanvas"));
        tabBarBorder.Bind(Border.BorderBrushProperty, tabBarBorder.GetResourceObservable("LineBrush"));

        // Hidden command stubs — menu items raise Click on these rather than duplicating handlers.
        var openButton = new Button { Content = "Open Folder…", IsVisible = false };
        // Save writes straight to the active tab's known location (from Open Folder/drag-drop/a
        // prior Save As) with no prompt, falling back to Save As's prompt only when no location
        // is known yet (new/Untitled tab, bundled sample). Save As always prompts.
        var saveButton = new Button { Content = "Save", IsVisible = false };
        var saveAsButton = new Button { Content = "Save As…", IsVisible = false };
        var reloadButton = new Button { Content = "Reload Changed Textures", IsVisible = false };

        void SetTheme(AppTheme theme)
        {
            currentTheme = theme;
            Application.Current!.RequestedThemeVariant = ThemeManager.ToVariant(currentTheme);
            settingsStore.SaveTheme(currentTheme);
        }

        // Desktop parity: Add Animation lives at the bottom of the animations tree panel;
        // shape/frame/delete commands are Edit-menu + tree context menus, not a global toolbar.
        var addAnimationButton = new Button
        {
            Content = "+ Add Animation",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 30,
        };
        var addFrameButton = new Button { IsVisible = false };
        var addRectButton = new Button { IsVisible = false };
        var addCircleButton = new Button { IsVisible = false };
        var deleteSelectedButton = new Button { IsVisible = false };

        var historyUndoButton = new Button
        {
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = Icon("IconUndo", 14),
            IsEnabled = false,
        };
        ToolTip.SetTip(historyUndoButton, "Undo");
        var historyRedoButton = new Button
        {
            Width = 26,
            Height = 26,
            Padding = new Thickness(0),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = Icon("IconRedo", 14),
            IsEnabled = false,
        };
        ToolTip.SetTip(historyRedoButton, "Redo");

        // Phase 8 (#648): mirrors desktop's Move/Magic-Wand edit-mode pill exactly -- two
        // mutually-exclusive ToggleButtons in one bordered group, split corner radii, no gap.
        // Move needs no new logic (drag events are already wired in WireframeControl's
        // constructor); this only adds the visual toggle desktop has and browser never did.
        var moveModeButton = new ToggleButton
        {
            IsChecked = true,
            Height = 26, MinHeight = 0, Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(3, 0, 0, 3),
            Content = IconLabel("IconMove", "Move"),
        };
        var magicWandButton = new ToggleButton
        {
            Height = 26, MinHeight = 0, Padding = new Thickness(10, 0),
            CornerRadius = new CornerRadius(0, 3, 3, 0),
            Content = IconLabel("IconMagicWand", "Magic Wand"),
        };
        var editModeDivider = new Border { Width = 1 };
        editModeDivider.Bind(Border.BackgroundProperty, editModeDivider.GetResourceObservable("LineBrush"));
        var editModePill = Pill(moveModeButton, editModeDivider, magicWandButton);

        moveModeButton.Click += (_, _) =>
        {
            wireframe.IsMagicWandMode = false;
            moveModeButton.IsChecked = true;
            magicWandButton.IsChecked = false;
        };
        magicWandButton.Click += (_, _) =>
        {
            wireframe.IsMagicWandMode = magicWandButton.IsChecked == true;
            moveModeButton.IsChecked = magicWandButton.IsChecked != true;
        };

        // FrameRegionChanged/ChainRegionChanged fire after a handle/chain drag commits;
        // FrameLiveUpdated fires on every pointer-move frame during the drag (no save, just
        // keep the inspector/preview in sync); FrameCreatedFromRegion fires from a magic-wand
        // click or plain-mode ctrl+click. All four mirror MainWindow's own handlers.
        wireframe.FrameRegionChanged += frame =>
        {
            appCommands.RefreshTreeNode(frame);
            applicationEvents.RaiseAnimationChainsChanged();
        };
        wireframe.ChainRegionChanged += _ => applicationEvents.RaiseAnimationChainsChanged();
        wireframe.FrameLiveUpdated += _ => appCommands.RefreshAnimationFrameDisplay();
        wireframe.FrameCreatedFromRegion += (minX, minY, maxX, maxY) =>
        {
            var chain = selectedState.SelectedChain;
            if (chain is null) return;

            // wireframe.LoadedTexturePath is DetermineTexturePath()'s achx-relative disk path,
            // which is synthetic here (the browser's ProjectManager.FileName is a logical
            // identity, not a real folder) -- it won't match the bare/relative names
            // ThumbnailService's cache and existing frames actually use. The texture being
            // edited is always whichever one the selected chain's frames already reference, so
            // reuse that name directly rather than deriving one from the fake path.
            var textureName = selectedState.SelectedFrame?.TextureName
                ?? chain.Frames.FirstOrDefault()?.TextureName;
            if (string.IsNullOrEmpty(textureName)) return;

            var (bitmapW, bitmapH) = wireframe.BitmapSize;
            if (bitmapW == 0 || bitmapH == 0) return;

            appCommands.AddFrameFromPixelBounds(chain, textureName, minX, minY, maxX, maxY, bitmapW, bitmapH);
        };

        void UpdateUndoRedoButtons()
        {
            historyUndoButton.IsEnabled = undoManager.CanUndo;
            historyRedoButton.IsEnabled = undoManager.CanRedo;
        }
        undoManager.StackChanged += UpdateUndoRedoButtons;

        // History panel: read-only ItemsControl matching desktop (not a focusable ListBox).
        // A ListBox steals keyboard/pointer focus when the History tab is active, which on
        // browser can interrupt wireframe pointer capture mid-drag and leave the chain stuck
        // following the cursor until a second click. ScrollViewer owns overflow scrolling.
        //
        // Avalonia's TabControl only hosts the selected tab's content in the visual tree, so
        // assigning ItemsSource while History is hidden can be lost when the tab is shown again.
        // historyRows is the always-updated snapshot (UndoManager itself already persists
        // regardless of which sidebar tab is open); we re-push it onto the ItemsControl whenever
        // History becomes selected.
        var historyList = new ItemsControl();
        var historyRows = new List<HistoryRowVm>();
        historyList.ItemTemplate = new FuncDataTemplate<HistoryRowVm>((row, _) => new TextBlock
        {
            Text = row!.Text,
            FontWeight = row.IsCurrent ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal,
            Opacity = row.IsRedo ? 0.55 : 1.0,
            Margin = new Thickness(4, 2),
        });

        void RefreshHistoryList()
        {
            historyRows.Clear();
            // Ordering/marking (oldest-applied first, current entry, then redo entries) lives in
            // Core's HistoryRowBuilder so desktop and browser stay in lockstep (#748).
            foreach (var row in HistoryRowBuilder.BuildRows(undoManager.UndoHistory, undoManager.RedoHistory))
                historyRows.Add(new HistoryRowVm(row.Description, row.IsCurrent, row.IsRedo));
            // New list instance so ItemsControl always sees a source change, even if it was
            // detached from the visual tree while History was not the selected sidebar tab.
            historyList.ItemsSource = historyRows.ToList();
        }
        undoManager.StackChanged += RefreshHistoryList;
        RefreshHistoryList();

        // Phase 4 (#620): multi-file tabs. TabManager/TabEditorCache are already fully built and
        // tested in Core (pure in-memory, zero disk dependency) -- this is wiring, not new logic.
        // One thing checked before wiring: TabEditorCache.HasFreshCache treats a tab as fresh
        // whenever its cached disk-write-time is null, and TryReadDiskWriteTimeUtc naturally
        // returns null for any path that doesn't exist on disk (every browser tab's path) --
        // so cached tabs are already correctly "always trusted" here with no code changes needed.
        // Phase 11 (#654): Border-based active/inactive tab look matching desktop's TabStrip
        // (BgActive/transparent background, Ink/InkMid label, 1px LineBrush divider) plus a close
        // button and a context menu. No drag-to-reorder here -- that's desktop-only ceremony
        // (pointer capture + ghost ItemsControl overlay) the roadmap's "visual polish + context
        // menus" scope doesn't call for.
        void RebuildTabStrip()
        {
            tabStrip.Children.Clear();
            var tabs = tabManager.Tabs;

            foreach (var tab in tabs)
            {
                var isActive = tab == tabManager.ActiveTab;
                var captured = tab;

                var tabBorder = new Border
                {
                    Height = 30,
                    BorderThickness = new Thickness(0, 0, 1, 0),
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                if (isActive) tabBorder.Bind(Border.BackgroundProperty, tabBorder.GetResourceObservable("BgActive"));
                else tabBorder.Background = Avalonia.Media.Brushes.Transparent;
                tabBorder.Bind(Border.BorderBrushProperty, tabBorder.GetResourceObservable("LineBrush"));
                ToolTip.SetTip(tabBorder, tab.Path.FullPath);

                var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Margin = new Thickness(8, 0, 0, 0) };
                var label = new TextBlock { Text = tab.DisplayName, FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
                label.Bind(TextBlock.ForegroundProperty, label.GetResourceObservable(isActive ? "Ink" : "InkMid"));
                Grid.SetColumn(label, 0);

                // Always closable, matching desktop — closing the last tab starts a blank project
                // (see CloseTab below). SVG IconClose (not Unicode ✕) so the glyph renders in WASM
                // where the ✕ codepoint often falls back to an empty/missing glyph.
                var closeBtn = new Button
                {
                    Content = Icon("IconClose", 12, "InkMid"),
                    Width = 20, Height = 20, Padding = new Thickness(0),
                    Background = Avalonia.Media.Brushes.Transparent, BorderThickness = new Thickness(0),
                    HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(2, 0, 2, 0),
                };
                ToolTip.SetTip(closeBtn, "Close Tab");
                Grid.SetColumn(closeBtn, 1);
                closeBtn.Click += (_, _) => CloseTab(captured);

                row.Children.Add(label);
                row.Children.Add(closeBtn);
                tabBorder.Child = row;

                // "Detach to New Window" has no browser equivalent -- remapped to "Open in New
                // Browser Tab" (a fresh instance via window.open, not true state transfer; see
                // docs/BROWSER_TABSTRIP_CONTEXT_MENU_DECISION.md).
                var openNewTabItem = new MenuItem { Header = "Open in New Browser Tab" };
                openNewTabItem.Click += async (_, _) =>
                {
                    var topLevel = TopLevel.GetTopLevel(tabBorder);
                    if (topLevel?.Launcher is { } launcher)
                        await launcher.LaunchUriAsync(new Uri(Program.PageUrl));
                };
                // Issue #546: same action as the header "Copy Full Path", for the right-clicked tab.
                var copyPathTabItem = new MenuItem { Header = "Copy Full Path" };
                copyPathTabItem.Click += async (_, _) =>
                {
                    var topLevel = TopLevel.GetTopLevel(tabBorder);
                    if (topLevel?.Clipboard is { } clipboard)
                        await clipboard.SetTextAsync(captured.Path.FullPath);
                };
                var closeTabItem = new MenuItem { Header = "Close Tab" };
                closeTabItem.Click += (_, _) => CloseTab(captured);
                tabBorder.ContextMenu = new ContextMenu { Items = { openNewTabItem, copyPathTabItem, closeTabItem } };

                // Left-click activates (skipping the close button, which handles its own click);
                // middle-click closes, matching desktop.
                tabBorder.PointerPressed += (_, args) =>
                {
                    if (args.Source is Button) return;
                    if (args.GetCurrentPoint(tabBorder).Properties.IsLeftButtonPressed) SwitchToTab(captured);
                };
                tabBorder.PointerReleased += (_, args) =>
                {
                    if (args.GetCurrentPoint(tabBorder).Properties.PointerUpdateKind == PointerUpdateKind.MiddleButtonReleased)
                    {
                        CloseTab(captured);
                        args.Handled = true;
                    }
                };

                tabStrip.Children.Add(tabBorder);
            }
            RefreshHeaderAndStatusLeft();
        }

        // Mirrors desktop's CloseTab: close, then activate whatever TabManager.Close already
        // picked as the next active tab (its own doc comment: next tab, else previous, else
        // null). A null ActiveTab means the last tab was closed -- start fresh with a blank
        // animation chain list, same fallback desktop uses.
        void CloseTab(TabEntry tab)
        {
            tabManager.Close(tab.Path);
            tabFileHandles.Remove(tab);
            var next = tabManager.ActiveTab;
            if (next != null)
            {
                appCommands.TryActivateTabFromCache(next);
                undoManager.RestoreSnapshot(next.UndoSnapshot ?? new UndoSnapshot(new List<IUndoableCommand>(), new List<IUndoableCommand>()));
            }
            else
            {
                projectManager.AnimationChainListSave = new AnimationChainListSave();
                projectManager.FileName = null;
                selectedState.Reset();
                undoManager.Clear();
            }
            UpdateUndoRedoButtons();
            animationTree.InitializeServices(selectedState, projectManager.AnimationChainListSave);
            // #687: InitializeServices always rebuilds fresh VMs (default-collapsed) -- restore
            // whatever expand state (including frame nodes with shape children) was captured for
            // `next` the last time it was the active tab (see SwitchToTab below).
            if (next != null) animationTree.ApplyExpandState(next.CachedTreeExpandState);
            textureListPanel.SetAnimationChainList(projectManager.AnimationChainListSave);
            RebuildTabStrip();
        }

        // Phase 8 (#648): feeds the branded header's filename and the status bar's left zone
        // (desktop's "filename + counts" zone, minus the save-state dot -- browser has no
        // disk-backed dirty tracking to show honestly). Called whenever the active tab changes
        // (via RebuildTabStrip) and whenever chains are added/removed (count changes without a
        // tab switch).
        void RefreshHeaderAndStatusLeft()
        {
            var displayName = tabManager.ActiveTab?.DisplayName ?? "Untitled";
            headerFileNameText.Text = displayName;
            var chainCount = projectManager.AnimationChainListSave?.AnimationChains.Count ?? 0;
            statusLeftText.Text = $"{displayName} · {chainCount} animation{(chainCount == 1 ? "" : "s")}";
        }

        // Captures the leaving tab's project/undo state, activates the target tab from its
        // cache (always fresh -- see the note above), and restores its undo history. Mirrors
        // MainWindow's own tab-switch sequence (capture outgoing -> TryActivateTabFromCache ->
        // RestoreSnapshot), minus the disk-reload fallback ActivateTabContentAsync has for a
        // stale cache, which browser tabs never hit.
        void SwitchToTab(TabEntry target)
        {
            if (target == tabManager.ActiveTab) return;

            var leaving = tabManager.ActiveTab;
            if (leaving != null)
            {
                tabController.CaptureLeavingTab(leaving);
            }

            tabManager.Activate(target.Path);
            appCommands.TryActivateTabFromCache(target);
            undoManager.RestoreSnapshot(target.UndoSnapshot ?? new UndoSnapshot(new List<IUndoableCommand>(), new List<IUndoableCommand>()));
            UpdateUndoRedoButtons();

            animationTree.InitializeServices(selectedState, projectManager.AnimationChainListSave);
            animationTree.ApplyExpandState(target.CachedTreeExpandState);
            textureListPanel.SetAnimationChainList(projectManager.AnimationChainListSave);
            RebuildTabStrip();
        }

        // Opens a new tab for the file BrowserProjectLoader just finished loading into
        // projectManager, capturing the tab that's being left first so switching back to it
        // later restores its state. displayName is the .achx's own file name (BrowserProjectLoader
        // uses achxFile.Name as ProjectManager.FileName's logical identity).
        void OpenNewTabForLoadedProject(string displayName)
        {
            var leaving = tabManager.ActiveTab;
            if (leaving != null)
            {
                tabController.CaptureLeavingTab(leaving);
            }

            tabManager.OpenOrFocus(new FilePath(displayName), displayName);
            appCommands.CaptureTabEditorState(tabManager.ActiveTab!);
            undoManager.Clear();
            UpdateUndoRedoButtons();
            RebuildTabStrip();
        }

        RebuildTabStrip();

        // Both Add/Delete commands raise AnimationChainsChanged -- refresh the tree once, here,
        // rather than after every individual button handler. Also refreshes the status bar's
        // animation count (Phase 8, #648) since that changes without a tab switch.
        applicationEvents.AnimationChainsChanged += animationTree.Refresh;
        applicationEvents.AnimationChainsChanged += RefreshHeaderAndStatusLeft;
        // Phase 12 (#655): a new/deleted frame can add or remove a referenced texture without a
        // tab switch (e.g. Add Frame on a chain that borrows a different texture).
        applicationEvents.AnimationChainsChanged += () => textureListPanel.SetAnimationChainList(projectManager.AnimationChainListSave);

        addAnimationButton.Click += (_, _) => appCommands.AddNewAnimationChain();

        addFrameButton.Click += (_, _) =>
        {
            if (selectedState.SelectedChain is { } chain)
                appCommands.AddFrame(chain);
        };

        addRectButton.Click += (_, _) =>
        {
            if (selectedState.SelectedFrame is { } frame)
                appCommands.AddAxisAlignedRectangle(frame);
        };

        addCircleButton.Click += (_, _) =>
        {
            if (selectedState.SelectedFrame is { } frame)
                appCommands.AddCircle(frame);
        };

        deleteSelectedButton.Click += (_, _) =>
        {
            if (selectedState.SelectedRectangle is { } rect && selectedState.SelectedFrame is { } rectFrame)
                appCommands.DeleteShapes(rectFrame, new List<AARectSave> { rect }, new List<CircleSave>());
            else if (selectedState.SelectedCircle is { } circle && selectedState.SelectedFrame is { } circleFrame)
                appCommands.DeleteShapes(circleFrame, new List<AARectSave>(), new List<CircleSave> { circle });
            else if (selectedState.SelectedFrame is { } frame)
                appCommands.DeleteFrames(new List<AnimationFrameSave> { frame });
            else if (selectedState.SelectedChain is { } selectedChain)
                appCommands.DeleteAnimationChains(new List<AnimationChainSave> { selectedChain });
        };

        historyUndoButton.Click += (_, _) => undoManager.Undo();
        historyRedoButton.Click += (_, _) => undoManager.Redo();

        // #535 M3 follow-up: no FileSystemWatcher in the browser, so texture edits made outside
        // the page (e.g. re-saving a PNG in an image editor) are only detected by polling
        // GetBasicPropertiesAsync (see BrowserFolderWatcher). Detected changes are queued here and
        // applied only when the user clicks Reload -- matching "see a diff, prompt to refresh"
        // rather than silently swapping textures out from under the user mid-edit.
        BrowserFolderWatcher? folderWatcher = null;
        IEditorFolder? watchedFolder = null;
        var pendingChangedPngs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        MenuItem? menuReloadTextures = null;

        void UpdateReloadButton()
        {
            var hasPending = pendingChangedPngs.Count > 0;
            reloadButton.IsVisible = hasPending;
            reloadButton.Content = $"Reload Changed Textures ({pendingChangedPngs.Count})";
            if (menuReloadTextures is not null)
            {
                menuReloadTextures.IsEnabled = hasPending;
                menuReloadTextures.Header = hasPending
                    ? $"Reload Changed _Textures ({pendingChangedPngs.Count})"
                    : "Reload Changed _Textures";
            }
        }

        reloadButton.Click += async (_, _) =>
        {
            if (watchedFolder is null || pendingChangedPngs.Count == 0) return;

            foreach (var name in pendingChangedPngs)
            {
                var file = await watchedFolder.GetFileAsync(name);
                if (file is null) continue;

                await using var pngStream = await file.OpenReadAsync();
                using var buffer = new System.IO.MemoryStream();
                await pngStream.CopyToAsync(buffer);

                var bitmap = SKBitmap.Decode(buffer.ToArray());
                if (bitmap is null) continue;

                thumbnailService.InvalidatePath(name);
                thumbnailService.SeedTexture(name, bitmap);
            }

            status.Text = $"Reloaded {pendingChangedPngs.Count} texture(s).";
            notifications.ShowToast($"Reloaded {pendingChangedPngs.Count} texture(s).");
            pendingChangedPngs.Clear();
            UpdateReloadButton();
            preview.InvalidateVisual();
        };

        // Phase 5 (#622): view/canvas polish. Every toggle here is already-built, already-tested
        // control state (PreviewControl.ShowOnionSkin/InterpolateOffsets/ShowOrigin/ShowUserGuides,
        // TextureViewport.DiagnosticsEnabled/SetZoomPercent/SetGrid) that MainWindow exposes via
        // its own toolbar controls on desktop -- this is the same wiring, minus persistence
        // (zoom%/grid-size/guide positions live in the desktop-only .aeproperties companion file,
        // which the browser has no persistence path for yet -- same gap already flagged in
        // docs/BROWSER_SETTINGS_DECISION.md for Phase 2's zoom/grid settings).
        // MinHeight 0 + Padding override (#501-style): Fluent forces ToggleButton min height ~32
        // and vertical padding that, combined with Height=26, clips descenders (the "p" in
        // "Interpolate") against the preview ruler below.
        var onionSkinButton = new ToggleButton
        {
            Height = 26, MinHeight = 0, Padding = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Content = IconLabel("IconOnionSkin", "Onion Skin"),
        };
        onionSkinButton.Click += (_, _) => preview.ShowOnionSkin = onionSkinButton.IsChecked == true;

        var interpolateButton = new ToggleButton
        {
            Height = 26, MinHeight = 0, Padding = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            // TextBlock (not a bare string) so VerticalAlignment.Center actually applies —
            // Fluent's string ContentPresenter top-aligns single-line text in a 26px button.
            Content = new TextBlock
            {
                Text = "Interpolate",
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        interpolateButton.Click += (_, _) => preview.InterpolateOffsets = interpolateButton.IsChecked == true;

        // Ruler-click-drag-to-create/move/right-click-to-remove guides is entirely self-contained
        // inside PreviewControl's own pointer handlers (present since Phase 1's wiring).
        var showOriginButton = new ToggleButton
        {
            Height = 26, MinHeight = 0, Padding = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 4, 0),
            Content = IconLabel("IconCrosshair", "Origin"),
        };
        showOriginButton.Click += (_, _) => preview.ShowOrigin = showOriginButton.IsChecked == true;

        // Only shown once the user has placed at least one guide (#689); mirrors the checked
        // state to PreviewControl.ShowUserGuides, including the auto-reveal-on-create case,
        // which happens without going through this button's own click handler.
        var showUserGuidesButton = new ToggleButton
        {
            Height = 26, MinHeight = 0, Padding = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            IsVisible = false,
            IsChecked = true,
            Content = IconLabel("IconGuides", "Guides"),
        };
        bool suppressGuideVisibilitySync = false;
        showUserGuidesButton.Click += (_, _) =>
        {
            if (suppressGuideVisibilitySync) return;
            preview.ShowUserGuides = showUserGuidesButton.IsChecked == true;
        };
        void UpdateGuideToggleVisibility()
        {
            showUserGuidesButton.IsVisible = preview.HGuideCount > 0 || preview.VGuideCount > 0;
            suppressGuideVisibilitySync = true;
            showUserGuidesButton.IsChecked = preview.ShowUserGuides;
            suppressGuideVisibilitySync = false;
        }
        preview.GuidesChanged += UpdateGuideToggleVisibility;

        var diagnosticsButton = new ToggleButton { Content = "Diagnostics (F3)", IsVisible = false };
        void ApplyDiagnostics(bool on)
        {
            wireframe.DiagnosticsEnabled = on;
            preview.DiagnosticsEnabled = on;
            diagnosticsButton.IsChecked = on;
        }
        diagnosticsButton.Click += (_, _) => ApplyDiagnostics(diagnosticsButton.IsChecked == true);

        // Phase 8 (#648): ZoomControl is the same reusable, already-tested, DynamicResource-styled
        // [−][editable %][+] widget desktop's wireframe/preview toolbars and PNG diff bar all
        // share (AnimationEditor.Views/Controls/ZoomControl.axaml). Attach() installs the preset
        // list and follows/drives the target's zoom, replacing the four bespoke +/- buttons and
        // their manual ZoomPresetStepper calls below with one shared control per viewport.
        var wireframeZoom = new ZoomControl();
        wireframeZoom.Attach(wireframe);

        var previewZoom = new ZoomControl();
        previewZoom.Attach(preview);

        var snapToGridCheck = new ToggleButton
        {
            Height = 26, MinHeight = 0, Padding = new Thickness(10, 0),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            IsChecked = false,
            IsThreeState = false,
            Content = IconLabel("IconGrid", "Grid"),
        };
        ToolTip.SetTip(snapToGridCheck, "Snap to Grid");
        // Ensure the wireframe starts with grid off (matches desktop's WireframeCtrl.SetGrid(false, 16)).
        wireframe.SetGrid(false, 16);
        var gridSizeInput = new TextBox
        {
            Text = "16",
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0),
            FontSize = 11,
            MinWidth = 0,
            MinHeight = 0,
            Height = 26,
            VerticalAlignment = VerticalAlignment.Center,
        };
        // Classes="flanker" (ThemeStyles) zeros MinHeight/Padding and centers content — without
        // it Fluent's default button chrome top-aligns "−"/"+" inside the 26px pill.
        var gridSizeMinusBtn = new Button
        {
            Classes = { "flanker" },
            Content = "−",
            Width = 22,
            Height = 26,
            BorderThickness = new Thickness(0, 0, 1, 0),
        };
        gridSizeMinusBtn.Bind(Button.BorderBrushProperty, gridSizeMinusBtn.GetResourceObservable("LineBrush"));
        var gridSizePlusBtn = new Button
        {
            Classes = { "flanker" },
            Content = "+",
            Width = 22,
            Height = 26,
            BorderThickness = new Thickness(1, 0, 0, 0),
        };
        gridSizePlusBtn.Bind(Button.BorderBrushProperty, gridSizePlusBtn.GetResourceObservable("LineBrush"));

        int GetGridSizeFromInput() => NumericToolbarInput.ParseGridSize(gridSizeInput.Text);

        void ApplyGrid()
        {
            var size = GetGridSizeFromInput();
            gridSizeInput.Text = size.ToString();
            wireframe.SetGrid(snapToGridCheck.IsChecked == true, size);
        }

        var gridSizeDock = new DockPanel();
        DockPanel.SetDock(gridSizeMinusBtn, Dock.Left);
        DockPanel.SetDock(gridSizePlusBtn, Dock.Right);
        gridSizeDock.Children.Add(gridSizeMinusBtn);
        gridSizeDock.Children.Add(gridSizePlusBtn);
        gridSizeDock.Children.Add(gridSizeInput);
        var gridSizePill = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Height = 26,
            Margin = new Thickness(0, 0, 2, 0),
            VerticalAlignment = VerticalAlignment.Center,
            IsEnabled = false,
            Child = gridSizeDock,
        };
        gridSizePill.Bind(Border.BorderBrushProperty, gridSizePill.GetResourceObservable("LineBrush"));

        // Click (same as Onion Skin / Guides) — IsCheckedChanged alone was unreliable in the
        // browser Fluent ToggleButton path after the toolbar-placement pass; Click always fires
        // after the toggle flips and is the path the other view toggles already use.
        void OnGridToggle()
        {
            gridSizePill.IsEnabled = snapToGridCheck.IsChecked == true;
            ApplyGrid();
        }
        snapToGridCheck.Click += (_, _) => OnGridToggle();
        snapToGridCheck.IsCheckedChanged += (_, _) => OnGridToggle();
        gridSizeInput.LostFocus += (_, _) => ApplyGrid();
        gridSizeInput.KeyDown += (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                ApplyGrid();
                e.Handled = true;
            }
        };
        gridSizeMinusBtn.Click += (_, _) =>
        {
            gridSizeInput.Text = Math.Max(GetGridSizeFromInput() - 1, 1).ToString();
            ApplyGrid();
        };
        gridSizePlusBtn.Click += (_, _) =>
        {
            gridSizeInput.Text = Math.Min(GetGridSizeFromInput() + 1, 512).ToString();
            ApplyGrid();
        };

        // PixiJsSpriteSheetExporter.Export is the same pure, already-tested core desktop's
        // AppCommands.ExportToPixiJsAsync calls -- what differs here is entirely the output path:
        // desktop writes the JSON + copies referenced PNGs to disk next to it; the browser has no
        // disk to write to, so both the JSON and each referenced texture (re-encoded from
        // ThumbnailService's already-decoded bitmap, never read from disk) are handed to the
        // browser as Blob downloads instead (see DownloadInterop / wwwroot/download.js).
        var exportPixiJsButton = new Button { Content = "Export to PixiJS", IsVisible = false };
        exportPixiJsButton.Click += (_, _) =>
        {
            var currentAcls = projectManager.AnimationChainListSave;
            if (currentAcls is null)
            {
                notifications.ShowErrorBanner("Nothing to export.");
                return;
            }

            (int Width, int Height)? ResolveTextureSize(string name)
            {
                var bmp = thumbnailService.GetBitmap(name);
                return bmp is null ? null : (bmp.Width, bmp.Height);
            }

            var result = PixiJsSpriteSheetExporter.Export(currentAcls, ResolveTextureSize);
            var baseName = string.IsNullOrEmpty(projectManager.FileName)
                ? "spritesheet"
                : System.IO.Path.GetFileNameWithoutExtension(projectManager.FileName);

            DownloadInterop.DownloadText($"{baseName}.json", result.Json, "application/json");

            var warnings = new List<string>(result.Warnings);
            foreach (var textureName in result.ReferencedTextures)
            {
                var bitmap = thumbnailService.GetBitmap(textureName);
                if (bitmap is null)
                {
                    warnings.Add($"Texture '{textureName}' was not found in memory, so it was not downloaded.");
                    continue;
                }

                using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
                var base64 = System.Convert.ToBase64String(data.ToArray());
                DownloadInterop.DownloadBase64(System.IO.Path.GetFileName(textureName), base64, "image/png");
            }

            status.Text = warnings.Count == 0
                ? $"Exported {baseName}.json and {result.ReferencedTextures.Count} texture(s)."
                : $"Exported {baseName}.json with {warnings.Count} warning(s): {string.Join(' ', warnings)}";
            notifications.ShowToast(warnings.Count == 0
                ? $"Exported {baseName}.json and {result.ReferencedTextures.Count} texture(s)."
                : $"Exported {baseName}.json with {warnings.Count} warning(s).");
        };

        var wireframeZoomLabel = new TextBlock
        {
            Text = "Zoom:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            FontSize = 11,
        };
        wireframeZoomLabel.Bind(TextBlock.ForegroundProperty, wireframeZoomLabel.GetResourceObservable("InkMid"));

        var wireframeToolbar = ToolbarChrome(new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                editModePill,
                ToolbarDivider(),
                snapToGridCheck,
                gridSizePill,
                ToolbarDivider(),
                wireframeZoomLabel,
                wireframeZoom,
            },
        });

        var previewZoomLabel = new TextBlock
        {
            Text = "Zoom:",
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 4, 0),
            FontSize = 11,
        };
        previewZoomLabel.Bind(TextBlock.ForegroundProperty, previewZoomLabel.GetResourceObservable("InkMid"));

        var previewToolbar = ToolbarChrome(new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Children =
            {
                onionSkinButton,
                showOriginButton,
                showUserGuidesButton,
                interpolateButton,
                ToolbarDivider(),
                previewZoomLabel,
                previewZoom,
            },
        });

        // Phase 9 (#649): matches desktop's sidebar shape -- ANIMATIONS tree always visible (top),
        // GridSplitter, then a TabControl below with Inspector/History/Files tabs.
        // RowDefinitions "2*,4,3*" mirrors desktop's LeftPanelGrid proportions exactly.
        var inspectorTab = new TabItem
        {
            Header = "Inspector",
            FontSize = 11, FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Padding = new Thickness(12, 0), Height = 36, MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Border { Child = inspector },
        };
        var historyTabHeader = new Border
        {
            Height = 30,
            BorderThickness = new Thickness(0, 0, 0, 1),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 2,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 4, 0),
                Children = { historyUndoButton, historyRedoButton },
            },
        };
        historyTabHeader.Bind(Border.BackgroundProperty, historyTabHeader.GetResourceObservable("BgRail"));
        historyTabHeader.Bind(Border.BorderBrushProperty, historyTabHeader.GetResourceObservable("LineBrush"));

        var historyScroll = new ScrollViewer
        {
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Disabled,
            Content = historyList,
        };
        historyScroll.Bind(ScrollViewer.BackgroundProperty, historyScroll.GetResourceObservable("BgPanel"));

        var historyContent = new Grid { RowDefinitions = new RowDefinitions("Auto,*") };
        Grid.SetRow(historyTabHeader, 0);
        Grid.SetRow(historyScroll, 1);
        historyContent.Children.Add(historyTabHeader);
        historyContent.Children.Add(historyScroll);
        historyContent.Bind(Grid.BackgroundProperty, historyContent.GetResourceObservable("BgPanel"));

        var historyTab = new TabItem
        {
            Header = "History",
            FontSize = 11, FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Padding = new Thickness(12, 0), Height = 36, MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = historyContent,
        };
        // Phase 12 (#655): "This File" scope only -- TextureListPanel.SetAnimationChainList is
        // re-pushed at every point animationTree.InitializeServices already is (tab switch/close,
        // Open Folder load, AnimationChainsChanged), since there's no single "the loaded file
        // changed" event to subscribe to instead. See docs/BROWSER_FILES_PANEL_DECISION.md.
        var filesTab = new TabItem
        {
            Header = "Files",
            FontSize = 11, FontWeight = Avalonia.Media.FontWeight.SemiBold,
            Padding = new Thickness(12, 0), Height = 36, MinHeight = 36,
            VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Border { Padding = new Thickness(4), Child = textureListPanel },
        };
        ((Border)inspectorTab.Content!).Bind(Border.BackgroundProperty, inspectorTab.GetResourceObservable("BgPanel"));
        ((Border)filesTab.Content!).Bind(Border.BackgroundProperty, filesTab.GetResourceObservable("BgPanel"));

        var sidebarTabs = new TabControl
        {
            Padding = new Thickness(0),
            Items = { inspectorTab, historyTab, filesTab },
        };
        sidebarTabs.Bind(TabControl.BackgroundProperty, sidebarTabs.GetResourceObservable("BgRail"));

        // Mirrors desktop's TabItem/TabItem:selected style pair (InkMid unselected, Ink selected)
        // -- only three fixed tabs here, so plain instance-level rebinding is simpler than a Style.
        void UpdateSidebarTabForegrounds()
        {
            inspectorTab.Bind(TabItem.ForegroundProperty,
                inspectorTab.GetResourceObservable(sidebarTabs.SelectedItem == inspectorTab ? "Ink" : "InkMid"));
            historyTab.Bind(TabItem.ForegroundProperty,
                historyTab.GetResourceObservable(sidebarTabs.SelectedItem == historyTab ? "Ink" : "InkMid"));
            filesTab.Bind(TabItem.ForegroundProperty,
                filesTab.GetResourceObservable(sidebarTabs.SelectedItem == filesTab ? "Ink" : "InkMid"));
        }
        sidebarTabs.SelectionChanged += (_, _) =>
        {
            UpdateSidebarTabForegrounds();
            // Re-push history rows when History is shown — TabControl may have detached the
            // ItemsControl while another sidebar tab was active, so edits made on Inspector
            // still appear the moment the user opens History.
            if (sidebarTabs.SelectedItem == historyTab)
                RefreshHistoryList();
        };
        UpdateSidebarTabForegrounds();

        var sidebarSplitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Rows,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        sidebarSplitter.Bind(GridSplitter.BackgroundProperty, sidebarSplitter.GetResourceObservable("LineStrong"));

        var addAnimationFooter = new Border
        {
            BorderThickness = new Thickness(0, 1, 0, 0),
            Padding = new Thickness(8, 6),
            Child = addAnimationButton,
        };
        addAnimationFooter.Bind(Border.BackgroundProperty, addAnimationFooter.GetResourceObservable("BgCanvas"));
        addAnimationFooter.Bind(Border.BorderBrushProperty, addAnimationFooter.GetResourceObservable("LineBrush"));
        addAnimationButton.Bind(Button.ForegroundProperty, addAnimationButton.GetResourceObservable("Ink"));

        var animationsBlock = new Grid { RowDefinitions = new RowDefinitions("*,Auto") };
        Grid.SetRow(animationTree, 0);
        Grid.SetRow(addAnimationFooter, 1);
        animationsBlock.Children.Add(animationTree);
        animationsBlock.Children.Add(addAnimationFooter);

        // Pixel column width (not Auto+Width) so the adjacent GridSplitter can redistribute —
        // Auto columns size to content and ignore drag. Matches desktop's "300,4,*".
        var leftColumn = new Grid
        {
            MinWidth = 180,
            RowDefinitions = new RowDefinitions("2*,4,3*"),
        };
        Grid.SetRow(animationsBlock, 0);
        Grid.SetRow(sidebarSplitter, 1);
        Grid.SetRow(sidebarTabs, 2);
        leftColumn.Children.Add(animationsBlock);
        leftColumn.Children.Add(sidebarSplitter);
        leftColumn.Children.Add(sidebarTabs);

        // Phase 14: portable timeline/scrubber strip + transport row below the preview canvas,
        // matching desktop's PreviewBlockGrid row-2 layout (52px fixed). Geometry/thumbnails live
        // in TimelineStripControl; this block is browser-only wiring glue.
        var timelineStrip = new TimelineStripControl();
        timelineStrip.InitializeServices(thumbnailService);

        var playPauseIcon = Icon("IconPlay", 14);
        var playPauseButton = new Button
        {
            Height = 26,
            Width = 32,
            VerticalAlignment = VerticalAlignment.Center,
            Content = playPauseIcon,
        };
        void UpdatePlayPauseChrome(bool isPlaying)
        {
            playPauseIcon.Path = isPlaying
                ? "avares://AnimationEditor.Views/Assets/icons/svg/IconPause.svg"
                : "avares://AnimationEditor.Views/Assets/icons/svg/IconPlay.svg";
            ToolTip.SetTip(playPauseButton, isPlaying ? "Pause" : "Play");
        }
        UpdatePlayPauseChrome(preview.Playback.IsPlaying);
        playPauseButton.Click += (_, _) => preview.TogglePlayPause();
        preview.IsPlayingChanged += UpdatePlayPauseChrome;

        var speedInput = new TextBox
        {
            Text = "1.0",
            HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
            TextAlignment = Avalonia.Media.TextAlignment.Center,
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(4, 0),
            FontSize = 11,
            MinWidth = 0,
            MinHeight = 0,
            Height = 26,
            VerticalAlignment = VerticalAlignment.Center,
        };
        double GetSpeedFromInput() => NumericToolbarInput.ParseSpeed(speedInput.Text);
        void ApplySpeedFromInput()
        {
            double s = GetSpeedFromInput();
            speedInput.Text = NumericToolbarInput.FormatSpeed(s);
            preview.SpeedMultiplier = s;
        }
        speedInput.LostFocus += (_, _) => ApplySpeedFromInput();
        var speedDownButton = new Button
        {
            Classes = { "flanker" },
            Content = "−",
            Width = 22,
        };
        speedDownButton.Bind(Button.BorderBrushProperty, speedDownButton.GetResourceObservable("LineBrush"));
        speedDownButton.Click += (_, _) =>
        {
            double s = Math.Max(Math.Round(GetSpeedFromInput() - 0.1, 1), 0.1);
            speedInput.Text = NumericToolbarInput.FormatSpeed(s);
            preview.SpeedMultiplier = s;
        };
        var speedUpButton = new Button
        {
            Classes = { "flanker" },
            Content = "+",
            Width = 22,
        };
        speedUpButton.Bind(Button.BorderBrushProperty, speedUpButton.GetResourceObservable("LineBrush"));
        speedUpButton.Click += (_, _) =>
        {
            double s = Math.Min(Math.Round(GetSpeedFromInput() + 0.1, 1), 10.0);
            speedInput.Text = NumericToolbarInput.FormatSpeed(s);
            preview.SpeedMultiplier = s;
        };
        var speedPill = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(4),
            ClipToBounds = true,
            Height = 26,
            Width = 92,
            VerticalAlignment = VerticalAlignment.Center,
            Child = new DockPanel
            {
                Children =
                {
                    speedDownButton,
                    speedUpButton,
                    speedInput,
                },
            },
        };
        ToolTip.SetTip(speedPill, "Playback speed (1.0 = runtime speed)");
        speedPill.Bind(Border.BorderBrushProperty, speedPill.GetResourceObservable("LineBrush"));
        DockPanel.SetDock(speedDownButton, Dock.Left);
        speedDownButton.BorderThickness = new Thickness(0, 0, 1, 0);
        DockPanel.SetDock(speedUpButton, Dock.Right);
        speedUpButton.BorderThickness = new Thickness(1, 0, 0, 0);

        void RefreshTimelineStrip()
        {
            var chain = TimelineChainResolver.GetChain(selectedState, objectFinder);
            int preferred = TimelineChainResolver.GetPreferredFrameIndex(
                selectedState, objectFinder, chain, preview.Playback.CurrentFrameIndex);
            timelineStrip.SetChain(chain, preferred);
            if (preferred >= 0)
                timelineStrip.ApplyPlaybackPosition(preferred, preview.Playback.FrameElapsed);
        }

        timelineStrip.FrameScrubbed += (frameIndex, fraction) =>
        {
            preview.ScrubToFrame(frameIndex, fraction);
            UpdatePlayPauseChrome(preview.Playback.IsPlaying);
        };
        selectedState.SelectionChanged += RefreshTimelineStrip;
        applicationEvents.AnimationChainsChanged += RefreshTimelineStrip;
        preview.Playback.FrameIndexChanged += index =>
        {
            if (selectedState.SelectedFrame is not null) return;
            timelineStrip.SetChain(TimelineChainResolver.GetChain(selectedState, objectFinder), index);
        };
        preview.Playback.PlaybackTicked += () =>
        {
            if (selectedState.SelectedFrame is not null) return;
            timelineStrip.ApplyPlaybackPosition(
                preview.Playback.CurrentFrameIndex,
                preview.Playback.FrameElapsed);
        };

        var transportColumn = new Border
        {
            BorderThickness = new Thickness(0, 1, 1, 0),
            Padding = new Thickness(6, 0),
            Child = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Children = { playPauseButton, speedPill },
            },
        };
        transportColumn.Bind(Border.BackgroundProperty, transportColumn.GetResourceObservable("BgRail"));
        transportColumn.Bind(Border.BorderBrushProperty, transportColumn.GetResourceObservable("LineBrush"));

        var timelineRow = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(transportColumn, 0);
        Grid.SetColumn(timelineStrip, 1);
        timelineRow.Children.Add(transportColumn);
        timelineRow.Children.Add(timelineStrip);

        var previewBlock = new Grid
        {
            MinHeight = 80,
            RowDefinitions = new RowDefinitions("Auto,*,52"),
        };
        Grid.SetRow(previewToolbar, 0);
        Grid.SetRow(preview, 1);
        Grid.SetRow(timelineRow, 2);
        previewBlock.Children.Add(previewToolbar);
        previewBlock.Children.Add(preview);
        previewBlock.Children.Add(timelineRow);
        RefreshTimelineStrip();

        // Phase 10 (#652): matches desktop's AchxEditorPane -- wireframe stacked over preview
        // (with a draggable row splitter), replacing the previous fixed side-by-side two-column
        // layout. Both are independent TextureViewport-derived controls; neither depends on this
        // layout shape, so this is a pure container reflow -- their pan/zoom math is
        // bounds-relative to their own control size, not the window/grid shape.
        var canvasSplitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Rows,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        canvasSplitter.Bind(GridSplitter.BackgroundProperty, canvasSplitter.GetResourceObservable("LineStrong"));

        var canvasColumn = new Grid
        {
            RowDefinitions = new RowDefinitions("Auto,*,4,*"),
        };
        Grid.SetRow(wireframeToolbar, 0);
        Grid.SetRow(wireframe, 1);
        Grid.SetRow(canvasSplitter, 2);
        Grid.SetRow(previewBlock, 3);
        canvasColumn.Children.Add(wireframeToolbar);
        canvasColumn.Children.Add(wireframe);
        canvasColumn.Children.Add(canvasSplitter);
        canvasColumn.Children.Add(previewBlock);

        var sidebarColumnSplitter = new GridSplitter
        {
            ResizeDirection = GridResizeDirection.Columns,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        sidebarColumnSplitter.Bind(GridSplitter.BackgroundProperty, sidebarColumnSplitter.GetResourceObservable("LineStrong"));

        // Fixed pixel + star (not Auto) so dragging the splitter actually changes column widths.
        var mainArea = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("260,4,*"),
        };
        Grid.SetColumn(leftColumn, 0);
        Grid.SetColumn(sidebarColumnSplitter, 1);
        Grid.SetColumn(canvasColumn, 2);
        mainArea.Children.Add(leftColumn);
        mainArea.Children.Add(sidebarColumnSplitter);
        mainArea.Children.Add(canvasColumn);

        // Phase 8 (#648): desktop's status bar has 3 zones (save-state+filename+counts |
        // cursor+selection | transient toast); browser only has data for 2 -- there's no
        // disk-backed dirty flag or cursor-position tracking to show honestly, so the middle
        // zone is dropped rather than faked.
        var statusBar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,*") };
        Grid.SetColumn(statusLeftText, 0);
        Grid.SetColumn(status, 1);
        statusBar.Children.Add(statusLeftText);
        statusBar.Children.Add(status);
        statusBar.Bind(Grid.BackgroundProperty, statusBar.GetResourceObservable("BgRail"));

        // Phase 13 (#662): File/Edit/View/Help menu bar. Mostly wiring -- every item here
        // delegates to a command already exposed via a toolbar button in Phases 2/5/8, either by
        // re-raising that button's existing Click handler (RoutedEventArgs(Button.ClickEvent),
        // zero logic duplication) or calling the same already-tested helper the button's handler
        // calls. Items with no real browser-side implementation (Load Recent, Copy/Cut/Paste/
        // Duplicate, Settings, View Log) are omitted rather than shown as disabled no-ops --
        // see docs/BROWSER_MENU_BAR_DECISION.md. No window controls (minimize/maximize/close) --
        // the browser tab already has real OS chrome for those.
        int untitledCounter = 0;
        var menuNew = new MenuItem { Header = "_New" };
        menuNew.Click += (_, _) =>
        {
            var leaving = tabManager.ActiveTab;
            if (leaving != null)
            {
                appCommands.CaptureTabEditorState(leaving);
                leaving.UndoSnapshot = undoManager.TakeSnapshot();
            }
            appCommands.NewFile();
            var displayName = TabManager.ComputeUntitledDisplayName(tabManager.Tabs.Select(t => t.DisplayName).ToList());
            tabManager.OpenOrFocus(new FilePath($"untitled-{++untitledCounter}"), displayName);
            appCommands.CaptureTabEditorState(tabManager.ActiveTab!);
            undoManager.Clear();
            UpdateUndoRedoButtons();
            animationTree.InitializeServices(selectedState, projectManager.AnimationChainListSave);
            textureListPanel.SetAnimationChainList(projectManager.AnimationChainListSave);
            RebuildTabStrip();
        };
        var menuLoad = new MenuItem { Header = "_Load Folder…" };
        menuLoad.Click += (_, _) => openButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menuSave = new MenuItem { Header = "_Save" };
        menuSave.Click += (_, _) => saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menuSaveAs = new MenuItem { Header = "Save _As…" };
        menuSaveAs.Click += (_, _) => saveAsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menuExport = new MenuItem { Header = "_Export to PixiJS" };
        menuExport.Click += (_, _) => exportPixiJsButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var fileMenu = new MenuItem
        {
            Header = "_File",
            Items = { menuNew, menuLoad, new Separator(), menuSave, menuSaveAs, new Separator(), menuExport },
        };

        var menuUndo = new MenuItem { Header = "_Undo" };
        menuUndo.Click += (_, _) => historyUndoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menuRedo = new MenuItem { Header = "_Redo" };
        menuRedo.Click += (_, _) => historyRedoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menuAddFrame = new MenuItem { Header = "Add _Frame" };
        menuAddFrame.Click += (_, _) => addFrameButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menuAddRect = new MenuItem { Header = "Add _Rectangle" };
        menuAddRect.Click += (_, _) => addRectButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menuAddCircle = new MenuItem { Header = "Add _Circle" };
        menuAddCircle.Click += (_, _) => addCircleButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menuDeleteSelected = new MenuItem { Header = "_Delete Selected" };
        menuDeleteSelected.Click += (_, _) => deleteSelectedButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        var menuReload = new MenuItem { Header = "Reload Changed _Textures", IsEnabled = false };
        menuReload.Click += (_, _) => reloadButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        menuReloadTextures = menuReload;
        var editMenu = new MenuItem
        {
            Header = "_Edit",
            Items =
            {
                menuUndo, menuRedo, new Separator(),
                menuAddFrame, menuAddRect, menuAddCircle, menuDeleteSelected,
                new Separator(), menuReload,
            },
        };

        var menuWireframeZoomIn = new MenuItem { Header = "_Wireframe Zoom In" };
        menuWireframeZoomIn.Click += (_, _) => wireframeZoom.StepUp();
        var menuWireframeZoomOut = new MenuItem { Header = "Wireframe Zoom _Out" };
        menuWireframeZoomOut.Click += (_, _) => wireframeZoom.StepDown();
        var menuPreviewZoomIn = new MenuItem { Header = "_Preview Zoom In" };
        menuPreviewZoomIn.Click += (_, _) => previewZoom.StepUp();
        var menuPreviewZoomOut = new MenuItem { Header = "Preview Zoom O_ut" };
        menuPreviewZoomOut.Click += (_, _) => previewZoom.StepDown();
        var menuShowHistory = new MenuItem { Header = "Show _History" };
        menuShowHistory.Click += (_, _) => sidebarTabs.SelectedItem = historyTab;
        var menuThemeLight = new MenuItem { Header = "_Light" };
        menuThemeLight.Click += (_, _) => SetTheme(AppTheme.Light);
        var menuThemeDark = new MenuItem { Header = "_Dark" };
        menuThemeDark.Click += (_, _) => SetTheme(AppTheme.Dark);
        var menuTheme = new MenuItem { Header = "_Theme", Items = { menuThemeLight, menuThemeDark } };
        var viewMenu = new MenuItem
        {
            Header = "_View",
            Items =
            {
                menuWireframeZoomIn, menuWireframeZoomOut, menuPreviewZoomIn, menuPreviewZoomOut,
                new Separator(), menuShowHistory, new Separator(), menuTheme,
            },
        };

        var menuDiagnostics = new MenuItem { Header = "_Diagnostics (F3)" };
        menuDiagnostics.Click += (_, _) => ApplyDiagnostics(diagnosticsButton.IsChecked != true);
        var menuAbout = new MenuItem { Header = "_About" };
        menuAbout.Click += (_, _) => status.Text = "Animation Editor (Avalonia Browser build).";
        var helpMenu = new MenuItem { Header = "_Help", Items = { menuDiagnostics, new Separator(), menuAbout } };

        var menuBar = new Menu { Items = { fileMenu, editMenu, viewMenu, helpMenu } };
        menuBar.Bind(Menu.BackgroundProperty, menuBar.GetResourceObservable("BgRail"));

        var root = new DockPanel();
        DockPanel.SetDock(headerBar, Dock.Top);
        root.Children.Add(headerBar);
        DockPanel.SetDock(menuBar, Dock.Top);
        root.Children.Add(menuBar);
        DockPanel.SetDock(tabBarBorder, Dock.Top);
        root.Children.Add(tabBarBorder);
        DockPanel.SetDock(statusBar, Dock.Bottom);
        root.Children.Add(statusBar);
        root.Children.Add(mainArea);

        // F3 is a best-effort accelerator only -- the diagnostics button above is the reliable
        // path, since browsers may intercept F3 themselves (e.g. "Find next") before the page
        // ever sees it.
        //
        // Dispatch goes through the same AnimationEditor.Core.Hotkeys.HotkeyRegistry.FindMatch
        // the desktop host uses (MainWindow.WireKeyboard/BuildHotkeyDefinitions), instead of a
        // hand-rolled if/else chain, so keypress-to-gesture matching exists in exactly one place
        // (#748). Ids/gestures below mirror BuildHotkeyDefinitions's save/undo/redo/toggle-
        // diagnostics entries -- keep them in sync if that table changes. Everything else in the
        // desktop table is left out: New/Load/Duplicate/panel-zoom-in/panel-zoom-out are
        // hard-reserved by the browser itself (BrowserHotkeys.ReservedIds -- Ctrl+N/L/D/+/- are
        // intercepted before the page ever sees them), and Copy/Cut/Paste/Delete/Rename/Space/
        // Move-up/Move-down have no action to wire in this build yet.
        var browserHotkeys = BrowserHotkeys.Filter(new List<HotkeyDefinition>
        {
            new()
            {
                Id = "save", Description = "Save", Category = "File",
                Gestures = new[] { new HotkeyGesture("S", HotkeyModifiers.Command) },
                Action = () => saveButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),
            },
            new()
            {
                Id = "undo", Description = "Undo", Category = "Edit",
                Gestures = new[] { new HotkeyGesture("Z", HotkeyModifiers.Command, Forbidden: HotkeyModifiers.Shift) },
                Action = () => historyUndoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),
            },
            new()
            {
                Id = "redo", Description = "Redo", Category = "Edit",
                Gestures = new[]
                {
                    new HotkeyGesture("Y", HotkeyModifiers.Command),
                    new HotkeyGesture("Z", HotkeyModifiers.Command | HotkeyModifiers.Shift, Forbidden: HotkeyModifiers.Alt),
                },
                Action = () => historyRedoButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)),
            },
            new()
            {
                Id = "toggle-diagnostics", Description = "Toggle Render Diagnostics", Category = "View",
                Gestures = new[] { new HotkeyGesture("F3") },
                Action = () => ApplyDiagnostics(diagnosticsButton.IsChecked != true),
            },
        });

        root.AttachedToVisualTree += (_, _) =>
        {
            var topLevelForKeys = TopLevel.GetTopLevel(root);
            if (topLevelForKeys is null) return;
            topLevelForKeys.KeyDown += (_, e) =>
            {
                if (e.Handled) return;

                var match = HotkeyRegistry.FindMatch(browserHotkeys, e.Key.ToString(), ToHotkeyModifiers(e.KeyModifiers));
                if (match is null) return;

                e.Handled = true;
                match.Action();
            };
        };

        DragDrop.SetAllowDrop(root, true);
        root.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        });
        root.AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            var rawFiles = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().ToList();
            if (rawFiles is null || rawFiles.Count == 0) return;

            var files = rawFiles.Select(f => (IEditorFile)new AvaloniaFileAdapter(f)).ToList();
            var achxFile = await BrowserProjectLoader.TryLoadAsync(
                files, projectManager, thumbnailService, selectedState);

            if (achxFile is not null)
            {
                animationTree.InitializeServices(selectedState, projectManager.AnimationChainListSave);
            textureListPanel.SetAnimationChainList(projectManager.AnimationChainListSave);
                OpenNewTabForLoadedProject(projectManager.FileName ?? "Untitled");
                // Same readwrite upgrade as Open Folder used to attempt, requested here (right
                // after the drop gesture) rather than at Save time -- see StoragePermissionInterop.
                // Unlike Open Folder (NativeFolderInterop bypasses this entirely now), drag-drop
                // has no equivalent "ask for readwrite mode as part of the picker" option to
                // switch to, so this remains best-effort with a Save As fallback on denial.
                var achxRawFile = rawFiles.First(f => f.Name == achxFile.Name);
                var (canWrite, dropPermissionDiagnostic) = await StoragePermissionInterop.EnsureReadWriteAsync(achxRawFile);
                if (canWrite) tabFileHandles[tabManager.ActiveTab!] = achxFile;
                status.Text = $"Loaded from {rawFiles.Count} dropped file(s). [write-permission: {dropPermissionDiagnostic}]";
            }
            else
            {
                status.Text = "Drop must include an .achx file (drop its texture PNG(s) alongside it).";
            }
        });

        // Diagnostic-only: not a decision point, just a console breadcrumb so a live repro shows
        // exactly which await in the Open Folder chain never returns (session handoff's "freeze
        // after clicking Upload" is otherwise unreproducible by the agent -- see docs/
        // BROWSER_OPEN_FOLDER_SAVE_SESSION_HANDOFF.md "Suggested next steps" #1). Console.WriteLine
        // (not Debug.WriteLine) because browser-wasm reliably pipes stdout to the DevTools console
        // with no listener setup required; Debug.WriteLine does not.
        var openFolderStopwatch = new Stopwatch();
        void LogOpenFolderStep(string step) =>
            Console.WriteLine($"[OpenFolder] {openFolderStopwatch.ElapsedMilliseconds}ms: {step}");

        // #763 fallback: directory enumeration (dirHandle.entries(), used by
        // folder.GetItemsAsync() below) can throw NotFoundError on some environments even though
        // named lookups (getFileHandle) on the identical handle keep working -- confirmed live on
        // a real project folder, deterministic (retrying does not help). Recover by asking the
        // user to pick the .achx directly (seeded already inside the granted folder), then
        // resolve it and its textures by name instead of by enumerating. If this fallback itself
        // fails or is cancelled, it propagates/returns null and the caller's existing
        // catch (JSException) around LoadFromNativeDirectoryAsync still shows the error toast.
        async Task<IEditorFile?> LoadViaNamedAchxFallbackAsync(JSObject nativeDir)
        {
            var achxName = await NativeFolderInterop.PickAchxFileNameAsync(nativeDir);
            if (achxName is null) return null; // user cancelled the fallback picker

            var achxFile = new NativeReadWriteFile(nativeDir, achxName);

            async Task<IEditorFile?> ResolveTextureAsync(string textureName)
            {
                try
                {
                    var file = new NativeReadWriteFile(nativeDir, textureName);
                    await file.GetBasicPropertiesAsync(); // getFileHandle(name) -- proven-working named lookup
                    return file;
                }
                catch (JSException)
                {
                    // Separate, out-of-scope problem (#763): a texture living outside the
                    // granted folder, or genuinely missing -- skip it instead of failing the load.
                    return null;
                }
            }

            return await BrowserProjectLoader.TryLoadFromNamedAchxAsync(
                achxFile, ResolveTextureAsync, projectManager, thumbnailService, selectedState);
        }

        // File → Load Folder uses Avalonia's picker (only path that opens from WASM menus).
        // Write grant is requested immediately after pick via EnsureReadWriteAsync.
        async Task LoadFromNativeDirectoryAsync(JSObject nativeDir, string writeState)
        {
            LogOpenFolderStep($"LoadFromNativeDirectoryAsync start (writeState={writeState})");
            var folder = new NativeReadWriteFolder(nativeDir);

            IEditorFile? achxFile;
            try
            {
                var files = new List<IEditorFile>();
                await foreach (var item in folder.GetItemsAsync())
                    files.Add(item);
                LogOpenFolderStep($"folder.GetItemsAsync() done ({files.Count} file(s))");

                achxFile = await BrowserProjectLoader.TryLoadAsync(
                    files, projectManager, thumbnailService, selectedState);
            }
            catch (JSException ex)
            {
                LogOpenFolderStep($"folder.GetItemsAsync() failed ({ex.Message}) -- falling back to named .achx picker");
                achxFile = await LoadViaNamedAchxFallbackAsync(nativeDir);
            }
            LogOpenFolderStep($"achx resolution done (achxFile={achxFile?.Name ?? "null"})");

            var writeSuffix = " " + WritePermissionGate.FormatStatusSuffix(writeState);

            status.Text = achxFile is not null
                ? $"Loaded from folder \"{folder.Name}\".{writeSuffix}"
                : $"No .achx found in \"{folder.Name}\".{writeSuffix}";

            if (achxFile is not null)
            {
                animationTree.InitializeServices(selectedState, projectManager.AnimationChainListSave);
                textureListPanel.SetAnimationChainList(projectManager.AnimationChainListSave);
                OpenNewTabForLoadedProject(projectManager.FileName ?? folder.Name);
                tabFileHandles[tabManager.ActiveTab!] = new NativeReadWriteFile(nativeDir, achxFile.Name);
            }

            folderWatcher?.Dispose();
            pendingChangedPngs.Clear();
            UpdateReloadButton();
            watchedFolder = folder;
            folderWatcher = new BrowserFolderWatcher(folder, TimeSpan.FromSeconds(2));
            folderWatcher.ChangedPngsDetected += names =>
            {
                foreach (var name in names) pendingChangedPngs.Add(name);
                UpdateReloadButton();
            };
            await folderWatcher.StartAsync();
            LogOpenFolderStep("folderWatcher.StartAsync() done -- LoadFromNativeDirectoryAsync complete");
        }

        // Avalonia OpenFolderPickerAsync is required for File→Load Folder — WASM menu clicks are
        // not a browser user activation, so direct showDirectoryPicker JSImport never opens a dialog.
        // Request write immediately after pick (same handler) while activation may still be valid.
        openButton.Click += async (_, _) =>
        {
            openFolderStopwatch.Restart();
            LogOpenFolderStep("openButton.Click -- calling OpenFolderPickerAsync");

            var topLevel = TopLevel.GetTopLevel(openButton);
            if (topLevel is null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open Animation Folder",
                AllowMultiple = false,
            });
            // Timing from here on is diagnostic, not user think-time -- the dialog has already
            // closed once OpenFolderPickerAsync returns, so nothing past this point should
            // legitimately wait on further user input.
            LogOpenFolderStep($"OpenFolderPickerAsync returned ({folders.Count} folder(s))");
            var rawFolder = folders.FirstOrDefault();
            if (rawFolder is null) return;

            var nativeDir = StoragePermissionInterop.TryGetNativeFileSystemHandle(rawFolder);
            LogOpenFolderStep($"TryGetNativeFileSystemHandle done (nativeDir={(nativeDir is null ? "null" : "ok")})");
            if (nativeDir is null)
            {
                status.Text = "Open Folder failed: no native directory handle (use a Chromium-based browser).";
                return;
            }

            // Pick-time write grant — must run before long async load work, not at Save.
            LogOpenFolderStep("calling NativeFolderInterop.EnsureReadWriteAsync");
            var writeState = await NativeFolderInterop.EnsureReadWriteAsync(nativeDir);
            LogOpenFolderStep($"EnsureReadWriteAsync returned (writeState={writeState})");

            // Directory reads (enumeration, file access) can fail for reasons outside this app's
            // control -- confirmed live: a valid, correctly-permissioned handle can still throw
            // NotFoundError from dirHandle.entries() while getFileHandle() on the same handle
            // succeeds (likely security-software interference with directory listing specifically;
            // see docs/BROWSER_OPEN_FOLDER_SAVE_SESSION_HANDOFF.md). WASM is single-threaded, so an
            // unhandled JSException here would abort the whole runtime, not just this operation --
            // must catch and degrade gracefully instead.
            try
            {
                await LoadFromNativeDirectoryAsync(nativeDir, writeState);
            }
            catch (JSException ex)
            {
                LogOpenFolderStep($"LoadFromNativeDirectoryAsync failed: {ex.Message}");
                var message = OpenFolderLoadFailure.FormatMessage(
                    NativeFolderInterop.DirectoryName(nativeDir), ex.Message);
                status.Text = message;
                notifications.ShowErrorBanner(message);
            }
        };

        // Prompts for a new save location (the OS-level save dialog) and writes there --
        // shared by Save As (always) and Save's first-time fallback (no known location yet).
        async Task<IEditorFile?> PromptAndWriteAsync(Control anchor, AnimationChainListSave acls)
        {
            var topLevel = TopLevel.GetTopLevel(anchor);
            if (topLevel is null) return null;

            var rawFile = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Animation Chain As",
                SuggestedFileName = "player.achx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Animation Chain") { Patterns = new[] { "*.achx" } },
                },
            });
            if (rawFile is null) return null;

            await using var stream = await rawFile.OpenWriteAsync();
            await projectManager.SaveAnimationChainListAsync(stream);
            return new AvaloniaFileAdapter(rawFile);
        }

        // Pick-time write grant only — do not requestPermission here (WASM Save menu loses activation).
        async Task<(bool Ok, string Diagnostic)> TryWriteToKnownFileAsync(IEditorFile knownFile, AnimationChainListSave acls)
        {
            if (knownFile is NativeReadWriteFile native)
            {
                var queryState = await NativeFolderInterop.QueryWritePermissionAsync(native.DirectoryHandle);
                var (canSave, failure) = WritePermissionGate.EvaluateSaveFromQueryState(queryState);
                if (!canSave)
                    return (false, failure!);
            }

            var writeTask = WriteCoreAsync();
            var winner = await Task.WhenAny(writeTask, Task.Delay(TimeSpan.FromSeconds(6)));
            if (winner != writeTask)
                return (false, "timed out after 6s (likely a stuck permission check)");
            if (writeTask.IsFaulted)
                return (false, writeTask.Exception?.GetBaseException().Message ?? "write faulted");
            return (true, "ok");

            async Task WriteCoreAsync()
            {
                await using var stream = await knownFile.OpenWriteAsync();
                await projectManager.SaveAnimationChainListAsync(stream);
            }
        }

        saveButton.Click += async (_, _) =>
        {
            var acls = projectManager.AnimationChainListSave;
            if (acls is null)
            {
                notifications.ShowErrorBanner("Nothing to save.");
                return;
            }

            var activeTab = tabManager.ActiveTab;
            if (activeTab is not null && tabFileHandles.TryGetValue(activeTab, out var knownFile))
            {
                var (ok, diagnostic) = await TryWriteToKnownFileAsync(knownFile, acls);
                if (ok)
                {
                    status.Text = $"Saved to {knownFile.Name}.";
                    notifications.ShowToast($"Saved to {knownFile.Name}.");
                }
                else
                {
                    // Forget the handle rather than retrying it -- a subsequent Save falls
                    // through to Save As's prompt below instead of hanging again. Deliberately
                    // don't auto-open Save As here: if the real cause is a still-pending
                    // permission prompt, popping a second dialog on top of it would only add to
                    // the confusion.
                    tabFileHandles.Remove(activeTab);
                    status.Text = $"Save failed: {diagnostic}";
                    notifications.ShowErrorBanner(
                        $"Couldn't save to {knownFile.Name} directly ({diagnostic}). Use Save As.");
                }
                return;
            }

            // No known location yet (new/Untitled tab, or the bundled sample) -- fall back to
            // Save As's prompt, same as a first-time save on desktop, and remember the picked
            // file so the next plain Save writes straight there.
            var savedFile = await PromptAndWriteAsync(saveButton, acls);
            if (savedFile is null) return;

            if (activeTab is not null) tabFileHandles[activeTab] = savedFile;
            status.Text = $"Saved to {savedFile.Name}.";
            notifications.ShowToast($"Saved to {savedFile.Name}.");
        };

        saveAsButton.Click += async (_, _) =>
        {
            var acls = projectManager.AnimationChainListSave;
            if (acls is null)
            {
                notifications.ShowErrorBanner("Nothing to save.");
                return;
            }

            var savedFile = await PromptAndWriteAsync(saveAsButton, acls);
            if (savedFile is null) return;

            // Save As redirects the active tab's save target -- the next plain Save writes to
            // this new location too, not back to wherever the tab was originally opened from.
            var activeTab = tabManager.ActiveTab;
            if (activeTab is not null) tabFileHandles[activeTab] = savedFile;
            status.Text = $"Saved to {savedFile.Name}.";
            notifications.ShowToast($"Saved to {savedFile.Name}.");
        };

        // Bug: these command-stub buttons (Open Folder, Save As, Reload Textures, ...) were
        // never added to any panel -- IsVisible=false hides them from view, but an unattached
        // Control also has no visual-tree Parent chain, so TopLevel.GetTopLevel(openButton)
        // (used by openButton's and saveAsButton's own Click handlers to reach
        // TopLevel.StorageProvider) always returned null and those handlers silently no-opped.
        // That's the "Load Folder does nothing" bug: the click never even reached the native
        // folder-picker call. Parenting them here (still IsVisible=false, so still invisible
        // and out of layout) gives GetTopLevel a real chain to walk once `shell` is attached.
        var hiddenCommandButtons = new StackPanel
        {
            IsVisible = false,
            Children =
            {
                openButton, saveButton, saveAsButton, reloadButton,
                addFrameButton, addRectButton, addCircleButton, deleteSelectedButton,
                exportPixiJsButton, diagnosticsButton,
            },
        };

        var shell = new Grid();
        shell.Children.Add(root);
        shell.Children.Add(notifications);
        shell.Children.Add(hiddenCommandButtons);

        return shell;
    }
}
