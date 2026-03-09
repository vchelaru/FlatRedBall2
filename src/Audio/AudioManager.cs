using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Media;

namespace FlatRedBall2.Audio;

/// <summary>
/// Manages sound effect playback and background music. Updated each frame by the engine.
/// </summary>
public class AudioManager
{
    private readonly List<(SoundEffect Effect, SoundEffectInstance Instance)> _activeSounds = new();
    private readonly HashSet<SoundEffect> _playedThisFrame = new();

    private float _soundVolume = 1f;
    private float _musicVolume = 1f;
    private bool _soundEnabled = true;
    private bool _musicEnabled = true;

    private Song? _currentSong;
    private Song[]? _playlist;
    private int _playlistIndex;
    private bool _songLoops;

    public AudioManager()
    {
        MediaPlayer.MediaStateChanged += OnMediaStateChanged;
    }

    // ── Sound effects ──────────────────────────────────────────────────────────

    /// <summary>
    /// Plays a sound effect. If the same <paramref name="soundEffect"/> was already played this
    /// frame, the call is silently ignored (per-frame dedup). Cross-frame stacking is allowed.
    /// Has no effect when <see cref="SoundEnabled"/> is <c>false</c> or <see cref="ConcurrentSounds"/>
    /// has reached <see cref="MaxConcurrentSounds"/>.
    /// </summary>
    public void Play(SoundEffect soundEffect, float volume = 1f, float pitch = 0f, float pan = 0f)
    {
        if (!_soundEnabled) return;
        if (_playedThisFrame.Contains(soundEffect)) return;
        if (_activeSounds.Count >= MaxConcurrentSounds) return;

        var instance = soundEffect.CreateInstance();
        instance.Volume = System.Math.Clamp(volume * _soundVolume, 0f, 1f);
        instance.Pitch = System.Math.Clamp(pitch, -1f, 1f);
        instance.Pan = System.Math.Clamp(pan, -1f, 1f);
        instance.Play();

        _activeSounds.Add((soundEffect, instance));
        _playedThisFrame.Add(soundEffect);
    }

    /// <summary>Returns <c>true</c> if any tracked instance of <paramref name="soundEffect"/> is currently playing.</summary>
    public bool IsPlaying(SoundEffect soundEffect)
    {
        foreach (var (effect, instance) in _activeSounds)
        {
            if (effect == soundEffect && instance.State == SoundState.Playing)
                return true;
        }
        return false;
    }

    // ── Music ──────────────────────────────────────────────────────────────────

    /// <summary>The song that is currently queued (may be paused or stopped).</summary>
    public Song? CurrentSong => _currentSong;

    /// <summary>
    /// Plays <paramref name="song"/> as the background track.
    /// Replaces any active song or playlist.
    /// Has no effect on volume when <see cref="MusicEnabled"/> is <c>false</c> — the song will
    /// begin playing but will immediately be paused by the <see cref="MusicEnabled"/> setter logic;
    /// use <see cref="MusicEnabled"/> to re-enable.
    /// </summary>
    public void PlaySong(Song song, bool loop = true)
    {
        _currentSong = song;
        _songLoops = loop;
        _playlist = null;
        MediaPlayer.IsRepeating = loop;
        MediaPlayer.Volume = _musicVolume;
        MediaPlayer.Play(song);

        if (!_musicEnabled)
            MediaPlayer.Pause();
    }

    /// <summary>
    /// Plays <paramref name="songs"/> in sequence, looping the playlist from the beginning after the last track.
    /// Replaces any active song.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="songs"/> is empty.</exception>
    public void PlayPlaylist(params Song[] songs)
    {
        if (songs.Length == 0)
            throw new ArgumentException("Playlist must contain at least one song.", nameof(songs));

        _playlist = songs;
        _playlistIndex = 0;
        _songLoops = false;
        MediaPlayer.IsRepeating = false;
        MediaPlayer.Volume = _musicVolume;
        _currentSong = songs[0];
        MediaPlayer.Play(songs[0]);

        if (!_musicEnabled)
            MediaPlayer.Pause();
    }

    /// <summary>Pauses the current song at its current position.</summary>
    public void PauseSong() => MediaPlayer.Pause();

    /// <summary>
    /// Resumes the current song from its paused position.
    /// Has no effect if <see cref="MusicEnabled"/> is <c>false</c> or no song is loaded.
    /// </summary>
    public void ResumeSong()
    {
        if (_currentSong != null && _musicEnabled)
            MediaPlayer.Resume();
    }

    /// <summary>Stops the current song or playlist and clears the current song state.</summary>
    public void StopSong()
    {
        MediaPlayer.Stop();
        _currentSong = null;
        _playlist = null;
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
    /// Setting this immediately updates <see cref="MediaPlayer.Volume"/>.
    /// </summary>
    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = System.Math.Clamp(value, 0f, 1f);
            MediaPlayer.Volume = _musicVolume;
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
                MediaPlayer.Pause();
            else if (_currentSong != null)
                MediaPlayer.Resume();
        }
    }

    // ── Limits / diagnostics ───────────────────────────────────────────────────

    /// <summary>Maximum number of concurrently tracked sound effect instances. Default is 32.</summary>
    public int MaxConcurrentSounds { get; set; } = 32;

    /// <summary>Number of currently tracked active sound effect instances.</summary>
    public int ConcurrentSounds => _activeSounds.Count;

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
            var (_, instance) = _activeSounds[i];
            if (instance.State == SoundState.Stopped)
            {
                instance.Dispose();
                _activeSounds.RemoveAt(i);
            }
        }
    }

    // ── Private helpers ────────────────────────────────────────────────────────

    private void OnMediaStateChanged(object? sender, EventArgs e)
    {
        if (MediaPlayer.State != MediaState.Stopped) return;
        if (_playlist == null) return;

        _playlistIndex = (_playlistIndex + 1) % _playlist.Length;
        _currentSong = _playlist[_playlistIndex];
        MediaPlayer.Play(_currentSong);

        if (!_musicEnabled)
            MediaPlayer.Pause();
    }
}
