# MusicPitchWebSpike — issue #799, KNI/BlazorGL counterpart to MusicPitchSpike

`../MusicPitchSpike` proved `DynamicSoundEffectInstance.Pitch` changes playback rate on
DesktopGL/OpenAL, measured objectively (buffer-consumption rate vs wall clock — no listening
needed). Web Audio has no equivalent "count buffers consumed" signal accessible this way from
outside the browser, so this is a listening test instead.

Press **1 / 2 / 3** to play a 0.6s tone at Pitch **-1 / 0 / +1**. All three should sound clearly
different if `Pitch` works on KNI's BlazorGL backend (Web Audio `playbackRate`).

No FlatRedBall2 engine involved (same isolation goal as `MusicPitchSpike`) and no content
pipeline — every tone is generated synthetically at runtime.

## Run

```
dotnet run --project MusicPitchWebSpike.BlazorGL
```

Opens `https://localhost:50500`. Click into the page first (Chrome blocks audio until a user
gesture), then press 1 / 2 / 3.
