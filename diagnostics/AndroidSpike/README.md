# AndroidSpike

Feasibility spike: does the FlatRedBall2 engine run on KNI's Android backend?

Needs the `android` workload (`dotnet workload install android`), a device with USB debugging on, and:

```
dotnet build -t:Run -f net10.0-android
```

## Findings (Android 12, OpenGL ES 3.2 phone)

- Raw KNI (`nkast.Kni.Platform.Android.GL`): boots, renders, delivers touch.
- `MainActivity` needs `ScreenOrientation = FullSensor`, or `AndroidGameActivity.OnCreate` throws.
- `GraphicsProfile.FL10_0` is rejected by the Android backend; the spike uses `HiDef`. The engine's
  own templates request `FL10_0` (for Apos.Shapes), so Android templates need `HiDef`.
- Apos.Shapes' `ShapeBatch` constructor logs "This effect is for an older version of KNI and needs to
  be rebuilt" and then hangs the app. Both the engine's `ShapesBatch` and Gum's `ShapeRenderer`
  created it at startup, so nothing rendered. `ShapesBatch` is now lazy and Gum's `ShapeRenderer`
  init is skipped on Android. Result: background and Gum labels render; shapes do not.
- Not yet checked: touch through the engine `Cursor`, content loading, audio, Gum controls that draw
  shapes (rounded buttons).
