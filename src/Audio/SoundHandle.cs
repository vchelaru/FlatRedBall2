using Microsoft.Xna.Framework.Audio;

namespace FlatRedBall2.Audio;

/// <summary>
/// Handle to one sound-effect playback started by <see cref="AudioManager.Play"/>. Use it to stop
/// the sound early or to ask whether it is still playing. Safe to keep and query after the sound
/// has finished: <see cref="IsPlaying"/> is then <c>false</c> and <see cref="Stop"/> does nothing.
/// <para>
/// A call that did not start a sound (sound disabled, per-frame dedup, concurrency cap) still
/// returns a handle; it is permanently not playing.
/// </para>
/// </summary>
public sealed class SoundHandle
{
    private ISoundInstance? _instance;

    internal SoundHandle(ISoundInstance? instance) => _instance = instance;

    /// <summary><c>true</c> while this playback is actively playing. <c>false</c> once finished, stopped, or if it never started.</summary>
    public bool IsPlaying => _instance?.State == SoundState.Playing;

    /// <summary>Stops this playback immediately. This is the only way to end a <c>loop: true</c> sound. No-op if it already ended.</summary>
    public void Stop() => _instance?.Stop();

    /// <summary>Called by <see cref="AudioManager"/> when it disposes the underlying instance.</summary>
    internal void Release() => _instance = null;
}
