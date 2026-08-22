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
/// Regression tests for issue #571: editing a property with multiple frames selected in the
/// tree must apply the edit to every selected frame, not just the primary one. This file covers
/// the property panel's mixed-value display — the read side of the same bug, where a field must
/// show blank/"(mixed)" instead of silently displaying one frame's value as if it applied to all.
/// </summary>
public class MultiSelectPropertyPanelTests
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

    [AvaloniaFact]
    public void RefreshPropertyPanel_FramesDisagreeOnFrameLength_ShowsBlankWithMixedPlaceholder()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f };
            var f1 = new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.2f };
            chain.Frames.AddRange(new[] { f0, f1 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            ctx.SelectedState.SelectedFrame = f0;
            ctx.SelectedState.SelectedNodes = new List<object> { f0, f1 };
            FlushUi();

            var propFrameLen = window.FindControl<FlankerNumericField>("PropFrameLen")!;

            Assert.Null(propFrameLen.Value);
            Assert.Equal("(mixed)", propFrameLen.PlaceholderText);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void RefreshPropertyPanel_FramesAgreeOnFrameLength_ShowsSharedValue()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.25f };
            var f1 = new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.25f };
            chain.Frames.AddRange(new[] { f0, f1 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            ctx.SelectedState.SelectedFrame = f0;
            ctx.SelectedState.SelectedNodes = new List<object> { f0, f1 };
            FlushUi();

            var propFrameLen = window.FindControl<FlankerNumericField>("PropFrameLen")!;

            Assert.Equal(0.25m, propFrameLen.Value);
        }
        finally { window.Close(); }
    }

    /// <summary>
    /// Issue #860: the Core-level bulk command (<c>SetFrameRelative_MultipleFrames_AppliesToAllAsOneUndoStep</c>
    /// in InspectorPropertyUndoTests) proves <c>IAppCommands.SetFrameRelative</c> itself works, but nothing
    /// previously drove the real property-panel Apply path (<c>ApplyFrameRelative</c>, wired to
    /// <c>PropRelX</c>/<c>PropRelY</c>'s <c>ValueChanged</c>) the way a user actually would.
    /// </summary>
    [AvaloniaFact]
    public void ApplyFrameRelative_MultipleFramesSelected_AppliesXAndYToBoth()
    {
        var (window, ctx) = CreateWindow();
        try
        {
            var chain = new AnimationChainSave { Name = "Walk" };
            var f0 = new AnimationFrameSave { TextureName = "a.png", FrameLength = 0.1f, RelativeX = 1f, RelativeY = 2f };
            var f1 = new AnimationFrameSave { TextureName = "b.png", FrameLength = 0.1f, RelativeX = 3f, RelativeY = 4f };
            chain.Frames.AddRange(new[] { f0, f1 });
            ctx.ProjectManager.AnimationChainListSave!.AnimationChains.Add(chain);

            ctx.SelectedState.SelectedFrame = f0;
            ctx.SelectedState.SelectedNodes = new List<object> { f0, f1 };
            FlushUi();

            var propRelX = window.FindControl<NumericUpDown>("PropRelX")!;
            var propRelY = window.FindControl<NumericUpDown>("PropRelY")!;
            propRelX.Value = 10m;
            propRelY.Value = 20m;
            FlushUi();

            Assert.Equal(10f, f0.RelativeX);
            Assert.Equal(20f, f0.RelativeY);
            Assert.Equal(10f, f1.RelativeX);
            Assert.Equal(20f, f1.RelativeY);
        }
        finally { window.Close(); }
    }
}
