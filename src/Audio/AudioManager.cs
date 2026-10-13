using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;
using NVorbis;
using XnaTitleContainer = Microsoft.Xna.Framework.TitleContainer;

namespace FlatRedBall2.Audio;

/// <summary>
/// Manages sound effect playback and background music. Updated each frame by the engine.
/// </summary>
public class AudioManager
{
    private readonly List<(object Key, ISoundInstance Instance, SoundHandle Handle)> _activeSounds = new();
    private readonly HashSet<object> _playedThisFrame = new();

    private float _soundVolume = 1f;
    private float _musicVolume = 1f;
    private float _musicPitch = 0f;
    private bool _soundEnabled = true;
    private bool _musicEnabled = true;

    private IMusicBackend? _musicBackend;

    // ── Sound effects ──────────────────────────────────────────────────────────

    /// <summary>
    /// Plays a sound effect and returns a <see cref="SoundHandle"/> for stopping it or checking
    /// whether it is still playing. Pass <c>loop: true</c> for a sound that repeats until you call
    /// <see cref="SoundHandle.Stop"/> (stop it yourself, e.g. in <c>CustomDestroy</c>; it is not
    /// stopped on screen change).
    /// <para>
    /// By default, a second call for the same <paramref name="soundEffect"/> in the same frame is
    /// silently ignored (per-frame dedup) so overlapping collision handlers do not stack the sound.
    /// Pass <paramref name="playAlways"/> <c>true</c> to bypass that and stack instances (for
    /// example the same effect at different pitches). Cross-frame stacking is always allowed.
    /// </para>
    /// Nothing plays (the returned handle is inert) when <see cref="SoundEnabled"/> is <c>false</c>
    /// or <see cref="ConcurrentSounds"/> has reached <see cref="MaxConcurrentSounds"/>; both apply
    /// even with <paramref name="playAlways"/>.
    /// </summary>
    /// <param name="volume">Per-play volume in [0, 1], multiplied by <see cref="SoundVolume"/>.</param>
    /// <param name="pitch">[-1, 1]; +/-1 is one octave up/down.</param>
    /// <param name="pan">[-1, 1]; -1 is full left, +1 is full right.</param>
    /// <param name="loop">Repeat until <see cref="SoundHandle.Stop"/> is called.</param>
    /// <param name="playAlways">Skip the same-frame dedup for this call.</param>
    public SoundHandle Play(SoundEffect soundEffect, float volume = 1f, float pitch = 0f, float pan = 0f,
        bool loop = false, bool playAlways = false)
        => PlayInstance(soundEffect, () => new XnaSoundInstance(soundEffect.CreateInstance()),
            volume, pitch, pan, loop, playAlways);

    /// <summary>
    /// Plays <paramref name="soundEffect"/> and returns a task that completes when it has finished
    /// playing, so game code can sequence on it (<c>await Engine.Audio.PlayAsync(sfx)</c>).
    /// <para>
    /// The wait is game time (<see cref="TimeManager.Delay"/>): it follows <see cref="TimeManager.TimeScale"/>,
    /// freezes while the screen is paused, and is shortened or lengthened by <paramref name="pitch"/>.
    /// It always waits the full duration, even when nothing audible played (sound disabled, deduped,
    /// capped), so sequencing does not change with the mute setting. Not for looping sounds; use
    /// <see cref="Play"/> with <c>loop: true</c>.
    /// </para>
    /// </summary>
    /// <param name="cancellationToken">
    /// Pass <see cref="Screen.Token"/> to cancel when the screen switches. Cancelling does not stop
    /// the sound itself.
    /// </param>
    /// <exception cref="InvalidOperationException">Thrown when this manager is not owned by an engine.</exception>
    public Task PlayAsync(SoundEffect soundEffect, float volume = 1f, float pitch = 0f, float pan = 0f,
        bool playAlways = false, CancellationToken cancellationToken = default)
        => PlayAsyncInstance(soundEffect, soundEffect.Duration,
            () => new XnaSoundInstance(soundEffect.CreateInstance()),
            volume, pitch, pan, playAlways, cancellationToken);

    internal SoundHandle PlayInstance(object key, Func<ISoundInstance> create,
        float volume, float pitch, float pan, bool loop, bool playAlways)
    {
        if (!_soundEnabled) return new SoundHandle(null);
        if (!playAlways && _playedThisFrame.Contains(key)) return new SoundHandle(null);
        if (_activeSounds.Count >= MaxConcurrentSounds) return new SoundHandle(null);

        var instance = create();
        instance.Volume = System.Math.Clamp(volume * _soundVolume, 0f, 1f);
        instance.Pitch = System.Math.Clamp(pitch, -1f, 1f);
        instance.Pan = System.Math.Clamp(pan, -1f, 1f);
        instance.IsLooped = loop;
        instance.Play();

        var handle = new SoundHandle(instance);
        _activeSounds.Add((key, instance, handle));
        if (!playAlways)
            _playedThisFrame.Add(key);
        return handle;
    }

    internal Task PlayAsyncInstance(object key, TimeSpan duration, Func<ISoundInstance> create,
        float volume, float pitch, float pan, bool playAlways, CancellationToken cancellationToken)
    {
        if (Time == null)
            throw new InvalidOperationException("PlayAsync requires an AudioManager owned by a FlatRedBallService.");

        PlayInstance(key, create, volume, pitch, pan, loop: false, playAlways);

        // XNA pitch: +/-1 = one octave = 2x/0.5x playback rate.
        var rate = System.Math.Pow(2, System.Math.Clamp(pitch, -1f, 1f));
        return Time.Delay(TimeSpan.FromTicks((long)(duration.Ticks / rate)), cancellationToken);
    }

    /// <summary>
    /// Returns <c>true</c> if any instance of <paramref name="soundEffect"/> started through
    /// <see cref="Play"/> is currently playing. Use the <see cref="SoundHandle"/> returned by
    /// <see cref="Play"/> to ask about one specific playback.
    /// </summary>
    public bool IsPlaying(SoundEffect soundEffect) => IsPlayingKey(soundEffect);

    internal bool IsPlayingKey(object key)
    {
        foreach (var (k, instance, _) in _activeSounds)
        {
            if (k == key && instance.State == SoundState.Playing)
                return true;
        }
        return false;
    }

    // ── Music ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// The song that is currently queued (may be paused or stopped). Always <c>null</c> while a
    /// <see cref="PlayPitchableSong"/> track is active, since that path bypasses <see cref="Song"/>.
    /// </summary>
    public Song? CurrentSong => (_musicBackend as MediaPlayerMusicBackend)?.CurrentSong;

    /// <summary>
    /// Plays <paramref name="song"/> as the background track.
    /// Replaces any active song, playlist, or <see cref="PlayPitchableSong"/> track.
    /// Has no effect on volume when <see cref="MusicEnabled"/> is <c>false</c> — the song will
    /// begin playing but will immediately be paused by the <see cref="MusicEnabled"/> setter logic;
    /// use <see cref="MusicEnabled"/> to re-enable.
    /// </summary>
    public void PlaySong(Song song, bool loop = true)
    {
        SetMusicBackend(new MediaPlayerMusicBackend(song, loop));
    }

    /// <summary>
    /// Plays <paramref name="songs"/> in sequence, looping the playlist from the beginning after the last track.
    /// Replaces any active song or <see cref="PlayPitchableSong"/> track.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="songs"/> is empty.</exception>
    public void PlayPlaylist(params Song[] songs)
    {
        if (songs.Length == 0)
            throw new ArgumentException("Playlist must contain at least one song.", nameof(songs));

        SetMusicBackend(new MediaPlayerMusicBackend(songs));
    }

    /// <summary>
    /// Plays a raw OGG file directly (bypassing <see cref="Song"/>) through a pitchable streaming
    /// backend, so <see cref="MusicPitch"/> has an audible effect. Replaces any active song,
    /// playlist, or previous pitchable track. OGG-only, matching <see cref="Song.FromUri"/>'s
    /// OGG-only convention.
    /// </summary>
    public void PlayPitchableSong(string oggFilePath, bool loop = true)
    {
        var stream = XnaTitleContainer.OpenStream(oggFilePath);
        var reader = new VorbisReader(stream, true);
        SetMusicBackend(new StreamingOggMusicBackend(reader, loop));
    }

    /// <summary>Pauses the current song at its current position. No-op if nothing is playing.</summary>
    public void PauseSong() => _musicBackend?.Pause();

    /// <summary>
    /// Resumes the current song from its paused position.
    /// Has no effect if <see cref="MusicEnabled"/> is <c>false</c> or no song is loaded.
    /// </summary>
    public void ResumeSong()
    {
        if (_musicEnabled)
            _musicBackend?.Resume();
    }

    /// <summary>Stops the current song, playlist, or pitchable track and releases its resources.</summary>
    public void StopSong()
    {
        _musicBackend?.Stop();
        _musicBackend = null;
    }

    // ── Volume / enable ────────────────────────────────────────────────────────

    /// <summary>Master volume for sound effects in the range [0, 1]. Default is 1.</summary>
    public float SoundVolume
    {
        get => _soundVolume;
        set => _soundVolume = System.Math.Clamp(value, 0f, 1f);
    }

    /// <summary>
    /// Master volume for music in the range [0, 1]. Default is 1.
    /// Takes effect immediately on whichever music backend is currently active.
    /// </summary>
    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = System.Math.Clamp(value, 0f, 1f);
            if (_musicBackend != null)
                _musicBackend.Volume = _musicVolume;
        }
    }

    /// <summary>
    /// Playback pitch for the currently active <see cref="PlayPitchableSong"/> track, in the range
    /// [-1, 1] (XNA semantics: +/-1 = one octave up/down = 2x/0.5x rate). Takes effect immediately.
    /// Setting this while a <see cref="PlaySong"/>/<see cref="PlayPlaylist"/> (MediaPlayer-backed)
    /// track is active is a silent no-op — <see cref="Song"/>/<see cref="MediaPlayer"/> expose no
    /// pitch control on any backend.
    /// </summary>
    public float MusicPitch
    {
        get => _musicPitch;
        set
        {
            _musicPitch = System.Math.Clamp(value, -1f, 1f);
            if (_musicBackend != null)
                _musicBackend.Pitch = _musicPitch;
        }
    }

    /// <summary>
    /// When <c>false</c>, calls to <see cref="Play"/> are silently ignored.
    /// Existing playing instances finish naturally; they are not stopped retroactively.
    /// </summary>
    public bool SoundEnabled
    {
        get => _soundEnabled;
        set => _soundEnabled = value;
    }

    /// <summary>
    /// When set to <c>false</c>, pauses the current song immediately.
    /// When set back to <c>true</c>, resumes the current song (if one is loaded).
    /// </summary>
    public bool MusicEnabled
    {
        get => _musicEnabled;
        set
        {
            _musicEnabled = value;
            if (!value)
                _musicBackend?.Pause();
            else
                _musicBackend?.Resume();
        }
    }

    /// <summary>Set by <see cref="FlatRedBallService"/>; clock used by <see cref="PlayAsync"/>.</summary>
    internal TimeManager? Time { get; set; }

    // ── Limits / diagnostics ───────────────────────────────────────────────────

    /// <summary>Maximum number of concurrently tracked sound effect instances. Default is 32.</summary>
    public int MaxConcurrentSounds { get; set; } = 32;

    /// <summary>Number of currently tracked active sound effect instances.</summary>
    public int ConcurrentSounds => _activeSounds.Count;

    // ── Testing seam ───────────────────────────────────────────────────────────

    /// <summary>
    /// Test-only seam for injecting a fake <see cref="IMusicBackend"/> so routing (volume, pitch,
    /// pause/resume/stop) can be asserted without a real audio device.
    /// </summary>
    internal IMusicBackend? MusicBackendForTesting
    {
        get => _musicBackend;
        set => _musicBackend = value;
    }

    // ── Internal update ────────────────────────────────────────────────────────

    /// <summary>
    /// Called once per frame by <see cref="FlatRedBallService"/>. Clears the per-frame dedup set
    /// and disposes any sound effect instances that have finished playing.
    /// </summary>
    internal void Update()
    {
        _playedThisFrame.Clear();

        for (int i = _activeSounds.Count - 1; i >= 0; i--)
        {
            var (_, instance, handle) = _activeSounds[i];
            if (instance.State == SoundState.Stopped)
            {
                handle.Release();
                instance.Dispose();
                _activeSounds.RemoveAt(i);
            }
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void SetMusicBackend(IMusicBackend backend)
    {
        _musicBackend?.Stop();
        _musicBackend = backend;
        _musicBackend.Volume = _musicVolume;
        _musicBackend.Pitch = _musicPitch;
        _musicBackend.Play();

        if (!_musicEnabled)
            _musicBackend.Pause();
    }
}
