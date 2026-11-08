using System;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;

namespace AnimationEditor.App;

/// <summary>
/// Builds the macOS system menu bar from <see cref="NativeMenuActions"/>. Kept apart from <see cref="App"/> so
/// headless tests can inspect which items exist, their gestures and their enabled state.
/// </summary>
internal static class NativeMenuBuilder
{
    internal static NativeMenu Build(NativeMenuActions a)
    {

        var fileMenu = new NativeMenu();
        fileMenu.Add(new NativeMenuItem("New Animation") { Command = Cmd(a.New),    Gesture = new KeyGesture(Key.N, KeyModifiers.Meta) });
        fileMenu.Add(new NativeMenuItem("Open…")    { Command = Cmd(a.Load),   Gesture = new KeyGesture(Key.O, KeyModifiers.Meta) });

        var recentMenu = new NativeMenu();
        foreach (var (header, execute) in a.RecentFiles())
            recentMenu.Add(new NativeMenuItem(header) { Command = Cmd(execute) });
        fileMenu.Add(new NativeMenuItem("Open Recent") { Menu = recentMenu });
        fileMenu.Add(new NativeMenuItem("Open Project Folder…") { Command = Cmd(a.OpenProjectFolder) });

        fileMenu.Add(new NativeMenuItemSeparator());
        var closeProjectItem = new NativeMenuItem("Close Project Folder") { Command = Cmd(a.CloseProjectFolder) };
        fileMenu.Add(closeProjectItem);
        fileMenu.Opening += (_, _) => closeProjectItem.IsEnabled = a.HasProjectFolder();

        fileMenu.Add(new NativeMenuItemSeparator());
        fileMenu.Add(new NativeMenuItem("Save")     { Command = Cmd(a.Save),   Gesture = new KeyGesture(Key.S, KeyModifiers.Meta) });
        fileMenu.Add(new NativeMenuItem("Save As…") { Command = Cmd(a.SaveAs), Gesture = new KeyGesture(Key.S, KeyModifiers.Meta | KeyModifiers.Shift) });

        var editMenu = new NativeMenu();
        editMenu.Add(new NativeMenuItem("Undo")  { Command = Cmd(a.Undo),  Gesture = new KeyGesture(Key.Z, KeyModifiers.Meta) });
        editMenu.Add(new NativeMenuItem("Redo")  { Command = Cmd(a.Redo),  Gesture = new KeyGesture(Key.Z, KeyModifiers.Meta | KeyModifiers.Shift) });
        editMenu.Add(new NativeMenuItemSeparator());
        editMenu.Add(new NativeMenuItem("Copy")  { Command = Cmd(a.Copy),  Gesture = new KeyGesture(Key.C, KeyModifiers.Meta) });
        editMenu.Add(new NativeMenuItem("Cut")   { Command = Cmd(a.Cut),   Gesture = new KeyGesture(Key.X, KeyModifiers.Meta) });
        editMenu.Add(new NativeMenuItem("Paste") { Command = Cmd(a.Paste), Gesture = new KeyGesture(Key.V, KeyModifiers.Meta) });
        editMenu.Add(new NativeMenuItem("Duplicate") { Command = Cmd(a.Duplicate), Gesture = new KeyGesture(Key.D, KeyModifiers.Meta) });
        editMenu.Add(new NativeMenuItemSeparator());
        editMenu.Add(new NativeMenuItem("Reload from Disk")  { Command = Cmd(a.ReloadFromDisk) });
        editMenu.Add(new NativeMenuItem("Enable Hot Reload") { Command = Cmd(a.ToggleHotReload) });
        editMenu.Add(new NativeMenuItemSeparator());
        editMenu.Add(new NativeMenuItem("Resize Texture…")  { Command = Cmd(a.ResizeTexture) });

        var viewMenu = new NativeMenu();
        var backItem = new NativeMenuItem("Back") { Command = Cmd(a.NavBack), Gesture = a.NavBackGesture };
        var forwardItem = new NativeMenuItem("Forward") { Command = Cmd(a.NavForward), Gesture = a.NavForwardGesture };
        viewMenu.Add(backItem);
        viewMenu.Add(forwardItem);
        // Live rather than on Opening: the menu bar's key equivalents are checked without opening the menu.
        void RefreshNavigationEnabled()
        {
            backItem.IsEnabled = a.CanGoBack();
            forwardItem.IsEnabled = a.CanGoForward();
        }
        RefreshNavigationEnabled();
        a.SubscribeNavigationChanged(RefreshNavigationEnabled);
        viewMenu.Add(new NativeMenuItemSeparator());
        viewMenu.Add(new NativeMenuItem("Show History") { Command = Cmd(a.ShowHistory) });

        var helpMenu = new NativeMenu();
        helpMenu.Add(new NativeMenuItem("View Log") { Command = Cmd(a.ViewLog) });
        helpMenu.Add(new NativeMenuItem("About AnimationEditor") { Command = Cmd(a.About) });

        var appMenu = new NativeMenu();
        appMenu.Add(new NativeMenuItem("File") { Menu = fileMenu });
        appMenu.Add(new NativeMenuItem("Edit") { Menu = editMenu });
        appMenu.Add(new NativeMenuItem("View") { Menu = viewMenu });
        appMenu.Add(new NativeMenuItem("Help") { Menu = helpMenu });

        return appMenu;
    }

    private static ICommand Cmd(Action action) => new RelayCommand(action);
}
