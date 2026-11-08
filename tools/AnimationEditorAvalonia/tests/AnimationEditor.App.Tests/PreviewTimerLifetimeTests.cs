using AnimationEditor.Core.CommandsAndState;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Shouldly;

namespace AnimationEditor.App.Tests;

/// <summary>
/// The preview's playback timer is rooted by the dispatcher, so it runs only while the control is in a window:
/// a closed window must not keep ticking, and a control put back resumes (see <c>WindowLeakTests</c>).
/// </summary>
public class PreviewTimerLifetimeTests
{
    [AvaloniaFact]
    public async Task PlaybackTimer_StopsWhenTheControlLeavesTheWindow_AndResumesWhenItReturns()
    {
        var ctx = TestHelpers.BuildServices();
        var preview = ctx.CreatePreviewControl();
        int ticks = 0;
        preview.SetPlaybackClock(new TickClock(() => ++ticks, 1000));
        var window = new Window { Content = preview };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        await Task.Delay(150);
        ticks.ShouldBeGreaterThan(0, "the timer ticks while the control is in a window");

        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        int atRemoval = ticks;
        await Task.Delay(150);
        ticks.ShouldBe(atRemoval, "no ticks after the control leaves the window");

        window.Content = preview;
        Dispatcher.UIThread.RunJobs();
        await Task.Delay(150);
        ticks.ShouldBeGreaterThan(atRemoval, "ticks resume when the control returns");

        window.Close();
    }
}
