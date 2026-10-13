# Golden image tests

Pixel-level render tests. Each scene is drawn offscreen through the real `Screen.Draw` path and compared with a checked-in PNG in `Golden/`.

This is a separate project from `FlatRedBall2.Tests` on purpose: it needs a real GL device, and Linux GL has had intermittent native test-host crashes (#1300). A crash here must not take the ~1800 headless tests with it.

## Where it runs

Only on the CI job `Golden image tests (ubuntu, llvmpipe)` (Mesa software GL under `xvfb`). Everywhere else every scene is skipped:

- **macOS never creates a device** (SDL aborts the host off the main thread, #1416).
- **Any other machine skips unless `FRB_GOLDEN_IMAGES=1`**, because a local GPU renders slightly differently from llvmpipe and would fail against the goldens.

With `FRB_GOLDEN_IMAGES=1` a missing GL device is a failure, not a skip, so a broken xvfb/Mesa setup cannot pass vacuously. The harness tests (comparison, PNG codec, verifier) are plain unit tests and run everywhere.

## Add a golden

1. Add a test in `Scenes/` (copy `SpriteSceneGoldenTests`). Use `[GoldenFact]`, the shared `GraphicsDeviceCollection`, and `GoldenDevice.Require(_fixture)`:

   ```csharp
   var device = GoldenDevice.Require(_fixture);
   var screen = new Screen();
   screen.Add(/* sprites, tile maps, shapes */);
   var image = OffscreenRenderer.Render(device, screen, width: 128, height: 96);
   GoldenImage.Verify("my-scene", image);
   ```

   - One world unit is one pixel; the camera starts at the origin (`cameraX`/`cameraY` move it).
   - The background is opaque on purpose. PNGs hold the premultiplied read-back, so semi-transparent output is hard to judge by eye.
   - Build textures in code (`Scenes/SceneArt.cs`) rather than shipping content files, and give them an asymmetric marker so flips, rotation and Y inversion are visible.
   - Keep scenes small (a few dozen pixels per side) and deterministic: no randomness, no real time. Advance animation with `screen.Update(...)`, not wall clock.
   - Golden names are file names: letters, digits, `-`, `_`.
2. Push the branch, then run **Actions > Regenerate golden images** on it (`gh workflow run golden-images-regenerate.yml --ref <branch>`). It renders on the same llvmpipe runner, commits `Golden/<name>.png` to the branch, and re-runs the tests.
3. Open the new PNG and look at it. A golden you did not look at only proves the renderer is deterministic, not correct.

To change an existing golden deliberately, run the same workflow; unchanged scenes are left byte-identical. Never generate goldens locally: update mode (`FRB_GOLDEN_UPDATE=1`) refuses to run outside GitHub Actions.

## When a golden test fails

The job uploads a `golden-image-failures` artifact with `<name>.actual.png`, `<name>.expected.png` and `<name>.diff.png` (matching pixels dimmed, differing pixels magenta). If the change is intended, regenerate; otherwise it is a rendering regression.

The per-channel tolerance (`GoldenOptions.Tolerance`, default 2) absorbs Mesa-version drift, not rendering changes.
