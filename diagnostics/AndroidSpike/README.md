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
- `new ShapeBatch(...)` compiles Apos.Shapes' large shader on the GPU driver. That takes about 1.4 s
  on the Motorola but about 142 s on the PowerVR phone, which Android kills as an ANR (5 s). The
  engine's `ShapesBatch` creates it lazily on first use, but Gum's `ShapeRenderer.Initialize` still
  builds one at startup, so the old phone cannot start the engine at all.
- Shapes default to `IsVisible = false` and `IsFilled = false`; set both to see one.
- Not yet checked: content loading, audio.
