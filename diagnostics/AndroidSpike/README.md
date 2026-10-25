# AndroidSpike

Feasibility spike: does the FlatRedBall2 engine run on KNI's Android backend?

Needs the `android` workload (`dotnet workload install android`), a device with USB debugging on, and:

```
dotnet build -t:Run -f net10.0-android
```

## Findings

Tested on a Motorola Edge 2025 (Android 16, Mali, 7.6 GB RAM) and an old 1.8 GB PowerVR GE8300 phone
(Android 12).

- Raw KNI (`nkast.Kni.Platform.Android.GL`): boots, renders, delivers touch.
- `MainActivity` needs `ScreenOrientation = FullSensor`, or `AndroidGameActivity.OnCreate` throws.
- `GraphicsProfile.FL10_0` is rejected by the Android backend; the spike uses `HiDef`. The engine's
  own templates request `FL10_0` (for Apos.Shapes), so Android templates need `HiDef`.
- The engine renders on the Motorola: Gum label, Gum rounded rectangle, engine `Circle` shapes, and
  touch through `Engine.Input.Cursor`.
- "This effect is for an older version of KNI" in logcat is only a warning: Apos.Shapes' knifx is
  format v11 and KNI reads v12. It still loads.
- `new ShapeBatch(...)` compiles Apos.Shapes' shader on the GPU driver. With Apos.Shapes 0.8.6 the
  fragment shader's GLSL was 436,712 characters, and `glCompileShader` alone took about 142 s on the
  PowerVR phone (the driver does not cache it across compiles) and about 1.4 s on the Motorola, past
  Android's 5 s ANR limit on the old phone. Gum's `ShapeRenderer.Initialize` builds a `ShapeBatch` at
  startup, so this cost can't be deferred.
- Apos.Shapes 0.9.1 (Gum 2026.10.25.3-preview.8), Motorola, Debug build, three cold launches. The
  spike logs these under the `FRBSPIKE` logcat tag:

  | Phase | Time |
  |---|---|
  | Process start to `OnCreate` | ~210 ms |
  | `OnCreate` to `Game.Initialize` | ~530 ms |
  | First `new ShapeBatch` | ~2.2 s (same on every launch) |
  | First `ShapeBatch` draw (program link) | 2-4 ms |
  | A second `ShapeBatch` in the same process | 75 ms |
  | `FlatRedBallService.Initialize` (ShapeRenderer already done) | ~820 ms |
  | First `Draw` | ~82 ms |
  | `OnCreate` to second frame drawn | ~3.6-3.7 s |

  The 2.2 s is not split into driver compile versus managed work (0.9.1 no longer embeds the GLSL as
  plain text, and Gum now requires Apos.Shapes >= 0.9.1, so 0.8.6 can't be swapped in to compare).
- Build times, Motorola: clean build 28 s, incremental 10 s, uninstall then `-t:Install` 24 s.
- Source-level tricks did not fix the 0.8.6 first compile on the PowerVR phone (baseline ~142 s):
  `#pragma optimize(off)`, `mediump`, GLSL ES 1.00, and single-pass loop rewrites changed nothing;
  stopping loop unrolling saved 31%; replacing `texture()` with a constant saved 43% but broke rendering.
- A saved program binary fixes repeat launches. On the PowerVR phone, with 0.8.6, `glGetProgramBinary`
  gave a 368,599-byte binary that `glProgramBinary` loaded in 5 ms on the next launch, versus ~151 s
  to compile. The binary is specific to the GPU and driver, so it must be produced on the device on
  first run, keyed by `GL_RENDERER`/`GL_VERSION`, with a recompile fallback. KNI doesn't do this today.
- `RectangleRuntime.Color` is obsolete and sets the stroke; the fill comes from `FillColor`
  (default white). Use `FillColor` with `IsFilled = true`.
- Shapes default to `IsVisible = false` and `IsFilled = false`; set both to see one.
- Not yet checked: Apos.Shapes 0.9.1 on the PowerVR phone, content loading, audio.
