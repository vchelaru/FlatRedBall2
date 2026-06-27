using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Tests for issue #454: Ctrl+D / Cmd+D duplicates the selected frame or chain,
/// mirroring the selection-dispatch of Ctrl+C and gated behind text-input focus.
/// </summary>
public class DuplicateShortcutTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    private static TreeNodeVm SeedAndSelect(MainWindow window, object data, string header)
    {
        var tree = window.FindControl<TreeView>("AnimTree")!;
        var roots = (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
        var vm = new TreeNodeVm { Header = header, Data = data };
        roots.Add(vm);
        tree.SelectedItems!.Add(vm);
        tree.Focus();
        Dispatcher.UIThread.RunJobs();
        return vm;
    }

    /// <summary>
    /// Chain selected → Ctrl+D duplicates the chain, appending a copy to the list.
    /// </summary>
    [AvaloniaFact]
    public void CtrlD_ChainSelected_DuplicatesChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Run" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            SeedAndSelect(window, chain, "Run");

            window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Count);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Frame selected → Ctrl+D duplicates the frame within its containing chain.
    /// </summary>
    [AvaloniaFact]
    public void CtrlD_FrameSelected_DuplicatesFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "run.png", FrameLength = 0.1f };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            SeedAndSelect(window, frame, "run.png");

            window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, chain.Frames.Count);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// macOS: Cmd+D (Meta) duplicates just like Ctrl+D on Windows/Linux.
    /// </summary>
    [AvaloniaFact]
    public void MetaD_ChainSelected_DuplicatesChain()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Run" };
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            SeedAndSelect(window, chain, "Run");

            window.KeyPress(Key.D, RawInputModifiers.Meta, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Count);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Rectangle selected → Ctrl+D duplicates the shape into its frame. Guards
    /// against the regression where Ctrl+D handled chains/frames but not shapes.
    /// </summary>
    [AvaloniaFact]
    public void CtrlD_RectangleSelected_DuplicatesShape()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "run.png" };
            frame.ShapesSave = new ShapesSave();
            var rect = new AARectSave { Name = "HitBox" };
            frame.ShapesSave.Shapes.Add(rect);
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            SeedAndSelect(window, rect, "HitBox");

            window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, frame.ShapesSave!.AARectSaves.Count());
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Circle selected → Ctrl+D duplicates the shape into its frame.
    /// </summary>
    [AvaloniaFact]
    public void CtrlD_CircleSelected_DuplicatesShape()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "run.png" };
            frame.ShapesSave = new ShapesSave();
            var circle = new CircleSave { Name = "Hurt", Radius = 4 };
            frame.ShapesSave.Shapes.Add(circle);
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            SeedAndSelect(window, circle, "Hurt");

            window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, frame.ShapesSave!.CircleSaves.Count());
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Text box focused → Ctrl+D must not duplicate; the keystroke belongs to the
    /// text editor, not the window-level handler.
    /// </summary>
    [AvaloniaFact]
    public void CtrlD_TextBoxFocused_DoesNothing()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            chain.Frames.Add(new AnimationFrameSave { TextureName = "run.png" });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = chain.Frames[0];

            var speedInput = window.FindControl<TextBox>("SpeedInput")!;
            speedInput.Focus();
            Dispatcher.UIThread.RunJobs();
            Assert.IsType<TextBox>(window.FocusManager?.GetFocusedElement());

            window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Single(chain.Frames);
            Assert.Single(ctx.ProjectManager.AnimationChainListSave!.AnimationChains);
        }
        finally { window.Close(); }
    }
}
