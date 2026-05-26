using AnimationEditor.Core.CommandsAndState.Commands;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that each tab's undo history is preserved when navigating between tabs.
/// </summary>
public class TabSwitchUndoTests
{
    private static string WriteAchx(string dir, string fileName, params string[] chainNames)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (var name in chainNames)
        {
            var chain = new AnimationChainSave { Name = name };
            chain.Frames.Add(new AnimationFrameSave { TextureName = name + ".png", FrameLength = 0.1f });
            acls.AnimationChains.Add(chain);
        }
        acls.Save(path);
        return path;
    }

    private static async Task ActivateTabAsync(MainWindow window, TabEntry tab)
    {
        var method = typeof(MainWindow)
            .GetMethod("ActivateTabAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        await (Task)method.Invoke(window, [tab])!;
    }

    private static TabManager GetTabManager(MainWindow window) =>
        (TabManager)typeof(MainWindow)
            .GetField("_tabManager", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(window)!;

    private sealed class StubUndoCmd : IUndoableCommand
    {
        private readonly string _desc;
        public StubUndoCmd(string desc) => _desc = desc;
        public string Description => _desc;
        public bool Do() => true;
        public void Undo() { }
        public void Redo() { }
    }

    // ── Tab switch preserves undo ─────────────────────────────────────────────

    /// <summary>
    /// Undo entries added while file A is active must survive a switch to file B
    /// and back to file A.
    /// </summary>
    [AvaloniaFact]
    public async Task SwitchingTabs_PreservesUndoHistoryForEachTab()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pathA = WriteAchx(dir, "a.achx", "Walk");
            var pathB = WriteAchx(dir, "b.achx", "Run");

            // Open file A and record an undo entry
            await window.OpenFileAsTab(pathA);
            Dispatcher.UIThread.RunJobs();
            ctx.UndoManager.Record(new StubUndoCmd("Edit A"));
            Assert.True(ctx.UndoManager.CanUndo);

            // Open file B — this triggers a tab switch and should save A's snapshot
            await window.OpenFileAsTab(pathB);
            Dispatcher.UIThread.RunJobs();

            // B is a fresh file — no undo history
            Assert.False(ctx.UndoManager.CanUndo);

            // Switch back to tab A
            var tabManager = GetTabManager(window);
            var tabA = tabManager.Tabs.First(t => t.Path.FullPath == pathA);
            await ActivateTabAsync(window, tabA);
            Dispatcher.UIThread.RunJobs();

            // A's undo history must be restored
            Assert.True(ctx.UndoManager.CanUndo);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Undo entries added while file B is active must survive a switch back to file A
    /// and then a switch to B again.
    /// </summary>
    [AvaloniaFact]
    public async Task BothTabs_HaveIndependentUndoHistories()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            var pathA = WriteAchx(dir, "a.achx", "Walk");
            var pathB = WriteAchx(dir, "b.achx", "Run");

            // Open file A, record two undo entries
            await window.OpenFileAsTab(pathA);
            Dispatcher.UIThread.RunJobs();
            ctx.UndoManager.Record(new StubUndoCmd("Edit A1"));
            ctx.UndoManager.Record(new StubUndoCmd("Edit A2"));

            // Open file B, record one undo entry
            await window.OpenFileAsTab(pathB);
            Dispatcher.UIThread.RunJobs();
            ctx.UndoManager.Record(new StubUndoCmd("Edit B1"));

            var tabManager = GetTabManager(window);
            var tabA = tabManager.Tabs.First(t => t.Path.FullPath == pathA);
            var tabB = tabManager.Tabs.First(t => t.Path.FullPath == pathB);

            // Switch to A — should see A's 2 entries
            await ActivateTabAsync(window, tabA);
            Dispatcher.UIThread.RunJobs();
            int countA = ctx.UndoManager.UndoHistory.Count;
            Assert.Equal(2, countA);

            // Switch back to B — should see B's 1 entry
            await ActivateTabAsync(window, tabB);
            Dispatcher.UIThread.RunJobs();
            int countB = ctx.UndoManager.UndoHistory.Count;
            Assert.Equal(1, countB);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
