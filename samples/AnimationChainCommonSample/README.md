# AnimationChainCommonSample

The most minimal way to use `FlatRedBall.AnimationChain.Common` on its own -- no FlatRedBall2
engine, no `FlatRedBall.AnimationChain.MonoGame`, no runtime playback types. Just the raw
`.achx`/`.achj` data model (`AnimationChainListSave`/`AnimationChainSave`/`AnimationFrameSave`),
a hand-rolled `double` elapsed-seconds counter, and a `SpriteBatch.Draw` call.

## Run locally

```bash
dotnet run --project samples/AnimationChainCommonSample/AnimationChainCommonSample.csproj
```

## Controls

- **Space**: toggle Walk / Idle
- **Escape**: exit

## How it works

1. `AnimationChainListSave.FromStream` parses `Content/hero.achj` once at startup into plain data:
   a list of `AnimationChainSave`, each a list of `AnimationFrameSave`.
2. `Update` just adds elapsed seconds to a `double _secondsIntoAnimation`.
3. `GetCurrentFrame` walks the current chain's frames, wrapping at the chain's total length, to
   find which `AnimationFrameSave` covers that time -- the same math `AnimationPlayer` does
   internally, inlined here instead of hidden behind a runtime type.
4. `Draw` builds an XNA `Rectangle` straight from the frame's `Left/Top/Right/BottomCoordinate`
   fields and calls `SpriteBatch.Draw`.
