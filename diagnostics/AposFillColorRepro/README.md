# AposFillColorRepro — regression check for issue #663 (pure Apos.Shapes, no FlatRedBall2)

Draws a black `FillRectangle`. It must render **black**. If it renders **blue**, issue #663 has
regressed.

## The bug

Apos.Shapes packs each pair of color bytes into one float (a Szudzik pairing) and decodes it in the
shader (`apos-shapes.fx`, `Unpair()`) with `floor(sqrt(n))`. On macOS's GL driver, `sqrt` of a
perfect square lands just below the integer (`sqrt(65025) = 254.9999...`), so `floor()` drops a whole
step and a color byte that should be `0` decodes as `~255`. Blue is paired with alpha, so every opaque
filled shape with blue `= 0` rendered with the blue channel forced to `1.0` (black→blue, red→magenta,
`Color.Green` (0,128,0)→light blue). Correct on Windows because x86 `sqrt` is correctly rounded.

The fix nudges the root back up when `(f1+1)^2` still fits within `n`. It lives in Apos's `Unpair()`
and ships through FlatRedBall2's precompiled `apos-shapes.xnb`. Until it lands in a released
Apos.Shapes version, FRB2 carries it as a local patch — see
`src/PrecompiledShaders/apos-shapes-663.patch` and the note in `src/PrecompiledShaders/AposShapes.props`.

## Why pure MonoGame

No FlatRedBall2 — just MonoGame + Apos.Shapes — so it isolates the bug to Apos.Shapes × the GL driver.
It consumes FRB2's precompiled `apos-shapes.xnb` (via `AposShapesPrecompiled.props`), which carries the
fix, so no Wine is needed.

## Run

```
dotnet run
```

Correct on Windows (always was); black on macOS DesktopGL after the fix.
