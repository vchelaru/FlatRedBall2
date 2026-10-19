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
- `new ShapeBatch(...)` compiles Apos.Shapes' shader on the GPU driver. The fragment shader's GLSL is
  436,712 characters. Compiling it directly with `glCompileShader` (bypassing KNI) takes about 142 s on
  the PowerVR GE8300 phone (143 s again on a second compile, so the driver does not cache it) and about
  1.4 s on the Motorola. KNI's own GLSL to GLSL ES 3.00 regex conversion is only ~110 ms. Android kills
  the app after 5 s (ANR). The engine's `ShapesBatch` creates the batch lazily on first use, but Gum's
  `ShapeRenderer.Initialize` still builds one at startup, so the old phone cannot start the engine.
- A saved program binary fixes repeat launches. On the PowerVR phone, `glGetProgramBinary` after the
  first compile and link gave a 368,599-byte binary (1 supported format, 37168). On the next launch
  `glProgramBinary` loaded it in 5 ms and linked fine, versus ~151 s to compile. The binary is
  specific to the GPU and driver, so it can't be shipped in the app. It must be produced on the device
  on first run, keyed by `GL_RENDERER`/`GL_VERSION`, with a recompile fallback if loading fails. KNI
  doesn't do this today; it compiles in `ConcreteShader.CreateShader` and links at first draw.
- Shapes default to `IsVisible = false` and `IsFilled = false`; set both to see one.
- Not yet checked: content loading, audio.
