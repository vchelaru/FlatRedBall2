using System;
using Microsoft.Xna.Framework.Audio;

namespace FlatRedBall2.Audio;

/// <summary>
/// The slice of <see cref="SoundEffectInstance"/> that <see cref="AudioManager"/> uses. Exists so
/// sound-effect routing (dedup, cap, looping, handles) can be tested without an audio device.
/// </summary>
internal interface ISoundInstance : IDisposable
{
    SoundState State { get; }
    float Volume { get; set; }
    float Pitch { get; set; }
    float Pan { get; set; }
    bool IsLooped { get; set; }
    void Play();
    void Stop();
}

internal sealed class XnaSoundInstance : ISoundInstance
{
    private readonly SoundEffectInstance _instance;

    public XnaSoundInstance(SoundEffectInstance instance) => _instance = instance;

    public SoundState State => _instance.State;
    public float Volume { get => _instance.Volume; set => _instance.Volume = value; }
    public float Pitch { get => _instance.Pitch; set => _instance.Pitch = value; }
    public float Pan { get => _instance.Pan; set => _instance.Pan = value; }
    public bool IsLooped { get => _instance.IsLooped; set => _instance.IsLooped = value; }
    public void Play() => _instance.Play();
    public void Stop() => _instance.Stop();
    public void Dispose() => _instance.Dispose();
}
