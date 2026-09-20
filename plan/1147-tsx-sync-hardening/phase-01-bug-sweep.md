# Phase 1 — Bug sweep: native .tsx / achx-push tile-animation sync

Tracking issue: [vchelaru/FlatRedBall2#1147](https://github.com/vchelaru/FlatRedBall2/issues/1147)
Branch: `fix/tsx-native-sync-identity` (single branch, no worktree — every subagent below commits
directly to it).

## What happened

A user removed one animation chain from a native `.tsx` AnimationEditor project and saved. ~15
unrelated tile animations in the real file were relocated or wiped. Root cause: the save path
(`MultiTileToTiledAnimationMapper` for native-tsx, `AchjToTiledAnimationMapper` for achx-push)
recomputed each chain's "owning" Tiled tile id from its first animation frame on *every* save,
instead of preserving whatever tile id the file already assigned. A hand-authored tile whose own
id isn't its own first frame — an entirely ordinary Tiled authoring pattern — got relocated every
save, and `NativeTsxAnimationSync`'s stale-tile-clearing step then wiped the original tile because
it no longer appeared in the freshly recomputed set.

## Process for this phase

This is a long-tail bug sweep, not a single fix. It runs as a loop:

1. Pick the top `TODO` item below.
2. Spawn a `coder` subagent on **this branch** (no worktree) with a self-contained prompt: what the
   suspected gap is, which files are involved, and the instruction to write a failing test first
   (TDD), confirm it fails for the right reason, then fix it, then confirm the full
   `AnimationEditor.Core.Tests` suite is green.
3. When the subagent reports back, verify its summary matches what actually changed (diff review),
   move the item to `DONE` with a one-line note (test name + what was wrong), and commit.
4. If the subagent (or anyone) surfaces a new plausible gap, add it to `TODO` — it does not need to
   be a proven bug yet, just a plausible one worth a test.
5. When `TODO` is empty, do a fresh-eyes pass (self or a subagent) across the whole subsystem
   looking for gap categories not yet considered, add findings to `TODO`, and keep going.

Stop condition: a fresh-eyes pass finds nothing new to add, twice in a row.

## Files in scope

- `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/Tiled/TsxWriter.cs`
- `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/Tiled/TsxCompatibilityChecker.cs`
- `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/Tiled/TsxAnimationValidator.cs`
- `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/Tiled/TiledAnimationToAchjMapper.cs`
- `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/Tiled/MultiTileToTiledAnimationMapper.cs`
- `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/Tiled/NativeTsxAnimationSync.cs`
- `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/Tiled/AchjToTiledAnimationMapper.cs`
- `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/Tiled/TilesetAnimationSync.cs`
- `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/Tiled/TiledTilesetSyncRunner.cs`
- `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/ProjectManager.cs` (native-tsx load/save,
  `_tsxEntryTileIdsByChain` tracking)
- Their respective test files under `tools/AnimationEditorAvalonia/tests/AnimationEditor.Core.Tests/Tiled/`

Run tests with:
```
dotnet test tools/AnimationEditorAvalonia/tests/AnimationEditor.Core.Tests/AnimationEditor.Core.Tests.csproj
```

## DONE

- [x] **Entry-tile-id recompute wipes/relocates hand-authored tiles whose owning tile ≠ their own
  first frame.** Fixed via `TiledAnimationToAchjMapper.Map` returning `entryTileIdsByChain` (keyed
  by chain reference) and `MultiTileToTiledAnimationMapper.Map` accepting it as a
  `knownEntryTileIds` hint; `ProjectManager` tracks and commits it across load/save. Test:
  `NativeTsxProjectRoundTripTests.LoadEditUnrelatedChainSave_OwnerTileNotItsOwnFirstFrame_StaysOnItsOriginalTile`
  (confirmed red without the fix, green with it).
- [x] **Two chains (or a chain's own anchor and satellite) computing the same tile id silently
  overwrite each other.** `NativeTsxAnimationSync.Apply` and `TilesetAnimationSync.Apply` now throw
  `InvalidOperationException` instead. Tests:
  `NativeTsxAnimationSyncTests.Apply_TwoChainsClaimSameEntryTile_ThrowsInsteadOfSilentlyOverwriting`,
  `Apply_SatelliteCollidesWithAnotherChainsAnchor_ThrowsInsteadOfSilentlyOverwriting`,
  `TilesetAnimationSyncTests.Apply_TwoChainsInSameSourceClaimSameEntryTile_ThrowsInsteadOfSilentlyOverwriting`.
- [x] **`TilesetAnimationSync` doc comment claims cross-source isolation but the overwrite step had
  no ownership check** — a second achx source could silently clobber a tile a different achx source
  already owned. Fixed with an ownership check that skips + warns instead. Test:
  `TilesetAnimationSyncTests.Apply_TileOwnedByDifferentSource_IsSkippedNotOverwritten`.
- [x] **`SetOrRemoveStringProperty`/`SetOrRemoveIntProperty`/`SetStringProperty` matched an existing
  property by name *and* CLR type**, so a hand-authored property with the right name but wrong type
  (e.g. `Name` written as an int) was invisible to the filter and a second, correctly-typed property
  got added alongside it — two `<property name="Name">` entries, invalid Tiled XML. Fixed to match
  by name first and replace regardless of type. Tests:
  `NativeTsxAnimationSyncTests.Apply_ExistingNamePropertyHasWrongType_IsReplacedNotDuplicated`,
  `Apply_ExistingParentIdPropertyHasWrongType_IsReplacedNotDuplicated`,
  `TilesetAnimationSyncTests.Apply_ExistingAnimationNamePropertyHasWrongType_IsReplacedNotDuplicated`.
- [x] **`TiledAnimationToAchjMapper.GetParentId` unchecked-cast a negative `ParentId` int into a
  huge `uint`** (-1 → 4294967295), sending lookups into nonsense territory instead of failing
  loudly. Now treated as "no ParentId." Test:
  `TiledAnimationToAchjMapperTests.Map_NegativeParentId_TreatedAsAnchorNotUncheckedCastToHugeId`.

- [x] **Brand-new chain's entry-tile id must stay stable across repeated saves.** Already correct —
  `ProjectManager.SaveTsxProject`'s post-save bookkeeping loop commits `mapped`'s `EntryTileId`
  (keyed by `SourceChain` reference) into `_tsxEntryTileIdsByChain` every save, so a chain's
  first-save id feeds back in as the next save's `knownEntryTileIds` hint. No source change; test
  added to pin the behavior (confirmed red when the hint was stubbed out, green against real code):
  `ProjectManagerTsxProjectTests.SaveTsxProject_BrandNewChain_EntryTileIdStaysStableAcrossRepeatedSaves`.
- [x] **Multi-tile group footprint shrinks or grows across a save.** Already correct --
  `NativeTsxAnimationSync.Apply`'s stale-vs-new tile id set diff (computed fresh from
  `results` every call) naturally drops a satellite no chain claims anymore and creates one a
  chain newly needs, and `ApplyTile`/`ClearTile` only ever touch the `Animation`/`Name`/`ParentId`
  properties this sync owns, leaving unrelated hand-authored properties on the affected tile
  alone in both directions. No source change; tests added to pin the behavior:
  `NativeTsxProjectRoundTripTests.LoadShrinkGroupFootprintSave_UnusedSatelliteCleared_ButUnrelatedPropertyKept`,
  `LoadGrowChainFootprintSave_NewSatelliteCreated_ButUnrelatedPropertyOnExistingTileKept`.

## TODO

- [ ] **Two different multi-tile chains have overlapping satellite/anchor footprints in the
  spritesheet** (legitimate geometry collision, not an authoring mistake) — confirm this is caught
  by the same collision detection as the identical-entry-tile case, with a clear message identifying
  both chains.
- [ ] **`TsxWriter` tile ordering**: when the original `.tsx` file's `<tile>` elements are *not* in
  ascending id order (Tiled doesn't strictly guarantee this), does
  `NativeTsxAnimationSync`/`TilesetAnimationSync`'s `tileset.Tiles.Sort(...)` cause every untouched
  tile to move position in the file (pure reordering diff, not a content bug, but defeats the
  "minimal diff" goal `TsxWriter`'s own doc comment promises)? Pin current behavior with a test; fix
  if cheap, otherwise document as a known limitation with a reason.
- [ ] **`TilesetAnimationSync`/achx-push against a tsx with pre-existing hand-authored animations
  and *no* prior `.tiledsync` association** (the very first sync ever run against a real file) —
  same shape as the native-tsx root cause, but for the achx-push path specifically. Confirm whether
  the "recompute vs. what's on disk" mismatch can also silently clobber hand-authored content here,
  not just cross-source content.
- [ ] **`TiledTilesetSyncRunner.SyncAll` partial-batch failure ordering**: if tsx #2 in the
  association list throws, are any writes already made to tsx #1 in the same batch left in a
  correct, self-consistent state (not half-applied)? Should already be fine (each tsx is
  independent) — write a test that pins it rather than assuming.
- [ ] **`TsxAnimationValidator` orphaned-`ParentId` satellite**: `TiledAnimationToAchjMapper.Map`
  silently drops a satellite tile's animation data from the editable model entirely when its
  `ParentId` doesn't resolve to an animated anchor (the validator flags it as a UI warning, but the
  data is still gone from what the user can edit/save). Decide and pin whether this is acceptable
  (warn-and-drop) or whether the tile should surface as its own independent chain instead so no data
  is silently unrecoverable.
- [ ] **Satellite tile's own `<animation>` content is never read on load** — only its static grid
  position relative to the anchor is trusted (`TiledAnimationToAchjMapper.Map`, satellites branch).
  A hand-edit to a satellite's frames directly in Tiled would be silently discarded on the next
  native-tsx load+save. Pin this as documented/intended behavior with an explicit test (not a
  behavior change) so it can't regress into something worse (e.g. corrupting instead of ignoring).
- [ ] **Zero-frame chain that used to have an entry tile** (user deletes all frames from a chain but
  doesn't delete the chain itself) — confirm this correctly clears the previously-owned tile on
  save, same as deleting the chain outright.
- [ ] **Rename a chain that has multi-tile satellites** — confirm the satellite's `ParentId` still
  resolves correctly and no satellite gets orphaned/cleared as a side effect of the anchor's identity
  being preserved-by-reference now.
- [ ] **`GetChainNamesWithTsxIssues` and `SaveTsxProject` must agree on entry tile ids** — now that
  both pass `_tsxEntryTileIdsByChain`, confirm with a `ProjectManager`-level test (not just the
  mapper) that the validator-driven UI warning list never disagrees with what an actual save would
  do.
- [ ] **Corrupt/negative or out-of-range values elsewhere**: audit `MultiTileToTiledAnimationMapper`
  and `AchjToTiledAnimationMapper` for other unchecked casts or unvalidated arithmetic on
  attacker/corruption-controlled input (frame rects producing negative or huge tile ids from
  malformed achx coordinates), similar in spirit to the `ParentId` fix above.
- [ ] **Fresh-eyes pass #1**: once the above are done, do a dedicated pass (self or subagent)
  re-reading every file in scope end to end asking "what haven't we tried yet" — new categories to
  consider: concurrent edits (two `ProjectManager` instances / two AnimationEditor windows open on
  the same tsx), very large tilesets (performance, not just correctness), non-Latin/unicode chain
  names round-tripping through the `Name` property, and the achj (JSON) vs achx (XML) serialization
  paths for anything this phase touches.
