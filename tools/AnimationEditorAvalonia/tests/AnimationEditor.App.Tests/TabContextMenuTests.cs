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
/// Regression guard for issue #472: right-clicking a document tab must show a single,
/// managed context menu — never stack a fresh menu on every press. The fix is to attach
/// one <see cref="Control.ContextMenu"/> per tab (Avalonia light-dismisses and reuses it)
/// instead of constructing and <c>Open()</c>-ing a new <see cref="ContextMenu"/> on each
/// right-click. This test asserts each tab Border carries that managed menu with the
/// expected items, which is impossible to stack by construction.
/// </summary>
public class TabContextMenuTests
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

    [AvaloniaFact]
    public async Task EachTab_HasManagedContextMenu_WithDetachAndCloseItems()
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
            // Two tabs so the tab strip is visible and populated.
            await window.OpenFileAsTab(WriteAchx(dir, "a.achx", "Walk"));
            Dispatcher.UIThread.RunJobs();
            await window.OpenFileAsTab(WriteAchx(dir, "b.achx", "Run"));
            Dispatcher.UIThread.RunJobs();

            var tabStrip = window.FindControl<Panel>("TabStrip")!;
            var tabBorders = tabStrip.Children.OfType<Border>().ToList();
            Assert.Equal(2, tabBorders.Count);

            foreach (var tabBorder in tabBorders)
            {
                // A managed ContextMenu means right-click reuses one menu instead of
                // stacking a new one — the defect in #472.
                Assert.NotNull(tabBorder.ContextMenu);
                var headers = tabBorder.ContextMenu!.Items
                    .OfType<MenuItem>()
                    .Select(m => (string?)m.Header)
                    .ToList();
                Assert.Contains("Detach to New Window", headers);
                Assert.Contains("Copy Full Path", headers);
                Assert.Contains("Close Tab", headers);
            }
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
