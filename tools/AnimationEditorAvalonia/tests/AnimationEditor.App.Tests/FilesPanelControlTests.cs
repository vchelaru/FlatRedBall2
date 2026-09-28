using AnimationEditor.App.Controls;
using AnimationEditor.App.Services;
using AnimationEditor.Core;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #1059: right-clicking a folder row in the Files panel showed an empty context menu --
/// only file rows got "View in Explorer" because folder nodes carried no <c>AbsolutePath</c>.
/// </summary>
public class FilesPanelControlTests
{
    private static void WritePng(string dir, string fileName, SKColor color)
    {
        using var bm = new SKBitmap(8, 8);
        bm.Erase(color);
        using var img = SKImage.FromBitmap(bm);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(Path.Combine(dir, fileName), data.ToArray());
    }

    [AvaloniaFact]
    public void RightClickingFolderRow_ShowsViewInExplorerItem()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(dir, "Sprites"));
        WritePng(Path.Combine(dir, "Sprites"), "hero.png", SKColors.Red);

        var control = new FilesPanelControl();
        var window = new Window { Content = control, Width = 400, Height = 400 };
        control.Initialize(new ThumbnailService(new ProjectManager()), window);
        window.Show();

        try
        {
            control.Refresh(dir, Array.Empty<string>(), null);
            window.Measure(new Size(400, 400));
            window.Arrange(new Rect(0, 0, 400, 400));
            Dispatcher.UIThread.RunJobs();

            RightClick(window, control, control.TreeRoots[0]); // "Sprites" folder

            var headers = control.FilesTree.ContextMenu!.Items.OfType<MenuItem>()
                .Select(i => i.Header).ToArray();
            Assert.Equal(new object?[] { "View in Explorer" }, headers);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void TypingInSearch_FiltersTree_AndSurvivesRefresh()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        WritePng(dir, "hero.png", SKColors.Red);
        WritePng(dir, "grass.png", SKColors.Green);

        var control = new FilesPanelControl();
        var window = new Window { Content = control, Width = 400, Height = 400 };
        control.Initialize(new ThumbnailService(new ProjectManager()), window);
        window.Show();

        try
        {
            control.Refresh(dir, Array.Empty<string>(), null);
            control.FilesSearchBox.SearchBox.Text = "her";
            Assert.Equal(["hero.png"], control.TreeRoots.Select(n => n.Name));

            // The host re-invokes Refresh on every model change; the filter is sticky like the
            // ANIMATIONS tree's, so a refresh must not drop it.
            control.Refresh(dir, Array.Empty<string>(), null);
            Assert.Equal(["hero.png"], control.TreeRoots.Select(n => n.Name));

            control.FilesSearchBox.SearchBox.Text = "";
            Assert.Equal(2, control.TreeRoots.Count);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    private static void RightClick(Window window, FilesPanelControl control, PngFilesTreeNodeVm node)
    {
        var tvi = control.FilesTree.GetVisualDescendants().OfType<TreeViewItem>()
            .First(t => ReferenceEquals(t.DataContext, node));
        var local = new Point(tvi.Bounds.Width / 2, 8);
        var p = tvi.TranslatePoint(local, window)!.Value;
        window.MouseDown(p, MouseButton.Right);
        window.MouseUp(p, MouseButton.Right);
        Dispatcher.UIThread.RunJobs();
    }
}
