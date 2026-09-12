# FlatRedBall.AnimationChain.Common

The `.achx` animation file-format data model (`AnimationChainListSave`, `AnimationChainSave`,
`AnimationFrameSave`, `ColorOperation`, `ShapesSave`) plus a generic, renderer-agnostic playback
runtime (`AnimationChain<TFrame>`, `AnimationChainList<TFrame>`, `AnimationPlayer<TFrame>`) used by
[FlatRedBall2](https://github.com/vchelaru/FlatRedBall2) and the FlatRedBall Animation Editor.

Pure C#, no MonoGame/KNI dependency — a renderer only has to close the generic runtime types over
its own texture handle type.

Most users should install `FlatRedBall2.MonoGame` or `FlatRedBall2.Kni` instead; both reference
this package automatically and provide the runtime bridge that turns these types into playable
animations backed by real textures. For the minimal DIY path -- raw MonoGame, no engine, no
`FlatRedBall.AnimationChain.MonoGame` -- see `samples/AnimationChainCommonSample`.

## License

MIT — see [LICENSE](https://github.com/vchelaru/FlatRedBall2/blob/main/LICENSE).
