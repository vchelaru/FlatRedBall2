using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;

namespace FlatRedBall2;

/// <summary>
/// Per-engine time service. Owns the running time clocks (since-game-start, since-screen-start),
/// the global <see cref="TimeScale"/>, and async delay primitives (<see cref="Delay"/>,
/// <see cref="DelayUntil"/>, <see cref="DelayFrames"/>) that complete on the game thread via
/// the engine's <see cref="GameSynchronizationContext"/>.
/// <para>
/// Access via <see cref="FlatRedBallService.TimeManager"/>. The engine calls <c>Update</c> and
/// <c>DoTaskLogic</c> internally each frame — game code never invokes them directly.
/// </para>
/// <para>
/// <b>Time-type convention:</b> all elapsed-time state and duration parameters in the public API
/// use <see cref="TimeSpan"/>, not <c>float</c> / <c>double</c> seconds. This applies engine-wide
/// — animation lengths, tween durations, cooldowns, double-click thresholds, all use
/// <see cref="TimeSpan"/>. The <b>only exception</b> is <see cref="FrameTime.DeltaSeconds"/>,
/// which stays <c>float</c> because it's used in physics integration (<c>Position += Velocity *
/// dt</c>) every frame on every entity — converting via <c>TimeSpan.TotalSeconds</c> at every
/// math site would be hostile to the most common consumer pattern.
/// </para>
/// </summary>
public class TimeManager
{
    private TimeSpan _sinceGameStart;
    private TimeSpan _sinceScreenStart;
    private TimeSpan _realSinceGameStart;

    /// <summary>Scaling factor applied to real elapsed time. Values &lt; 1 slow the game; &gt; 1 speed it up.</summary>
    public float TimeScale { get; set; } = 1f;

    /// <summary>
    /// The <see cref="FrameTime"/> bundle for the frame currently in progress. Updated once per
    /// frame at the top of <c>FlatRedBallService.Update</c>; passed automatically to
    /// <see cref="Entity.CustomActivity"/> — there's rarely a reason to read this directly.
    /// </summary>
    public FrameTime CurrentFrameTime { get; private set; }

    /// <summary>Elapsed game time since the current screen was activated. Respects <see cref="TimeScale"/> and freezes while the screen is paused.</summary>
    public TimeSpan CurrentScreenTime => _sinceScreenStart;

    /// <summary>
    /// Elapsed wall-clock time since <see cref="FlatRedBallService.Initialize"/> was called.
    /// Unaffected by <see cref="TimeScale"/> and unaffected by screen pause — strictly monotonic.
    /// Use this for input gestures (double-click thresholds, hold timers) and any other timing that
    /// should not freeze when the game pauses or slow down when the game runs in slow motion.
    /// </summary>
    public TimeSpan RealTimeSinceStart => _realSinceGameStart;

    /// <summary>Total number of frames that have elapsed since <see cref="FlatRedBallService.Initialize"/> was called.</summary>
    public long CurrentFrame { get; private set; }

    // -------------------------------------------------------------------------
    // Delay task lists — kept sorted by completion time / frame so DoTaskLogic
    // can early-exit as soon as the first unready entry is found.
    // -------------------------------------------------------------------------

    private readonly List<TimedTask> _timedTasks = new();
    private readonly List<PredicateTask> _predicateTasks = new();
    private readonly List<FrameTask> _frameTasks = new();

    private readonly record struct TimedTask(TimeSpan CompletionTime, TaskCompletionSource Tcs, CancellationToken Token);
    private readonly record struct PredicateTask(Func<bool> Predicate, TaskCompletionSource Tcs, CancellationToken Token);
    private readonly record struct FrameTask(long TargetFrame, TaskCompletionSource Tcs);

    // -------------------------------------------------------------------------
    // Async delay APIs
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns a task that completes after <paramref name="duration"/> of screen time has elapsed.
    /// Screen time respects <see cref="TimeScale"/>, pauses when the screen is paused, and resets when the screen changes.
    /// </summary>
    /// <param name="duration">How long to wait. Values ≤ <see cref="TimeSpan.Zero"/> complete immediately.</param>
    /// <param name="cancellationToken">
    /// Pass <see cref="Screen.Token"/> to have the task automatically cancel when the screen switches.
    /// </param>
    /// <summary>
    /// Convenience wrapper around <see cref="Delay(TimeSpan, CancellationToken)"/> that takes a
    /// raw seconds value, so call sites don't need <c>TimeSpan.FromSeconds(...)</c> for the common
    /// case of a literal duration.
    /// </summary>
    /// <param name="seconds">Seconds to wait. Values ≤ 0 complete immediately.</param>
    /// <param name="cancellationToken">
    /// Pass <see cref="Screen.Token"/> to have the task automatically cancel when the screen switches.
    /// </param>
    public Task DelaySeconds(double seconds, CancellationToken cancellationToken = default)
        => Delay(TimeSpan.FromSeconds(seconds), cancellationToken);

    /// <summary>
    /// Returns a task that completes after the specified game-time duration.
    /// </summary>
    /// <param name="duration">Duration to delay.</param>
    /// <param name="cancellationToken">Token to cancel the wait early.</param>
    public Task Delay(TimeSpan duration, CancellationToken cancellationToken = default)
    {
        if (duration <= TimeSpan.Zero)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var completionTime = CurrentScreenTime + duration;

        // Insert sorted by completion time for efficient early-exit in DoTaskLogic.
        var index = _timedTasks.Count;
        for (int i = 0; i < _timedTasks.Count; i++)
        {
            if (_timedTasks[i].CompletionTime > completionTime)
            {
                index = i;
                break;
            }
        }
        _timedTasks.Insert(index, new TimedTask(completionTime, tcs, cancellationToken));
        return tcs.Task;
    }

    /// <summary>
    /// Returns a task that completes once <paramref name="predicate"/> returns <c>true</c>.
    /// Checked once per frame.
    /// </summary>
    /// <param name="cancellationToken">
    /// Pass <see cref="Screen.Token"/> to have the task automatically cancel when the screen switches.
    /// </param>
    public Task DelayUntil(Func<bool> predicate, CancellationToken cancellationToken = default)
    {
        if (predicate())
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _predicateTasks.Add(new PredicateTask(predicate, tcs, cancellationToken));
        return tcs.Task;
    }

    /// <summary>
    /// Returns a task that completes after <paramref name="frameCount"/> frames have elapsed.
    /// </summary>
    /// <param name="frameCount">Frames to wait. Values ≤ 0 complete immediately.</param>
    public Task DelayFrames(int frameCount)
    {
        if (frameCount <= 0)
            return Task.CompletedTask;

        var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var targetFrame = CurrentFrame + frameCount;

        var index = _frameTasks.Count;
        for (int i = 0; i < _frameTasks.Count; i++)
        {
            if (_frameTasks[i].TargetFrame > targetFrame)
            {
                index = i;
                break;
            }
        }
        _frameTasks.Insert(index, new FrameTask(targetFrame, tcs));
        return tcs.Task;
    }

    // -------------------------------------------------------------------------
    // Internal lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Zeroes <see cref="CurrentScreenTime"/>. Called by the engine on screen transition;
    /// rarely useful from game code (use <see cref="Screen.RestartScreen(RestartMode)"/> for a
    /// proper restart with full lifecycle hooks).
    /// </summary>
    public void ResetScreen() => _sinceScreenStart = TimeSpan.Zero;

    internal void Update(GameTime gameTime, bool screenIsPaused)
    {
        var realDelta = gameTime.ElapsedGameTime;
        var scaledDelta = TimeSpan.FromSeconds(realDelta.TotalSeconds * TimeScale);
        _realSinceGameStart += realDelta;
        _sinceGameStart += scaledDelta;
        if (!screenIsPaused)
            _sinceScreenStart += scaledDelta;
        CurrentFrame++;
        CurrentFrameTime = new FrameTime(scaledDelta, _sinceScreenStart, _sinceGameStart);
    }

    /// <summary>
    /// Completes any delay tasks whose conditions are now met. Called once per frame by
    /// <see cref="FlatRedBallService"/>, immediately before flushing <see cref="GameSynchronizationContext"/>.
    /// </summary>
    internal void DoTaskLogic()
    {
        // Timed tasks — sorted, so stop at the first one that's not ready.
        while (_timedTasks.Count > 0)
        {
            var task = _timedTasks[0];
            if (task.Token.IsCancellationRequested)
            {
                _timedTasks.RemoveAt(0);
                task.Tcs.TrySetCanceled(task.Token);
            }
            else if (task.CompletionTime <= CurrentScreenTime)
            {
                _timedTasks.RemoveAt(0);
                task.Tcs.TrySetResult();
            }
            else
            {
                break;
            }
        }

        // Predicate tasks — check all (no ordering guarantee).
        for (int i = _predicateTasks.Count - 1; i >= 0; i--)
        {
            var task = _predicateTasks[i];
            if (task.Token.IsCancellationRequested)
            {
                _predicateTasks.RemoveAt(i);
                task.Tcs.TrySetCanceled(task.Token);
            }
            else if (task.Predicate())
            {
                _predicateTasks.RemoveAt(i);
                task.Tcs.TrySetResult();
            }
        }

        // Frame tasks — sorted, so stop at the first one that's not ready.
        while (_frameTasks.Count > 0)
        {
            var task = _frameTasks[0];
            if (task.TargetFrame <= CurrentFrame)
            {
                _frameTasks.RemoveAt(0);
                task.Tcs.TrySetResult();
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>
    /// Cancels and discards all pending delay tasks. Called on screen transition.
    /// </summary>
    internal void ClearTasks()
    {
        foreach (var task in _timedTasks)
            task.Tcs.TrySetCanceled();
        _timedTasks.Clear();

        foreach (var task in _predicateTasks)
            task.Tcs.TrySetCanceled();
        _predicateTasks.Clear();

        foreach (var task in _frameTasks)
            task.Tcs.TrySetCanceled();
        _frameTasks.Clear();
    }
}
