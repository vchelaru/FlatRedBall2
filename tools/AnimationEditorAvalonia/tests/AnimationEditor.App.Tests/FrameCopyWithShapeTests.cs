using AnimationEditor.Core.IO;
using AnimationEditor.Core.ViewModels;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Repro for issue #431 follow-up: selecting a frame that contains a shape
/// (e.g. "Frame 1" under "IdleDown" with a "BulletOrigin" rect) and pressing
/// Ctrl+C must copy the FRAME. Today the copy silently no-ops because
/// <see cref="ClipboardPayload"/> serializes through XmlSerializer, which throws
/// on <see cref="ShapesSave.Shapes"/> (a List&lt;object&gt;); the App's
/// fire-and-forget copy handler swallows the exception and the clipboard is
/// left unchanged.
/// </summary>
public class FrameCopyWithShapeTests
{
    private static (MainWindow Window, TestServices Ctx, AnimationFrameSave Frame)
        CreateLoadedWindow()
    {
        var ctx = TestHelpers.BuildServices();

        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "IdleDown" };
        var frame = new AnimationFrameSave { TextureName = "lava.png", FrameLength = 0.1f };
        var rect = new AARectSave { Name = "BulletOrigin", X = 1f, Y = 2f };
        frame.ShapesSave = new ShapesSave();
        frame.ShapesSave.Shapes.Add(rect);
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);

        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        // Build the authentic tree (chain -> frame -> shape) into the same
        // ObservableCollection the window's SyncTreeSelection reads.
        var tree = window.FindControl<TreeView>("AnimTree")!;
        var roots = (ObservableCollection<TreeNodeVm>)tree.ItemsSource!;
        roots.Clear();
        foreach (var node in TreeBuilder.BuildTree(acls))
            roots.Add(node);
        Dispatcher.UIThread.RunJobs();

        return (window, ctx, frame);
    }

    private static string Describe(object? data) => data switch
    {
        null => "null",
        AnimationChainSave c => $"Chain({c.Name})",
        AnimationFrameSave f => $"Frame({f.TextureName})",
        AARectSave r => $"Rect({r.Name})",
        CircleSave => "Circle",
        _ => data.GetType().Name,
    };

    /// <summary>
    /// The canonical "frame is selected" state (only SelectedFrame set). Ctrl+C must
    /// place the frame on the clipboard. Today it silently leaves the clipboard untouched.
    /// </summary>
    [AvaloniaFact]
    public async Task CtrlC_FrameWithShapeSelected_CopiesFrame()
    {
        var (window, ctx, frame) = CreateLoadedWindow();
        try
        {
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            var tree = window.FindControl<TreeView>("AnimTree")!;
            var selectedData = (tree.SelectedItem as TreeNodeVm)?.Data;

            await window.Clipboard!.SetTextAsync("seed");
            tree.Focus();
            window.KeyPress(Key.C, RawInputModifiers.Control, PhysicalKey.None, null);
            Dispatcher.UIThread.RunJobs();

            var clip = await window.Clipboard!.TryGetTextAsync();
            ClipboardPayload.TryDeserialize(clip!, out _, out var frames, out var rectangles, out _);
            var rectangle = rectangles is { Count: > 0 } ? rectangles[0] : null;

            Assert.True(frames is { Count: > 0 },
                $"AnimTree.SelectedItem.Data = {Describe(selectedData)}; " +
                $"expected a FRAME on the clipboard but rect={(rectangle is null ? "null" : rectangle.Name)}, clip='{clip}'");
        }
        finally { window.Close(); }
    }
}
