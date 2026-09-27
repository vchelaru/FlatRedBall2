---
name: desktop-distribution
description: Shipping a desktop FRB2 build — self-contained per-RID publish and the macOS .app layout. Triggers: PublishProfile, SelfContained, RuntimeIdentifier, osx-arm64, Contents/MacOS.
---

# Desktop Distribution

FRB2 ships no publish profiles or packaging targets, so the game project owns them. `samples/Solitaire/Solitaire.GumPackage.targets` shows the pattern for a game-owned targets file that stays inert unless a property turns it on. For the web head see `multiplatform-conversion`.

## Landmines

- **A framework-dependent publish builds clean but won't launch on a player's machine.** Each RID needs its own profile in `Properties/PublishProfiles/` that sets `RuntimeIdentifier` and `SelfContained=true`. A self-contained artifact's `runtimeconfig.json` lists `includedFrameworks` rather than `framework`. `Configuration` in a `.pubxml` is ignored, so pass `-c` instead (`dotnet publish` defaults to Release).
- **On macOS, `Content/` goes in `<App>.app/Contents/MacOS/`, not in the conventional `Contents/Resources/`.** FRB2 roots some content loads at `AppContext.BaseDirectory`, the folder that holds the game's `.dll` (#1221).
- **`Compress-Archive` stores no Unix file modes**, so a zip made on Windows ships an apphost that macOS and Linux won't execute. Setting mode `0755` through `ZipArchiveEntry.ExternalAttributes` (the high 16 bits) fixes it.
