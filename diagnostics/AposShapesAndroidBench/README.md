# AposShapesAndroidBench

Bare KNI Android app (no FlatRedBall2, no Gum) that times Apos.Shapes startup, so 0.8.6 and 0.9.1
can be compared on the same device the same way.

```
dotnet build -c Release -p:AposVer=0.8.6     # or 0.9.1 (default)
adb -s <serial> install -r bin/Release/net10.0-android/com.flatredball.aposbench-Signed.apk
adb -s <serial> shell cmd package resolve-activity --brief com.flatredball.aposbench   # activity name
adb -s <serial> shell am start -n <activity> --es mode batch
adb -s <serial> logcat -s APOSBENCH:I
```

The work runs on the first `Update` after one plain frame. Modes, one per cold launch:

- `batch`: `new ShapeBatch(GraphicsDevice, Content)`, then two draws of one filled circle.
- `split`: the same work in three timed parts: read and decompress the embedded `.knifx`,
  `new Effect(GraphicsDevice, bytes)` (KNI parse plus GL shader compiles), and the rest of the
  `ShapeBatch` constructor given that effect.
- `gl` / `glrev`: `glCompileShader` plus `GL_COMPILE_STATUS` on each unique GLSL shader pulled out of
  the `.knifx`, through `GLES20` directly, in file order or reversed. Driver compile only, no KNI.

## Results

Release builds, KNI 4.3.9001, `GraphicsProfile.HiDef`. Every launch rendered the circle (checked by
screencap). Motorola Edge 2025 (Mali-G615 MC2) and the 1.8 GB phone (PowerVR Rogue GE8300, driver
1.10@5130912).

| | Motorola 0.8.6 | Motorola 0.9.1 | PowerVR 0.8.6 | PowerVR 0.9.1 |
|---|---|---|---|---|
| `new ShapeBatch`, each cold launch | 1.32, 1.29, 1.29 s | 2.76\*, 2.17, 2.18 s | 144.1\*, 142.2 s | 429.3\*, 339.3, 429.3\* s |
| split: read + decompress `.knifx` | 21 ms | 87 ms | 82 ms | 276 ms |
| split: `new Effect` | 446 ms | 1.91 s | 142.1 s | 428.3 s\* |
| split: rest of the `ShapeBatch` ctor | 828 ms | 51 ms | 179 ms | 230 ms |
| direct compile of the unique shaders | 0.31 s | 1.75 s | 142.6 s | 511.6 s (reversed: 512.9 s) |
| first / second circle draw | 2-3 / 2 ms | 4-8 / 2 ms | 7 / 3-4 ms | 20-24 / 4-10 ms |
| GLSL in the `.knifx` | | | 1 fragment shader, 437 K chars | 16 fragment shaders, 2.82 M chars |

\* First launch after `adb install`. On the PowerVR phone that was 429.3 s all three times versus
339.3 s otherwise, so something the install leaves running costs about 90 s. Compare the steady
numbers: 142 s versus 339 s.

## Conclusions

Verified:

- **0.9.1 regressed startup**: 2.4x on the PowerVR phone (142 s to 339 s) and 1.7x on the Mali
  phone (1.29 s to 2.17 s), with nothing but KNI and Apos.Shapes in the process.
- **The time is the GPU driver compiling GLSL, not managed code.** Managed work is under 0.5 s on
  both versions (decompress 276 ms, ctor remainder 230 ms on PowerVR). `new Effect` is the whole
  cost, and compiling the same GLSL directly with `glCompileShader` costs the same or more. The FRB2
  spike's Debug 0.9.1 number (~340 s) matches this Release number, so the JIT doesn't matter; the
  Debug run was skipped for that reason.
- **No driver cache across launches**: a second cold launch is no faster. The PowerVR driver also
  doesn't cache within a process: recompiling identical 0.8.6 fragment text took another 146 s.
- **Why 0.9.1 is slower**: the `.knifx` went from one 437 K-char fragment shader to 16 variants
  totalling 2.82 M chars, each stored twice. On PowerVR, variants 0-7 compile in 1.7-25 s each
  (~80 s total) and variants 8-15 in 41-73 s each (~430 s total), the same in either order, so the
  cost belongs to the variant. Variants 8-15 each have about 12 more `texture()` calls and one more
  loop than their counterpart in 0-7.

Inferred, not checked:

- KNI's `new Effect` (339 s) is cheaper than compiling all 16 unique fragment shaders directly
  (512 s). KNI may skip some variants or compile modified source; reading KNI's GL effect loader
  would settle it.
- On the Mali phone 0.8.6's ctor remainder (828 ms) is far above PowerVR's (179 ms), which suggests
  Mali finishes the compile lazily on first use inside the ctor, not managed work.

## Draft upstream issue (Apos.Shapes, not filed)

> **0.9.x shader variants make first `ShapeBatch` 2.4x slower to create on Android GL ES**
>
> Bare KNI 4.3.9001 Android app, Release, only Apos.Shapes.KNI referenced. Time for
> `new ShapeBatch(GraphicsDevice, ...)` on a cold launch:
>
> | | 0.8.6 | 0.9.1 |
> |---|---|---|
> | PowerVR Rogue GE8300 (Android 12) | 142 s | 339 s |
> | Mali-G615 MC2 (Android 16) | 1.29 s | 2.17 s |
>
> Almost all of it is `glCompileShader`. 0.9.1's GL effect has 16 fragment-shader variants
> (2.82 M chars of GLSL) where 0.8.6 had one (437 K chars). The compile happens when the effect
> loads (the first draw afterwards takes ~20 ms), though a typical app draws few of the variants. On PowerVR the eight variants with the extra
> texture sampling take 41-73 s each. Could the variants compile lazily, on the first draw that
> needs them? The PowerVR driver has no shader cache, so every launch pays the full cost.
