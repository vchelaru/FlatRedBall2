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

Dispatch by extension; each path either patches the live object or returns `false` so you can fall back to restart:

```csharp
WatchContentDirectory("Content", rel =>
{
    var ext = Path.GetExtension(rel);
    if (ext == ".json")      ReloadJsonConfig(rel);                              // copy new values onto live object
    else if (ext == ".png")  { }                                                 // engine auto-reloaded before callback
    else if (ext == ".tmx"   && !map.TryReloadFrom("Content/" + rel))            RestartScreen(RestartMode.HotReload);
    else if (ext == ".achx"  && !Animations.TryReloadFrom("Content/" + rel, Engine.Content)) RestartScreen(RestartMode.HotReload);
    else RestartScreen(RestartMode.HotReload);
});
```
<!-- skill-creator: allow-long-csharp reason="canonical dispatch table — collapsing to prose loses the per-type pattern that makes the section useful" -->

- **`.png`** — auto-reloaded before the callback (`AutoReloadAction` → `Engine.Content.TryReload`, patches `Texture2D` pixels in place). Only tracks PNGs loaded via `Engine.Content.Load<Texture2D>("path.png")` (extension required; xnb pipeline loads are not tracked). Opt out: set `watcher.AutoReloadAction = null`. Dimension change silently fails — add an explicit `.png → Engine.Content.TryReload` + restart fallback if you edit resolutions.
- **`.tmx`** — `TileMap.TryReloadFrom` patches tile IDs and rebuilds every TSC registered via `GenerateCollisionFromClass`/`Property`. Returns `false` on structural change (map/layer/tileset diff). Hand-authored mutations on a generated TSC are **wiped** — put augmentations in `CustomInitialize`.
- **`.achx`** — `AnimationChainList.TryReloadFrom` patches by chain name; live `Sprite.CurrentAnimation` references keep playing. Every sprite must share one list instance — per-spawn re-parse defeats this. See the `animation` skill.

### 2. Screen restart — `RestartScreen(RestartMode.HotReload)`

Use when the change invalidates references the game holds.

- **PNG with new dimensions** — must `new` the `Texture2D`, orphans every `Sprite` referencing the old one.
- **Structural TMX changes** — layers added/removed, map resized, object layer modified.
- **Anything you're unsure about** — restart is always safe; in-place can leave dangling refs.

`RestartMode.HotReload` triggers `SaveHotReloadState` / `RestoreHotReloadState` so the player doesn't snap back to spawn. See the `screens` skill for the recipe.

## Source root detection

`FlatRedBallService.SourceContentRoots` is a list of source directories, auto-detected at engine construction:

1. Walk up from `AppContext.BaseDirectory` looking for a `.sln` or `.slnx`. If found, every referenced project that has a `Content/` subdirectory is added to the list — multi-project layouts (e.g. `Common`+`Desktop`+`Web`) just work.
2. If no solution is found, fall back to the first `.csproj` directory walking up (single root).
3. If neither, the list is empty and `WatchContent`* methods no-op.

`WatchContent("Content/foo.json", ...)` and `WatchContentDirectory("Content", ...)` resolve the path against every root and register a watcher per root that contains the path.

Override for unusual layouts:
```csharp
engine.SourceContentRoots.Clear();
engine.SourceContentRoots.Add("C:/path/to/my/project");
```

`OutputContentRoot` defaults to `AppContext.BaseDirectory`. Override only if your build writes content to a non-standard location.

## Shipping builds

In a shipped game there's no `.csproj`/`.sln` next to the executable, so `SourceContentRoots` is empty. `WatchContent`/`WatchContentDirectory` return `null` and skip registration. **No `#if DEBUG` needed** — hot-reload is a dev-only no-op in release.

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

## Only files already in the build output are tracked (with an allowlist)

By default the engine ignores any source-folder change for a file that doesn't already exist in the build output. This filters out editor temp files (Photoshop scratch files, IDE autosaves, lock files) that appear in the content directory but were never copied by MSBuild — they don't trigger your callback, so a directory-wide `RestartScreen` handler isn't fired by editor noise.

**Exception: the auto-copy allowlist.** `ContentDirectoryWatcher.AutoCopyExtensions` defaults to `{ ".png", ".tsx" }`. Files with those extensions flow through even when the destination doesn't exist yet — the engine creates the dest directory, copies the file, and fires the callback. This covers the common "TMX now references a newly-added PNG / tileset" case without requiring a rebuild first.

Customize per-watcher:
```csharp
var w = WatchContentDirectory("Content", reload);
w.AutoCopyExtensions.Add(".ogg");    // opt in a new type
w.AutoCopyExtensions.Remove(".png"); // or opt out of a default
```

Gum file types (`.gumx`, `.gusx`, `.gutx`, `.behx`, `.ganx`) are intentionally excluded — Gum runs its own hot-reload pipeline and doubling up would conflict. Don't add them.

Other extensions still follow the dest-exists gate — a brand-new `.json` or `.tmx` still requires one rebuild before hot-reload notices it (acceptable for non-asset files, since code usually needs to reference them anyway).

## Gotchas

- **Watch the source folder, not `bin/Debug`.** The engine handles this for you when you use the path-based overloads (`WatchContent("Content/foo.json", ...)`); paths are resolved against `SourceContentRoots`. If you bypass it with the `IFileWatcher` injection overload, you choose the path yourself.
- **Hot-reload is dev-time iteration.** Don't rely on `WatchContent` calls as gameplay logic — in shipping they no-op.
- **In-place reload requires the type/shape to be unchanged.** A schema change in your JSON still requires a screen restart — the live object's fields don't know about new property names.
