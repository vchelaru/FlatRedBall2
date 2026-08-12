# Glue loader test fixtures

Vendored snapshots of FlatRedBall 1 Glue project files, used to test the `.gluj`/`.glsj`/`.glej`
loader (see `plan/804-glue-project-loader/`). Committed here rather than read from the sibling
`FlatRedBall` checkout so the tests are self-contained.

## TopDownProject

| | |
|---|---|
| Source repo | `FlatRedBall` (sibling checkout, `C:\git\flatredball`) |
| Source path | `Tests/TestProjectDesktopNet6/TestProjectDesktopNet6/` |
| Synced | 2026-08-07 |
| `FileVersion` | 54 |

The only top-down fixture. `Entities/TopDownMovementEntity.glej` and its
`Content/Entities/TopDownMovementEntity/TopDownValuesStatic.csv` are copied **byte-for-byte**.

**The `.gluj` is the one hand-edited file here**, and only its reference lists: the source project
references 226 elements, of which one is vendored, so `ScreenReferences` is emptied,
`EntityReferences` is trimmed to the single entity, and `StartUpScreen` is cleared. Everything else —
`FileVersion`, display settings, global files — is the real project's.

Chosen deliberately: `TopDownMovementEntity` declares **no `NamedObjects`**, so it sidesteps the
short-form `SourceClassType` problem (below) entirely while still exercising the real `IsTopDown`
property, the real CSV shape, and the real referenced-file wiring.

**Why the rest of that project is not vendored.** It is `FileVersion` 54 and writes `SourceClassType`
unqualified — 110 of its files use a short name, and 7 mix both spellings in the same file, so it is
not a clean version gate. `GlueTypeMap` now aliases the four short names it can construct
(`Sprite`, `AxisAlignedRectangle`, `Circle`, `Polygon`), and the tile predicates already accepted both
forms, so vendoring more of it is now possible — `Screens/TmxScreen.glsj` is the only
`TileNodeNetwork` in all of FRB1, and `Screens/TiledLevelScreen.glsj` the only screen that really
spawns entities from tiles.

## DoorsDemo

| | |
|---|---|
| Source repo | `FlatRedBall` (sibling checkout, `C:\git\flatredball`) |
| Source path | `Samples/Platformer/DoorsDemo/DoorsDemo/` |
| Source commit | `7346ae1cd1d076cebed948b8c1b605489c5efb2f` |
| Synced | 2026-08-01 |
| `FileVersion` | 60 |

Copied **as-is** — do not re-save or hand-edit. Only the Glue project files are vendored, plus the
content later phases actually read; the `.csproj` and generated code are not needed to test the
loader.

**Gum content added for Phase 5** (2026-08-05, same source path, `Content/GumProject/`): the
`.gumx`, its three `.gusx` screens, the `Standards`, and the font cache — 25 files. Deliberately
excluded: `Libraries/bmfont.exe` (an executable has no place in a test fixture) and `EventExport/`
(neither is read at load time). This is the fixture the Gum phase uses rather than
`FormsSampleProject`, because DoorsDemo's `GameScreen` already references a `.gusx` and its `Level1`
is the inheriting case that has no `.gusx` of its own.

Note that Gum's `FileManager` resolves paths against the *app's* `Content` folder, not the Glue
project's — so tests stage this folder next to the test binary. A real game needs no equivalent
step, because its `Content` folder is the Glue project's.

Why this project rather than the two the tracking issue names (ChickenClicker, Beefball): those are
`FileVersion` 42, which sits *below* three gates that change what lands on disk
(`RemoveRedundantDerivedData` 38, `VariantsInsteadOfTypes` 53, `CaseSensitiveLoading` 55). DoorsDemo
at 60 is above all of them, and it also carries two entities, two screens, and a derived screen
(`Level1` sets `BaseScreen`) in one small project.

**Most of this fixture is intentionally unmappable in Phase 1.** Its `NamedObjects` are mostly tile
collision, collision relationships, and a camera controller — all owned by later phases. Loading it
is expected to emit unmapped-type warnings; the bar is zero *errors*.

## Beefball

| | |
|---|---|
| Source path | `Samples/Beefball/Beefball/` |
| Source commit | `7346ae1cd1d076cebed948b8c1b605489c5efb2f` |
| Synced | 2026-08-02 |
| `FileVersion` | 42 |

The project that can actually be *seen* working. It is shapes-only and tile-free, so every one of
its visual objects is a type the loader can already build — unlike DoorsDemo, whose tilemaps and
collision relationships belong to much later phases.

Its version is 42, below the file-shape gates, which makes it a weaker schema fixture than DoorsDemo
but does not affect the objects this phase builds. Use DoorsDemo to test the *reader* and Beefball to
test what the reader *produces*.

## ChickenClicker — deliberately outdated

| | |
|---|---|
| Source path | `Samples/ChickenClicker/ChickenClicker/` |
| Source commit | `7346ae1cd1d076cebed948b8c1b605489c5efb2f` |
| Synced | 2026-08-01 |
| `FileVersion` | 42 |

Vendored **because** it is stale, not in spite of it. At version 42 it sits below three gates that
change what lands on disk, so it is the only fixture that proves the loader degrades with a clear
diagnostic instead of silently misreading an old file. Do not "fix" or upgrade it — its age is the
entire point.

It also carries a populated `CustomClasses` array, which this epic excludes outright, so it doubles
as proof that excluded shapes are tolerated rather than rejected.

## Re-syncing

Re-copy from the same source path and update the commit hash and date above. Do not "upgrade" a
fixture by opening it in Glue: opening and saving does **not** raise `FileVersion` (only new
projects get the current version), and hand-editing the version would make Glue show a version error
to anyone opening that sample.

## Vendored content

`DoorsDemo/Content/` carries a focused slice of the sample's content — not the whole 1.4 MB folder:
the door and player animation chains, the player texture, the platformer CSV, the level's `.tmx` and
its two tilesets.

**This fixture is the regression test for external-tileset image resolution.** `Level1Map.tmx` sits
in `Content/Screens/Level1/` and references `../../StandardTileset.tsx`, whose own image reference is
relative to *the tileset*, in `Content/`. That combination used to fail, and was worked around here by
duplicating the PNG next to the map; the duplicate is gone and `TileMap` now rewrites a tileset's
image paths to be map-relative when it serves the `.tsx`. Re-adding a copy of
`StandardTilesetIcons.png` under `Content/Screens/Level1/` would mask a regression of that fix.
