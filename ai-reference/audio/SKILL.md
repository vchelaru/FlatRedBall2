---
name: audio
description: "Audio in FlatRedBall2. Use when working with sound effects, background music, AudioManager, loading Song or SoundEffect, volume control, or collision sound triggers."
---

# Audio in FlatRedBall2

## Access

```csharp
Engine.Audio  // AudioManager instance on FlatRedBallService
```

## Loading Audio

### Standard path — MGCB content pipeline (preferred)

Add audio files to `Content/Content.mgcb`, then load via `Engine.Content.Load<>`. MGCB handles copying processed output; no raw `<Content>` items needed in the `.csproj`.

**Content.mgcb entries:**

```
#begin MySong.mp3
/importer:Mp3Importer
/processor:SongProcessor
/build:MySong.mp3

#begin Hit.wav
/importer:WavImporter
/processor:SoundEffectProcessor
/build:Hit.wav
```

**Code:**

```csharp
var song = Engine.Content.Load<Song>("MySong");
var sfx  = Engine.Content.Load<SoundEffect>("Hit");
// No manual tracking needed — ContentLoader disposes on screen transition
```

Usings required: `Microsoft.Xna.Framework.Audio`, `Microsoft.Xna.Framework.Media`.

### Direct path — OGG only

`Song.FromUri` works if the file is OGG Vorbis. MP3 fails at runtime because MonoGame DesktopGL uses NVorbis, which only reads OGG.

```csharp
// Only works for .ogg files
var song = Song.FromUri("MySong", new Uri(Path.GetFullPath("Content/MySong.ogg")));

// SoundEffect loaded from file stream — manual tracking required
var sfx = SoundEffect.FromStream(File.OpenRead("Content/Hit.wav"));
Engine.Content.Track(sfx);  // ensures disposal on screen transition
```

Usings required: `System.IO`, `Microsoft.Xna.Framework.Audio`, `Microsoft.Xna.Framework.Media`.

## Sound Effects

```csharp
Engine.Audio.Play(sfx);                             // play with defaults
Engine.Audio.Play(sfx, volume: 0.5f, pitch: 0f, pan: 0f);
Engine.Audio.IsPlaying(sfx);                        // true if any instance is active
```

Per-frame dedup: calling `Play(sfx)` multiple times in a single frame (e.g., from `CollisionOccurred` firing on multiple pairs) plays the sound only once. Cross-frame overlap is allowed.

## Background Music

```csharp
Engine.Audio.PlaySong(song);          // loops by default
Engine.Audio.PlaySong(song, loop: false);
Engine.Audio.PauseSong();             // holds position
Engine.Audio.ResumeSong();            // resumes from position
Engine.Audio.StopSong();              // clears position
```

### Playlist

```csharp
Engine.Audio.PlayPlaylist(song1, song2, song3);  // plays sequentially, loops back to start
```

## Volume and Enable/Disable

```csharp
Engine.Audio.SoundVolume = 0.8f;   // [0, 1], default 1
Engine.Audio.MusicVolume = 0.5f;   // [0, 1], default 1; takes effect immediately
Engine.Audio.SoundEnabled = false; // silences new Play() calls; active instances finish naturally
Engine.Audio.MusicEnabled = false; // pauses current song immediately; true resumes it
```

## Gotchas

- **Music does not stop automatically on screen transition** — call `Engine.Audio.StopSong()` in `CustomDestroy`, or music keeps playing into the next screen.
- **`Song.FromUri` only works with OGG** — on DesktopGL (NVorbis), `Song.FromUri` fails at runtime with MP3. Use the MGCB pipeline and `Engine.Content.Load<Song>` for MP3 files.
- **Track SoundEffect only when loaded via `FromStream`** — call `Engine.Content.Track(sfx)` when using `SoundEffect.FromStream` so it is disposed on screen transition. MGCB-loaded assets (`Engine.Content.Load<SoundEffect>`) are disposed automatically by the ContentLoader — do not call `Track` for those.
- **Per-frame dedup in collision handlers** — `Play(sfx)` in a `CollisionOccurred` handler is safe to call unconditionally; it fires at most once per frame regardless of how many pairs collide.
