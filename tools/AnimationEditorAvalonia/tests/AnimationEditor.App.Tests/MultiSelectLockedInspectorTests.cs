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

    private static void SelectChains(TestServices ctx, params AnimationChainSave[] chains)
    {
        ctx.SelectedState.SelectedChain = chains[0];
        ctx.SelectedState.SelectedNodes = new List<object>(chains);
        FlushUi();
    }

    [AvaloniaTheory]
    [InlineData(true, true, true)]
    [InlineData(false, false, false)]
    public void MultipleChains_LockedCheckboxIsCheckedOnlyWhenAllLocked(bool a, bool b, bool expected)
    {
        var (window, ctx) = CreateWindow();
        try
        {
            AddTwoChains(ctx);
            var chains = ctx.ProjectManager.AnimationChainListSave!.AnimationChains;
            chains[0].IsLocked = a;
            chains[1].IsLocked = b;
            SelectChains(ctx, chains[0], chains[1]);

            Assert.Equal(expected, window.FindControl<CheckBox>("PropChainLocked")!.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void MultipleChains_SomeLocked_LockedCheckboxIsIndeterminate(bool lockedIsPrimary)
    {
        var (window, ctx) = CreateWindow();
        try
        {
            AddTwoChains(ctx);
            var chains = ctx.ProjectManager.AnimationChainListSave!.AnimationChains;
            SelectChains(ctx, lockedIsPrimary ? chains[0] : chains[1], lockedIsPrimary ? chains[1] : chains[0]);

            Assert.Null(window.FindControl<CheckBox>("PropChainLocked")!.IsChecked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void MultipleChains_TogglingLocked_LocksEveryChain_AsOneUndoStep()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            AddTwoChains(ctx);
            var chains = ctx.ProjectManager.AnimationChainListSave!.AnimationChains;
            SelectChains(ctx, chains[1], chains[0]);

            window.FindControl<CheckBox>("PropChainLocked")!.IsChecked = true;
            FlushUi();

            Assert.All(chains, c => Assert.True(c.IsLocked));
            ctx.UndoManager.Undo();
            Assert.True(chains[0].IsLocked);
            Assert.False(chains[1].IsLocked);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void LockedNotice_ShownOnlyWhenEverySelectedItemIsLocked()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (locked, open) = AddTwoChains(ctx);
            var notice = window.FindControl<TextBlock>("PropLockedNotice")!;

            ctx.SelectedState.SelectedFrame = locked;
            ctx.SelectedState.SelectedNodes = new List<object> { locked };
            FlushUi();
            Assert.True(notice.IsVisible);

            ctx.SelectedState.SelectedFrame = locked;
            ctx.SelectedState.SelectedNodes = new List<object> { locked, open };
            FlushUi();
            Assert.False(notice.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditingFrameFlipRelativeColorAlpha_ChangesOnlyTheUnlockedFrame(bool lockedIsPrimary)
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

            window.FindControl<Avalonia.Controls.Primitives.ToggleButton>("PropFlipH")!.IsChecked = true;
            FlushUi();
            window.FindControl<NumericUpDown>("PropRelX")!.Value = 4m;
            FlushUi();
            // Color channels commit on focus loss.
            var red = window.FindControl<NumericUpDown>("PropRed")!;
            var alpha = window.FindControl<NumericUpDown>("PropAlpha")!;
            red.Focus();
            FlushUi();
            red.Value = 77m;
            alpha.Focus();
            FlushUi();
            alpha.Value = 66m;
            red.Focus();
            FlushUi();

            Assert.True(open.FlipHorizontal);
            Assert.Equal(4f, open.RelativeX);
            Assert.Equal(77, open.Red);
            Assert.Equal(66, open.Alpha);
            Assert.False(locked.FlipHorizontal);
            Assert.Equal(0f, locked.RelativeX);
            Assert.Null(locked.Red);
            Assert.Null(locked.Alpha);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(true)]
    [InlineData(false)]
    public void EditingShapeProps_ChangesOnlyTheUnlockedShape(bool lockedIsPrimary)
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var (lockedFrame, openFrame) = AddTwoChains(ctx);
            var lockedRect = new AARectSave { Name = "LR", X = 1 };
            var openRect = new AARectSave { Name = "OR", X = 5 };
            var lockedCircle = new CircleSave { Name = "LC", Radius = 3 };
            var openCircle = new CircleSave { Name = "OC", Radius = 9 };
            var lockedPoly = new PolygonSave { Name = "LP", X = 1 };
            var openPoly = new PolygonSave { Name = "OP", X = 7 };
            lockedFrame.ShapesSave!.Add(lockedRect);
            lockedFrame.ShapesSave.Add(lockedCircle);
            lockedFrame.ShapesSave.Add(lockedPoly);
            openFrame.ShapesSave!.Add(openRect);
            openFrame.ShapesSave.Add(openCircle);
            openFrame.ShapesSave.Add(openPoly);

            void Select(ShapeSave locked, ShapeSave open, System.Action<ShapeSave> setPrimary)
            {
                setPrimary(lockedIsPrimary ? locked : open);
                ctx.SelectedState.SelectedNodes = lockedIsPrimary
                    ? new List<object> { locked, open }
                    : new List<object> { open, locked };
                FlushUi();
            }

            Select(lockedRect, openRect, s => ctx.SelectedState.SelectedRectangle = (AARectSave)s);
            window.FindControl<NumericUpDown>("PropRectX")!.Value = 40m;
            FlushUi();
            Assert.Equal(40f, openRect.X);
            Assert.Equal(1f, lockedRect.X);

            Select(lockedCircle, openCircle, s => ctx.SelectedState.SelectedCircle = (CircleSave)s);
            window.FindControl<NumericUpDown>("PropCircleRadius")!.Value = 20m;
            FlushUi();
            Assert.Equal(20f, openCircle.Radius);
            Assert.Equal(3f, lockedCircle.Radius);

            Select(lockedPoly, openPoly, s => ctx.SelectedState.SelectedPolygon = (PolygonSave)s);
            window.FindControl<NumericUpDown>("PropPolygonX")!.Value = 30m;
            FlushUi();
            Assert.Equal(30f, openPoly.X);
            Assert.Equal(1f, lockedPoly.X);
        }
        finally { window.Close(); }
    }
}
