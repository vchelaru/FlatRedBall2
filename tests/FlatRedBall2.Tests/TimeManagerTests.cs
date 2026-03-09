using System;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Shouldly;
using Xunit;

namespace FlatRedBall2.Tests;

public class TimeManagerTests
{
    private static GameTime MakeGameTime(double seconds)
        => new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(seconds));

    /// <summary>Advances the TimeManager by one frame of <paramref name="seconds"/> real time.</summary>
    private static void Tick(TimeManager time, double seconds, bool paused = false)
    {
        time.Update(MakeGameTime(seconds), paused);
        time.DoTaskLogic();
    }

    // -------------------------------------------------------------------------
    // DelaySeconds — basic completion
    // -------------------------------------------------------------------------

    [Fact]
    public void DelaySeconds_CompletesAfterEnoughScreenTime()
    {
        var time = new TimeManager();
        var task = time.DelaySeconds(1.0);

        Tick(time, 0.5);
        task.IsCompleted.ShouldBeFalse();

        Tick(time, 0.5); // screen time now 1.0
        task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void DelaySeconds_DoesNotCompleteWhilePaused()
    {
        var time = new TimeManager();
        var task = time.DelaySeconds(1.0);

        Tick(time, 0.5);               // 0.5 s — not done
        Tick(time, 0.5, paused: true); // paused: screen time frozen
        Tick(time, 0.5, paused: true); // paused: still frozen

        task.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public void DelaySeconds_CompletesAfterUnpausing()
    {
        var time = new TimeManager();
        var task = time.DelaySeconds(1.0);

        Tick(time, 0.5);               // 0.5 s unpaused
        Tick(time, 1.0, paused: true); // paused — screen time stays at 0.5
        Tick(time, 0.5);               // 1.0 s unpaused — task should fire

        task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void DelaySeconds_ZeroOrNegative_CompletesImmediately()
    {
        var time = new TimeManager();

        time.DelaySeconds(0).IsCompleted.ShouldBeTrue();
        time.DelaySeconds(-1).IsCompleted.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // DelayFrames — not pause-aware
    // -------------------------------------------------------------------------

    [Fact]
    public void DelayFrames_CompletesWhilePaused()
    {
        var time = new TimeManager();
        var task = time.DelayFrames(2);

        Tick(time, 0.016, paused: true);
        Tick(time, 0.016, paused: true);

        task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void DelayFrames_CountsFramesRegardlessOfPause()
    {
        var time = new TimeManager();
        var task = time.DelayFrames(3);

        Tick(time, 0.016, paused: true);
        task.IsCompleted.ShouldBeFalse();

        Tick(time, 0.016, paused: true);
        task.IsCompleted.ShouldBeFalse();

        Tick(time, 0.016, paused: true);
        task.IsCompleted.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // TimeScale
    // -------------------------------------------------------------------------

    [Fact]
    public void TimeScale_ScalesScreenTime()
    {
        var time = new TimeManager();
        time.TimeScale = 2f;

        Tick(time, 0.5); // 0.5 real × 2 = 1.0 screen time

        time.CurrentScreenTimeSeconds.ShouldBe(1.0, tolerance: 0.0001);
    }

    [Fact]
    public void DelaySeconds_RespectsTimeScale()
    {
        var time = new TimeManager();
        time.TimeScale = 2f;
        var task = time.DelaySeconds(1.0);

        Tick(time, 0.5); // 1.0 screen time at 2× — task should fire

        task.IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public void TimeScale_DoesNotAffectPauseScreenTimeFreezing()
    {
        var time = new TimeManager();
        time.TimeScale = 2f;
        var task = time.DelaySeconds(1.0);

        Tick(time, 0.5, paused: true); // paused — screen time stays 0

        task.IsCompleted.ShouldBeFalse();
        time.CurrentScreenTimeSeconds.ShouldBe(0.0, tolerance: 0.0001);
    }

    // -------------------------------------------------------------------------
    // ResetScreen
    // -------------------------------------------------------------------------

    [Fact]
    public void ResetScreen_ResetsCurrentScreenTimeSeconds()
    {
        var time = new TimeManager();
        Tick(time, 1.0);

        time.ResetScreen();

        time.CurrentScreenTimeSeconds.ShouldBe(0.0, tolerance: 0.0001);
    }

    [Fact]
    public void ResetScreen_DoesNotAffectCurrentFrame()
    {
        var time = new TimeManager();
        Tick(time, 0.016);
        Tick(time, 0.016);

        time.ResetScreen();

        time.CurrentFrame.ShouldBe(2);
    }

    // -------------------------------------------------------------------------
    // Screen transition — ClearTasks
    // -------------------------------------------------------------------------

    [Fact]
    public void ClearTasks_CancelsTimedTask()
    {
        var time = new TimeManager();
        var task = time.DelaySeconds(5.0);

        time.ClearTasks();

        task.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public void ClearTasks_CancelsPredicateTask()
    {
        var time = new TimeManager();
        var task = time.DelayUntil(() => false);

        time.ClearTasks();

        task.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public void ClearTasks_CancelsFrameTask()
    {
        var time = new TimeManager();
        var task = time.DelayFrames(100);

        time.ClearTasks();

        task.IsCanceled.ShouldBeTrue();
    }

    // -------------------------------------------------------------------------
    // CancellationToken — Screen.Token path
    // -------------------------------------------------------------------------

    [Fact]
    public void DelaySeconds_WhenTokenCancelled_TaskIsCancelled()
    {
        var time = new TimeManager();
        var cts = new System.Threading.CancellationTokenSource();
        var task = time.DelaySeconds(5.0, cts.Token);

        cts.Cancel();
        Tick(time, 0.016); // DoTaskLogic checks cancellation

        task.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public void DelaySeconds_WhenTokenCancelled_DoesNotComplete()
    {
        var time = new TimeManager();
        var cts = new System.Threading.CancellationTokenSource();
        var task = time.DelaySeconds(0.5, cts.Token);

        cts.Cancel();
        Tick(time, 1.0); // enough time would have elapsed, but it should be cancelled not completed

        task.IsCompletedSuccessfully.ShouldBeFalse();
        task.IsCanceled.ShouldBeTrue();
    }

    [Fact]
    public void DelayUntil_WhenTokenCancelled_TaskIsCancelled()
    {
        var time = new TimeManager();
        var cts = new System.Threading.CancellationTokenSource();
        var task = time.DelayUntil(() => false, cts.Token);

        cts.Cancel();
        Tick(time, 0.016);

        task.IsCanceled.ShouldBeTrue();
    }
}
