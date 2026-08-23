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

The fix nudges the root back up when `(f1+1)^2` still fits within `n`. It landed upstream in Apos's
`Unpair()` (folded into the 0.7.8 release) — FRB2 no longer carries a local patch or a precompiled
shader for it; the fix ships in whatever `$(AposShapesVersion)` (see `Directory.Packages.props`)
resolves.

## Why pure MonoGame

No FlatRedBall2 — just MonoGame + Apos.Shapes — so it isolates the bug to Apos.Shapes × the GL driver.
Apos.Shapes 0.7.2+ embeds its shader directly in the package assembly, so no content pipeline
compile and no Wine dependency either way.

## Run

```
dotnet run
```

Correct on Windows (always was); black on macOS DesktopGL after the fix.
