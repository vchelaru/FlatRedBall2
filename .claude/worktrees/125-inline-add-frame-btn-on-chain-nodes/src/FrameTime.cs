using System;

namespace FlatRedBall2;

/// <summary>
/// Per-frame time bundle passed to <see cref="Entity.CustomActivity"/> and other update hooks.
/// <see cref="Delta"/> respects <see cref="TimeManager.TimeScale"/>; <see cref="UnscaledDelta"/>
/// is the wall-clock delta and ignores <c>TimeScale</c>. Both keep advancing while the screen
/// is paused — pause is enforced by <see cref="Screen"/>, not by zeroing the delta.
/// <see cref="SinceScreenStart"/> is paused when the screen is paused; <see cref="SinceGameStart"/>
/// keeps advancing.
/// </summary>
/// <param name="Delta">Time elapsed since the previous frame, multiplied by <see cref="TimeManager.TimeScale"/>.</param>
/// <param name="UnscaledDelta">Wall-clock time elapsed since the previous frame. Not multiplied by <see cref="TimeManager.TimeScale"/>.</param>
/// <param name="SinceScreenStart">Time accumulated since the current screen activated; pauses with the screen.</param>
/// <param name="SinceGameStart">Time accumulated since <see cref="FlatRedBallService.Initialize"/>.</param>
public readonly record struct FrameTime(TimeSpan Delta, TimeSpan UnscaledDelta, TimeSpan SinceScreenStart, TimeSpan SinceGameStart)
{
    /// <summary>
    /// <see cref="Delta"/> as a <see cref="float"/> in seconds — the standard <c>dt</c> for physics
    /// integration (<c>Position += Velocity * dt</c>).
    /// <para>
    /// <b>This is the deliberate exception to the engine's TimeSpan convention.</b> All other public
    /// time-state and duration parameters use <see cref="TimeSpan"/>; the per-frame delta stays
    /// <see cref="float"/> because forcing <c>Velocity * (float)Delta.TotalSeconds</c> at every
    /// math site in every entity's activity loop would be hostile to the most common usage. Use
    /// <see cref="DeltaSeconds"/> in math; use <see cref="Delta"/> when you need to add to or
    /// compare against a <see cref="TimeSpan"/>-typed field.
    /// </para>
    /// </summary>
    public float DeltaSeconds => (float)Delta.TotalSeconds;

    /// <summary>
    /// <see cref="UnscaledDelta"/> as a <see cref="float"/> in seconds. Use for per-frame math that
    /// must not respond to <see cref="TimeManager.TimeScale"/> (UI tween animation, cursor movement,
    /// debug overlays).
    /// </summary>
    public float UnscaledDeltaSeconds => (float)UnscaledDelta.TotalSeconds;
}
