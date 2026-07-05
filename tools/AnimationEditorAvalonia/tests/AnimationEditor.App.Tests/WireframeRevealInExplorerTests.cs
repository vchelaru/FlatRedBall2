using AnimationEditor.App.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using FlatRedBall2.Animation.Content;
using SkiaSharp;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #573 — right-clicking the Wireframe pane (top) shows a "View &lt;filename&gt; in
/// Explorer" context menu item for the currently loaded texture, mirroring the Preview pane's
/// menu (<see cref="PreviewRevealInExplorerTests"/>).
/// </summary>
public class WireframeRevealInExplorerTests
{
    private static void OpenContextMenu(WireframeControl ctrl) =>
        typeof(WireframeControl)
            .GetMethod("OnContextMenuOpening", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(ctrl, new object?[] { null, new CancelEventArgs() });

    private static string WriteSolidPng(string dir, string name)
    {
        var path = System.IO.Path.Combine(dir, name);
        using var bm = new SKBitmap(4, 4);
        bm.Erase(SKColors.Red);
        using var data = bm.Encode(SKEncodedImageFormat.Png, 100);
        System.IO.File.WriteAllBytes(path, data.ToArray());
        return path;
    }

    [AvaloniaFact]
    public void OnContextMenuOpening_NoSelection_CancelsAndLeavesMenuEmpty()
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreateWireframeControl();

        OpenContextMenu(ctrl);

        Assert.Empty(ctrl.ContextMenu!.Items);
    }

    [AvaloniaFact]
    public void OnContextMenuOpening_TextureSelected_AddsRevealItemWithFilename()
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreateWireframeControl();
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), System.Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var pngPath = WriteSolidPng(dir, "hero.png");
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = pngPath };
            chain.Frames.Add(frame);
            ctx.SelectedState.SelectedChain = chain;
            ctx.SelectedState.SelectedFrame = frame;

            OpenContextMenu(ctrl);

            var item = ctrl.ContextMenu!.Items.OfType<MenuItem>().Single();
            Assert.Equal("View hero.png in Explorer", item.Header);
        }
        finally { System.IO.Directory.Delete(dir, recursive: true); }
    }

    [AvaloniaFact]
    public void OnContextMenuOpening_ClickingItem_ReportsMissingFileViaShowError()
    {
        var ctx = TestHelpers.BuildServices();
        string? reportedError = null;
        var ctrl = ctx.CreateWireframeControl(msg => reportedError = msg);
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = @"C:\does\not\exist\hero.png" };
        chain.Frames.Add(frame);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        OpenContextMenu(ctrl);
        var item = ctrl.ContextMenu!.Items.OfType<MenuItem>().Single();
        item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

        Assert.NotNull(reportedError);
    }
}
