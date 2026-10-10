using AnimationEditor.Core.ViewModels;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.AnimationEditorCommon;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// macOS keyboards have no Delete key — Backspace is the delete key there — so on macOS Backspace
/// runs the same delete hotkey, with the same text-input and Inspector guards, as Delete. Other
/// platforms leave Backspace unbound.
/// </summary>
public class BackspaceDeleteTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow(bool macOS = true)
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        var window = ctx.CreateMainWindow(useMacOSChrome: macOS);
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    private static void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Backspace_ShapeSelectedInTree_DeletesIt_AndUndoRestoresIt()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            var rect = new AARectSave { Name = "Rect" };
            frame.ShapesSave.Add(rect);
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            typeof(MainWindow).GetMethod("RebuildTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, new object[] { System.Array.Empty<string>() });
            FlushUi();

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNode = ((System.Collections.IEnumerable)tree.ItemsSource!).Cast<TreeNodeVm>().First();
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(chainNode.Children[0].Children[0]);
            FlushUi();

            tree.Focus();
            window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.None, null);
            FlushUi();

            Assert.Empty(frame.ShapesSave!.Shapes);

            window.KeyPress(Key.Z, RawInputModifiers.Control, PhysicalKey.None, null);
            FlushUi();

            Assert.Equal(new[] { rect }, frame.ShapesSave!.AARectSaves);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Backspace_TextBoxFocused_DoesNotDeleteSelectedFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave { TextureName = "run.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = chain.Frames[0];

            var speedInput = window.FindControl<FlankerNumericField>("SpeedInput")!
                .GetVisualDescendants().OfType<TextBox>().First();
            speedInput.Focus();
            Dispatcher.UIThread.RunJobs();

            window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(chain.Frames);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Backspace_NotMacOS_DoesNotDeleteSelectedShape()
    {
        var (window, ctx) = CreateWindow(macOS: false);
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
            frame.ShapesSave.Add(new AARectSave { Name = "Rect" });
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            typeof(MainWindow).GetMethod("RebuildTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, new object[] { System.Array.Empty<string>() });
            FlushUi();

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var chainNode = ((System.Collections.IEnumerable)tree.ItemsSource!).Cast<TreeNodeVm>().First();
            tree.SelectedItems!.Clear();
            tree.SelectedItems.Add(chainNode.Children[0].Children[0]);
            FlushUi();

            tree.Focus();
            window.KeyPress(Key.Back, RawInputModifiers.None, PhysicalKey.None, null);
            FlushUi();

            Assert.Single(frame.ShapesSave!.Shapes);
        }
        finally { window.Close(); }
    }
}
