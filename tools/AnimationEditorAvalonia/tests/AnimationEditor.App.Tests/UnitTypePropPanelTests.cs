using AnimationEditor.Core;
using AnimationEditor.Core.CommandsAndState;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.Animation.Content;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless integration tests that verify the property panel reacts correctly when
/// a frame is selected.
/// </summary>
public class UnitTypePropPanelTests
{
    private TestServices ctx = null!;

    // ── Helpers ───────────────────────────────────────────────────────────────

    private MainWindow CreateWindowWithFrame(out AnimationFrameSave frame)
    {
        var acls  = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        frame = new AnimationFrameSave
        {
            TextureName      = "dummy.png",
            FrameLength      = 0.1f,
            LeftCoordinate   = 0.25f,
            RightCoordinate  = 0.50f,
            TopCoordinate    = 0.0f,
            BottomCoordinate = 0.25f,
        };
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);

        // Reset all singletons before creating the window.
        ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.ProjectManager.FileName = null;
        ctx.SelectedState.SelectedChain = null;
        ctx.SelectedState.SelectedFrame = null;
        ctx.SelectedState.SelectedNodes = new System.Collections.Generic.List<object>();
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.ConfirmAsync = (_, _) => Task.FromResult(true);
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        // Create and show FIRST so the window subscribes to SelectionChanged.
        var window = ctx.CreateMainWindow();
        window.Show();

        // THEN set selection — this fires SelectionChanged which the window now handles.
        ctx.SelectedState.SelectedFrame = frame;
        Dispatcher.UIThread.RunJobs();   // flush InvokeAsync(RefreshPropertyPanel)

        return window;
    }

    private static T FindCtrl<T>(MainWindow w, string name) where T : Control
        => w.FindControl<T>(name)
           ?? throw new InvalidOperationException($"Control '{name}' not found");

    // ── Initial state ─────────────────────────────────────────────────────────

    [AvaloniaFact]
    public void PropFramePanel_WhenFrameSelected_IsVisible()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            // Frame was selected in CreateWindowWithFrame + RunJobs — panel must be visible.
            var panel = FindCtrl<StackPanel>(window, "PropFramePanel");
            Assert.True(panel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PropNoneLabel_WhenChainSelected_ShowsContextHint()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            // Directly set a chain with no frame — SelectedChain setter clears frame/rect/circ
            ctx.SelectedState.SelectedChain = new AnimationChainSave { Name = "Walk" };
            Dispatcher.UIThread.RunJobs();

            var label = FindCtrl<TextBlock>(window, "PropNoneLabel");
            Assert.True(label.IsVisible);
            Assert.Equal("Select a frame or shape to edit its properties.", label.Text);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PropNoneLabel_WhenNoFrameSelected_IsVisible()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            // Clear selection — fires SelectionChanged which refreshes the panel
            ctx.SelectedState.SelectedFrame = null;
            Dispatcher.UIThread.RunJobs();

            var label = FindCtrl<TextBlock>(window, "PropNoneLabel");
            Assert.True(label.IsVisible);

            var framePanel = FindCtrl<StackPanel>(window, "PropFramePanel");
            Assert.False(framePanel.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PropNoneLabel_WhenNothingSelected_ShowsNoSelection()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            // Reset clears chain + frame + shapes and fires SelectionChanged
            ctx.SelectedState.Reset();
            Dispatcher.UIThread.RunJobs();

            var label = FindCtrl<TextBlock>(window, "PropNoneLabel");
            Assert.True(label.IsVisible);
            Assert.Equal("No selection", label.Text);
        }
        finally { window.Close(); }
    }

    // ── Pixel section always visible ──────────────────────────────────────────

    [AvaloniaFact]
    public void PropPixelSection_WhenFrameSelected_IsVisible()
    {
        var window = CreateWindowWithFrame(out _);
        try
        {
            Assert.True(FindCtrl<Control>(window, "PropPixelSection").IsVisible,
                "PropPixelSection should always be visible when a frame is selected");
        }
        finally { window.Close(); }
    }

    // ── Shape selection ───────────────────────────────────────────────────────

    [AvaloniaFact]
    public void CircleSelection_ShowsOnlyCircleInspector()
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "dummy.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        var circle = new CircleSave { Name = "HurtCircle", X = 6, Y = 2, Radius = 7 };
        frame.ShapesSave!.CircleSaves.Add(circle);
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);

        ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = acls;
        var window = ctx.CreateMainWindow();
        window.Show();

        try
        {
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();
            ctx.SelectedState.SelectedCircle = circle;
            Dispatcher.UIThread.RunJobs();

            Assert.False(FindCtrl<StackPanel>(window, "PropFramePanel").IsVisible);
            Assert.False(FindCtrl<StackPanel>(window, "PropRectPanel").IsVisible);
            Assert.True(FindCtrl<StackPanel>(window, "PropCirclePanel").IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RectangleSelection_ShowsOnlyRectangleInspector()
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        var frame = new AnimationFrameSave { TextureName = "dummy.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        var rect = new AARectSave { Name = "HitBox", X = 3, Y = 4, ScaleX = 8, ScaleY = 9 };
        frame.ShapesSave!.AARectSaves.Add(rect);
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);

        ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = acls;
        var window = ctx.CreateMainWindow();
        window.Show();

        try
        {
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();
            ctx.SelectedState.SelectedRectangle = rect;
            Dispatcher.UIThread.RunJobs();

            Assert.False(FindCtrl<StackPanel>(window, "PropFramePanel").IsVisible);
            Assert.True(FindCtrl<StackPanel>(window, "PropRectPanel").IsVisible);
            Assert.False(FindCtrl<StackPanel>(window, "PropCirclePanel").IsVisible);
        }
        finally { window.Close(); }
    }
}
