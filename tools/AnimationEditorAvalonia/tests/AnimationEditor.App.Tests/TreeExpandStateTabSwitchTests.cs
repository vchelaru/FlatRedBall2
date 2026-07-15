using AnimationEditor.Core.IO;
using AnimationEditor.Core.Models;
using AnimationEditor.Core.Paths;
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
/// Repro/regression coverage for #687: a frame node's expand state (only meaningful when the
/// frame has a shape child, since only then does it have anything to expand) must survive
/// switching to another tab and back, not just chain-level expand state (which the companion
/// file already persists).
/// </summary>
public class TreeExpandStateTabSwitchTests
{
    private static string WriteAchxWithShapeOnFirstFrame(string dir, string fileName, string chainName)
    {
        var path = Path.Combine(dir, fileName);
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        var chain = new AnimationChainSave { Name = chainName };
        var frame = new AnimationFrameSave { TextureName = chainName + ".png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        frame.ShapesSave!.Shapes.Add(new AARectSave { Name = "HitBox" });
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);
        acls.Save(path);
        return path;
    }

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

    [AvaloniaFact]
    public async Task SwitchingTabsAndBack_PreservesExpandedFrameNodeWithShape()
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
            var pathA = WriteAchxWithShapeOnFirstFrame(dir, "a.achx", "Walk");
            var pathB = WriteAchx(dir, "b.achx", "Run");

            await window.OpenFileAsTab(pathA);
            Dispatcher.UIThread.RunJobs();

            var frameNode = window.GetTreeRoots()[0].Children[0];
            Assert.NotEmpty(frameNode.Children); // has the shape child, so it's expandable
            frameNode.IsExpanded = true;

            // Switching to file B triggers a tab switch away from A.
            await window.OpenFileAsTab(pathB);
            Dispatcher.UIThread.RunJobs();

            var tabManager = (TabManager)typeof(MainWindow)
                .GetField("_tabManager", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .GetValue(window)!;
            var tabA = tabManager.Tabs.First(t => t.Path == new FilePath(pathA));

            var activateMethod = typeof(MainWindow)
                .GetMethod("ActivateTabAsync", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            await (Task)activateMethod.Invoke(window, [tabA])!;
            Dispatcher.UIThread.RunJobs();

            var frameNodeAfterSwitchBack = window.GetTreeRoots()[0].Children[0];
            Assert.True(frameNodeAfterSwitchBack.IsExpanded);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
