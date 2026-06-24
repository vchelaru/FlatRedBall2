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

Two paths. In the Common/Desktop template layout, prefer the **direct path** — it matches how every other raw asset (png, tmx, achx) is handled, with audio files living alongside them in `Common/Content/`. Reach for MGCB only when a format limit forces it (MP3).

### Direct path — load the raw file (default for OGG/WAV)

Drop the file in `Common/Content/` (e.g. `Common/Content/Audio/song.ogg`); it is linked into the Desktop output's `Content/` automatically, no `.mgcb` entry needed. Load by full path with extension.

**Song (music) — OGG only.** `Song.FromUri` accepts only OGG Vorbis on DesktopGL; MP3 fails at runtime (NVorbis backend).

```csharp
var song = Song.FromUri("song", new Uri(Path.GetFullPath("Content/Audio/song.ogg")));
```

**SoundEffect — WAV only.** `SoundEffect.FromStream` accepts only PCM WAV. OGG/MP3 effects need the MGCB path below.

```csharp
using var stream = File.OpenRead("Content/Audio/hit.wav");
var sfx = SoundEffect.FromStream(stream);
Engine.Content.Track(sfx);  // disposes on screen transition
```

Usings: `System.IO`, `Microsoft.Xna.Framework.Audio`, `Microsoft.Xna.Framework.Media`.

### MGCB pipeline — for MP3 or compressed formats

Needed only when the direct path's format limits bite. The `.mgcb` lives in the **`.Desktop` head**, not `Common`: `MonoGame.Content.Builder.Task` runs only on the head, so a mgcb added to `Common` is silently never built. Put the `#begin` entry *and* the source file under `Desktop/Content/`. See `multiplatform-conversion` for the content-split rationale.

```
#begin Audio/song.mp3
/importer:Mp3Importer
/processor:SongProcessor
/build:Audio/song.mp3
```

Load with the asset name **minus the source extension** — `/build:Audio/song.mp3` builds to `Audio/song.xnb`:

```csharp
var song = Engine.Content.Load<Song>("Audio/song");  // not "Audio/song.mp3"
// ContentLoader disposes Load<>'d assets on screen transition — no Track() needed
```

Usings: `Microsoft.Xna.Framework.Audio`, `Microsoft.Xna.Framework.Media`.

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
- **`Load<>` name drops the source extension** — `Load<Song>("Audio/song")`, not `"Audio/song.ogg"`. Passing the extension makes MonoGame look for `song.ogg.xnb` (the pipeline builds `song.xnb`) and throws `FileNotFoundException`. The `.mgcb` defining it must be in the `.Desktop` head, not `Common`.
- **Track SoundEffect only when loaded via `FromStream`** — call `Engine.Content.Track(sfx)` when using `SoundEffect.FromStream` so it is disposed on screen transition. MGCB-loaded assets (`Engine.Content.Load<SoundEffect>`) are disposed automatically by the ContentLoader — do not call `Track` for those.
- **Per-frame dedup in collision handlers** — `Play(sfx)` in a `CollisionOccurred` handler is safe to call unconditionally; it fires at most once per frame regardless of how many pairs collide.
