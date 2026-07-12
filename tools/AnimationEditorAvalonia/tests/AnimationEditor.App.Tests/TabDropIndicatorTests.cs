using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Coverage for issue #545: dragging a document tab to reorder it shows a drop-position
/// indicator line so the target slot is clear before release. The pointer-driven drag itself
/// is exercised manually (same convention as <c>FrameDragReorderHeadlessTests</c>); this
/// covers the extracted, testable geometry that decides where the indicator line goes.
/// </summary>
public class TabDropIndicatorTests
{
    private static string WriteAchx(string dir, string fileName, string chainName)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = chainName };
        chain.Frames.Add(new AnimationFrameSave { TextureName = chainName + ".png", FrameLength = 0.1f });
        acls.AnimationChains.Add(chain);
        acls.Save(path);
        return path;
    }

    private static async Task<(MainWindow Window, string Dir)> CreateWindowWithThreeTabs()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var ctx = TestHelpers.BuildServices();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;
        var window = ctx.CreateMainWindow();
        window.Width = 1200;
        window.Height = 800;
        window.Show();

        await window.OpenFileAsTab(WriteAchx(dir, "a.achx", "Walk"));
        Dispatcher.UIThread.RunJobs();
        await window.OpenFileAsTab(WriteAchx(dir, "b.achx", "Run"));
        Dispatcher.UIThread.RunJobs();
        await window.OpenFileAsTab(WriteAchx(dir, "c.achx", "Jump"));
        Dispatcher.UIThread.RunJobs();

        return (window, dir);
    }

    [AvaloniaFact]
    public async Task ComputeTabDropLineX_BeforeFirstTabCentre_ReturnsFirstTabLeftEdge()
    {
        var (window, dir) = await CreateWindowWithThreeTabs();
        try
        {
            var tabStrip = window.FindControl<Panel>("TabStrip")!;
            var tabs = tabStrip.Children.OfType<Border>().ToList();
            Assert.Equal(3, tabs.Count);

            double x = tabs[0].Bounds.Left; // left of every tab's centre
            Assert.Equal(tabs[0].Bounds.Left, window.ComputeTabDropLineX(x), precision: 3);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task ComputeTabDropLineX_BetweenTwoTabs_ReturnsBoundaryBetweenThem()
    {
        var (window, dir) = await CreateWindowWithThreeTabs();
        try
        {
            var tabStrip = window.FindControl<Panel>("TabStrip")!;
            var tabs = tabStrip.Children.OfType<Border>().ToList();

            // Just past tab 0's centre → boundary is tab 1's left edge (== tab 0's right edge).
            double x = tabs[0].Bounds.Left + tabs[0].Bounds.Width / 2.0 + 1;
            Assert.Equal(tabs[1].Bounds.Left, window.ComputeTabDropLineX(x), precision: 3);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public async Task ComputeTabDropLineX_PastLastTabCentre_ReturnsLastTabRightEdge()
    {
        var (window, dir) = await CreateWindowWithThreeTabs();
        try
        {
            var tabStrip = window.FindControl<Panel>("TabStrip")!;
            var tabs = tabStrip.Children.OfType<Border>().ToList();

            double x = tabs[2].Bounds.Right + 50; // well past the last tab
            Assert.Equal(tabs[2].Bounds.Right, window.ComputeTabDropLineX(x), precision: 3);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
