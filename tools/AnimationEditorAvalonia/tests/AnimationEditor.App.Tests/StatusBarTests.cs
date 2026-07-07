using System.IO;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

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
}
