using System;

namespace FlatRedBall2.Audio;

public interface IAudioBackend
{
    void PlaySong(string name, bool loop);
    void StopSong();
    void PlaySoundEffect(string name, float volume);
}

public class AudioManager
{
    // TODO: Implement audio system with a real IAudioBackend (MonoGame audio, NAudio, etc.)
    public IAudioBackend? Backend { get; set; }

    public void PlaySong(string name, bool loop = true)
        => throw new NotImplementedException("Audio system not yet implemented. See design/TODOS.md");

    public void StopSong()
        => throw new NotImplementedException("Audio system not yet implemented. See design/TODOS.md");

    public void PlaySoundEffect(string name, float volume = 1f)
        => throw new NotImplementedException("Audio system not yet implemented. See design/TODOS.md");
}
