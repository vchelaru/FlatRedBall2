using AnimationEditor.Core.IO;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using System.Collections.Generic;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// With a multi-selection spanning locked and unlocked chains, the inspector reflects only what is
/// editable: locked items don't contribute to "(mixed)", and the panel is disabled only when every
/// selected item is locked.
/// </summary>
public class MultiSelectLockedInspectorTests
{
    private static (MainWindow Window, TestServices Ctx) CreateWindow()
    {
        var ctx = TestHelpers.BuildServices();
        ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
        ctx.ProjectManager.FileName = null;
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        return (window, ctx);
    }

    private static void FlushUi()
    {
        Dispatcher.UIThread.RunJobs();
        Dispatcher.UIThread.RunJobs();
    }

    private static (AnimationFrameSave Locked, AnimationFrameSave Unlocked) AddTwoChains(TestServices ctx)
    {
        var lockedChain = new AnimationChainSave { Name = "Locked", IsLocked = true };
        var openChain = new AnimationChainSave { Name = "Open" };
        var locked = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, ShapesSave = new ShapesSave() };
        var open = new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.2f, ShapesSave = new ShapesSave() };
        lockedChain.Frames.Add(locked);
        openChain.Frames.Add(open);
        ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(lockedChain);
        ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(openChain);
        return (locked, open);
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void LockedAndUnlockedFrames_ShowEditableFrameValue_NotMixed_AndPanelEnabled(bool lockedIsPrimary)
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (locked, open) = AddTwoChains(ctx);

            ctx.SelectedState.SelectedFrame = lockedIsPrimary ? locked : open;
            ctx.SelectedState.SelectedNodes = lockedIsPrimary
                ? new List<object> { locked, open }
                : new List<object> { open, locked };
            FlushUi();

            var frameLen = window.FindControl<FlankerNumericField>("PropFrameLen")!;
            Assert.Equal(0.2m, frameLen.Value);
            Assert.True(window.FindControl<StackPanel>("PropFramePanel")!.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AllFramesLocked_PanelDisabled()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (locked, open) = AddTwoChains(ctx);
            ctx.AppCommands.SetChainLocked(ctx.ProjectManager.AnimationChainListSave!.AnimationChains[1], true);

            ctx.SelectedState.SelectedFrame = locked;
            ctx.SelectedState.SelectedNodes = new List<object> { locked, open };
            FlushUi();

            Assert.False(window.FindControl<StackPanel>("PropFramePanel")!.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LockedAndUnlockedFrames_ColorFieldIgnoresLockedFrame_NotMixed()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (locked, open) = AddTwoChains(ctx);
            locked.Red = 10;
            open.Red = 20;

            ctx.SelectedState.SelectedFrame = locked;
            ctx.SelectedState.SelectedNodes = new List<object> { locked, open };
            FlushUi();

            var red = window.FindControl<NumericUpDown>("PropRed")!;
            Assert.Equal(20m, red.Value);
            Assert.NotEqual("(mixed)", red.PlaceholderText);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void LockedAndUnlockedRects_ShowEditableRectValue_NotMixed_AndPanelEnabled(bool lockedIsPrimary)
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (lockedFrame, openFrame) = AddTwoChains(ctx);
            var lockedRect = new AARectSave { Name = "L", X = 1 };
            var openRect = new AARectSave { Name = "O", X = 5 };
            lockedFrame.ShapesSave!.Add(lockedRect);
            openFrame.ShapesSave!.Add(openRect);

            ctx.SelectedState.SelectedRectangle = lockedIsPrimary ? lockedRect : openRect;
            ctx.SelectedState.SelectedNodes = lockedIsPrimary
                ? new List<object> { lockedRect, openRect }
                : new List<object> { openRect, lockedRect };
            FlushUi();

            Assert.Equal(5m, window.FindControl<NumericUpDown>("PropRectX")!.Value);
            Assert.True(window.FindControl<StackPanel>("PropRectPanel")!.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void LockedAndUnlockedCircles_ShowEditableValue_NotMixed_AndPanelEnabled(bool lockedIsPrimary)
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (lockedFrame, openFrame) = AddTwoChains(ctx);
            var lockedCircle = new CircleSave { Name = "L", Radius = 3 };
            var openCircle = new CircleSave { Name = "O", Radius = 9 };
            lockedFrame.ShapesSave!.Add(lockedCircle);
            openFrame.ShapesSave!.Add(openCircle);

            ctx.SelectedState.SelectedCircle = lockedIsPrimary ? lockedCircle : openCircle;
            ctx.SelectedState.SelectedNodes = lockedIsPrimary
                ? new List<object> { lockedCircle, openCircle }
                : new List<object> { openCircle, lockedCircle };
            FlushUi();

            Assert.Equal(9m, window.FindControl<NumericUpDown>("PropCircleRadius")!.Value);
            Assert.True(window.FindControl<StackPanel>("PropCirclePanel")!.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void LockedAndUnlockedPolygons_ShowEditableValue_NotMixed_AndPanelEnabled(bool lockedIsPrimary)
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (lockedFrame, openFrame) = AddTwoChains(ctx);
            var lockedPoly = new PolygonSave { Name = "L", X = 1 };
            var openPoly = new PolygonSave { Name = "O", X = 7 };
            lockedFrame.ShapesSave!.Add(lockedPoly);
            openFrame.ShapesSave!.Add(openPoly);

            ctx.SelectedState.SelectedPolygon = lockedIsPrimary ? lockedPoly : openPoly;
            ctx.SelectedState.SelectedNodes = lockedIsPrimary
                ? new List<object> { lockedPoly, openPoly }
                : new List<object> { openPoly, lockedPoly };
            FlushUi();

            Assert.Equal(7m, window.FindControl<NumericUpDown>("PropPolygonX")!.Value);
            Assert.True(window.FindControl<StackPanel>("PropPolygonPanel")!.IsEnabled);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditingFrameLength_ChangesOnlyTheUnlockedFrame(bool lockedIsPrimary)
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (locked, open) = AddTwoChains(ctx);

            ctx.SelectedState.SelectedFrame = lockedIsPrimary ? locked : open;
            ctx.SelectedState.SelectedNodes = lockedIsPrimary
                ? new List<object> { locked, open }
                : new List<object> { open, locked };
            FlushUi();

            window.FindControl<FlankerNumericField>("PropFrameLen")!.Value = 0.5m;
            FlushUi();

            Assert.Equal(0.5f, open.FrameLength);
            Assert.Equal(0.1f, locked.FrameLength);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void AllRectsLocked_PanelDisabled()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (lockedFrame, _) = AddTwoChains(ctx);
            var r1 = new AARectSave { Name = "A" };
            var r2 = new AARectSave { Name = "B" };
            lockedFrame.ShapesSave!.Add(r1);
            lockedFrame.ShapesSave!.Add(r2);

            ctx.SelectedState.SelectedRectangle = r1;
            ctx.SelectedState.SelectedNodes = new List<object> { r1, r2 };
            FlushUi();

            Assert.False(window.FindControl<StackPanel>("PropRectPanel")!.IsEnabled);
        }
        finally { window.Close(); }
    }
}
