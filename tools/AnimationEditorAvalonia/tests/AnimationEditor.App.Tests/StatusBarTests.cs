using System;
using System.IO;
using System.Threading.Tasks;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless tests for the status bar: save-state label, filename, and chain/frame counts.
/// </summary>
public class StatusBarTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    private static string WriteAchx(string dir, params string[] chainNames)
    {
        var path = Path.Combine(dir, "test.achx");
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

    // ── Save-state label ──────────────────────────────────────────────────────

    [AvaloniaFact]
    public void StatusBar_ShowsNotSaved_Initially()
    {
        var (window, _) = CreateWindow();
        try
        {
            var label = window.FindControl<TextBlock>("StatusSaveLabel")!;
            Assert.Equal("Not saved", label.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StatusBar_ShowsAutoSaveOn_AfterMarkSaved()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.MarkSaved();
            Dispatcher.UIThread.RunJobs();

            var label = window.FindControl<TextBlock>("StatusSaveLabel")!;
            Assert.Equal("Auto Save On", label.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StatusBar_ShowsNotSaved_AfterClear()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.MarkSaved();
            Dispatcher.UIThread.RunJobs();

            ctx.UndoManager.Clear();
            Dispatcher.UIThread.RunJobs();

            var label = window.FindControl<TextBlock>("StatusSaveLabel")!;
            Assert.Equal("Not saved", label.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StatusBar_ShowsAutoSaveOn_AfterLoadFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var path = WriteAchx(dir, "Walk");
            ctx.AppCommands.LoadAnimationChain(path);
            Dispatcher.UIThread.RunJobs();

            var label = window.FindControl<TextBlock>("StatusSaveLabel")!;
            Assert.Equal("Auto Save On", label.Text);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void StatusBar_ShowsNotSaved_AfterNewFile()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.MarkSaved();
            Dispatcher.UIThread.RunJobs();

            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            var label = window.FindControl<TextBlock>("StatusSaveLabel")!;
            Assert.Equal("Not saved", label.Text);
        }
        finally { window.Close(); }
    }

    // ── Filename label ────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void StatusBar_ShowsFilename_AfterLoad()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var path = WriteAchx(dir, "Walk");
            ctx.AppCommands.LoadAnimationChain(path);
            Dispatcher.UIThread.RunJobs();

            var label = window.FindControl<TextBlock>("StatusFilename")!;
            Assert.Equal("test.achx", label.Text);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // ── Chain/frame count label ───────────────────────────────────────────────

    [AvaloniaFact]
    public void StatusBar_ShowsCounts_AfterChainsChanged()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave { TextureName = "Tex.png" });
            chain.Frames.Add(new AnimationFrameSave { TextureName = "Tex2.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            ctx.ApplicationEvents.RaiseAnimationChainsChanged();
            Dispatcher.UIThread.RunJobs();

            // Both frames have the default FrameLength (0), so the total time is 0.00s (#623).
            var counts = window.FindControl<TextBlock>("StatusCounts")!;
            Assert.Equal("1 chains · 2 frames · 0.00s", counts.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StatusBar_CountsBlank_AfterNewFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            // Load a file with chains so counts are non-empty
            var path = WriteAchx(dir, "Walk", "Run");
            ctx.AppCommands.LoadAnimationChain(path);
            Dispatcher.UIThread.RunJobs();

            // New file clears the project
            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            var counts = window.FindControl<TextBlock>("StatusCounts")!;
            Assert.Equal(string.Empty, counts.Text);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void StatusBar_ShowsCounts_AfterLoadFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            // WriteAchx adds one 0.1s frame per chain — 3 frames total = 0.30s (#623).
            var path = WriteAchx(dir, "Walk", "Run", "Jump");
            ctx.AppCommands.LoadAnimationChain(path);
            Dispatcher.UIThread.RunJobs();

            var counts = window.FindControl<TextBlock>("StatusCounts")!;
            Assert.Equal("3 chains · 3 frames · 0.30s", counts.Text);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void StatusBar_ShowsSelectedCount_WhenMultipleChainsSelected()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var walk = new AnimationChainSave { Name = "Walk" };
            var run  = new AnimationChainSave { Name = "Run" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(walk);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(run);

            // Selecting 2+ chains replaces the whole-file summary with just the count (#623).
            ctx.SelectedState.SelectedNodes = new() { walk, run };
            Dispatcher.UIThread.RunJobs();

            var counts = window.FindControl<TextBlock>("StatusCounts")!;
            Assert.Equal("2 chains selected", counts.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StatusBar_ShowsAutoSaveFailed_AfterMarkSaveFailed()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.UndoManager.MarkSaveFailed();
            Dispatcher.UIThread.RunJobs();

            var label = window.FindControl<TextBlock>("StatusSaveLabel")!;
            Assert.Equal("Auto Save Failed", label.Text);
        }
        finally { window.Close(); }
    }

    // ── Tiled sync status (issue #1139) ───────────────────────────────────────

    private static string WriteFixtureTileset(string dir)
    {
        var tileset = new DotTiled.Tileset
        {
            Name = "Heroes", TileWidth = 16, TileHeight = 16, TileCount = 64, Columns = 4,
            // Width/height let the mapper convert the achx's UV frame coordinates back to
            // pixels -- ProjectManager.AnimationChainListSave is always UV in memory.
            Image = new DotTiled.Image { Source = "Heroes.png", Width = 64, Height = 16 },
        };
        var path = Path.Combine(dir, "Heroes.tsx");
        AnimationEditor.Core.Tiled.TsxWriter.Write(tileset, path);
        return path;
    }

    [AvaloniaFact]
    public void TiledSyncStatus_HiddenInitially()
    {
        var (window, _) = CreateWindow();
        try
        {
            var panel = window.FindControl<StackPanel>("TiledSyncStatusPanel")!;
            Assert.False(panel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void TiledSyncStatus_ShowsFailureDetail_AfterAssociatedTsxMissingFromDisk()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var achxPath = WriteAchx(dir, "Walk");
            ctx.AppCommands.LoadAnimationChain(achxPath);
            Dispatcher.UIThread.RunJobs();

            var missingTsxPath = Path.Combine(dir, "DoesNotExist.tsx");
            ctx.AppCommands.AddAssociatedTiledTileset(missingTsxPath);
            ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);
            Dispatcher.UIThread.RunJobs();

            var panel = window.FindControl<StackPanel>("TiledSyncStatusPanel")!;
            var label = window.FindControl<TextBlock>("TiledSyncLabel")!;
            Assert.True(panel.IsVisible);
            Assert.Contains("failed", label.Text, System.StringComparison.OrdinalIgnoreCase);
            var tip = ToolTip.GetTip(window.FindControl<Ellipse>("TiledSyncDot")!) as string;
            Assert.Contains("DoesNotExist.tsx", tip);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void TiledSyncStatus_ShowsOk_AfterAssociatedTsxSyncsSuccessfully()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var tsxPath = WriteFixtureTileset(dir);
            var achxPath = Path.Combine(dir, "Hero.achx");
            ctx.ProjectManager.FileName = achxPath;
            ctx.AppCommands.AddAssociatedTiledTileset(tsxPath);

            // Tile 0 occupies pixels [0,16)x[0,16) of the 64x16 fixture texture -> UV [0,0.25)x[0,1).
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave
            {
                TextureName = "Heroes.png", FrameLength = 0.1f,
                LeftCoordinate = 0f, TopCoordinate = 0f, RightCoordinate = 0.25f, BottomCoordinate = 1f,
            });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            ctx.AppCommands.SaveCurrentAnimationChainList(achxPath);
            Dispatcher.UIThread.RunJobs();

            var panel = window.FindControl<StackPanel>("TiledSyncStatusPanel")!;
            var label = window.FindControl<TextBlock>("TiledSyncLabel")!;
            Assert.True(panel.IsVisible);
            Assert.DoesNotContain("failed", label.Text, System.StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// The real trigger is a background <see cref="System.IO.FileSystemWatcher"/> plus the hot
    /// reload watcher's own 100ms debounce timer -- poll until <paramref name="condition"/> holds
    /// or <paramref name="timeout"/> elapses (same reasoning as ProjectFolderExternalWatchTests's
    /// PumpUntilAsync for the equivalent project-folder watcher).
    /// </summary>
    private static async Task PumpUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition()) return;
            await Task.Delay(50);
        }
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task TiledSyncStatus_ShowsChangedOnDiskNote_AfterTiledSyncFileModifiedExternally()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var achxPath = WriteAchx(dir, "Walk");
            ctx.AppCommands.LoadAnimationChain(achxPath);
            Dispatcher.UIThread.RunJobs();
            File.WriteAllText(Path.Combine(dir, "test.tiledsync"), "{}");

            // Simulates a teammate's git pull changing the association file while open.
            File.WriteAllText(Path.Combine(dir, "test.tiledsync"), "{ \"TiledTilesetPaths\": [] }");

            var label = window.FindControl<TextBlock>("TiledSyncLabel")!;
            await PumpUntilAsync(() => label.Text?.Contains("changed on disk", System.StringComparison.OrdinalIgnoreCase) == true,
                TimeSpan.FromSeconds(5));

            var panel = window.FindControl<StackPanel>("TiledSyncStatusPanel")!;
            Assert.True(panel.IsVisible);
            Assert.Contains("changed on disk", label.Text, System.StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
