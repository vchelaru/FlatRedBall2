using FlatRedBall2.Math;
using FlatRedBall2.Rendering;

namespace FlatRedBall2;

/// <summary>
/// Convenience helpers on <see cref="HotReloadState"/> that preserve and restore the full
/// kinematic bundle (position, velocity, acceleration, rotation, rotation velocity) of an
/// <see cref="Entity"/> or <see cref="Camera"/> in one call. Intended to collapse the common
/// boilerplate of writing six to eight <c>Set</c>/<c>TryGet</c> calls by hand in
/// <see cref="Screen.SaveHotReloadState"/> / <see cref="Screen.RestoreHotReloadState"/>.
/// </summary>
public static class HotReloadStateExtensions
{
    /// <summary>
    /// Preserves an <see cref="Entity"/>'s position, velocity, acceleration, rotation, and
    /// rotation velocity so they survive a <see cref="RestartMode.HotReload"/> restart. Mirror
    /// this call in <see cref="Screen.RestoreHotReloadState"/> with <see cref="Restore(HotReloadState, Entity, string?)"/>.
    /// <para>
    /// When <paramref name="name"/> is omitted, a key is auto-generated as
    /// <c>"{typeof(entity).Name}_{n}"</c> with an internal per-type counter. Two unnamed
    /// <c>Preserve</c> calls on the same entity type get <c>"Player_1"</c> and <c>"Player_2"</c>.
    /// Restore must be called in the same order for the same types, or state will silently go
    /// to the wrong entity. Pass an explicit <paramref name="name"/> when you want control.
    /// </para>
    /// </summary>
    public static void Preserve(this HotReloadState state, Entity entity, string? name = null)
    {
        var k = name ?? state.NextSaveKey(entity.GetType());
        state.Set($"{k}.x",   entity.X);
        state.Set($"{k}.y",   entity.Y);
        state.Set($"{k}.vx",  entity.VelocityX);
        state.Set($"{k}.vy",  entity.VelocityY);
        state.Set($"{k}.ax",  entity.AccelerationX);
        state.Set($"{k}.ay",  entity.AccelerationY);
        state.Set($"{k}.rot", entity.Rotation);
        state.Set($"{k}.rv",  entity.RotationVelocity);
    }

    /// <summary>
    /// Restores an <see cref="Entity"/>'s kinematic state previously captured via
    /// <see cref="Preserve(HotReloadState, Entity, string?)"/>. Any property whose matching
    /// key isn't in the state dict is left untouched, so on the first screen load (no prior
    /// save) this is a no-op and the entity keeps its <c>CustomInitialize</c>-assigned values.
    /// <para>
    /// When <paramref name="name"/> is omitted, the key is pulled from an internal per-type
    /// restore counter that mirrors the save counter's order. Call order must match
    /// <see cref="Screen.SaveHotReloadState"/>.
    /// </para>
    /// </summary>
    public static void Restore(this HotReloadState state, Entity entity, string? name = null)
    {
        var k = name ?? state.NextRestoreKey(entity.GetType());
        if (state.TryGet<float>($"{k}.x",   out var x))   entity.X = x;
        if (state.TryGet<float>($"{k}.y",   out var y))   entity.Y = y;
        if (state.TryGet<float>($"{k}.vx",  out var vx))  entity.VelocityX = vx;
        if (state.TryGet<float>($"{k}.vy",  out var vy))  entity.VelocityY = vy;
        if (state.TryGet<float>($"{k}.ax",  out var ax))  entity.AccelerationX = ax;
        if (state.TryGet<float>($"{k}.ay",  out var ay))  entity.AccelerationY = ay;
        if (state.TryGet<Angle>($"{k}.rot", out var rot)) entity.Rotation = rot;
        if (state.TryGet<Angle>($"{k}.rv",  out var rv))  entity.RotationVelocity = rv;
    }

    /// <summary>
    /// Preserves a <see cref="Camera"/>'s position, velocity, and acceleration across a
    /// hot-reload restart. Camera has no rotation or drag, so only six floats are captured.
    /// <para>
    /// Typically you preserve the <c>CameraControllingEntity</c>'s target entity instead and
    /// let the camera snap to it on frame 1 — this overload is for games that drive the
    /// camera directly.
    /// </para>
    /// </summary>
    public static void Preserve(this HotReloadState state, Camera camera, string? name = null)
    {
        var k = name ?? state.NextSaveKey(camera.GetType());
        state.Set($"{k}.x",  camera.X);
        state.Set($"{k}.y",  camera.Y);
        state.Set($"{k}.vx", camera.VelocityX);
        state.Set($"{k}.vy", camera.VelocityY);
        state.Set($"{k}.ax", camera.AccelerationX);
        state.Set($"{k}.ay", camera.AccelerationY);
    }

    /// <summary>
    /// Restores a <see cref="Camera"/>'s kinematic state. Missing keys leave the matching
    /// property untouched (no-op on first load).
    /// </summary>
    public static void Restore(this HotReloadState state, Camera camera, string? name = null)
    {
        var k = name ?? state.NextRestoreKey(camera.GetType());
        if (state.TryGet<float>($"{k}.x",  out var x))  camera.X = x;
        if (state.TryGet<float>($"{k}.y",  out var y))  camera.Y = y;
        if (state.TryGet<float>($"{k}.vx", out var vx)) camera.VelocityX = vx;
        if (state.TryGet<float>($"{k}.vy", out var vy)) camera.VelocityY = vy;
        if (state.TryGet<float>($"{k}.ax", out var ax)) camera.AccelerationX = ax;
        if (state.TryGet<float>($"{k}.ay", out var ay)) camera.AccelerationY = ay;
    }
}
