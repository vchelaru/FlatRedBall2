using AnimationEditor.Core;
using AnimationEditor.Core.IO;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// Headless coverage for issue #1032 follow-up: the Inspector's frame/shape property panels
/// must disable (not just silently no-op) when the selected frame/shape belongs to a locked
/// chain, so the UI doesn't imply an edit is possible when <c>AppCommands</c> would discard it.
/// </summary>
public class PropertyPanelLockTests
{
    private static (MainWindow Window, TestServices Ctx, AnimationChainSave Chain, AnimationFrameSave Frame, AARectSave Rect) CreateWindowWithFrameAndRect()
    {
        var acls = new AnimationChainListSave();
        var chain = new AnimationChainSave { Name = "Walk" };
        var rect = new AARectSave { Name = "HitBox", X = 3, Y = 4, ScaleX = 8, ScaleY = 9 };
        var frame = new AnimationFrameSave
        {
            TextureName = "dummy.png",
            FrameLength = 0.1f,
            ShapesSave = new ShapesSave(),
        };
        frame.ShapesSave!.Shapes.Add(rect);
        chain.Frames.Add(frame);
        acls.AnimationChains.Add(chain);

        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.FileName = null;
        ctx.AppCommands.DoOnUiThread = a => a();
        ctx.AppCommands.FileDialogService = NullFileDialogService.Instance;

        // MainWindow.OnOpened resets AnimationChainListSave to a fresh empty one when there's no
        // CLI file / saved tabs -- assigning the project must happen after Show(), not before.
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();

        ctx.ProjectManager.AnimationChainListSave = acls;
        ctx.SelectedState.SelectedFrame = frame;
        Dispatcher.UIThread.RunJobs();

        return (window, ctx, chain, frame, rect);
    }

    private static T FindCtrl<T>(MainWindow w, string name) where T : Control
        => w.FindControl<T>(name) ?? throw new InvalidOperationException($"Control '{name}' not found");

    [AvaloniaFact]
    public void PropFramePanel_SelectedFrameChainLocked_IsDisabled()
    {
        var (window, ctx, chain, _, _) = CreateWindowWithFrameAndRect();
        try
        {
            ctx.AppCommands.SetChainLocked(chain, true);
            Dispatcher.UIThread.RunJobs();

            Assert.False(FindCtrl<StackPanel>(window, "PropFramePanel").IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PropFramePanel_SelectedFrameChainUnlocked_IsEnabled()
    {
        var (window, _, _, _, _) = CreateWindowWithFrameAndRect();
        try
        {
            Assert.True(FindCtrl<StackPanel>(window, "PropFramePanel").IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PropRectPanel_OwningChainLocked_IsDisabled()
    {
        var (window, ctx, chain, _, rect) = CreateWindowWithFrameAndRect();
        try
        {
            ctx.SelectedState.SelectedRectangle = rect;
            Dispatcher.UIThread.RunJobs();

            ctx.AppCommands.SetChainLocked(chain, true);
            Dispatcher.UIThread.RunJobs();

            Assert.False(FindCtrl<StackPanel>(window, "PropRectPanel").IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PropChainLocked_ChainLocked_CheckboxStaysEnabled()
    {
        var (window, ctx, chain, _, _) = CreateWindowWithFrameAndRect();
        try
        {
            ctx.AppCommands.SetChainLocked(chain, true);
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            Assert.True(FindCtrl<CheckBox>(window, "PropChainLocked").IsEnabled);
        }
        finally { window.Close(); }
    }
}
