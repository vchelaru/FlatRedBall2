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
- `stock` / `v0` / `v012`: `new Effect` from the shipped `.knifx` (all 16 techniques) or from a
  rebuild of 0.9.1's own `apos-shapes.fx` keeping only `Shapes0` or `Shapes0`-`Shapes2`
  (`Variants/`, made by `Variants/make-variants.py`; with all 16 kept the rebuild is byte-identical
  to the shipped file). Then `new ShapeBatch(GraphicsDevice, effect)` and a grid of eight shapes,
  each in its own `Begin`/`End` so each one's first draw is timed.

`adb shell pm clear com.flatredball.aposbench` before a launch empties the driver's shader cache
(see below); `bench.sh` in the session scratchpad does not do it on its own.

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

\* First launch after `adb install`, which starts with an empty driver shader cache (see
"Choosing shader variants" below). On PowerVR that was 429.3 s versus 339.3 s with the cache
partly filled. Compare like with like: 142 s versus 339 s.

## Conclusions

Verified:

- **0.9.1 regressed startup**: 2.4x on the PowerVR phone (142 s to 339 s) and 1.7x on the Mali
  phone (1.29 s to 2.17 s), with nothing but KNI and Apos.Shapes in the process.
- **The time is the GPU driver compiling GLSL, not managed code.** Managed work is under 0.5 s on
  both versions (decompress 276 ms, ctor remainder 230 ms on PowerVR). `new Effect` is the whole
  cost, and compiling the same GLSL directly with `glCompileShader` costs the same or more. The FRB2
  spike's Debug 0.9.1 number (~340 s) matches this Release number, so the JIT doesn't matter; the
  Debug run was skipped for that reason.
- **The driver cache barely helps the full shader**: PowerVR keeps a per-app cache across launches
  (see below), but for 0.9.1's full shader it only cuts 435 s to 339 s, and 0.8.6 from 144 s to 142 s.
  The PowerVR driver doesn't cache within a process: recompiling identical 0.8.6 fragment text took another 146 s.
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

## Choosing shader variants (0.9.x)

Apos.Shapes 0.9.x splits its shader into 16 techniques, `Shapes0`-`Shapes15`, indexed by
`ShapeFeatures` bits: `Dashes` = 1, `Gradients` = 2 (fill gradient), `BorderGradients` = 4,
`Text` = 8 (text and SVG). Flat filled or outlined circles, rectangles, rounded rectangles and
lines all draw with `Shapes0`. The public API is `ShapeBatch.Warmup(ShapeFeatures)` plus the
constructor's `warmup` flag (default `true` runs `Warmup(ShapeFeatures.None)`).

That API does not avoid any compile on KNI GL. `ShapeBatch.Warmup` only forces a draw, on the
assumption that "a technique's shaders compile the first time something draws with it"
(`apos-shapes.fx`, v0.9.1). KNI 4.3.9001's `ConcretePixelShader` constructor calls
`CreateShader`, which runs `glCompileShader`, so `new Effect` compiles every technique up front.
The default `new ShapeBatch` already warms only `None`, and that is the 339-435 s run. The only
way to compile fewer variants today is to pass an `Effect` built with fewer techniques, which is
what `v0`/`v012` do. A draw that needs a technique the effect lacks doesn't compile anything or
fail: `PickTechnique` returns -1 and the draw uses the effect's current technique (`Shapes0`), so
dashes and gradients silently render as plain solid shapes (checked by screencap).

Release, KNI 4.3.9001, Apos.Shapes 0.9.1. "Cleared" = `pm clear` first (empty driver cache);
"warm" = launched again without clearing. 2 launches each; times are `new Effect` + `new ShapeBatch`
(incl. `Warmup(None)`), then the work's end measured from process start.

| | Motorola (Mali-G615) | PowerVR GE8300 |
|---|---|---|
| `v0` cleared: effect + batch | 0.24 + 0.19 s; 0.19 + 0.17 s | 2.33 + 0.18 s; 2.28 + 0.20 s |
| `v0` cleared: from process start | 1.2, 0.9 s | 4.2, 4.2 s |
| `v0` warm: effect + batch | 0.21 + 0.06 s | 0.44 + 0.19 s (2.3 s from process start) |
| `v012` cleared: effect + batch | 0.38 + 0.18 s; 0.37 + 0.17 s | 13.6 + 0.20 s; 13.7 + 0.21 s |
| `v012` warm: effect | 0.37 s | 11.8, 5.4, 0.48, 0.48 s (cache fills over launches) |
| `stock` cleared: effect | 1.98, 2.01 s | 434.8 s |
| `stock` warm: effect | 1.91 s | 338.9 s |
| first draw, flat shapes (`Shapes0`) | 0-3 ms each | 1-23 ms each |
| first draw, `Shapes1`/`Shapes2`/`Shapes3` present | 205-220 / 202-216 / 295 ms | 38-59 / 8-24 / 21 ms |
| first draw of a missing technique | 0-9 ms, drawn with `Shapes0` | 1-39 ms, drawn with `Shapes0` |
| any second draw | 0-1 ms | 0-3 ms |

Verified:

- **Compiling only `Shapes0` fixes startup.** PowerVR goes from 339-435 s to 2.5 s of shader work
  on an empty cache (4.2 s from process start) and 0.6 s warm. Mali goes from 2.0 s to 0.2-0.4 s.
- **Each extra variant is expensive on PowerVR**: adding `Shapes1` and `Shapes2` takes the empty-
  cache effect from 2.3 s to 13.6 s, which is back over the 5 s ANR limit.
- **PowerVR has a persistent per-app shader cache**, which `pm clear` empties. `v0` drops from 2.3 s
  to 0.44 s on the next launch; `v012` fills it over three launches. `stock` gets only partway
  (435 s, then 339 s). This explains the "first launch after install" outlier above: that was
  the empty cache, 435 s, not something left running.
- **Mali links on first draw**: each technique's first draw costs ~200-300 ms whether or not it was
  compiled in `new Effect`. PowerVR does the work in `glCompileShader` and links in tens of ms.

Inferred, not checked:

- `stock` never fully caches on PowerVR because the 16 variants exceed the driver/Android blob
  cache's size limit.
- The APK reinstall invalidating the cache (the old first-launch outlier) suggests the cache key
  includes the APK build.

## Upstream issue

Filed as https://github.com/Apostolique/Apos.Shapes/issues/40.
