# ShapeFillColorRepro — issue #663

A one-screen visual test for the macOS DesktopGL bug where the **first filled Apos.Shapes shape of a
session renders the wrong color** (blue instead of black). Apos.Shapes binds sampler unit 0 only when
an image/font is drawn, so a pure filled rectangle leaves it unbound; the macOS GL driver then
substitutes a "zero texture" and corrupts the fill color. The fix (in `ShapesBatch`) keeps a loaded
1×1 white texture bound to unit 0 for every shapes batch.

The window shows a single centered rectangle — the only filled shape drawn, so it is that first
shape. Just look at its color.

## Run it (flip the fix on/off)

```
dotnet run                              # fix ON  -> BLACK rectangle
FRB2_DISABLE_FILL_PRIME=1 dotnet run    # fix OFF -> BLUE rectangle on an affected Mac
```

| What you see | Meaning |
|---|---|
| Black rectangle on gray | Correct (fix working) |
| Blue rectangle on gray  | Bug reproduced |

Press Escape to quit.

If the rectangle is black even with `FRB2_DISABLE_FILL_PRIME=1`, the bug does not reproduce on your
machine — reconcile that before trusting the fix.
