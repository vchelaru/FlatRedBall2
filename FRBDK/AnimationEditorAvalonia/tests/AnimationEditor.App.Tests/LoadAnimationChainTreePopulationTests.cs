using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

// Regression guard for the user-visible flow "File -> Load -> tree populates".
// AnimationEditor.Core.Tests cover the load chain up to RefreshTreeViewRequested firing,
// but they cannot observe the actual MainWindow._treeRoots ObservableCollection that the
// TreeView is bound to. This test does — load a real .achx, drive UI jobs, assert the tree
// reflects the chains.
public class LoadAnimationChainTreePopulationTests
{
    private const string TwoChainAchx =
        """
        <?xml version="1.0" encoding="utf-8"?>
        <AnimationChainArraySave>
          <CoordinateType>Pixel</CoordinateType>
          <AnimationChain>
            <Name>Idle</Name>
            <Frame>
              <TextureName>Sheet.png</TextureName><FrameLength>0.1</FrameLength>
              <LeftCoordinate>0</LeftCoordinate><RightCoordinate>32</RightCoordinate>
              <TopCoordinate>0</TopCoordinate><BottomCoordinate>32</BottomCoordinate>
            </Frame>
          </AnimationChain>
          <AnimationChain>
            <Name>Walk</Name>
            <Frame>
              <TextureName>Sheet.png</TextureName><FrameLength>0.1</FrameLength>
              <LeftCoordinate>32</LeftCoordinate><RightCoordinate>64</RightCoordinate>
              <TopCoordinate>0</TopCoordinate><BottomCoordinate>32</BottomCoordinate>
            </Frame>
          </AnimationChain>
        </AnimationChainArraySave>
        """;

    [AvaloniaFact]
    public void LoadAnimationChain_ViaAppCommands_PopulatesTreeRoots()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = AnimationEditor.Core.IO.NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();

        var tree = window.FindControl<TreeView>("AnimTree")!;
        var roots = (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;

        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var achxPath = Path.Combine(dir, "test.achx");
        File.WriteAllText(achxPath, TwoChainAchx, Encoding.UTF8);
        try
        {
            ctx.AppCommands.LoadAnimationChain(achxPath);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, roots.Count);
            Assert.Equal("Idle", roots[0].Header);
            Assert.Equal("Walk", roots[1].Header);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, recursive: true);
        }
    }
}
