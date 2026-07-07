using System;
using System.Collections.Generic;
using System.Linq;
using AnimationEditor.App.Controls;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.App.Theming;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Utilities;
using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using FilePath = AnimationEditor.Core.Paths.FilePath;

namespace AnimationEditor.Browser;

public partial class App : Application
{
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
        TabEditorCache.CaptureFromProject(tabManager.ActiveTab!, projectManager);

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
            projectManager, undoManager, pendingCutState,
            thumbnailService: thumbnailService);

        // Phase 1 (#603): read-only browsing of every chain/frame/shape in the loaded file,
        // replacing the previous hardcoded "always show AnimationChains[0]" behavior. Both
        // controls are independent of each other and of PreviewControl -- any of the three can
        // drive ISelectedState, and the others react via SelectionChanged.
        var animationTree = new AnimationTreeControl();
        var inspector = new InspectorControl();
        inspector.InitializeServices(selectedState);
        inspector.EnableEditing(appCommands);
        animationTree.InitializeServices(selectedState, acls);
        animationTree.EnableRename(appCommands);

        // Selecting a chain with no frame pinned auto-plays it (PreviewControl.OnSelectionChanged).
        selectedState.SelectedChain = acls.AnimationChains[0];

        var status = new TextBlock { Margin = new Thickness(8), Text = "Loaded bundled sample." };

        var tabStrip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Margin = new Thickness(8, 0, 8, 0) };

        var openButton = new Button { Content = "Open Folder…" };
        var saveAsButton = new Button { Content = "Save As…" };
        var reloadButton = new Button { Content = "Reload Changed Textures", IsVisible = false };
        var themeButton = new Button { Content = $"Theme: {currentTheme}" };
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8),
            Children = { openButton, saveAsButton, reloadButton, themeButton },
        };

        themeButton.Click += (_, _) =>
        {
            currentTheme = currentTheme == AppTheme.Dark ? AppTheme.Light : AppTheme.Dark;
            Application.Current!.RequestedThemeVariant = ThemeManager.ToVariant(currentTheme);
            settingsStore.SaveTheme(currentTheme);
            themeButton.Content = $"Theme: {currentTheme}";
        };

        // Phase 2 (#610): mutation + Undo/Redo, routed entirely through the already-built,
        // already-tested AppCommands/UndoManager -- no new mutation logic here, only wiring.
        // Renaming, shape add/delete, editable inspector fields, and settings persistence are
        // deferred to follow-up issues (see the PR description for the full list).
        var addAnimationButton = new Button { Content = "Add Animation" };
        var addFrameButton = new Button { Content = "Add Frame" };
        var addRectButton = new Button { Content = "Add Rectangle" };
        var addCircleButton = new Button { Content = "Add Circle" };
        var deleteSelectedButton = new Button { Content = "Delete Selected" };
        var undoButton = new Button { Content = "Undo", IsEnabled = false };
        var redoButton = new Button { Content = "Redo", IsEnabled = false };
        var magicWandButton = new ToggleButton { Content = "Magic Wand" };
        var editToolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8, 0, 8, 8),
            Children =
            {
                addAnimationButton, addFrameButton, addRectButton, addCircleButton,
                deleteSelectedButton, undoButton, redoButton, magicWandButton,
            },
        };

        // Phase 3 (#614): Move mode (drag existing handles/chains) is the wireframe's default
        // and needs no toggle -- it's just pointer events already wired in WireframeControl's
        // constructor. Magic Wand mode is the one the user must opt into.
        magicWandButton.Click += (_, _) => wireframe.IsMagicWandMode = magicWandButton.IsChecked == true;

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
            undoButton.IsEnabled = undoManager.CanUndo;
            redoButton.IsEnabled = undoManager.CanRedo;
        }
        undoManager.StackChanged += UpdateUndoRedoButtons;

        // Phase 4 (#620): multi-file tabs. TabManager/TabEditorCache are already fully built and
        // tested in Core (pure in-memory, zero disk dependency) -- this is wiring, not new logic.
        // One thing checked before wiring: TabEditorCache.HasFreshCache treats a tab as fresh
        // whenever its cached disk-write-time is null, and TryReadDiskWriteTimeUtc naturally
        // returns null for any path that doesn't exist on disk (every browser tab's path) --
        // so cached tabs are already correctly "always trusted" here with no code changes needed.
        void RebuildTabStrip()
        {
            tabStrip.Children.Clear();
            foreach (var tab in tabManager.Tabs)
            {
                var isActive = tab == tabManager.ActiveTab;
                var tabButton = new Button
                {
                    Content = tab.DisplayName,
                    FontWeight = isActive ? Avalonia.Media.FontWeight.Bold : Avalonia.Media.FontWeight.Normal,
                };
                tabButton.Click += (_, _) => SwitchToTab(tab);
                tabStrip.Children.Add(tabButton);
            }
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
                TabEditorCache.CaptureFromProject(leaving, projectManager);
                leaving.UndoSnapshot = undoManager.TakeSnapshot();
            }

            tabManager.Activate(target.Path);
            appCommands.TryActivateTabFromCache(target);
            undoManager.RestoreSnapshot(target.UndoSnapshot ?? new UndoSnapshot(new List<IUndoableCommand>(), new List<IUndoableCommand>()));
            UpdateUndoRedoButtons();

            animationTree.InitializeServices(selectedState, projectManager.AnimationChainListSave);
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
                TabEditorCache.CaptureFromProject(leaving, projectManager);
                leaving.UndoSnapshot = undoManager.TakeSnapshot();
            }

            tabManager.OpenOrFocus(new FilePath(displayName), displayName);
            TabEditorCache.CaptureFromProject(tabManager.ActiveTab!, projectManager);
            undoManager.Clear();
            UpdateUndoRedoButtons();
            RebuildTabStrip();
        }

        RebuildTabStrip();

        // Both Add/Delete commands raise AnimationChainsChanged -- refresh the tree once, here,
        // rather than after every individual button handler.
        applicationEvents.AnimationChainsChanged += animationTree.Refresh;

        addAnimationButton.Click += (_, _) =>
        {
            var currentAcls = projectManager.AnimationChainListSave;
            if (currentAcls is null) return;
            var existingNames = currentAcls.AnimationChains.Select(c => c.Name).ToList();
            var name = StringFunctions.MakeStringUnique("NewAnimation", existingNames);
            appCommands.AddAnimationChainWithName(name);
        };

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

        undoButton.Click += (_, _) => undoManager.Undo();
        redoButton.Click += (_, _) => undoManager.Redo();

        // #535 M3 follow-up: no FileSystemWatcher in the browser, so texture edits made outside
        // the page (e.g. re-saving a PNG in an image editor) are only detected by polling
        // GetBasicPropertiesAsync (see BrowserFolderWatcher). Detected changes are queued here and
        // applied only when the user clicks Reload -- matching "see a diff, prompt to refresh"
        // rather than silently swapping textures out from under the user mid-edit.
        BrowserFolderWatcher? folderWatcher = null;
        IStorageFolder? watchedFolder = null;
        var pendingChangedPngs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void UpdateReloadButton()
        {
            reloadButton.IsVisible = pendingChangedPngs.Count > 0;
            reloadButton.Content = $"Reload Changed Textures ({pendingChangedPngs.Count})";
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
            pendingChangedPngs.Clear();
            UpdateReloadButton();
            preview.InvalidateVisual();
        };

        // Left column: tree (fills available height) over inspector (sized to content).
        // Right: preview. Both new controls are pure UserControls with no dependency on this
        // layout shape -- MainWindow lays the equivalent panels out differently.
        var leftColumn = new Grid
        {
            Width = 260,
            RowDefinitions = new RowDefinitions("*,Auto"),
        };
        Grid.SetRow(animationTree, 0);
        Grid.SetRow(inspector, 1);
        leftColumn.Children.Add(animationTree);
        leftColumn.Children.Add(inspector);

        // Middle: wireframe (shape editing canvas). Right: preview (playback). Both are
        // independent TextureViewport-derived controls; neither depends on this layout shape --
        // MainWindow arranges the equivalent panels differently (tabs, not a fixed 3-column split).
        var mainArea = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*,*"),
        };
        Grid.SetColumn(leftColumn, 0);
        Grid.SetColumn(wireframe, 1);
        Grid.SetColumn(preview, 2);
        mainArea.Children.Add(leftColumn);
        mainArea.Children.Add(wireframe);
        mainArea.Children.Add(preview);

        var root = new DockPanel();
        DockPanel.SetDock(tabStrip, Dock.Top);
        root.Children.Add(tabStrip);
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        DockPanel.SetDock(editToolbar, Dock.Top);
        root.Children.Add(editToolbar);
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);
        root.Children.Add(mainArea);

        DragDrop.SetAllowDrop(root, true);
        root.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
        });
        root.AddHandler(DragDrop.DropEvent, async (_, e) =>
        {
            var files = e.DataTransfer.TryGetFiles()?.OfType<IStorageFile>().ToList();
            if (files is null || files.Count == 0) return;

            bool loaded = await BrowserProjectLoader.TryLoadAsync(
                files, projectManager, thumbnailService, selectedState);
            status.Text = loaded
                ? $"Loaded from {files.Count} dropped file(s)."
                : "Drop must include an .achx file (drop its texture PNG(s) alongside it).";

            if (loaded)
            {
                animationTree.InitializeServices(selectedState, projectManager.AnimationChainListSave);
                OpenNewTabForLoadedProject(projectManager.FileName ?? "Untitled");
            }
        });

        openButton.Click += async (_, _) =>
        {
            var topLevel = TopLevel.GetTopLevel(openButton);
            if (topLevel is null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Open Animation Folder",
                AllowMultiple = false,
            });
            var folder = folders.FirstOrDefault();
            if (folder is null) return;

            var files = new System.Collections.Generic.List<IStorageFile>();
            await foreach (var item in folder.GetItemsAsync())
                if (item is IStorageFile f) files.Add(f);

            bool loaded = await BrowserProjectLoader.TryLoadAsync(
                files, projectManager, thumbnailService, selectedState);
            status.Text = loaded
                ? $"Loaded from folder \"{folder.Name}\"."
                : $"No .achx found in \"{folder.Name}\".";

            if (loaded)
            {
                animationTree.InitializeServices(selectedState, projectManager.AnimationChainListSave);
                OpenNewTabForLoadedProject(projectManager.FileName ?? folder.Name);
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
        };

        saveAsButton.Click += async (_, _) =>
        {
            var acls = projectManager.AnimationChainListSave;
            if (acls is null)
            {
                status.Text = "Nothing to save.";
                return;
            }

            var topLevel = TopLevel.GetTopLevel(saveAsButton);
            if (topLevel is null) return;

            var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save Animation Chain As",
                SuggestedFileName = "player.achx",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("Animation Chain") { Patterns = new[] { "*.achx" } },
                },
            });
            if (file is null) return;

            await using var stream = await file.OpenWriteAsync();
            projectManager.SaveAnimationChainList(stream);
            status.Text = $"Saved to {file.Name}.";
        };

        return root;
    }
}
