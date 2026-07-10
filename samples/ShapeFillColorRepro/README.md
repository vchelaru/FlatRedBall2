# ShapeFillColorRepro — issue #663

A one-screen visual test for the macOS DesktopGL bug where the **first filled Apos.Shapes shape of a
session renders the wrong color** (blue instead of black). Apos.Shapes binds sampler unit 0 only when
an image/font is drawn, so a pure filled rectangle leaves it unbound; the macOS GL driver then
substitutes a "zero texture" and corrupts the fill color. The fix (in `ShapesBatch`) keeps a loaded
1×1 white texture bound to unit 0 for every shapes batch.

The window shows a single centered rectangle — the only filled shape drawn, so it is that first
shape. Just look at its color.

## Run it (flip the fix on/off)

This sample **defaults to showing the bug**, so a fresh `dotnet run` reproduces it immediately:

```
dotnet run                              # BLUE rectangle (bug) on an affected Mac — the default
FRB2_DISABLE_FILL_PRIME=0 dotnet run    # BLACK rectangle (fix)
```

| What you see | Meaning |
|---|---|
| Blue rectangle on gray  | Bug reproduced (this sample's default) |
| Black rectangle on gray | Fix working (`FRB2_DISABLE_FILL_PRIME=0`) |

Press Escape to quit.

The default only affects this sample: `Program.cs` sets `FRB2_DISABLE_FILL_PRIME=1` when unset, while
the engine's own default keeps the fix on for every other sample and game.

If the rectangle is **black on the plain `dotnet run`**, the bug does not reproduce on your machine —
reconcile that before trusting the fix.
