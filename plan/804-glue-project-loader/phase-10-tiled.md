# Phase 10 — Tiled (TMX)

| | |
|---|---|
| **Initiative** | Load FRB1 Glue projects (`.gluj`/`.glsj`/`.glej`) into FRB2 |
| **Tracking issue** | [vchelaru/FlatRedBall2#804](https://github.com/vchelaru/FlatRedBall2/issues/804) |
| **Status** | Implemented — node networks and tile-spawned entities landed; unsupported creation options are reported, see §9. |
| **Depends on** | Phase 4 (referenced files), Phase 6 (the map is a derived override), Phase 8 (tiles spawn entities) |
| **Blocks** | Nothing |
| **Suggested branch** | `804-phase-10-tiled` |

---

## 1. The problem

DoorsDemo is a platformer whose level *is* a `.tmx` file. Six of its thirteen unmapped-type warnings
are tile types — the largest single block. Until this phase lands, the primary fixture loads a screen
with no ground, no walls, and nothing to stand on.

The work is three separable pieces: load the map, build collision from it, and (optionally) build a
pathfinding network from it.

**Progress metric.** This phase drives DoorsDemo's unmapped count down by **6 (13 → 7)**. Beefball
is unaffected — it has no tile content.

---

## 2. Scope

### In scope

1. A `.tmx` `ReferencedFileSave` → an FRB2 `TileMap`.
2. The `Map` NamedObject in both its abstract and its file-sourced form.
3. `TileShapeCollection` NamedObjects — the `FromType`, `FromProperties`, `FillCompletely`,
   `BorderOutline`, `FromLayer`, and `FromMapCollision` creation options.
4. `TileNodeNetwork` NamedObjects.
5. `CreateEntitiesFromTiles`.

### Out of scope

- `ImplementsITiledTileMetadata` — no FRB2 equivalent and **zero occurrences repo-wide** (G105).
- `.scnx` and `.tilb` as map sources — FRB2 reads only `.tmx`.
- `RepositionUpdateStyle` — no FRB2 equivalent (G103).

---

## 3. Features and stories

| | Feature | The story it serves | Built in |
|---|---|---|---|
| F1 | The map draws | DoorsDemo's level is visible. | §6.2 |
| F2 | Collision exists | The player stands on the ground instead of falling. | §6.3 |
| F3 | Cloud platforms behave | Jump-through platforms are jump-through. | §6.3 |
| F4 | Tiles spawn entities | Doors placed in Tiled appear as `Door` entities. | §6.5 |
| F5 | Pathfinding works | An enemy can navigate the map. | §6.4 |

---

## 4. Proposed resolution

### The map

A `.tmx` RFS carries `RuntimeType: "FlatRedBall.TileGraphics.LayeredTileMap"`. FRB1 loads it with
`LayeredTileMap.FromTiledMapSave(file, contentManager)`
(`AssetTypeInfoAdder.cs:205`); FRB2's equivalent is the `TileMap` constructor
(`src/Tiled/TileMap.cs:94`) — note it takes a `GraphicsDevice` and does **not** go through
`ContentLoader`.

The `Map` NamedObject appears in two shapes, both in DoorsDemo:

| | `GameScreen` (abstract base) | `Level1` (derived) |
|---|---|---|
| `SourceType` | `2` (FlatRedBallType) | **absent** → `0` = `File` |
| `SourceFile` | `FlatRedBall.TileGraphics.LayeredTileMap` | `Screens/Level1/Level1Map.tmx` |
| flags | `SetByDerived: true` | `DefinedByBase: true` |

This is exactly Phase 6's G62 case — the derived must re-instantiate despite `InstantiatedByBase`,
or every level renders the base's placeholder.

### Collision

`CollisionCreationOptions` (`TileShapeCollectionPropertiesViewModel.cs:18-30`) —
`Empty=0, FillCompletely=1, BorderOutline=2, FromProperties=3, FromType=4, FromLayer=5,
FromMapCollision=6`.

DoorsDemo uses `FromType` (4) for both `SolidCollision` and `CloudCollision`, keyed on
`CollisionTileTypeName`.

| Glue | FRB1 | FRB2 |
|---|---|---|
| `FromType` | `AddCollisionFromTilesWithType(tsc, map, type, removeTiles)` | `TileMap.GenerateCollisionFromClass(className)` |
| `FromProperties` | `AddCollisionFromTilesWithProperty(tsc, map, prop)` | `TileMap.GenerateCollisionFromProperty(propertyName)` |
| `FillCompletely` | nested `AddCollisionAtWorld` loops | `TileShapes.AddTileAtCell` loop |
| `BorderOutline` | nested loops | `TileShapes.AddRectangleBorder(colMin, rowMin, colMax, rowMax)` |
| `FromLayer` | `map.Collisions.First(name == "<layer>_<type>")` | `GenerateCollisionFromClass(cls, layerName)` |
| `FromMapCollision` | `map.Collisions.First(name == TmxCollisionName)` | — (G104) |

---

## 5. Gotchas

### G100 — Property-bag defaults are the view-model's `[DefaultValue]`, not `default(T)` · **Blocker**

`TileShapeCollectionCodeGenerator.Get<T>` (`:271-293`) reflects the `DefaultValueAttribute` off the
view-model when a key is absent. FRB2's `PropertySaveExtensions.GetValue<T>` returns `default(T)`
unconditionally (`src/Glue/PropertySaveExtensions.cs:38`).

So a missing `CollisionTileSize` reads `0` where FRB1 reads `16` — and a tile collection with a grid
size of zero produces no geometry at all.

The table this phase needs:

| Key | Default |
|---|---|
| `CollisionTileSize` | `16f` |
| `CollisionFillWidth` | `32` |
| `CollisionFillHeight` | `1` |
| `InnerSizeWidth` | `800f` |
| `InnerSizeHeight` | `600f` |
| `NodeNetworkTileSize` | `16f` |
| `NodeNetworkFillWidth` | `32` |
| `NodeNetworkFillHeight` | `32` |

This is Phase 1's G3 in a third location — bag entries, not members. Same fix, same test shape:
assert on JSON that omits the key.

### G101 — Two "creation options" enums with different numbering

| Value | `CollisionCreationOptions` | `TileNodeNetworkCreationOptions` |
|---|---|---|
| 0 | `Empty` | `Empty` |
| 1 | `FillCompletely` | `FillCompletely` |
| 2 | `BorderOutline` | `FromProperties` |
| 3 | `FromProperties` | `FromType` |
| 4 | `FromType` | `FromLayer` |
| 5 | `FromLayer` | — |
| 6 | `FromMapCollision` | — |

They read from similarly-named keys (`CollisionCreationOptions` / `NetworkCreationOptions`), and a
shared "decode creation options" helper will silently misread one. **Two enums, two decoders, two
pinning tests** — Phase 1's G11 discipline.

### G102 — `RuntimeType` is a weak discriminator; use the extension

`.tmx`, `.scnx`, and `.tilb` **all** carry `RuntimeType: "FlatRedBall.TileGraphics.LayeredTileMap"`.
CSVs carry no `RuntimeType` at all.

FRB1 itself discriminates on extension (`IsCsvOrTreatedAsCsv`, `ReferencedFileSave.cs:453-459`; ATI
lookup by `Extension`). Do the same. Phase 4's dispatch is `(extension, RuntimeType)` and the
extension is the stronger half here.

### G103 — `RepositionUpdateStyle` has no FRB2 equivalent, and DoorsDemo needs it

DoorsDemo's `CloudCollision` carries the instruction `RepositionUpdateStyle = "Upward"` — that is
what makes its platforms jump-through.

FRB2 has no such property. It expresses one-way platforms through the *relationship* instead —
`OneWayDirection` and `CanDropThrough` on `CollisionRelationship` (Phase 9 §4).

**How we tackle it.** Translate the tile-collection property into relationship configuration at the
point the relationship is built, and record the translation in both phase docs. Do **not** add a
property to `TileShapes` that FRB2's collision does not read.

Likewise `AdjustRepositionDirectionsOnAddAndRemove` (always on in FRB2, no opt-out) and `SortAxis`
(moot — FRB2's `TileShapes` is dictionary-keyed by cell).

### G104 — Four FRB1 collision-creation paths have no clean FRB2 target

1. **Return-vs-append.** FRB1 *adds into* a pre-constructed `TileShapeCollection`; FRB2's
   `GenerateCollisionFromClass` **returns a new** `TileShapes`. A Glue project that builds one
   collection from two sources cannot be expressed by chaining.
2. **`FromMapCollision`** reads `map.Collisions` by name. FRB2's `TileMap` has no named-collision
   registry.
3. **`AddMergedCollisionFromTilesWithType`** — the `IsCollisionMerged` variant. No FRB2 merge mode.
4. **`removeTiles`** — FRB1's fourth argument strips the source tiles after building collision.
   `GenerateCollisionFromClass` has no such parameter (though `CreateEntities` does).

Also: `GenerateCollisionFromProperty` does **not** set `Name`, while `GenerateCollisionFromClass`
does (`TileMap.cs:480`) — so a property-built collection is anonymous and cannot be found by name
later.

**How we tackle it.** Implement 1 and 4 as small engine additions if they block a fixture; diagnose 2
and 3. Decide per D100.

### G105 — `ImplementsITiledTileMetadata` has no fixture anywhere

Zero occurrences across every `.glej`/`.glsj`/`.gluj` in FRB1. The codegen does exactly one thing —
append `FlatRedBall.Entities.ITiledTileMetadata` to the generated base list
(`ITiledTileCodeGenerator.cs:13-24`) — and the interface is a single method carrying texture
coordinates, rotation, size, and name.

**FRB2 has neither the interface nor the struct.** Its nearest mechanism is `CreateEntities<T>`'s
reflection pass, which sets Tiled custom properties onto matching public properties and populates an
opt-in `TiledGid` (`src/Tiled/TileMap.cs:536-549`).

**How we tackle it.** Out of scope. Diagnose if the flag is ever set. Note the latent FRB1 bug found
here: the guard method is named `ShouldSerializeImplementsITiledTileSprite()`
(`EntitySave.cs:134`) and does not match its property, so it never fires — harmless only because
`DefaultValueHandling.Ignore` already omits `false`. Worth reporting upstream.

### G106 — `TileNodeNetwork` `FromLayer` + `FromType` is unimplemented in FRB1

`TileNodeNetworkCodeGenerator.cs:213` returns the literal string
`"return new System.NotImplementedException();"` — which is not even valid C#. The ATI's
`GetTileNodeNetworkObjectFromFileFunc` throws `NotImplementedException` for `FromLayer`
(`AssetTypeInfoAdder.cs:411`).

**How we tackle it.** Treat the combination as unsupported and diagnose, matching FRB1. Do not
implement something FRB1 cannot produce.

### G107 — FRB2 has no `TileNodeNetworkCreator`

FRB1 has `CreateFromTypes`, `CreateFromTilesWithProperties`, and `CreateFromEmptyTiles`. FRB2 has
`TileNodeNetwork` and `TileMap` but nothing that builds one from the other.

**How we tackle it.** This is genuinely new engine code — three small builders over
`TileMap.GetLayer` plus `TileNodeNetwork.AddAndLinkNode`. Keep it in `src/AI/` next to the type it
serves, not in `src/Glue/`, so hand-written games can use it too. That is the test of whether it
belongs in the engine at all.

### G108 — The fixtures carry the JSON but not the map

`tests/FlatRedBall2.Tests/Glue/Fixtures/` holds only `.gluj`/`.glsj`/`.glej`. `Level1Map.tmx` is not
vendored, so no test in this phase can run end to end until it is (Phase 4 G49).

No fixture exists at all for: `TileNodeNetwork` (only in FRB1's test project),
`CollisionCreationOptions` other than 4 and 5, or `ImplementsITiledTileMetadata` (nowhere).

---

## 6. Tasks

Test-first throughout.

### 6.1 — Map loading

- [ ] Vendor `Level1Map.tmx` and its tileset images (G108).
- [ ] Failing test: a `.tmx` RFS produces a `TileMap`.
- [ ] Failing test: discrimination is by extension, not `RuntimeType` (G102).
- [ ] Failing test: `Level1`'s `Map` uses its own file, not the base's placeholder (Phase 6 G62).
- [ ] Failing test: `ShiftMapToMoveGameplayLayerToZ0` and `CreateEntitiesFromTiles` are read.

### 6.2 — Bag defaults

- [ ] Failing test: an absent `CollisionTileSize` reads `16`, not `0` (G100) — assert on JSON that
      omits the key.
- [ ] Implement the per-key default table for both view-models.
- [ ] Failing test: both creation-option enums are pinned, and their values differ (G101).

### 6.3 — Collision

- [ ] Failing test: `FromType` with `CollisionTileTypeName: "SolidCollision"` produces geometry.
- [ ] Failing test: `FromProperties` likewise.
- [ ] Failing test: `FillCompletely` and `BorderOutline` produce the right cell counts.
- [ ] Failing test: `FromLayer` decomposes the `<layer>_<type>` naming convention.
- [ ] Failing test: `FromMapCollision` diagnoses as unsupported (G104).
- [ ] Failing test: `RepositionUpdateStyle: "Upward"` translates to one-way relationship config, and
      Phase 9's doc references it (G103).
- [ ] Failing test: `DefinedByBase` suppresses the fill, as FRB1 does — only the base fills.

### 6.4 — Node networks

- [x] Failing test: `FromType` builds a network from a map.
      `BuildObjects_ATileNodeNetworkFromType_HasNodesWhereThatTypesTilesAre` walks every cell and
      asserts a node exists exactly where a tile of that type does — not merely that some were made.
- [x] Failing test: `EliminateCutCorners` is honoured (read from the bag and applied).
- [x] Failing test: an unsupported option diagnoses rather than building nothing
      (`BuildObjects_AnUnsupportedNodeNetworkOption_SaysSoRatherThanBuildingNothing`). A network that
      quietly has no nodes reads as a pathfinding bug, not a missing feature.
- [x] Implement the builders in `GlueTileBuilder.BuildNodeNetwork`.
- [x] ~~Vendor `TmxScreen.glsj`~~ — **not vendored.** FRB1's only `TileNodeNetwork` fixture is in its
      test project, which writes short-form `SourceClassType` and would need hand-editing to vendor
      (see `plan/plan.md`'s fixture caveat). The tests instead add a synthetic `NamedObjectSave`,
      shaped exactly as Glue writes one, to Level1's **real** map: the map, the tile types, the grid
      alignment and every builder path are real, and only the declaration is arranged.

**The network's grid comes from the `TileShapes` the same query produces, not from the map's bounds.**
Deriving it from the map would drift by half a tile wherever the map's origin is not the collection's,
and the drift would only show up as pathfinding that hugs the wrong side of a wall.

### 6.5 — Entity spawning

- [x] Failing test: a tile typed after an entity spawns one per tile, at the tile's centre
      (`CreateEntitiesFromTiles_ATileTypedAfterAnEntity_SpawnsOnePerTile`).
- [x] Tile-spawned entities land in the right list — `GlueProject.InstancesOf`, the same list Phase 8
      tracks and Phase 9 collides against.

**Spawning cannot go through `TileMap.CreateEntities<T>`.** That needs a `Factory<T>`, and every
loaded entity is a `GlueEntity`, so one factory could not tell a Door from a Player — Phase 8 G80
again. It routes through `GlueProject.CreateEntity` instead, which is also what makes the spawned
instances visible to `InstancesOf` and to pooling.

**An `EntitySave` overload of `CreateEntity` came out of this.** Iterating the project's elements and
then looking each name back up repeats work, and fails outright for a save whose name no longer
matches its dictionary key. The public string overload now delegates to it.

**No vendored map paints a tile typed after an entity** — DoorsDemo places its doors as
`NamedObjects`, and its `EntityLayer` objects carry a `gid` with no class attribute. The test pairs a
real entity with a tile type the map really uses, so the tiles, the lookup and the spawn are real and
only the pairing is arranged.

### 6.6 — Wrap-up

- [ ] Failing test: DoorsDemo's unmapped count drops 13 → 7.
- [ ] XML docs; update this document and `plan/plan.md`.
- [ ] Report FRB1's `ShouldSerializeImplementsITiledTileSprite` typo upstream (G105).

---

## 7. Open decisions

| # | Decision | Recommendation |
|---|---|---|
| D100 | Add an append-mode collision API to `TileMap`? | **Yes, if a fixture needs it.** An `AddCollisionFromClass(TileShapes target, …)` overload alongside the existing `Generate*` is small and benefits hand-written games too. Do not add `FromMapCollision` or merge mode — no fixture uses them. |
| D101 | Where do the node-network builders live? | **`src/AI/`, not `src/Glue/`.** If they are only useful to the loader they should not be in the engine at all; putting them next to `TileNodeNetwork` forces that question to be answered honestly. |
| D102 | Implement `ImplementsITiledTileMetadata`? | **No** (G105). Zero occurrences repo-wide. Revisit if a real project sets it. |
| D103 | Give `GenerateCollisionFromProperty` a `Name`? | **Yes — engine fix.** The asymmetry with `GenerateCollisionFromClass` is almost certainly an oversight, and an unnamed collection cannot be looked up later. Small, and it benefits hand-written games. |

---

## 8. Definition of done

- [x] `dotnet build` clean; `dotnet test` green (**1348**).
- [x] **DoorsDemo's `Level1` draws its map** — confirmed by booting it and screenshotting: sky,
      clouds, brick pillars, grass platforms.
- [x] `SolidCollision` and `CloudCollision` build real geometry from their authored tile types.
- [x] Bag defaults are asserted against an object with no bag at all (G100).
- [x] The two creation-option enums are pinned and proven to disagree (G101).
- [ ] The player standing on `SolidCollision` — needs Phase 12.
- [ ] `CloudCollision` as a jump-through platform (G103) — needs Phase 9, which owns the
      relationship configuration this translates into.
- [x] Every gotcha in §5 is covered by a test or explicitly deferred.

---

## 9. What landed

6 new tests, full suite **1348 green**, build clean, and the level confirmed rendering.

| Piece | File |
|---|---|
| Creation-option enums, bag defaults | `src/Glue/GlueTileDefaults.cs` |
| Map and collection building, in dependency order | `src/Glue/GlueTileBuilder.cs` |
| `.tmx` loading and caching | `src/Glue/GlueContentSource.cs` |

**Scope actually delivered:** the map itself, plus `FromType` and `FromProperties` collision — which
is what DoorsDemo uses and what makes a level appear. `FillCompletely`, `BorderOutline`, `FromLayer`
and `FromMapCollision` are decoded and reported as unsupported rather than silently ignored; no
vendored fixture exercises any of them. Tile node networks (§6.4) and entity spawning from tiles
(§6.5) are untouched — the latter needs Phase 8.

### Found while building

- **An engine bug in TMX loading — since fixed.** An external tileset's image resolved against the
  *map's* directory rather than the `.tsx`'s, contradicting the TMX spec and breaking the normal way
  Tiled projects share a tileset between levels. It was recorded in `design/TODOS.md` with the
  fixture carrying a duplicated PNG as a workaround. Both are now gone: `TileMap` rewrites a
  tileset's image paths to be map-relative as it serves the `.tsx`, because the parser takes a single
  scalar base directory and the converter discards the tileset's own location before the image is
  ever loaded. Rewritten paths stay relative — an absolute one would bypass `TitleContainer` and
  break every filesystem-less backend.
- **A tile map is not an `IRenderable`.** It has its own `Screen.Add(TileMap)` overload because it
  owns one layer per Tiled layer. The register callback tested only for `IRenderable`, so the map
  loaded correctly and never drew — a silent failure that no test would have caught, since the tests
  assert on `Objects` rather than on rendering. Caught by looking at the screenshot.
- **Ordering had to be explicit.** A collection is derived from a map, so maps build first. Every
  real project happens to declare them in that order, which is exactly the kind of accident worth
  not depending on (the same reasoning as Phase 9's G92).
- **One Phase 2 test asserted that `LayeredTileMap` is unmapped.** True until this phase; it now
  points at a collision relationship, which Phase 9 still owns.
