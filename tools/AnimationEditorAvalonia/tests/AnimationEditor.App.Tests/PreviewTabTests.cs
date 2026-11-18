using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.Models;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #841: a single click on a file in the Open Project Folder tree opens it in a single
/// reusable "preview" tab instead of a permanent one. Double-clicking the tree row, or editing
/// the previewed file, promotes it to a permanent tab. TabManager's own OpenPreview/Promote
/// logic is covered by TabManagerTests (Core.Tests); these tests cover MainWindow's wiring --
/// that ProjectPanel's click/double-click events actually reach it, and that an undo-recorded
/// edit promotes the active preview tab.
/// </summary>
public class PreviewTabTests
{
    private static string WriteAchx(string dir, string fileName)
    {
        var path = Path.Combine(dir, fileName);
        new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel }.Save(path);
        return path;
    }

    private static async Task<(MainWindow Window, string Dir)> CreateWindowWithProjectFolderAsync(params string[] fileNames)
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        foreach (var f in fileNames) WriteAchx(dir, f);

        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        await ctx.OpenProjectFolderAsync(window, dir);
        Dispatcher.UIThread.RunJobs();
        return (window, dir);
    }

    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    private static async Task ClickTreeRowAsync(MainWindow window, int index)
    {
        window.ProjectPanel.ProjectTree.SelectedItem = window.ProjectPanel.TreeRoots[index];
        Dispatcher.UIThread.RunJobs();
        await window.LastEditorProjectModelChangedTask;
    }

    private static async Task DoubleClickTreeRowAsync(MainWindow window, int index)
    {
        var node = window.ProjectPanel.TreeRoots[index];
        var tvi = window.ProjectPanel.ProjectTree.GetVisualDescendants().OfType<TreeViewItem>()
            .First(t => ReferenceEquals(t.DataContext, node));

        var local = new Avalonia.Point(tvi.Bounds.Width / 2, tvi.Bounds.Height / 2);
        var p = tvi.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        window.MouseDown(p, MouseButton.Left);
        window.MouseUp(p, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
        await window.LastEditorProjectModelChangedTask;
    }

    private sealed class StubUndoCmd : IUndoableCommand
    {
        public string Description => "Stub edit";
        public bool Do() => true;
        public void Undo() { }
        public void Redo() { }
    }

    [AvaloniaFact]
    public async Task SingleClickingTreeFile_OpensAsPreviewTab()
    {
        var (window, dir) = await CreateWindowWithProjectFolderAsync("hero.achx");
        try
        {
            await ClickTreeRowAsync(window, 0);

            var tabs = GetTabManager(window).Tabs;
            Assert.Single(tabs);
            Assert.True(tabs[0].IsPreview);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task SingleClickingSecondTreeFile_ReplacesPreviewTabInsteadOfAddingOne()
    {
        var (window, dir) = await CreateWindowWithProjectFolderAsync("hero.achx", "enemy.achx");
        try
        {
            await ClickTreeRowAsync(window, 0);

            var secondFileName = window.ProjectPanel.TreeRoots[1].Name;
            await ClickTreeRowAsync(window, 1);

            var tabs = GetTabManager(window).Tabs;
            Assert.Single(tabs);
            Assert.EndsWith(secondFileName, tabs[0].Path.FullPath);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task DoubleClickingTreeFile_PromotesPreviewTabToPermanent()
    {
        var (window, dir) = await CreateWindowWithProjectFolderAsync("hero.achx");
        try
        {
            await ClickTreeRowAsync(window, 0);

            await DoubleClickTreeRowAsync(window, 0);

            Assert.False(GetTabManager(window).Tabs[0].IsPreview);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }

    [AvaloniaFact]
    public async Task EditingPreviewedFile_PromotesItToPermanentTab()
    {
        var (window, dir) = await CreateWindowWithProjectFolderAsync("hero.achx");
        try
        {
            await ClickTreeRowAsync(window, 0);

            var undoManagerField = typeof(MainWindow)
                .GetField("_undoManager", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var undoManager = (IUndoManager)undoManagerField.GetValue(window)!;
            undoManager.Execute(new StubUndoCmd());

            Assert.False(GetTabManager(window).Tabs[0].IsPreview);
        }
        finally { window.Close(); Directory.Delete(dir, true); }
    }
}
