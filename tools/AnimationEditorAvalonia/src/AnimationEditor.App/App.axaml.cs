using AnimationEditor.App.Services;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Windows.Input;

namespace AnimationEditor.App;

public partial class App : Application
{
    /// <summary>
    /// Set by <see cref="Program.Main"/> before Avalonia starts. Used to wire the
    /// single-instance file-open pipe to the running <see cref="MainWindow"/>.
    /// </summary>
    internal static SingleInstanceServer? SingleInstance { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
        Name = TitleBarHelper.AppName;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = BuildServices();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var window = services.GetRequiredService<MainWindow>();
            window.Icon = LoadAppIcon();
            desktop.MainWindow = window;
            RegisterNativeMenu(window);

            // Wire single-instance IPC: file paths received from a second process open as tabs.
            if (SingleInstance != null)
            {
                SingleInstance.FileOpenRequested += path =>
                    Dispatcher.UIThread.InvokeAsync(() => window.OpenFileAsTab(path));
            }

            // Post the Dock icon update to the next UI tick. Avalonia's macOS backend
            // may clear NSApplication.applicationIconImage during window assignment
            // (when WindowDecorations="None" bypasses the native title-bar path), so
            // we set it AFTER Avalonia's own initialisation has finished.
            Dispatcher.UIThread.Post(SetMacOSDockIcon);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void SetMacOSDockIcon()
    {
        using var stream = AssetLoader.Open(
            new Uri("avares://AnimationEditor/Assets/icons/achx-icon-256.png"));
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        MacOSDockIcon.Set(ms.ToArray());
    }

    private static WindowIcon LoadAppIcon()
    {
        using var stream = AssetLoader.Open(
            new Uri("avares://AnimationEditor/Assets/icons/achx-icon-256.png"));
        return new WindowIcon(new Bitmap(stream));
    }

    private void RegisterNativeMenu(MainWindow window)
    {
        var a = window.CreateNativeMenuActions();

        var fileMenu = new NativeMenu();
        fileMenu.Add(new NativeMenuItem("New")      { Command = Cmd(a.New),    Gesture = new KeyGesture(Key.N, KeyModifiers.Meta) });
        fileMenu.Add(new NativeMenuItem("Open…")    { Command = Cmd(a.Load),   Gesture = new KeyGesture(Key.L, KeyModifiers.Meta) });

        var recentMenu = new NativeMenu();
        foreach (var (header, execute) in a.RecentFiles())
            recentMenu.Add(new NativeMenuItem(header) { Command = Cmd(execute) });
        fileMenu.Add(new NativeMenuItem("Open Recent") { Menu = recentMenu });

        fileMenu.Add(new NativeMenuItemSeparator());
        fileMenu.Add(new NativeMenuItem("Save")     { Command = Cmd(a.Save),   Gesture = new KeyGesture(Key.S, KeyModifiers.Meta) });
        fileMenu.Add(new NativeMenuItem("Save As…") { Command = Cmd(a.SaveAs), Gesture = new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Shift) });

        var editMenu = new NativeMenu();
        editMenu.Add(new NativeMenuItem("Undo")  { Command = Cmd(a.Undo),  Gesture = new KeyGesture(Key.Z, KeyModifiers.Meta) });
        editMenu.Add(new NativeMenuItem("Redo")  { Command = Cmd(a.Redo),  Gesture = new KeyGesture(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift) });
        editMenu.Add(new NativeMenuItemSeparator());
        editMenu.Add(new NativeMenuItem("Copy")  { Command = Cmd(a.Copy),  Gesture = new KeyGesture(Key.C, KeyModifiers.Meta) });
        editMenu.Add(new NativeMenuItem("Paste") { Command = Cmd(a.Paste), Gesture = new KeyGesture(Key.V, KeyModifiers.Meta) });
        editMenu.Add(new NativeMenuItem("Duplicate") { Command = Cmd(a.Duplicate), Gesture = new KeyGesture(Key.D, KeyModifiers.Meta) });
        editMenu.Add(new NativeMenuItemSeparator());
        editMenu.Add(new NativeMenuItem("Reload from Disk")  { Command = Cmd(a.ReloadFromDisk) });
        editMenu.Add(new NativeMenuItem("Enable Hot Reload") { Command = Cmd(a.ToggleHotReload) });
        editMenu.Add(new NativeMenuItemSeparator());
        editMenu.Add(new NativeMenuItem("Resize Texture…")  { Command = Cmd(a.ResizeTexture) });

        var viewMenu = new NativeMenu();
        viewMenu.Add(new NativeMenuItem("Show History") { Command = Cmd(a.ShowHistory) });

        var helpMenu = new NativeMenu();
        helpMenu.Add(new NativeMenuItem("About Animation Editor") { Command = Cmd(a.About) });

        var appMenu = new NativeMenu();
        appMenu.Add(new NativeMenuItem("File") { Menu = fileMenu });
        appMenu.Add(new NativeMenuItem("Edit") { Menu = editMenu });
        appMenu.Add(new NativeMenuItem("View") { Menu = viewMenu });
        appMenu.Add(new NativeMenuItem("Help") { Menu = helpMenu });

        NativeMenu.SetMenu(window, appMenu);
    }

    private static ICommand Cmd(Action action) => new RelayCommand(action);

    internal static MainWindow CreateDetachedWindow()
    {
        var services = BuildServices();
        var window = services.GetRequiredService<MainWindow>();
        window.Icon = LoadAppIcon();
        return window;
    }

    private static ServiceProvider BuildServices()
    {
        var sc = new ServiceCollection();

        sc.AddSingleton<ProjectManager>();
        sc.AddSingleton<IProjectManager>(sp => sp.GetRequiredService<ProjectManager>());

        sc.AddSingleton<ApplicationEvents>();
        sc.AddSingleton<IApplicationEvents>(sp => sp.GetRequiredService<ApplicationEvents>());

        sc.AddSingleton<SelectedState>(sp =>
            new SelectedState(sp.GetRequiredService<IProjectManager>()));
        sc.AddSingleton<ISelectedState>(sp => sp.GetRequiredService<SelectedState>());

        sc.AddSingleton<AppState>(sp =>
            new AppState(sp.GetRequiredService<IApplicationEvents>(),
                         sp.GetRequiredService<ISelectedState>()));
        sc.AddSingleton<IAppState>(sp => sp.GetRequiredService<AppState>());

        sc.AddSingleton<IoManager>(sp =>
            new IoManager(sp.GetRequiredService<IAppState>()));
        sc.AddSingleton<IIoManager>(sp => sp.GetRequiredService<IoManager>());

        sc.AddSingleton<ObjectFinder>(sp =>
            new ObjectFinder(sp.GetRequiredService<IProjectManager>()));
        sc.AddSingleton<IObjectFinder>(sp => sp.GetRequiredService<ObjectFinder>());

        sc.AddSingleton<UndoManager>();
        sc.AddSingleton<IUndoManager>(sp => sp.GetRequiredService<UndoManager>());

        sc.AddSingleton<AppCommands>(sp =>
            new AppCommands(
                sp.GetRequiredService<IProjectManager>(),
                sp.GetRequiredService<ISelectedState>(),
                sp.GetRequiredService<IApplicationEvents>(),
                sp.GetRequiredService<IIoManager>(),
                sp.GetRequiredService<IObjectFinder>(),
                sp.GetRequiredService<IUndoManager>()));
        sc.AddSingleton<IAppCommands>(sp => sp.GetRequiredService<AppCommands>());

        sc.AddSingleton<ThumbnailService>(sp =>
            new ThumbnailService(sp.GetRequiredService<IProjectManager>()));

        // File association is registry-based on Windows; other platforms get the no-op
        // service so the startup prompt simply never appears (IsSupported == false).
        if (OperatingSystem.IsWindows())
            sc.AddSingleton<IFileAssociationService, WindowsFileAssociationService>();
        else
            sc.AddSingleton<IFileAssociationService, NullFileAssociationService>();

        sc.AddTransient<MainWindow>(sp => new MainWindow(
            sp.GetRequiredService<IProjectManager>(),
            sp.GetRequiredService<ISelectedState>(),
            sp.GetRequiredService<IAppCommands>(),
            sp.GetRequiredService<IAppState>(),
            sp.GetRequiredService<IApplicationEvents>(),
            sp.GetRequiredService<IIoManager>(),
            sp.GetRequiredService<IObjectFinder>(),
            sp.GetRequiredService<IUndoManager>(),
            sp.GetRequiredService<ThumbnailService>(),
            sp.GetRequiredService<IFileAssociationService>(),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)));

        return sc.BuildServiceProvider();
    }
}
