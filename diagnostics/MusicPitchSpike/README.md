# MusicPitchSpike — feasibility spike for issue #799 (pure MonoGame, no FlatRedBall2)

Answers one question before committing to the #799 fix design: does
`DynamicSoundEffectInstance.Pitch` actually change effective playback rate on the DesktopGL
(OpenAL) backend, the way `AL_PITCH` is documented to?

## Method

No file/NVorbis decode — ogg decode is already proven elsewhere in MonoGame's own `Song`
loading, so it isn't the unknown here. Instead: generate a synthetic sine tone, feed fixed-size
PCM chunks to a `DynamicSoundEffectInstance` via `SubmitBuffer`, and cycle `Pitch` through
`0, +1, -1` (XNA semantics: +/-1 octave = 2x/0.5x rate). For each phase, count how many chunks
get consumed in a fixed wall-clock window and compare the observed rate to the expected `2^Pitch`
multiplier. Prints PASS/FAIL per phase to stdout and exits automatically.

## Run

```
dotnet run
```

A window opens (audio device init needs it) and closes itself after ~10s. Read the result off
stdout — no listening required.
