# Glue loader test fixtures

Vendored snapshots of FlatRedBall 1 Glue project files, used to test the `.gluj`/`.glsj`/`.glej`
loader (see `plan/804-glue-project-loader/`). Committed here rather than read from the sibling
`FlatRedBall` checkout so the tests are self-contained.

## DoorsDemo

| | |
|---|---|
| Source repo | `FlatRedBall` (sibling checkout, `C:\git\flatredball`) |
| Source path | `Samples/Platformer/DoorsDemo/DoorsDemo/` |
| Source commit | `7346ae1cd1d076cebed948b8c1b605489c5efb2f` |
| Synced | 2026-08-01 |
| `FileVersion` | 60 |

Copied **as-is** — do not re-save or hand-edit. Only the Glue project files are vendored; the
`.csproj`, content, and generated code are not needed to test the loader.

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

**`Content/Screens/Level1/StandardTilesetIcons.png` is a deliberate duplicate** of the copy at
`Content/`. It works around an engine bug where an external tileset's image resolves against the map's
directory rather than the `.tsx`'s — see the entry in `design/TODOS.md`. Delete the duplicate to
reproduce that bug, and delete it permanently once the bug is fixed.
