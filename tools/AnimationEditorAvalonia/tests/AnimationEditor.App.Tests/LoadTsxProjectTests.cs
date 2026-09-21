using AnimationEditor.Core.Rendering;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Proves a .tsx opens through the real <see cref="MainWindow"/> pipeline (issue #1140) --
/// <c>LoadAnimationFileAsync</c>'s tab bookkeeping, <c>TabEntry.InferKind</c> (needs no tsx-specific
/// case: a non-png extension already infers <c>TabKind.Achx</c>, the full-editor tab kind), and
/// <c>AppCommands.OpenProjectWorkflowAsync</c>'s extension dispatch -- not just
/// <c>AppCommands.OpenTsxWorkflowAsync</c> in isolation (see <c>AppCommandsOpenTsxWorkflowTests</c>
/// in Core.Tests).
/// </summary>
public class LoadTsxProjectTests
{
    private const string TsxFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="16" columns="4">
         <image source="Heroes.png" width="64" height="64"/>
         <tile id="0">
          <animation>
           <frame tileid="0" duration="200"/>
           <frame tileid="1" duration="200"/>
          </animation>
         </tile>
        </tileset>
        """;

    // Tile 8 is the anchor of a 2-tile group; tile 9's second frame (14) is hand-edited out of
    // lockstep with the anchor's second frame (12), which should be column 1 of that row (13).
    private const string InconsistentGroupFixtureXml = """
        <?xml version="1.0" encoding="UTF-8"?>
        <tileset version="1.10" tiledversion="1.12.2" name="Heroes" tilewidth="16" tileheight="16" tilecount="64" columns="4">
         <image source="Heroes.png" width="64" height="256"/>
         <tile id="8">
          <animation>
           <frame tileid="8" duration="150"/>
           <frame tileid="12" duration="150"/>
          </animation>
         </tile>
         <tile id="9">
          <properties>
           <property name="ParentId" type="int" value="8"/>
          </properties>
          <animation>
           <frame tileid="9" duration="150"/>
           <frame tileid="14" duration="150"/>
          </animation>
         </tile>
        </tileset>
        """;

    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName               = null;
        ctx.SelectedState.SelectedChain           = null;
        ctx.SelectedState.SelectedFrame           = null;
        ctx.SelectedState.SelectedNodes           = new List<object>();
        ctx.AppCommands.ConfirmAsync              = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService         = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        return (window, ctx);
    }

    [AvaloniaFact]
    public void LoadAnimationFileAsync_TsxFile_PopulatesTreeWithIdLabelAndMarksNativeTsxProject()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var path = Path.Combine(dir, "Heroes.tsx");
            File.WriteAllText(path, TsxFixtureXml);

            typeof(MainWindow)
                .GetMethod("LoadAnimationFileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, [path, false]);
            Dispatcher.UIThread.RunJobs();

            Assert.True(ctx.ProjectManager.IsNativeTsxProject);

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var roots = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)tree.ItemsSource!;
            Assert.Single(roots);
            Assert.Equal("ID:0", roots[0].Header);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void LoadAnimationFileAsync_TsxFile_ForcesGridSizeToTsxTileGridAndLocksSnapToGrid()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var path = Path.Combine(dir, "Heroes.tsx");
            File.WriteAllText(path, TsxFixtureXml);

            typeof(MainWindow)
                .GetMethod("LoadAnimationFileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, [path, false]);
            Dispatcher.UIThread.RunJobs();

            var gridSizeInput = window.FindControl<AnimationEditor.Views.Controls.FlankerNumericField>("GridSizeInput")!;
            var snapToGridCheck = window.FindControl<Avalonia.Controls.Primitives.ToggleButton>("SnapToGridCheck")!;
            Assert.Equal(16m, gridSizeInput.Value);
            Assert.True(snapToGridCheck.IsChecked);

            // Editing the grid size for a native tsx project must revert.
            gridSizeInput.Value = 32m;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(16m, gridSizeInput.Value);

            // Unchecking snap-to-grid must revert too -- the tsx grid can't be turned off.
            snapToGridCheck.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.True(snapToGridCheck.IsChecked);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void LoadAnimationFileAsync_TsxFileWithMarginAndSpacing_WireframeGridCarriesBothAndSurvivesSnapToggle()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var path = Path.Combine(dir, "Heroes.tsx");
            File.WriteAllText(path, TsxFixtureXml.Replace("tilecount=\"16\"", "margin=\"2\" spacing=\"1\" tilecount=\"16\""));

            typeof(MainWindow)
                .GetMethod("LoadAnimationFileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, [path, false]);
            Dispatcher.UIThread.RunJobs();

            var wireframe = window.FindControl<AnimationEditor.App.Controls.WireframeControl>("WireframeCtrl")!;
            var expected = new TileGrid(16, 16, Margin: 2, Spacing: 1);
            Assert.Equal(expected, wireframe.Grid);

            // The snap-to-grid revert re-applies the grid; it must not collapse to a plain 16px one.
            var snapToGridCheck = window.FindControl<Avalonia.Controls.Primitives.ToggleButton>("SnapToGridCheck")!;
            snapToGridCheck.IsChecked = false;
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(expected, wireframe.Grid);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void SelectingFrame_NativeTsxProject_HidesTransformAndColorSections()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var path = Path.Combine(dir, "Heroes.tsx");
            File.WriteAllText(path, TsxFixtureXml);

            typeof(MainWindow)
                .GetMethod("LoadAnimationFileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, [path, false]);
            Dispatcher.UIThread.RunJobs();

            var chain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Single();
            ctx.SelectedState.SelectedFrame = chain.Frames[0];
            Dispatcher.UIThread.RunJobs();

            var framePanel = window.FindControl<StackPanel>("PropFramePanel")!;
            var transformSection = window.FindControl<Border>("PropTransformSection")!;
            var colorSection = window.FindControl<Border>("PropColorSection")!;
            Assert.True(framePanel.IsVisible);
            Assert.False(transformSection.IsVisible);
            Assert.False(colorSection.IsVisible);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    [AvaloniaFact]
    public void LoadAnimationFileAsync_InconsistentMultiTileGroup_FlagsAnchorChainWithValidationIssue()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var path = Path.Combine(dir, "Heroes.tsx");
            File.WriteAllText(path, InconsistentGroupFixtureXml);

            typeof(MainWindow)
                .GetMethod("LoadAnimationFileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, [path, false]);
            Dispatcher.UIThread.RunJobs();

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var roots = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)tree.ItemsSource!;
            var anchorNode = Assert.Single(roots);
            Assert.Equal("ID:8", anchorNode.Header);
            Assert.True(anchorNode.HasValidationIssue);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
