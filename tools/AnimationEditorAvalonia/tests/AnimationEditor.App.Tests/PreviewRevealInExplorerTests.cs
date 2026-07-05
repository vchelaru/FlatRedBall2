using AnimationEditor.App.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Issue #573 — right-clicking the Preview pane (away from any guide) shows a
/// "View &lt;filename&gt; in Explorer" context menu item for the currently previewed texture.
/// </summary>
public class PreviewRevealInExplorerTests
{
    private static void OpenContextMenu(PreviewControl ctrl) =>
        typeof(PreviewControl)
            .GetMethod("OnContextMenuOpening", BindingFlags.NonPublic | BindingFlags.Instance)!
            .Invoke(ctrl, new object?[] { null, new CancelEventArgs() });

    // ── ResolveSelectedTexturePath ────────────────────────────────────────────

    [AvaloniaFact]
    public void ResolveSelectedTexturePath_NoSelection_ReturnsNull()
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();

        Assert.Null(ctrl.ResolveSelectedTexturePath());
    }

    [AvaloniaFact]
    public void ResolveSelectedTexturePath_FrameHasNoTexture_ReturnsNull()
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "" };
        chain.Frames.Add(frame);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;

        Assert.Null(ctrl.ResolveSelectedTexturePath());
    }

    [AvaloniaFact]
    public void ResolveSelectedTexturePath_ResolvesRelativeToAchxDirectory()
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "textures/hero.png" };
        chain.Frames.Add(frame);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;
        ctx.ProjectManager.FileName = @"C:\proj\anim.achx";

        Assert.Equal("C:/proj/textures/hero.png", ctrl.ResolveSelectedTexturePath());
    }

    // ── OnContextMenuOpening ───────────────────────────────────────────────────

    [AvaloniaFact]
    public void OnContextMenuOpening_NoSelection_CancelsAndLeavesMenuEmpty()
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();

        OpenContextMenu(ctrl);

        Assert.Empty(ctrl.ContextMenu!.Items);
    }

    [AvaloniaFact]
    public void OnContextMenuOpening_TextureSelected_AddsRevealItemWithFilename()
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "textures/hero.png" };
        chain.Frames.Add(frame);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;
        ctx.ProjectManager.FileName = @"C:\proj\anim.achx";

        OpenContextMenu(ctrl);

        var item = ctrl.ContextMenu!.Items.OfType<MenuItem>().Single();
        Assert.Equal("View hero.png in Explorer", item.Header);
    }

    [AvaloniaFact]
    public void OnContextMenuOpening_ClickingItem_ReportsMissingFileViaShowError()
    {
        var ctx = TestHelpers.BuildServices();
        string? reportedError = null;
        var ctrl = ctx.CreatePreviewControl(msg => reportedError = msg);
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "textures/hero.png" };
        chain.Frames.Add(frame);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;
        ctx.ProjectManager.FileName = @"C:\proj\anim.achx";

        OpenContextMenu(ctrl);
        var item = ctrl.ContextMenu!.Items.OfType<MenuItem>().Single();
        item.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));

        Assert.NotNull(reportedError);
    }

    // ── Right-click suppression (guide vs. menu) ───────────────────────────────

    [AvaloniaFact]
    public void SimulateRightClick_HitsGuide_ReturnsTrue()
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.Measure(new Avalonia.Size(64, 64));
        ctrl.Arrange(new Avalonia.Rect(0, 0, 64, 64));
        ctrl.AddHGuide(0f); // world Y=0 -> screen Y=42

        Assert.True(ctrl.SimulateRightClick(30f, 42f));
    }

    [AvaloniaFact]
    public void SimulateRightClick_MissesGuide_ReturnsFalse()
    {
        var ctx = TestHelpers.BuildServices();
        var ctrl = ctx.CreatePreviewControl();
        ctrl.Measure(new Avalonia.Size(64, 64));
        ctrl.Arrange(new Avalonia.Rect(0, 0, 64, 64));
        ctrl.AddHGuide(0f); // world Y=0 -> screen Y=42

        Assert.False(ctrl.SimulateRightClick(30f, 20f));
    }

    // ── End-to-end: real right-click through the pointer pipeline ─────────────
    //
    // Regression for a real bug found in manual testing: marking the PointerPressedEventArgs
    // Handled does NOT stop the ContextMenu from opening. Avalonia's Control.OnPointerReleased
    // raises ContextRequested based on the *PointerReleasedEventArgs*' own Handled flag (checked
    // before this control's OnPointerReleased override even runs its own logic, since the base
    // call happens first) — a completely separate event object from the press. These tests drive
    // real synthetic press+release events through a live window instead of calling the test-only
    // SimulateRightClick hook, which only mirrors the press half and would not have caught this.

    private static (MainWindow Window, TestServices Ctx) CreateWindowWithTexturedFrame()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "hero.png" };
        chain.Frames.Add(frame);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);
        ctx.SelectedState.SelectedChain = chain;
        ctx.SelectedState.SelectedFrame = frame;
        Dispatcher.UIThread.RunJobs();

        return (window, ctx);
    }

    private static Point PreviewCenterInWindow(MainWindow window, PreviewControl preview)
    {
        // Mirrors PreviewControl's own CenterX/CenterY math (RulerSize = 20).
        float centerX = (float)((preview.Bounds.Width - 20) / 2 + 20);
        float centerY = (float)((preview.Bounds.Height - 20) / 2 + 20);
        return preview.TranslatePoint(new Point(centerX, centerY), window)!.Value;
    }

    [AvaloniaFact]
    public void RealRightClick_OnGuide_RemovesGuideAndDoesNotOpenContextMenu()
    {
        var (window, _) = CreateWindowWithTexturedFrame();
        try
        {
            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;
            preview.AddHGuide(0f); // world Y=0 -> screen Y = preview's own centre
            Dispatcher.UIThread.RunJobs();

            var point = PreviewCenterInWindow(window, preview);
            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(0, preview.HGuideCount);
            Assert.Empty(preview.ContextMenu!.Items);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RealRightClick_AwayFromGuide_OpensContextMenuWithRevealItem()
    {
        var (window, _) = CreateWindowWithTexturedFrame();
        try
        {
            var preview = window.FindControl<PreviewControl>("PreviewCtrl")!;

            var point = PreviewCenterInWindow(window, preview);
            window.MouseDown(point, MouseButton.Right);
            window.MouseUp(point, MouseButton.Right);
            Dispatcher.UIThread.RunJobs();

            var item = preview.ContextMenu!.Items.OfType<MenuItem>().Single();
            Assert.Equal("View hero.png in Explorer", item.Header);
        }
        finally { window.Close(); }
    }
}
