using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Copy / Paste / Duplicate must resolve the selection from the selection model
/// (<see cref="ISelectedState"/>), not from <c>AnimTree.SelectedItem</c>. The tree
/// item is null whenever the selected node isn't realized — e.g. a frame is selected
/// while its chain row is collapsed — yet the selection model still holds it. Reading
/// only the tree item made Ctrl+C/V/D silently no-op in that state.
/// </summary>
public class SelectionModelFallbackTests
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

    // Selects a frame in the model only (no realized tree node), focuses the tree,
    // and asserts AnimTree.SelectedItem is null so the fallback is what's under test.
    private static AnimationFrameSave SelectFrameViaModelOnly(MainWindow window, TestServices ctx)
    {
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "run.png" };
        frame.ShapesSave = new ShapesSave();
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

        ctx.SelectedState.SelectedFrame = frame;
        var tree = window.FindControl<TreeView>("AnimTree")!;
        tree.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Null(tree.SelectedItem);   // precondition: tree has no realized selection
        return frame;
    }

    [AvaloniaFact]
    public async Task CtrlC_FrameSelectedInModelOnly_CopiesFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            SelectFrameViaModelOnly(window, ctx);
            await window.Clipboard!.SetTextAsync("seed");

            window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            var text = await window.Clipboard!.TryGetTextAsync();
            Assert.True(ClipboardPayload.TryDeserialize(text!, out _, out var frames, out _));
            Assert.NotNull(frames);
            Assert.Single(frames!);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CtrlD_FrameSelectedInModelOnly_DuplicatesFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var frame = SelectFrameViaModelOnly(window, ctx);
            var chain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];

            window.KeyPress(Key.D, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, chain.Frames.Count);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Delete_FrameSelectedInModelOnly_DeletesFrame()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            SelectFrameViaModelOnly(window, ctx);
            var chain = ctx.ProjectManager.AnimationChainListSave!.AnimationChains[0];

            window.KeyPress(Key.Delete, RawInputModifiers.None, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            Assert.Empty(chain.Frames);
        }
        finally { window.Close(); }
    }
}
