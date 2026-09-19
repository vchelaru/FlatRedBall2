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
}
