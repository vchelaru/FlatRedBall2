using AnimationEditor.Views.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using FlatRedBall2.Animation;
using FlatRedBall2.AnimationEditorCommon;
using System;
using System.Linq;
using System.Reflection;
using Xunit;

namespace AnimationEditor.App.Tests;

/// <summary>The event flag must not change a frame cell's layout: the thumbnail sits at the same Y with or without it.</summary>
public class TimelineEventFlagLayoutTests
{
    private static AnimationFrameSave Frame() => new()
    {
        TextureName = "a.png",
        FrameLength = 0.1f,
        LeftCoordinate = 0f, TopCoordinate = 0f,
        RightCoordinate = 1f, BottomCoordinate = 1f,
        ShapesSave = new ShapesSave(),
    };

    [AvaloniaFact]
    public void FlaggedCell_ThumbnailIsAtTheSameYAsAnUnflaggedCell()
    {
        var ctx = TestHelpers.BuildServices();
        var chain = new AnimationChainSave { Name = "Run" };
        chain.Frames.Add(Frame());
        chain.Frames.Add(Frame());
        chain.Frames[0].Events.Add(new AnimationFrameEvent { Name = "Step" });
        var acls = new AnimationChainListSave();
        acls.AnimationChains.Add(chain);

        var window = ctx.CreateMainWindow();
        window.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            ctx.ProjectManager.AnimationChainListSave = acls;
            typeof(MainWindow).GetMethod("RebuildTreeView", BindingFlags.NonPublic | BindingFlags.Instance)!
                .Invoke(window, new object[] { Array.Empty<string>() });
            Dispatcher.UIThread.RunJobs();
            ctx.SelectedState.SelectedChain = chain;
            Dispatcher.UIThread.RunJobs();

            var strip = window.FindControl<ItemsControl>("TimelineStrip")!;
            double ThumbnailY(int index)
            {
                var cell = strip.ContainerFromIndex(index)!;
                var image = cell.GetVisualDescendants().OfType<Image>().First();
                return image.TranslatePoint(new Point(0, 0), strip)!.Value.Y;
            }

            Assert.Equal(1, strip.ContainerFromIndex(0)!.GetVisualDescendants().OfType<EventFlagGlyph>().Count(g => g.IsVisible));
            Assert.Equal(0, strip.ContainerFromIndex(1)!.GetVisualDescendants().OfType<EventFlagGlyph>().Count(g => g.IsVisible));
            Assert.Equal(ThumbnailY(1), ThumbnailY(0));
        }
        finally { window.Close(); }
    }
}
