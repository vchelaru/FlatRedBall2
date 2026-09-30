using System.Linq;
using AnimationEditor.Views.Controls;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using FlatRedBall2.AnimationEditorCommon;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>
/// #1274: number fields never pad trailing zeros (0.1, not 0.100). Every format's fractional
/// part must be optional digits ("0.###"), not required ones ("0.000", "0.0#").
/// </summary>
public class NumberFieldFormatTests
{
    private static bool Pads(string format)
    {
        int dot = format.IndexOf('.');
        return dot >= 0 && format[(dot + 1)..].Contains('0');
    }

    [AvaloniaFact]
    public void MainWindowNumberFields_FormatStrings_DoNotPadTrailingZeros()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            var descendants = window.GetLogicalDescendants().ToList();
            var formats =
                descendants.OfType<FlankerNumericField>().Select(f => (f.Name, f.FormatString))
                .Concat(descendants.OfType<NumericUpDown>().Select(n => (n.Name, n.FormatString)))
                .ToList();

            Assert.NotEmpty(formats);
            var padding = formats.Where(f => Pads(f.FormatString ?? "")).Select(f => $"{f.Name}: {f.FormatString}");
            Assert.Empty(padding);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void PropFrameLen_FrameLengthPointOne_ShowsPointOne()
    {
        var ctx = TestHelpers.BuildServices();
        var window = ctx.CreateMainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        try
        {
            ctx.ProjectManager.AnimationChainListSave = new AnimationChainListSave();
            var chain = new AnimationChainSave { Name = "Walk" };
            var frame = new AnimationFrameSave { TextureName = "f0.png", ShapesSave = new ShapesSave(), FrameLength = 0.1f };
            chain.Frames.Add(frame);
            ctx.ProjectManager.AnimationChainListSave.AnimationChains.Add(chain);
            ctx.SelectedState.SelectedFrame = frame;
            Dispatcher.UIThread.RunJobs();

            var propFrameLen = window.FindControl<FlankerNumericField>("PropFrameLen")!;

            Assert.Equal("0.1", propFrameLen.ValueBox.Text);
        }
        finally { window.Close(); }
    }
}
