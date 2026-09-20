using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Verifies that File → New and File → Load reset the sprite shown in the
/// wireframe and the animation playing in the preview by clearing selection.
/// </summary>
public class NewAndLoadResetTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

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

    private static string WriteAchx(string dir, params string[] chainNames)
    {
        var path = Path.Combine(dir, "test.achx");
        var acls = new AnimationChainListSave { CoordinateType = TextureCoordinateType.Pixel };
        foreach (var name in chainNames)
        {
            var chain = new AnimationChainSave { Name = name };
            // Give each chain a frame so expand/collapse state is meaningful.
            chain.Frames.Add(new AnimationFrameSave { TextureName = name + ".png", FrameLength = 0.1f });
            acls.AnimationChains.Add(chain);
        }
        acls.Save(path);
        return path;
    }

    // ── File → New ────────────────────────────────────────────────────────────

    /// <summary>
    /// File → New must clear the tree so it shows no chains.
    /// (Regression: OnNewClick was missing RefreshTreeView.)
    /// </summary>
    [AvaloniaFact]
    public void New_ClearsTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();

            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            var tree  = window.FindControl<TreeView>("AnimTree")!;
            var roots = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)tree.ItemsSource!;
            Assert.Empty(roots);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// File → New must reset SelectedChain and SelectedFrame so the wireframe
    /// and preview stop showing the previous file's sprite and animation.
    /// </summary>
    [AvaloniaFact]
    public void New_ClearsSelection()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "Tex.png", ShapesSave = new ShapesSave() };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame; // simulate a frame being selected

            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Null(ctx.SelectedState.SelectedChain);
            Assert.Null(ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// File → New must reset SelectedChain even when selection was made via the tree.
    /// In a real window Avalonia fires AnimTree.SelectionChanged when _treeRoots is
    /// cleared; the handler must not re-apply the stale tree item after Reset().
    /// </summary>
    [AvaloniaFact]
    public void New_ClearsSelection_WhenSelectedViaTree()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            ctx.AppCommands.AddAnimationChainWithName("Walk");
            Dispatcher.UIThread.RunJobs();

            var tree  = window.FindControl<TreeView>("AnimTree")!;
            var roots = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)tree.ItemsSource!;
            Assert.NotEmpty(roots);

            // Simulate the user clicking the chain node in the tree
            tree.SelectedItem = roots[0];
            Dispatcher.UIThread.RunJobs();

            // File → New
            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.Null(ctx.SelectedState.SelectedChain);
            Assert.Null(ctx.SelectedState.SelectedFrame);
        }
        finally { window.Close(); }
    }

    // Issue #1147: OpenAsNewUnsavedDocument (which OnNewClick calls) used to assign
    // AnimationChainListSave/FileName directly, bypassing the RestoreTsxState(null) reset
    // NewFile/CloseProject already had -- File > New from an active native tsx tab left
    // IsNativeTsxProject stuck true for the brand-new blank document.
    [AvaloniaFact]
    public async Task New_AfterOpeningTsxTab_ClearsNativeTsxState()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var tsxPath = Path.Combine(dir, "Heroes.tsx");
            File.WriteAllText(tsxPath,
                """
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
                """);
            await window.OpenFileAsTab(tsxPath);
            Dispatcher.UIThread.RunJobs();
            Assert.True(ctx.ProjectManager.IsNativeTsxProject);

            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.False(ctx.ProjectManager.IsNativeTsxProject);
            Assert.Null(ctx.ProjectManager.TsxTileSize);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // Issue #1147: OpenAsNewUnsavedDocument (which OnNewClick calls) never called UpdateTitle.
    // ActivateUntitledTabContent and the "all tabs closed" branch both call it explicitly right
    // after resetting, but OnNewClick's own follow-up (SaveCurrentAnimationChainListAsync) only
    // updates the title on a *successful* Save As -- if the user cancels the dialog (simulated
    // here by the default NullFileDialogService), the title bar was left showing the just-closed
    // tsx file's name even though FileName/IsNativeTsxProject had already reset underneath it.
    [AvaloniaFact]
    public async Task New_AfterOpeningTsxTabAndCancelingSaveAs_TitleNoLongerShowsOldFileName()
    {
        var dir = Path.Combine(Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var tsxPath = Path.Combine(dir, "Heroes.tsx");
            File.WriteAllText(tsxPath,
                """
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
                """);
            await window.OpenFileAsTab(tsxPath);
            Dispatcher.UIThread.RunJobs();
            Assert.Contains("Heroes.tsx", window.Title);

            // NullFileDialogService (the default here) always cancels, so the Save As
            // OnNewClick triggers never completes.
            window.FindControl<MenuItem>("MenuNew")!
                  .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Dispatcher.UIThread.RunJobs();

            Assert.DoesNotContain("Heroes.tsx", window.Title);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    // ── File → Load ───────────────────────────────────────────────────────────

    /// <summary>
    /// Loading a new .achx must clear the old selection so the wireframe and
    /// preview stop rendering the previous file's sprite/animation.
    /// </summary>
    [AvaloniaFact]
    public void Load_ClearsSelectionBeforePopulatingNewFile()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            // Set up an initial chain and select it
            var oldChain = new AnimationChainSave { Name = "OldWalk" };
            var oldFrame = new AnimationFrameSave { TextureName = "Old.png", ShapesSave = new ShapesSave() };
            oldChain.Frames.Add(oldFrame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(oldChain);
            ctx.SelectedState.SelectedFrame = oldFrame;

            // Load a different .achx
            var path = WriteAchx(dir, "NewRun");
            typeof(MainWindow)
                .GetMethod("LoadAnimationFileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, [path, false]);
            Dispatcher.UIThread.RunJobs();

            Assert.Null(ctx.SelectedState.SelectedFrame);
            // SelectedChain should be the first chain of the *new* file, not the old one
            Assert.Equal("NewRun", ctx.SelectedState.SelectedChain?.Name);
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }

    /// <summary>
    /// Loading a .achx must present every chain collapsed so the tree is scannable
    /// — even with many chains — instead of force-expanding every chain's frames.
    /// </summary>
    [AvaloniaFact]
    public void Load_CollapsesAllChains()
    {
        var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var (window, ctx) = CreateWindow();
        try
        {
            var path = WriteAchx(dir, "Walk", "Run", "Idle");
            typeof(MainWindow)
                .GetMethod("LoadAnimationFileAsync", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, [path, false]);
            Dispatcher.UIThread.RunJobs();

            var tree  = window.FindControl<TreeView>("AnimTree")!;
            var roots = (System.Collections.ObjectModel.ObservableCollection<AnimationEditor.Core.ViewModels.TreeNodeVm>)tree.ItemsSource!;

            Assert.Equal(3, roots.Count);
            Assert.All(roots, node => Assert.False(node.IsExpanded));
        }
        finally
        {
            window.Close();
            Directory.Delete(dir, true);
        }
    }
}
