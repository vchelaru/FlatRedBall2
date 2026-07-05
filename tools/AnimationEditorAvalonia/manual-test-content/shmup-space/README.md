# Manual test fixture: Open Folder / drag-drop (#535)

Copied from `samples/ShmupSpace/ShmupSpace.Common/Content/Animations/`. Use this folder
(not `AnimationEditor.Browser/wwwroot/sample/`, which is the bundled sample already shown
on load — opening it back up looks identical to doing nothing) to manually verify Open
Folder / drag-drop in a running browser build:

1. Run `AnimationEditor.Browser` and let the bundled "ColorCycle" sample load.
2. Open Folder (or drag-drop both files) pointing at this directory.
3. Status bar should name `ShmupSpace.achx`, and the preview should show a ship sprite cut
   from `arcade_space_shooter.png` instead of the colored-square "keyhole" shape.

`ShmupSpace.achx` uses `CoordinateType=Pixel` (unlike the bundled UV sample), so this
folder also exercises the pixel-to-UV conversion path in `ProjectManager.LoadAnimationChain`
via the `knownTextureSizes` the browser supplies from its already-decoded PNGs.

**The image you'll see will be static, not animated — that's expected.** This spike's UI
has no chain selector; it always shows `AnimationChains[0]` from the file, which for this
`.achx` is `ShipTurnLeft`, a single static frame (a ship-rotation pose, not a cycling
animation). The file *does* contain real multi-frame chains (`Explosion`, `EnemyClam`,
`ShipBoosterStrong`/`ShipBoosterWeak`), but there's currently no UI to reach them — see
"Known gap: no chain selector in this spike's UI" in `docs/BROWSER_SPIKE_FINDINGS.md`.
This folder still fully exercises what it's meant to: Open Folder/drag-drop loading a
Pixel-coordinate multi-chain file with a different texture than the bundled sample.
