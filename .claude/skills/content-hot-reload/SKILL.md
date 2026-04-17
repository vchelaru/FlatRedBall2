---
name: content-hot-reload
description: Content hot-reload in FlatRedBall2. Use when watching content files (JSON configs, PNGs, TMX maps, etc.) for changes during development and reloading them without killing the game. Covers Screen.WatchContentDirectory, Screen.WatchContent, source/output mapping, debouncing, and the in-place vs screen-restart decision.
---

# Content Hot-Reload in FlatRedBall2

When iterating on content during development — tweaking JSON configs, swapping a sprite, editing a TMX map — you don't want to restart the game. The engine watches your **source content folder** (the one with the `.csproj`, the one your editor saves to) and reloads on change.

## Two-folder model

A built .NET game has content in two places:

| Folder | What it is |
|--------|------------|
| `<csproj_dir>/Content/` | **Source.** Where you edit. Lives in source control. |
| `bin/Debug/.../Content/` | **Output.** Build copies here. App reads from here. |

Default game code reads the output (`PlatformerConfig.FromJson("Content/player.json")` resolves against `AppContext.BaseDirectory`). When the user edits the source, the output is stale until MSBuild re-copies.

The engine handles this by **copying source → output** as part of the watch pipeline, before invoking your callback. You write reload logic that reads from the output path as usual; the engine just makes sure the output is fresh.

## API

### Directory watch (the common case)

```csharp
public override void CustomInitialize()
{
    // Restart the screen on any change under Content/. Restart handles every file type — entities,
    // sprites, tile collections all rebuild from scratch via CustomInitialize.
    WatchContentDirectory("Content", _ => RestartScreen(RestartMode.HotReload));
}
```

To dispatch by file type:

```csharp
WatchContentDirectory("Content", relPath =>
{
    switch (Path.GetExtension(relPath))
    {
        case ".json":
            ReloadJsonConfig(relPath);  // in-place, no restart
            break;
        default:
            RestartScreen(RestartMode.HotReload);
            break;
    }
});
```

### Single-file watch

```csharp
WatchContent("Content/player.platformer.json", () =>
    PlatformerConfig.FromJson("Content/player.platformer.json").ApplyTo(player.Platformer));
```

Use when you want surgical control or to skip the cost of watching a whole tree.

### Custom source/output mapping

By default, source path == output path. If your build pipeline maps differently (e.g. `<None Update="Assets/..." TargetPath="Content/..." />`), pass the destination explicitly:

```csharp
WatchContent("Assets/Configs/player.json", reload, destinationPath: "Content/player.json");
WatchContentDirectory("Assets", relPath => ..., destinationDirectory: "Content");
```

## Two reload strategies

### 1. In-place reload — patch the existing object

Use when the change is small and the type/shape is unchanged. No screen restart, no state loss.

- **JSON configs** — read the new file, copy values onto the live object.
- **PNG with same dimensions** — `Texture2D.SetData` patches pixels; existing `Sprite` references keep working.
- **Tile-data-only TMX changes** — `TileMap.TryReloadFrom(path)` patches tile IDs in existing layers and rebuilds every TSC registered via `GenerateCollisionFromClass` / `GenerateCollisionFromProperty`. Returns `true` if applied; `false` if the new TMX differs structurally (map dims, layer set, tilesets, object layers) — caller falls back to `RestartScreen(RestartMode.HotReload)`. Hand-authored mutations on a generated TSC after `Generate*` (e.g. extra `AddPolygonTileAtCell` calls) are **wiped** on in-place reload — put augmentations in `CustomInitialize` so they survive a full restart.

  ```csharp
  WatchContentDirectory("Content", relPath =>
  {
      if (relPath.EndsWith(".tmx", StringComparison.OrdinalIgnoreCase))
      {
          if (!map.TryReloadFrom("Content/" + relPath))
              RestartScreen(RestartMode.HotReload);
      }
      else RestartScreen(RestartMode.HotReload);
  });
  ```

### 2. Screen restart — `RestartScreen(RestartMode.HotReload)`

Use when the change invalidates references the game holds.

- **PNG with new dimensions** — must `new` the `Texture2D`, orphans every `Sprite` referencing the old one.
- **Structural TMX changes** — layers added/removed, map resized, object layer modified.
- **Anything you're unsure about** — restart is always safe; in-place can leave dangling refs.

`RestartMode.HotReload` triggers `SaveHotReloadState` / `RestoreHotReloadState` so the player doesn't snap back to spawn. See the `screens` skill for the recipe.

## Source root detection

`FlatRedBallService.SourceContentRoot` is auto-detected at engine construction by walking up from `AppContext.BaseDirectory` looking for a `.csproj`. Common project layouts just work:

- `<csproj_dir>/bin/Debug/net10.0/MyGame.exe` → walks up to `<csproj_dir>` → finds `MyGame.csproj` → that's the root.

Override if auto-detection picks the wrong root (multi-project repos, unusual layouts):
```csharp
engine.SourceContentRoot = "C:/path/to/my/project";
```

`OutputContentRoot` defaults to `AppContext.BaseDirectory`. Override only if your build writes content to a non-standard location.

## Shipping builds

In a shipped game there's no `.csproj` next to the executable, so `SourceContentRoot` is `null`. `WatchContent`/`WatchContentDirectory` return `null` and silently skip registration. **No `#if DEBUG` needed** — hot-reload is a dev-only no-op in release.

## Debouncing

Editors and build tools fire bursts of events around each save (write + flush + rename). The engine uses **global debouncing**: it waits until all writes settle (no new events for ~150 ms) before processing the dirty batch. Per-file debouncing would let the first file's reload trigger before later writes in the same burst landed.

For directory watches: every dirty file in the batch fires its own callback in the same tick. If your callback always restarts the screen, multiple invocations collapse to a single restart automatically (the pending-change slot is last-write-wins).

Tune via the returned watcher:
```csharp
var w = WatchContentDirectory("Content", reload);
w.Debounce = TimeSpan.FromMilliseconds(50);
```

## Lifecycle

- Watchers are owned by the screen that created them. Auto-disposed on `MoveToScreen`, `RestartScreen`, or `RestartScreen(RestartMode.HotReload)`.
- File events fire on a background thread; the engine queues them and drains on the game thread during `Update` (right after the pending screen change is flushed, before entity / collision / activity passes).
- If your callback throws `IOException` (file mid-write), the watcher silently retries after the next debounce window. Other exceptions propagate.

## Only files already in the build output are tracked

The engine ignores any file that doesn't already exist in the build output, even if a change event fires for it in the source folder. This filters out editor temp files (Photoshop scratch files, IDE autosaves, lock files) that appear in the content directory but were never copied by MSBuild — they don't trigger your callback, so a directory-wide `RestartScreen` handler isn't fired by editor noise.

**Side effect: brand-new content files require one rebuild before hot-reload notices them.** If you drop a new `enemy.png` into `Content/` and edit it, nothing happens until you rebuild — the rebuild copies it to the output, and from then on edits flow through the watcher. This matches the normal "I added a new asset" workflow (you usually need to rebuild anyway to reference the file from code).

## Gotchas

- **Watch the source folder, not `bin/Debug`.** The engine handles this for you when you use the path-based overloads (`WatchContent("Content/foo.json", ...)`); paths are resolved against `SourceContentRoot`. If you bypass it with the `IFileWatcher` injection overload, you choose the path yourself.
- **Hot-reload is dev-time iteration.** Don't rely on `WatchContent` calls as gameplay logic — in shipping they no-op.
- **In-place reload requires the type/shape to be unchanged.** A schema change in your JSON still requires a screen restart — the live object's fields don't know about new property names.
