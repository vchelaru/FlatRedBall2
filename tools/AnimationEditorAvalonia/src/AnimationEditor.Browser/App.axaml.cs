using System.Linq;
using AnimationEditor.App.Controls;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
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

        var acls = AnimationChainListSave.FromString(SampleContent.AchxText);
        // "sample/player.achx" doesn't exist on disk (no filesystem in the browser); preParsed
        // means LoadAnimationChain never tries to read it, only uses it as a logical identity.
        projectManager.LoadAnimationChain(new FilePath("sample/player.achx"), acls);

        var bitmap = SKBitmap.Decode(SampleContent.PngBytes);
        thumbnailService.SeedTexture("player.png", bitmap);

        var preview = new PreviewControl();
        preview.InitializeServices(
            selectedState, appState, appCommands, applicationEvents,
            projectManager, undoManager, thumbnailService, pendingCutState);

        // Selecting a chain with no frame pinned auto-plays it (PreviewControl.OnSelectionChanged).
        selectedState.SelectedChain = acls.AnimationChains[0];

        var status = new TextBlock { Margin = new Thickness(8), Text = "Loaded bundled sample." };

        var openButton = new Button { Content = "Open Folder…" };
        var saveAsButton = new Button { Content = "Save As…" };
        var toolbar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 8,
            Margin = new Thickness(8),
            Children = { openButton, saveAsButton },
        };

        var root = new DockPanel();
        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        DockPanel.SetDock(status, Dock.Bottom);
        root.Children.Add(status);
        root.Children.Add(preview);

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
            acls.Save(stream);
            status.Text = $"Saved to {file.Name}.";
        };

        return root;
    }
}
