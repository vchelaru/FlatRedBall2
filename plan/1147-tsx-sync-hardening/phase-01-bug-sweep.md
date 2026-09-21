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

Deliberately expanded once, for the `TabEditorCache` fix below: that item's root cause (a
long-lived `ProjectManager` with private tsx state no cache layer could see) followed directly
from the just-fixed `LoadAnimationChain` leak and was reachable via an everyday action (switching
tabs), so it was fixed in-sweep rather than left open -- see its DONE entry for the touched files
outside this original list (`IProjectManager.cs`, `Models/TabEditorCache.cs`, `Models/TabEntry.cs`,
plus the `IProjectManager` test fakes that had to grow the two new interface members).

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

- [x] **Two different multi-tile chains have overlapping satellite/anchor footprints in the
  spritesheet.** Already correct — `ValidateNoTileIdCollisions` claims every result's entry tile id
  and every satellite tile id generically in one pass, so a satellite-vs-satellite collision throws
  the same way anchor-vs-anchor already did, and the message names both chains and the tile id. No
  source change; test added to pin the behavior:
  `NativeTsxAnimationSyncTests.Apply_TwoMultiTileChainsSatellitesCollide_ThrowsNamingBothChainsAndTileId`.
- [x] **`TsxWriter` tile ordering.** Already correct, and not fixable-as-cheap even for the
  cosmetic reordering — `TryWritePatchedCore` keys `originalSlicesById`/`originalTilesById` by tile
  id (dictionaries built from the original file's document order), and the final emission loop
  walks `tileset.Tiles` (whatever order the caller sorted it into) doing id lookups into those
  dictionaries. Slicing itself (`tileLineStarts`, `prologueEnd`, `closingTagOffset`) is computed
  purely from original-file line positions and never touches `tileset.Tiles`' order, so an
  ascending `Sort()` before `Write` is order-independent for correctness: every unchanged tile's
  original text is reused byte-for-byte, just re-emitted in the new (sorted) position — no content
  loss, merging, or corruption. The known cosmetic side effect (an out-of-order original file
  produces a reordering diff for untouched tiles, defeating the "minimal diff" goal for that one
  save) is not trivial to avoid: it would require patch mode to preserve each unchanged tile's
  *original* position while only relocating changed/new tiles, which means diffing two id orderings
  and deciding insertion points for new ids — real restructuring, not a one-line fix — so left as a
  known limitation. Test:
  `TsxWriterTests.Write_OriginalFileTilesNotInAscendingIdOrder_SortBeforeWriteReusesSlicesReorderedNotCorrupted`.
- [x] **`TilesetAnimationSync`/achx-push against a tile with a pre-existing hand-authored animation
  and no prior achx association.** Real bug, confirmed red before the fix — the overwrite loop's
  ownership check only fired when `achjSourceFile` was set to a *different* source; a tile with no
  `achjSourceFile` at all (hand-authored, untracked) fell through and got its animation silently
  clobbered whenever an achx chain's geometry happened to compute the same entry tile id. Fixed by
  also skipping+warning when `achjSourceFile` is unset but the tile already has a non-empty
  `Animation`. Test:
  `TilesetAnimationSyncTests.Apply_TileHasHandAuthoredAnimationNoSourceProperty_IsSkippedNotOverwritten`.
- [x] **`TiledTilesetSyncRunner.SyncAll` partial-batch failure ordering.** Already correct — each
  loop iteration's `tileset`/`results` are per-iteration locals and `TsxWriter.Write` for tsx #1
  fully completes before tsx #2's iteration starts, so tsx #2 throwing can't touch tsx #1's already
  -written file. No source change; test added to pin the behavior:
  `TiledTilesetSyncRunnerTests.SyncAll_SecondTsxInBatchThrows_FirstTsxWriteAlreadyOnDiskStaysFullyCorrect`.
- [x] **`TsxAnimationValidator` orphaned-`ParentId` satellite**: real bug, confirmed red first —
  `Map_ParentIdDoesNotResolveToAnimatedTile_SurfacesAsItsOwnChainInsteadOfDropped` failed against the
  old code (`Assert.Single()` on an empty chain collection), proving the tile's animation data was
  completely absent from the returned model, not just unedited. Decision: **option 2, surface as its
  own independent chain**, not warn-and-drop. Reasoning: the fix is small and localized (only changes
  which tiles count as anchor-eligible in `TiledAnimationToAchjMapper.Map` — a tile whose `ParentId`
  doesn't resolve to an animated tile is now treated the same as having no `ParentId`, matching the
  existing negative-`ParentId` precedent in the same method); it doesn't touch
  `TsxAnimationValidator`, which keeps flagging the dangling `ParentId` as a warning so the user still
  learns their group is broken; and on the next save, `MultiTileToTiledAnimationMapper`/
  `NativeTsxAnimationSync.ApplyTile` writes the now-standalone chain with `parentId: null`, which
  `SetOrRemoveIntProperty` clears from the tile — so the stale `ParentId` property doesn't linger as
  half-broken state, it's cleanly removed once the tile is no longer a satellite. The "confusingly
  auto-fixes a Tiled authoring mistake" downside is real but minor: the tile keeps its exact
  animation, just as its own chain instead of vanishing, and the validator warning still tells the
  user their intended grouping didn't take effect. Test:
  `TiledAnimationToAchjMapperTests.Map_ParentIdDoesNotResolveToAnimatedTile_SurfacesAsItsOwnChainInsteadOfDropped`.
- [x] **Chained/nested `ParentId` (satellite-of-a-satellite) silently drops the innermost tile**:
  real bug, confirmed red first —
  `Map_ChainedParentId_SatelliteOfASatelliteSurfacesAsItsOwnChainInsteadOfDropped` failed against
  the old code (only 1 chain instead of 2 — tile C's data completely absent). Decision: **option
  (b), treat a chained ParentId as invalid** (same "structurally-invalid ParentId is treated as no
  ParentId" pattern as the orphan fix above) rather than resolving it to its ultimate root anchor —
  a satellite-of-a-satellite has no corresponding multi-tile-group shape AnimationEditor's own UI
  could ever produce (a footprint is always one simple rectangle relative to a single anchor), and
  AnimationEditor's UI never writes chained `ParentId` in the first place, so there's no real
  semantics to preserve by resolving the chain. Fixed in
  `TiledAnimationToAchjMapper.Map` by replacing the "resolves to any animated tile" anchor-eligibility
  check with "resolves to a *true* (unchained) anchor" — `trueAnchorTileIds` now holds only animated
  tiles with no `ParentId` of their own, so a tile whose `ParentId` points at a tile that is itself a
  satellite falls through to `IsAnchor` the same way an orphaned/unresolvable `ParentId` already did.
  Also fixed `TsxAnimationValidator`, which previously did not detect this case at all — a chained
  `ParentId` resolves to an *animated* tile, so the existing "does not reference an animated tile"
  check passed, and the per-frame lockstep check happened to also pass whenever the chained tile's
  frames matched its immediate (non-root) parent's frames, meaning the validator could report zero
  issues for a group the mapper was silently splitting apart. Added a check: if the referenced
  anchor tile itself has a `ParentId` that resolves to an animated tile, flag it as "itself a
  satellite (chained/nested ParentId) rather than a true anchor." Tests:
  `TiledAnimationToAchjMapperTests.Map_ChainedParentId_SatelliteOfASatelliteSurfacesAsItsOwnChainInsteadOfDropped`,
  `TsxAnimationValidatorTests.Validate_ChainedParentId_ReferencesTileThatIsItselfASatellite_ReturnsIssue`.
- [x] **`TiledTilesetSyncRunner.SyncAll`'s per-tsx `catch (Exception ex)` is a blanket catch.**
  Traced the full chain: `Error` is the raw `Exception` instance passed straight into
  `FailureOutcome` (no stringify/flatten), consumed by `AppCommands.SyncAssociatedTiledTilesets`
  (which forwards it unchanged via `TiledSyncFailed`), and finally displayed in
  `MainWindow.axaml.cs` (`AnimationEditor.App`, not `.Core`) as
  `$"{fileName}: {ex.Message}"` — message only, no exception type name. That last hop is outside
  this sweep's scope (`AnimationEditor.Core` only, per "Files in scope" above) and outside what
  `AnimationEditor.Core.Tests` can exercise; it's also arguably fine as-is, since a collision's
  `InvalidOperationException` message already names both chains and the tile id while a genuine bug's
  message (e.g. an NRE's "Object reference not set...") reads nothing like it, even without the type
  name printed. Confirmed the testable core at the `.Core` layer: `SyncAll` never flattens the
  exception before storing it, so type + message survive intact for any future consumer that wants
  to branch on exception kind. Test (pinning, not a fix):
  `TiledTilesetSyncRunnerTests.SyncAll_TwoChainsClaimSameEntryTile_OutcomeErrorPreservesExactExceptionTypeAndMessage`.
- [x] **Satellite tile's own `<animation>` content is never read on load.** Confirmed
  documented/intended, not a bug: `TiledAnimationToAchjMapper.Map` folds a satellite's frames from
  its anchor + its own static grid offset, never from the satellite's on-disk `Animation`; on save,
  `NativeTsxAnimationSync.ApplyTile` always writes that anchor-derived sequence back onto the
  satellite tile, overwriting whatever was there. Verified the safety net that makes this
  acceptable ("ignore, not corrupt") actually fires for a hand-edited satellite with a shorter frame
  count than its anchor: `TsxAnimationValidator.Validate` reports the count-mismatch issue on the
  originally-loaded tileset (a validator branch that existed but had no dedicated test), the bad
  on-disk frame data never leaks into the mapped `AnimationChainListSave`, and a save without
  addressing the warning overwrites the satellite to the correctly-derived sequence rather than
  leaving the bad data or crashing. Confirmed the test has teeth by temporarily disabling the
  satellite-overwrite loop in `NativeTsxAnimationSync.Apply` and observing the assertion fail with
  the original (999ms, 1-frame) data instead of the derived one, then reverting. No source change.
  Test:
  `NativeTsxProjectRoundTripTests.LoadMapApplySave_SatelliteHandEditedFramesInconsistentWithAnchor_IgnoredOnLoadWarnedByValidatorOverwrittenOnSave`.
- [x] **Zero-frame chain that used to have an entry tile.** Already correct --
  `MultiTileToTiledAnimationMapper.MapChain`'s `Frames.Count == 0` early return sets `EntryTileId =
  null` before `knownEntryTileIds` is ever consulted, so `NativeTsxAnimationSync.Apply` naturally
  drops that tile into the `previouslyAnimatedTileIds.Except(newTileIds)` stale-clearing path, and
  `ProjectManager.SaveTsxProject`'s post-save bookkeeping loop only re-adds a chain to
  `_tsxEntryTileIdsByChain` when `EntryTileId` is non-null, so the stale hint is dropped rather than
  lingering for a later re-populated save to wrongly reuse. Confirmed the test has teeth by
  temporarily reverting that rebuild-from-scratch loop to an in-place mutate-without-removing
  version and observing the assertion fail (stale tile-0 id reused instead of freshly computed
  tile-4 id), then reverting. No source change. Test:
  `ProjectManagerTsxProjectTests.SaveTsxProject_AllFramesDeletedFromChain_ClearsPreviouslyOwnedTileAndDoesNotStickOnResave`.

- [x] **Owner-not-own-first-frame anchor combined with a multi-tile satellite relocates the
  satellite on save, unlike the anchor.** Real bug, confirmed red first -- a load-save-with-zero-edits
  test (`LoadSaveWithNoEdits_OwnerNotFirstFrameAnchorWithMultiTileSatellite_SatelliteStaysOnItsOriginalTile`)
  failed against the old code with `KeyNotFoundException: Property 'ParentId' not found` on the
  reloaded tile: the fix from the very first DONE item above only preserves the *anchor's* tile id
  via `knownEntryTileIds` when the anchor's own id isn't its own frame-0 tile; a satellite had no
  equivalent hint, so its tile id was always freshly computed as the anchor's frame-0 position plus
  the satellite's (dx, dy) offset -- a different base than `TiledAnimationToAchjMapper.Map`'s load-side
  offset (derived from the anchor's and satellite's *static* tile ids), so the two bases differed by
  a fixed delta and the satellite silently drifted to a new tile id every save, orphaning the
  original. Fixed with the same hint pattern as the anchor, extended to satellites:
  - `TiledSatelliteMapping` gained an `Offset` field (`(int Dx, int Dy)`) alongside `TileId`/`Frames`,
    so a satellite's position within the footprint is explicit data instead of only recoverable from
    iteration order.
  - `TiledAnimationToAchjMapper.Map` gained a second `out` parameter, `satelliteTileIdsByChain`
    (`IReadOnlyDictionary<AnimationChainSave, IReadOnlyDictionary<(int Dx, int Dy), uint>>`) -- each
    chain's satellites' actual on-disk tile ids, keyed by chain reference then by (dx, dy) offset from
    the anchor's *static* position (mirrors `entryTileIdsByChain`'s shape one level down, since a
    chain can have more than one satellite in a 2xN/NxM footprint).
  - `MultiTileToTiledAnimationMapper.Map`/`MapChain` gained a matching optional `knownSatelliteTileIds`
    parameter; when a chain+offset pair has a hint, that tile id wins over the freshly-computed
    anchor-frame-0-plus-offset id.
  - `ProjectManager` gained `_tsxSatelliteTileIdsByChain`, populated on `LoadTsxProject` from the new
    `out` parameter and threaded into `SaveTsxProject`'s and `GetChainNamesWithTsxIssues`'s
    `MultiTileToTiledAnimationMapper.Map` calls exactly like `_tsxEntryTileIdsByChain` already was;
    `SaveTsxProject`'s post-save commit-forward loop was extended to also rebuild
    `_tsxSatelliteTileIdsByChain` from each result's `Satellites` (keyed by `Offset` -> `TileId`) so a
    brand-new satellite's first-save id stays stable on later saves the same way a brand-new chain's
    entry id already did.
  Confirmed the achx-push path (`AchjToTiledAnimationMapper`/`TilesetAnimationSync`) can't have this
  bug: it's single-cell-only by design (grepped both files for `Satellite`/`ParentId` -- zero matches),
  so there's no satellite-offset computation for this class of drift to affect. Test:
  `NativeTsxProjectRoundTripTests.LoadSaveWithNoEdits_OwnerNotFirstFrameAnchorWithMultiTileSatellite_SatelliteStaysOnItsOriginalTile`.

- [x] **`TsxAnimationValidator`'s per-frame lockstep check only compared `TileID`, never
  `Duration`.** Real gap, confirmed red first --
  `Validate_SatelliteDurationOutOfLockstep_ReturnsIssue` failed against the old code
  (`Assert.Single()` on an empty issue list) for a satellite with the correct tile-id sequence but
  a hand-edited duration on one frame. Decision: **add the `Duration` comparison**, matching the
  recommendation -- it's a small, consistent extension of a check that already exists for exactly
  this purpose (warning the user their hand-edit will be silently discarded), doesn't touch the
  "ignore, don't corrupt" load/save design at all, and closes a real coverage gap with the same
  shape as the tile-id-mismatch check right next to it. No plausible false-positive scenario found:
  every legitimate satellite is expected to match its anchor's duration per frame exactly, same as
  it must match tile id. Fixed in `TsxAnimationValidator.Validate`'s per-frame loop, which now also
  compares `tile.Animation[i].Duration` against `anchor.Animation[i].Duration` and reports
  `"tile {id}: frame {i} has duration {actual}, expected {expected} to stay in lockstep with anchor
  tile {anchorId}."` (same message shape as the existing tile-id check). No existing test
  regressed -- the "satellites match" tests already used identical durations across anchor and
  satellite by construction. Test:
  `TsxAnimationValidatorTests.Validate_SatelliteDurationOutOfLockstep_ReturnsIssue`.
- [x] **Rename a chain that has multi-tile satellites.** Already correct -- a satellite's tile id
  and `ParentId` are derived purely from frame geometry (never from the chain's `Name`), and a
  rename touches no frame geometry, so both the anchor's tile id and the satellite's `ParentId`
  are trivially stable across a rename; the only real risk was the anchor's `Name` property
  actually getting updated to the new value in place (not left stale, not duplicated) via the
  reference-keyed `knownEntryTileIds` hint, which the test confirmed has teeth by temporarily
  forcing `explicitName: null` in `NativeTsxAnimationSync.Apply` and observing the reload assert
  fail (`KeyNotFoundException: Property 'Name' not found`), then reverting. No source change.
  Test: `NativeTsxProjectRoundTripTests.LoadRenameChainWithMultiTileSatelliteSave_AnchorNameUpdatedInPlace_SatelliteParentIdStaysOnSameAnchor`.

- [x] **`GetChainNamesWithTsxIssues` and `SaveTsxProject` must agree on entry tile ids.** Already
  correct -- both already pass the same `_tsxEntryTileIdsByChain`/`_tsxSatelliteTileIdsByChain`
  hints into `MultiTileToTiledAnimationMapper.Map`. Confirmed with a `ProjectManager`-level test
  using an owner-not-first-frame anchor (tile 9, frame 0 is tile 8) with a satellite hand-edited
  out of lockstep: `GetChainNamesWithTsxIssues()` names the chain via its hint-derived entry tile
  id (9), and a subsequent `SaveTsxProject()` keeps writing to that same tile 9 (not the
  frame-0-derived 8) while correcting the satellite. Confirmed the test has teeth by temporarily
  passing empty dictionaries in `GetChainNamesWithTsxIssues`'s `Map` call and observing both new
  tests fail (the hint-less recompute lands on tile 8, no longer matching the validator's
  anchor-tile-9 issue), then reverting -- no source change. Tests:
  `ProjectManagerTsxValidationIssuesTests.GetChainNamesWithTsxIssues_ThenSaveTsxProject_AgreeOnEntryTileIdForFlaggedChain`,
  `GetChainNamesWithTsxIssues_AfterSaveFixesLockstep_ReturnsEmpty`.

- [x] **Corrupt/negative or out-of-range values elsewhere.** Audited
  `AchjToTiledAnimationMapper.MapFrame`, `MultiTileToTiledAnimationMapper.MapChain`,
  `TiledAnimationToAchjMapper.Map`, and `TsxAnimationValidator.Validate` for unchecked casts/
  unvalidated arithmetic on corruption-controlled input. Two real bugs found and fixed, one
  category already safe:
  - **achx-side: a frame rect origin that's an exact negative multiple of the tile size** (e.g.
    `LeftCoordinate = -16` with a 16px tile) passes the existing grid-alignment check (`left %
    tileWidth == 0` -- C#'s `%` keeps the dividend's sign, so a negative-but-aligned value has
    remainder 0) yet resolves to a negative column/row, which both mappers then unchecked-cast to
    `uint`, wrapping to 4294967295 -- same bug shape as the already-fixed `ParentId` cast. Fixed by
    checking `column < 0 || row < 0` (resp. `originColumn`/`originRow`) right after computing them
    and skipping (achj mapper: per-frame, new `SkipCounts.NegativeCoordinate` bucket) or aborting
    the whole chain with a warning (multi-tile mapper, matching its existing per-chain-abort
    pattern for other geometry failures) instead of proceeding to the cast. Tests:
    `AchjToTiledAnimationMapperTests.Map_NegativeAlignedCoordinate_SkipsFrameAndWarnsInsteadOfUncheckedCastToHugeId`,
    `MultiTileToTiledAnimationMapperTests.Map_NegativeAlignedFrameOrigin_SkipsChainAndWarnsInsteadOfUncheckedCastToHugeTileId`.
  - **Tiled-side: `Columns <= 0`** in a corrupt/hand-edited tsx. Both `TiledAnimationToAchjMapper.Map`
    and `TsxAnimationValidator.Validate` do `tileId % columns` / `tileId / columns` unconditionally;
    `Columns == 0` throws an unhandled `DivideByZeroException` (uint division/modulo by zero
    throws, unlike float), and a negative `Columns` unchecked-casts to a huge `uint` divisor,
    silently misplacing every tile instead of crashing. Neither was previously guarded --
    confirmed both crash for the right reason before fixing. Fixed with an
    `InvalidOperationException` guard at the top of each method (`Columns <= 0` -> throw with a
    clear message), matching the existing "fail loud, not corrupt" `InvalidOperationException`
    precedent in `NativeTsxAnimationSync`. Verified this integrates cleanly with the existing UI
    error path: `AppCommands.OpenTsxWorkflowAsync` already wraps `LoadTsxProject` in a blanket
    `catch (Exception ex)` that surfaces `ex.Message` to the user, and `TsxAnimationValidator` is
    only ever called on a tileset that already loaded successfully (so its own guard is
    defense-in-depth for any future/independent caller, not reachable via the current
    `ProjectManager` flow). Tests:
    `TiledAnimationToAchjMapperTests.Map_ColumnsIsZero_ThrowsInsteadOfDivideByZero`,
    `TsxAnimationValidatorTests.Validate_ColumnsIsZero_ThrowsInsteadOfDivideByZero`.
  - **Already safe: `MultiTileToTiledAnimationMapper`'s footprint-size computation.** A negative or
    zero `firstRect.Width`/`Height` (e.g. from a corrupt achx with `RightCoordinate < LeftCoordinate`)
    is already caught by the existing `footprintColumns < 1 || footprintRows < 1` check before any
    cast happens -- no gap here, no test added (already covered by
    `Map_FrameSizeNotWholeMultipleOfTile_SkipsChainAndWarns`'s existing coverage of that guard).

## TODO

Traced, not added as new TODO items (fresh-eyes pass #1, see DONE below for the full reasoning):
same-chain satellites colliding on `(Dx, Dy)` (structurally impossible — traced), concurrent
`ProjectManager` instances / static state (only `TileMapInformationList`, unrelated to tsx sync;
`_tsxEntryTileIdsByChain`/`_tsxSatelliteTileIdsByChain`/`_tsxTileset` are plain instance fields),
non-Latin/unicode chain names (plain string passthrough, no char-level manipulation anywhere in this
subsystem), achj-vs-achx serialization interaction with the entry/satellite tracking dictionaries
(confirmed these never touch disk in either format).

Traced, not added as new TODO items (fresh-eyes pass #2, see DONE below for the full reasoning):
`TsxAnimationValidator.Validate` is O(n) not O(n²) (its two dictionary builds/lookups are all O(1)
per tile, same shape as the already-fixed sync classes); duplicate chain names in an
`AnimationChainListSave` can't be conflated anywhere in this subsystem (`TiledAnimationToAchjMapper`
keys its identity-tracking dictionaries by chain *object reference*, and
`AchjToTiledAnimationMapper`/`MultiTileToTiledAnimationMapper`'s results are plain lists, never
keyed by name); a duplicate tsx path appearing twice in `TiledTilesetSyncRunner.SyncAll`'s
association list is harmless -- the second iteration reloads the now-already-synced file fresh,
computes an identical `Apply` result, and `TilesetAnimationSyncResult.Changed` comes back `false`,
so `TsxWriter.Write` is never called a second time (already covered by this subsystem's existing
"reapplying an unchanged sync is a no-op" tests, so no new pinning test added); `MainWindow.axaml.cs`'s
`SyncTsxValidationIssuesIntoTree` calls `GetChainNamesWithTsxIssues` (and therefore
`TsxAnimationValidator.Validate`, which can throw `InvalidOperationException` for `Columns <= 0` or a
duplicate tile id) with no surrounding try/catch, but this is unreachable in practice now that
`LoadTsxProject` is all-or-nothing (see DONE below): those corruption conditions can only exist in a
tileset that was never successfully loaded in the first place, and nothing in this editor's UI can
introduce a duplicate tile id or change `Columns` after a successful load.

- [x] **`ProjectManager.ReferencedPngs` has the same "reused-instance state leaks across tab
  switches" shape as `_tsxTileset`/`_knownTextureSizes`, but only for the `TabEditorCache` cache-hit
  path (`TryActivateTabFromCache`).** Fixed in fresh-eyes pass #6 (see below) -- pass #5's severity
  assessment ("only consumer never called on tab activation") turned out to be wrong: `AppCommands.
  TryActivateTabFromCache` itself raises `AvailableTexturesChanged` (line 384), which is wired
  straight to `MainWindow.RefreshTextureCombo`, so the stale dropdown *does* refresh on every
  cache-hit tab switch, not just after a texture drag-drop/resize. Fixed by making
  `IProjectManager.ReferencedPngs` settable and round-tripping it directly through
  `TabEditorCache.CaptureFromProject`/`ApplyToProject` (a new `TabEntry.CachedReferencedPngs`
  field) -- simpler than the opaque-snapshot pattern used for `CaptureTsxState`/
  `CaptureTextureSizeState`, since `FilePath[]` is already a public interface type with nothing to
  hide. Test:
  `TabSwitchCacheReferencedPngTests.TryActivateTabFromCache_SwitchBackAfterAnotherTabLoaded_RestoresThisTabsReferencedPngsNotTheOtherTabs`.

- [x] **Fresh-eyes pass #5.** Investigated both of pass #4's suggested targets to a firm conclusion,
  re-read `TiledAnimationToAchjMapper.cs`'s anchor/satellite/footprint logic end to end for internal
  consistency, and audited every private/near-private `ProjectManager` field against the
  `TabEditorCache` round-trip pattern. Found and fixed one new real, confirmed bug (below, a third
  instance of the exact leak class pass #4 fixed twice already), added one new TODO item (above, a
  narrower/lower-severity sibling of that same bug class), and reached firm conclusions on both
  suggested targets -- neither needed a source change:
  - **Target 1 (`AchjToTiledAnimationMapper.MapFrame`'s per-frame skip semantics) -- firm
    conclusion: intentional, not a gap.** `ChainMappingResult.EntryTileId` is documented as "the
    first non-skipped frame's tile id," and that's correct given achx-push's actual design: unlike
    native-tsx (which threads `knownEntryTileIds`/`knownSatelliteTileIds` hints through
    `ProjectManager` specifically to preserve a tile's identity save over save), achx-push has no
    identity-preservation concept at all -- `TilesetAnimationSync`'s own
    `Apply_RenamedChainMovesToDifferentTile_ClearsOldTileAndPopulatesNewTile` test already
    establishes that an achx-push chain simply follows wherever its geometry currently points to,
    full stop. A chain whose frame 0 becomes newly skipped (e.g. a texture-reference typo) moving
    its entry tile to frame 1's position is the exact same class of "geometry changed, entry moves"
    as a chain being re-authored to start at a different tile -- and `TilesetAnimationSync.Apply`'s
    source-scoped stale-tile-clearing (keyed by `achjSourceFile`) already self-heals the old tile in
    both cases identically, with no special-casing needed. Pinned with a new test (no source
    change): `AchjToTiledAnimationMapperTests.
    Map_FirstFrameSkippedButLaterFrameValid_EntryTileIdIsFirstSurvivingFrameNotOriginalFrameZero`.
  - **Target 2 (`TsxWriter.TopLevelEquals`'s order-sensitive `PropertiesEqual`) -- firm conclusion:
    acceptable, matches existing precedent, not fixed.** Traced how tileset-level property order
    could ever actually diverge between the freshly-reloaded `original` and the in-memory `tileset`
    being saved: `NativeTsxAnimationSync.CloneForSave` shares `Tileset.Properties` by reference
    (never touches it), and nothing else in this sync pipeline ever reorders or reassigns
    `Tileset.Properties` -- so under this codebase's own normal operation, the two lists are always
    identical (same reference even) at save time. The only way they'd differ in order is a
    concurrent external edit to the file between load and save (a hand-edit or another tool) --
    genuinely narrow, and when it happens the consequence is exactly the already-accepted
    "TsxWriter tile ordering" tradeoff: patch mode's own `TopLevelEquals` gate falls back to a full
    rewrite, which is always safe/correct, just not minimal-diff for that one save. Considered
    fixing it anyway (sorting property signatures before comparing is a one-line, low-risk change)
    but left it as documented, pinned behavior instead -- consistent with treating "full rewrite is
    a safe fallback" as this file's established acceptable-limitation pattern rather than a bug
    needing a source change every time it's reachable. Pinned with a new test, confirmed to have
    teeth by temporarily making `PropertiesEqual` order-insensitive and observing it fail (patch
    mode would then reuse the original property order instead of falling back), then reverting:
    `TsxWriterTests.Write_TopLevelPropertiesReorderedButContentUnchanged_FallsBackButStaysCorrect`.
  - **`TiledAnimationToAchjMapper.cs` coherence re-read -- no gap found.** Traced `IsAnchor` (backward/
    chained/orphaned ParentId) -> `tentativeSatellitesByAnchor` -> the footprint-completeness pass ->
    `incompleteAnchorIds` -> `IsEffectiveAnchor` -> `satellitesByAnchor` end to end: every stage
    consistently builds on the previous one's exclusions (e.g. a backward-offset tile is already
    excluded from `tentativeSatellitesByAnchor` via `IsAnchor`, so it can never participate in or
    corrupt the footprint-completeness dx/dy math), and this exact chain was already exercised by
    pass #4's composition test
    (`Map_NonRectangularFootprintGapFilledByChainedParentIdTile_AllFourTilesSurfaceAsIndependentChains`).
    Reads as one coherent piece of logic, not a patchwork -- no new test added.
  - **Real bug, confirmed red first -- `ProjectManager.ReferencedPngs` leaked across a plain
    `LoadAnimationChain` call the same way `_tsxTileset` did before its own fix.**
    `LoadAnimationChain_SecondFileHasNoProjectFile_ClearsPreviouslyLoadedReferencedPngs` failed
    against the old code (`ReferencedPngs` still held the first achx's PNGs after loading a second
    achx with no `ProjectFile` at all). Root cause: `LoadAnimationChain` only ever calls
    `TryLoadProjectFile` (which repopulates `ReferencedPngs`) when the newly loaded achx declares a
    `ProjectFile` -- there was no `else` branch clearing it for an achx that doesn't. This is a plain
    "File > Open a second file in the same window" repro, not even tab-switch-cache-specific
    (reachable identically whether or not `TabEditorCache` exists), and a third confirmed instance
    of the exact leak class fixed twice already for `_tsxTileset` and `_knownTextureSizes`. Fixed
    with a one-line `else ReferencedPngs = new FilePath[0];` in `LoadAnimationChain`, mirroring the
    tsx-fields reset immediately above it. The tab-switch-cache-specific extension of this same field
    (bypassing `LoadAnimationChain` entirely) is a distinct, lower-severity gap -- see the new TODO
    item above. Test:
    `ProjectManagerReferencedPngTests.LoadAnimationChain_SecondFileHasNoProjectFile_ClearsPreviouslyLoadedReferencedPngs`.
  - **Full `ProjectManager.cs` private-field audit against the `TabEditorCache` round-trip pattern --
    one gap found (`ReferencedPngs`, above), everything else confirmed fine.** Listed every private
    field (`_tsxTileset`, `_tsxEntryTileIdsByChain`, `_tsxSatelliteTileIdsByChain`,
    `_knownTextureSizes`, static `mTileMapInformationList`) and every settable public property
    (`AnimationChainListSave`, `FileName`, `OnDiskCoordinateType`, `ReferencedPngs`,
    `ProjectFolderPath`). The four tsx/texture-size fields are already covered by
    `CaptureTsxState`/`CaptureTextureSizeState`; `AnimationChainListSave`/`FileName`/
    `OnDiskCoordinateType` are already directly round-tripped by `TabEditorCache`;
    `mTileMapInformationList` was already traced (fresh-eyes pass #1) as static and unrelated to tsx
    sync; `ProjectFolderPath`'s own doc comment explicitly establishes it as a session-wide setting,
    not per-achx-load state ("stays set... even with zero tabs open") -- correctly excluded from any
    per-tab round-trip. No more instances of this pattern beyond `ReferencedPngs`.
  - **Honest assessment: not a clean pass.** One new real, confirmed bug was found and fixed (a third
    instance of the reused-instance-state-leak class), plus one new TODO item describing a narrower,
    lower-severity sibling of that same bug in the tab-switch-cache path. Per the stop condition, a
    pass #6 is needed -- but note the bar has visibly narrowed: this pass's only real bug was a
    single-line omission in code adjacent to (not part of) prior fixes, both suggested investigation
    targets resolved to "already correct, pin and document" with no source change, and the dedicated
    coherence re-read of the most-patched file in scope found nothing. If pass #6 is also this thin,
    the phase is close to exhausted.

## DONE (continued)

- [x] **`TilesetAnimationSync` ownership check keys off `Animation.Count > 0`, not
  `achjAnimationName`.** Decision: **do not extend the check** — `achjAnimationName` alone stays
  outside the ownership test; only `Animation.Count > 0` (actual frame data) or a set
  `achjSourceFile` count as "owned." Reasoning: the scenario is a tile with `achjAnimationName` set,
  no `achjSourceFile`, and an *empty* `Animation` list (partially-written/crashed save, pre-
  `achjSourceFile` schema, or a hand-edit that cleared frames but left the name property). Extending
  the check to treat `achjAnimationName` alone as "tracked" would skip+warn on this tile forever with
  no way out: the skip path never writes `achjSourceFile` (that only happens on a successful claim),
  so `owningSource` stays `null` on every future sync attempt and the tile can never satisfy its own
  "needs matching achjSourceFile to overwrite" condition — a permanent, unresolvable warning trap for
  metadata that protects zero actual animation data. Contrast with the existing
  `Animation.Count > 0` check this extends from: that one also never sets `achjSourceFile` on skip,
  but it is guarding real frame content a human might have drawn, so "requires manual intervention to
  reclaim" is the correct tradeoff there. Here there's nothing to protect but a stale string, so
  self-healing (claim the tile, overwrite `achjAnimationName` to the new chain, and *do* set
  `achjSourceFile` this time) is strictly better than a warning that can never resolve. Confirmed via
  a pinning test (passes unmodified against current code — no source change): a tile with
  `achjAnimationName = "OldChain"`, no `achjSourceFile`, empty `Animation` is claimed by a new achx
  chain, ending up with the new chain's name and `achjSourceFile` set. Test:
  `TilesetAnimationSyncTests.Apply_TileHasStaleAnimationNamePropertyButEmptyAnimationAndNoSourceProperty_IsClaimedNotPermanentlyBlocked`.

- [x] **Row/`TileCount` bottom-edge overflow — the row equivalent of the column-overflow bug fixed
  in the same pass (fresh-eyes pass #1, see below).** Real bug, confirmed red first: with the row
  check temporarily disabled (`if (false && ...)`), `Map_RowBeyondTilesetTileCount_...` failed with
  a mapped frame carrying `TileId = 16` against a 16-tile tileset, and (crucially, at the
  integration layer) `NativeTsxAnimationSync.ApplyTile`/`TsxWriter.Write` were caught concretely
  fabricating a phantom `Tile { ID = 64, Animation = [Frame { TileID = 64 }] }` in the *reloaded*
  output file for a 64-tile fixture -- confirming the suspected severity (a silently-corrupt but
  plausible-looking `.tsx`, not a crash) rather than assuming it. `AchjToTiledAnimationMapper.MapFrame`
  and `MultiTileToTiledAnimationMapper.MapChain` already rejected a column at or past `ColumnCount`,
  but neither checked the equivalent bound on the row axis, and unlike column overflow this doesn't
  wrap into an existing tile -- it computes a tile id with no real cell at all. Fixed:
  - `TilesetAnimationInfo` gained a required `TileCount` field (Tiled's `tilecount`), threaded
    through both builders that construct it (`ProjectManager.BuildTsxTilesetInfo`,
    `TiledTilesetSyncRunner.BuildTilesetInfo`, both already had a `DotTiled.Tileset` in scope to read
    it from).
  - `AchjToTiledAnimationMapper.MapFrame` checks the final computed `tileId >= TileCount` (a new
    `SkipCounts.RowOutOfRange` bucket) rather than deriving a row bound from `ColumnCount` --
    deliberately, since a tileset's last row can be partial (`TileCount` not a whole multiple of
    `ColumnCount`), and checking the final id is correct in that case while a row-count bound
    derived via `ceil(TileCount / ColumnCount)` would not be.
  - `MultiTileToTiledAnimationMapper.MapChain` checks the footprint's bottom-right cell's tile id
    (`(originRow + footprintRows - 1) * ColumnCount + (originColumn + footprintColumns - 1)`) against
    `TileCount`, aborting the whole chain with a warning (matching its existing per-chain-abort
    pattern) -- this is the single largest tile id any cell in the footprint can compute to, since
    the existing column-bound check already guarantees every cell's column is in range.
  - Verified the already-landed column-overflow fix's footprint handling in the same pass, per the
    task's request: `MultiTileToTiledAnimationMapper` already checked
    `originColumn + footprintColumns > ColumnCount` (the full right-hand extent, not just the
    origin), so no residual gap was found there -- the column check was already correct for
    footprints.
  Existing test fixtures constructing `TilesetAnimationInfo` directly (two mapper-test static
  fixtures, seven sites in `NativeTsxProjectRoundTripTests.cs`) needed a `TileCount` value added to
  compile; all nine sites had a real tile count available (a fixture constant or `tileset.TileCount`)
  so no synthetic/arbitrary values were needed. Tests:
  `AchjToTiledAnimationMapperTests.Map_RowBeyondTilesetTileCount_SkipsFrameAndWarnsInsteadOfFabricatingOutOfRangeTile`,
  `Map_TallySkipsTrue_RowOutOfRangeTalliedInSkipCounts`,
  `MultiTileToTiledAnimationMapperTests.Map_FootprintBottomRowBeyondTilesetTileCount_SkipsChainAndWarnsInsteadOfFabricatingOutOfRangeTile`,
  `NativeTsxProjectRoundTripTests.LoadMapApplySave_FrameBeyondTilesetTileCount_SkipsInsteadOfFabricatingPhantomTile`.

- [x] **`TiledAnimationToAchjMapper`'s satellite offset math assumed every satellite sits at or
  below/right of its anchor.** Real bug, confirmed red first —
  `TiledAnimationToAchjMapperTests.Map_BackwardParentId_SatelliteAboveAnchorSurfacesAsItsOwnChainInsteadOfBeingSilentlyExcluded`
  failed against the old code (1 chain instead of 2 — the backward tile's animation data completely
  absent from the returned model, not just miscomputed), and a second red test at the validator
  layer confirmed the danger of the "silently excluded" framing was understated: with the anchor
  and backward tile's frame counts equal, the validator's own dx/dy lockstep math (same uint-
  subtraction shape as the mapper's bug) happened to wrap back around to the tile's own correct
  `TileID` and report **zero issues** for `TsxAnimationValidatorTests.Validate_BackwardParentId_ReferencesAnchorAtLargerColumnOrRow_ReturnsIssue`,
  i.e. the pre-existing lockstep check was not a reliable safety net for this case either. Decision:
  **option (a), treat a backward ParentId as invalid/orphaned** — same "structurally impossible via
  AnimationEditor's own UI, only reachable via hand-editing" pattern as every other broken-`ParentId`
  fix on this branch (orphaned, chained/nested). Reasoning: AnimationEditor's own UI only ever grows
  a multi-tile footprint to the right/down from its anchor, so a backward-pointing `ParentId` has no
  real semantics to preserve as a satellite; making the tile its own anchor/chain (rather than
  clamping the offset to non-negative, which would just misplace it differently without fixing
  anything) keeps its animation data fully intact and matches every prior decision in this sweep.
  Combined with **option (c)**, a dedicated `TsxAnimationValidator` check, following the exact
  precedent set by the chained-ParentId fix's own added validator check. Fixed:
  - `TiledAnimationToAchjMapper.Map` gained an `IsBackwardOffset(anchorId, satelliteId)` local
    function (`(satelliteId % columns) < (anchorId % columns) || (satelliteId / columns) < (anchorId
    / columns)`) and extended the existing `IsAnchor` eligibility check (already covering orphaned/
    chained `ParentId`) to also treat a backward offset as "not a real satellite" — the tile falls
    through to become an anchor of its own instead of ever entering the uint dx/dy subtraction that
    was silently underflowing. `satellitesByAnchor` was reordered to filter via `!IsAnchor(t)`
    (previously it filtered only on "has a ParentId at all," independently of the anchor-eligibility
    checks used one loop later, which is what let a backward tile slip through as a satellite in the
    first place).
  - `TsxAnimationValidator.Validate` gained a dedicated backward-offset check
    (`(tile.ID % columns) < (anchorId % columns) || (tile.ID / columns) < (anchorId / columns)`),
    placed before the per-frame lockstep loop for the same reason the mapper fix orders `IsAnchor`
    checks before the dx/dy math — the lockstep loop's own uint arithmetic has the identical
    underflow shape and can't be trusted to catch this case on its own (confirmed above: it
    coincidentally reported zero issues for the red test's frame-count-equal scenario).
  Tests:
  `TiledAnimationToAchjMapperTests.Map_BackwardParentId_SatelliteAboveAnchorSurfacesAsItsOwnChainInsteadOfBeingSilentlyExcluded`,
  `TsxAnimationValidatorTests.Validate_BackwardParentId_ReferencesAnchorAtLargerColumnOrRow_ReturnsIssue`.

- [x] **`ProjectManager.SaveTsxProject`'s `targetPath` "Save As to a new file" branch had no test
  at the `ProjectManager` layer.** Already correct, confirmed with two new tests, no source change.
  `SaveTsxProject(targetPath: <new path>)` writes a complete tsx to the new path (via `TsxWriter`'s
  full-rewrite branch, since a brand-new path never satisfies its `File.Exists(path)` patch-mode
  gate) and leaves the source file byte-for-byte untouched. Traced the `FileName`-after-Save-As
  question precisely: `SaveTsxProject` never updates `FileName` itself -- same as
  `SaveAnimationChainList(string)`, the achx/achj equivalent, which also never updates it. That's
  intentional layering, not a gap: `AppCommands.SaveCurrentAnimationChainListAsync` (the UI's actual
  Save-As command) is the layer that sets `_pm.FileName = path` after a successful save, uniformly
  for both tsx and achx. So a subsequent no-args `SaveTsxProject()` call correctly keeps targeting
  the original file, not the Save-As path -- verified by renaming a chain after a Save-As, saving
  with no args, and confirming the rename landed in the original file while the Save-As copy stayed
  unchanged. Tests:
  `ProjectManagerTsxProjectTests.SaveTsxProject_TargetPath_WritesCompleteFileAtNewPathWithoutModifyingOriginal`,
  `SaveTsxProject_TargetPath_DoesNotUpdateFileNameAndSubsequentNoArgSaveStaysOnOriginalFile`.

- [x] **Fresh-eyes pass #1.** Re-read every file in "Files in scope" end to end (not just the
  diffs from prior fixes), working through the phase doc's four suggested categories
  (concurrency/static state, large tilesets, unicode names, achj-vs-achx serialization) plus
  general tile-grid boundary math. Found and fixed one real bug (below), and added four new
  plausible-gap TODO items above; the rest of the suggested categories were traced and confirmed
  not to be gaps (see the "Traced, not added" note above the TODO list for the reasoning on each).
  - **Real bug, confirmed red first — a multi-tile/single-tile frame whose column (or footprint's
    right-hand column) is at or past the tileset's `ColumnCount` silently wraps into a real tile in
    the *next* row instead of failing.** `AchjToTiledAnimationMapper.MapFrame` computes
    `tileId = row * ColumnCount + column` and `MultiTileToTiledAnimationMapper.MapChain` computes
    each footprint cell's id the same way; neither checked that `column`/`originColumn + dx` stays
    below `ColumnCount` before that arithmetic — a column value one past the last valid index folds
    into a legitimate-looking id belonging to the next row's leftmost tile(s), which then gets its
    animation silently overwritten instead of the frame being rejected. Same failure shape as the
    already-fixed negative-coordinate bugs (unchecked arithmetic on corruption-controlled input),
    just on the other edge. Fixed with a new bounds check in both mappers, matching each one's
    existing skip/abort pattern: `AchjToTiledAnimationMapper` gained a `SkipCounts.ColumnOutOfRange`
    per-frame skip category; `MultiTileToTiledAnimationMapper` aborts the whole chain with a warning
    (matching its existing per-chain-abort precedent for other geometry failures). Tests:
    `AchjToTiledAnimationMapperTests.Map_ColumnBeyondTilesetWidth_SkipsFrameAndWarnsInsteadOfWrappingIntoNextRow`,
    `MultiTileToTiledAnimationMapperTests.Map_FrameFootprintExtendsPastTilesetRightEdge_SkipsChainAndWarnsInsteadOfWrappingIntoNextRow`.
  - **Traced (not a bug): can two satellites of the same chain ever compute the same `(Dx, Dy)`
    key?** No — structurally impossible on both load and save. On load
    (`TiledAnimationToAchjMapper.Map`), `(Dx, Dy)` is a deterministic function of a satellite's own
    tile id relative to a fixed anchor position (`id % columns`, `id / columns`), and Tiled tile ids
    within one tileset are unique, so two different satellite tiles always land on two different
    `(Dx, Dy)` pairs. On save (`MultiTileToTiledAnimationMapper.MapChain`), `perOffset` is built by
    iterating every `(dx, dy)` cell of an NxM grid exactly once, so collisions can't arise there
    either. No pinning test added — this is a mathematical guarantee of the id-to-offset mapping,
    not a subtle behavioral invariant of this codebase's logic.
  - **Honest assessment: this pass was not exhaustive.** It covered the phase doc's four suggested
    categories and general boundary-value tracing (tile id 0/last id, 1-column tilesets, empty
    chain lists) reasonably thoroughly, and found one real, confirmed bug plus four plausible new
    gaps. It did *not* deeply chase: error-path combinations across multiple simultaneous corruption
    conditions in one file (e.g. a tileset that is both `Columns <= 0` *and* has duplicate tile ids),
    the Avalonia UI layer's own handling of the warnings/exceptions this sweep's `.Core` layer
    produces (out of scope per "Files in scope", but a second pass could double check nothing new
    leaks through), or a line-by-line audit of `TsxWriter`'s XML-escaping behavior for property
    values containing characters that are special in XML (`<`, `&`, `"`) — plausible but not
    investigated this pass. A second fresh-eyes pass should pick up here.

- [x] **Fresh-eyes pass #2.** Re-read every file in "Files in scope" end to end, working through
  pass #1's own explicitly-flagged under-explored areas (combined/simultaneous corruption
  conditions, `TsxWriter`'s XML-escaping, the `AnimationEditor.App` UI layer's exception handling
  for this sweep's new throw sites) plus general fresh-eyes tracing. Found and fixed two real bugs
  (below), confirmed one "already correct" behavior with a teeth-tested pinning test, and traced
  several other angles to "not a gap" (see the "Traced, not added" note above the TODO list). One
  plausible gap was deferred to TODO as genuinely needing more design work (see above).
  - **Real bug, confirmed red first — a tsx with two `<tile>` elements sharing one id crashed with
    `Dictionary`'s own raw `ArgumentException` ("An item with the same key has already been added")
    instead of this codebase's established "fail loud with a clear message" precedent
    (`Columns <= 0`, tile-id collisions between chains, etc).** This is exactly the kind of
    "combined corruption condition" pass #1 flagged not having chased down: any of the four places
    in this subsystem that key a dictionary by tile id (`NativeTsxAnimationSync.Apply`,
    `TilesetAnimationSync.Apply` -- both key *every* tile in the tileset;
    `TiledAnimationToAchjMapper.Map`, `TsxAnimationValidator.Validate` -- both key only *animated*
    tiles) had this gap; `TsxWriter.TryWritePatchedCore`'s own equivalent dictionary build was
    already safe (wrapped in the surrounding try/catch that falls back to a full rewrite -- see its
    existing comment). Fixed all four by replacing the raw `.ToDictionary(t => t.ID)` call with a
    loop using `Dictionary.TryAdd`, throwing a clear `InvalidOperationException` naming the tileset
    and the colliding tile id the first time a duplicate is seen. Tests:
    `NativeTsxAnimationSyncTests.Apply_TilesetHasDuplicateTileIds_ThrowsClearErrorInsteadOfRawDictionaryException`,
    `TilesetAnimationSyncTests.Apply_TilesetHasDuplicateTileIds_ThrowsClearErrorInsteadOfRawDictionaryException`,
    `TiledAnimationToAchjMapperTests.Map_TilesetHasDuplicateAnimatedTileIds_ThrowsClearErrorInsteadOfRawDictionaryException`,
    `TsxAnimationValidatorTests.Validate_TilesetHasDuplicateAnimatedTileIds_ThrowsClearErrorInsteadOfRawDictionaryException`.
  - **Real bug, confirmed red first — `ProjectManager.LoadTsxProject` could leave the project in a
    half-loaded, internally-inconsistent state when it threw.** Its own doc comment promised "the
    project is left unchanged when this is thrown," and that held for the one exception type it
    documented (`NotSupportedException` from `TsxCompatibilityChecker`, which runs *before*
    `_tsxTileset` is assigned) -- but not for an exception thrown by
    `TiledAnimationToAchjMapper.Map` itself (`Columns <= 0`, or the duplicate-tile-id guard just
    added above), because the old code assigned `_tsxTileset = tileset` *before* calling `Map`. A
    `Map` throw left `_tsxTileset` pointing at the new (corrupt) tileset while
    `AnimationChainListSave` stayed at whatever the *previous* project's chains were (or `null`) --
    `IsNativeTsxProject` would report `true` for a tileset with no matching chain data at all.
    Fixed by computing `Map`'s result into a local variable first and only assigning
    `_tsxTileset`/`AnimationChainListSave`/the tracking dictionaries after every step that can throw
    has already succeeded -- this generalizes to *any* future exception `Map` might throw, not just
    the two guards known today. Test:
    `ProjectManagerTsxProjectTests.LoadTsxProject_MapThrows_ThrowsAndLeavesProjectUnchanged`.
  - **Traced (not a bug), but pinned since it's genuinely non-obvious: `TsxWriter`'s patch-mode path
    (`TryWritePatchedCore` -> `RenderTileFragment` -> `WriteTile`) correctly escapes XML-special
    characters (`&`, `<`, `"`) in a chain `Name` value, with no naive string manipulation of its own
    (concatenation, slicing) that could bypass `XmlWriter.WriteAttributeString`'s built-in
    escaping.** Confirmed the test has teeth by temporarily replacing the `WriteAttributeString`
    call with a raw, unescaped `WriteRaw` and observing the reload throw `XmlException` (`"An error
    occurred while parsing EntityName"`) instead of the value round-tripping, then reverting. Also
    traced `PropertySignature`'s pipe-delimited comparison string (used only for patch-mode's
    unchanged-vs-changed diffing, never written to disk) for a theoretical name/value boundary
    collision (`"X"` + `"Y|Z"` vs `"X|Y"` + `"Z"` both producing `"string|X|Y|Z"`) -- unreachable in
    practice because every property *name* this subsystem writes is a fixed compile-time constant
    (`"Name"`, `"ParentId"`, `"achjAnimationName"`, `"achjSourceFile"`) that never itself contains a
    pipe, so only the value side can vary and a full-string equality check can't be fooled by a
    fixed, pipe-free prefix. No source change; test:
    `NativeTsxProjectRoundTripTests.LoadRenameChainToNameWithXmlSpecialCharacters_SaveInPlacePatchMode_EscapesCorrectlyAndReloadsExactValue`.

- [x] **O(n) tile lookups inside per-result loops could become O(n²) on a large tileset.**
  Decision: **fixed**, not deferred — the dictionary rewrite was genuinely straightforward once
  traced. Both `NativeTsxAnimationSync.Apply` and `TilesetAnimationSync.Apply` now build
  `tilesById = tileset.Tiles.ToDictionary(t => t.ID)` once at the top of `Apply`, and every
  `.Single(t => t.ID == ...)`/`.FirstOrDefault(t => t.ID == ...)` scan (stale-tile-clear loop,
  `ApplyTile`'s per-tile lookup) now does an O(1) dictionary lookup instead. Traced the one risk
  called out up front — `ApplyTile` (native) and the inline creation branch (achj-push) both
  sometimes *add* a brand-new tile mid-`Apply`-call — and confirmed no lookup within the same call
  ever needs to see a tile created earlier in that same call: `ValidateNoTileIdCollisions`
  (native)/the `claimedBy` check (achj-push) already guarantee every entry/satellite tile id
  touched in one `Apply` invocation is unique, and the stale-clear loop (which only reads, never
  creates) always runs before the apply loop. Still updated `tilesById[tileId] = tile` at both
  tile-creation sites anyway, defensively, so the dictionary can't silently drift out of sync with
  `tileset.Tiles` for any future caller that adds another lookup later in the method. Tests
  (correctness-equivalence, not a perf benchmark — pin that the dictionary-based lookup produces
  the exact same result as the old sequential scan on a single `Apply` call that mixes all three
  code paths: stale-clear, existing-tile update, and new-tile creation):
  `NativeTsxAnimationSyncTests.Apply_StaleClearExistingUpdateAndNewTileAllInOneCall_DictionaryLookupMatchesSequentialScan`,
  `TilesetAnimationSyncTests.Apply_StaleClearExistingUpdateAndNewTileAllInOneCall_DictionaryLookupMatchesSequentialScan`.

- [x] **`ProjectManager.SaveTsxProject` mutated the live `_tsxTileset` in place before
  `TsxWriter.Write` was attempted.** Decision: **fixed**, not deferred -- re-scoped investigation
  (per this item's own instructions) found the clone was much smaller than the original assessment
  assumed. `NativeTsxAnimationSync.Apply` only ever mutates three things: `Tileset.Tiles`
  (membership -- `ApplyTile` adds new tiles), and per tile, `Tile.Properties` (add/remove/`.Value =`
  in place) and `Tile.Animation` (always wholesale-reassigned, never appended to in place). Every
  other `Tileset`/`Tile` field (name, size, image, wangsets, transformations, tileset-level
  properties, per-tile type/probability/x/y/width/height/image/object layer) is read-only for
  `Apply`'s purposes. Confirmed via DotTiled's own source (`v1.0.0` tag, read directly -- not
  decompiled) that `Tile`/`Tileset`/`Frame` are plain mutable classes with settable properties, and
  that `IProperty` already ships a `Clone()` method built for exactly this purpose ("cloning
  properties when performing overriding with templates"). That combination made a minimal, exactly-
  scoped clone -- new `Tileset`/`Tile` objects with a fresh `Tiles` list, fresh per-tile `Properties`
  list of cloned `IProperty` instances, and a fresh (but Frame-instance-sharing) `Animation` list;
  every other field shared by reference with the original -- genuinely small, not the "real,
  non-trivial work" the original assessment expected. Added `NativeTsxAnimationSync.CloneForSave`
  (plus a private `CloneTile` helper) and changed `SaveTsxProject` to run
  `MultiTileToTiledAnimationMapper.Map` + `NativeTsxAnimationSync.Apply` against a working copy,
  calling `TsxWriter.Write` with that copy and only assigning `_tsxTileset = workingTileset` after
  `Write` returns -- mirroring `LoadTsxProject`'s existing "compute into locals, commit only after
  every throwable step succeeds" pattern exactly.
  - **The originally-suspected failure mode (a subsequent successful save silently persisting the
    phantom mutation to disk) does not actually reproduce.** Traced exhaustively, then confirmed
    with a throwaway probe test (not kept -- it passed both before and after the fix, so it had no
    discriminating power): `Apply`'s stale-tile diff (`previouslyAnimatedTileIds`) is recomputed
    fresh from `tileset.Tiles`'s *live* state on every call, and `ApplyTile` fully overwrites (never
    incrementally patches) every property/animation it touches. That makes every phantom mutation
    left by a failed attempt self-heal the moment any later call re-runs `Apply` against the current
    `AnimationChainListSave` -- confirmed by temporarily disabling the stale-tile-clear loop and
    observing the probe test fail, then reverting.
  - **The real, user-visible bug was in `GetChainNamesWithTsxIssues`, not final file content.**
    `TsxAnimationValidator.Validate` reads tile content directly off `_tsxTileset`, and
    `NativeTsxAnimationSync.ApplyTile` always overwrites a satellite's frames to match its anchor.
    So without this fix, a failed save would leave `_tsxTileset`'s satellite tile already "fixed" in
    memory even though nothing reached disk -- `GetChainNamesWithTsxIssues()` called right after
    would wrongly report zero issues for a tsx that, on disk, still has the exact problem it was
    reporting a moment earlier. Confirmed red before the fix, green after:
    `ProjectManagerTsxValidationIssuesTests.GetChainNamesWithTsxIssues_AfterSaveFails_StillReflectsUnsavedOnDiskState`
    (targets a save path inside a nonexistent directory so `TsxWriter.Write`'s `File.Create` throws
    before any bytes reach disk, asserts the validator issue is still reported and the on-disk
    satellite frames are still the original hand-edited ones).

- [x] **Fresh-eyes pass #3.** Investigated the 5 areas the task specifically called out, plus a
  general fresh read of `TiledTilesetSyncRunner.cs`, `TilesetAnimationSync.cs`, and
  `AchjToTiledAnimationMapper.cs`. Found and fixed two new real, confirmed bugs, both in territory
  the first two passes never covered (multi-satellite group *shape*, as opposed to any single
  satellite's own `ParentId` validity), plus one doc defect, and traced the 5 suggested areas to
  "safe" with no source change:
  - **Real bug, confirmed red first -- an anchor's satellites are folded into one wide chain based
    only on their bounding box, never checking that every cell inside that box is actually
    populated.** Two satellites individually pass every existing check (forward offset, resolves to
    a true anchor, own frames in lockstep) yet together imply a rectangle larger than what's
    actually declared -- e.g. a satellite at (1,0) and another at (0,1) with nothing at (1,1).
    `TiledAnimationToAchjMapper.Map` took the bounding box at face value, producing a chain whose
    frame rect silently spanned a fourth tile that was never marked as part of any group -- which
    `NativeTsxAnimationSync.Apply`/`MultiTileToTiledAnimationMapper` (which always fill every cell of
    a computed footprint) would then silently claim on the very next save, corrupting a tile that
    had nothing to do with the chain. Same failure shape as every other broken-`ParentId` fix on this
    branch, just evaluated at the group level instead of per-tile. Fixed by adding a completeness
    pass: for each tentative anchor, check whether its satellites' offsets fill every cell of the
    rectangle their own bounding box implies; if not, every satellite in that group is reclassified
    as its own independent anchor (same "surface as its own chain instead of silently
    misinterpreted" precedent as orphaned/chained/backward `ParentId`). Added the matching
    `TsxAnimationValidator` check (same shape as the existing backward/chained checks) since none of
    the existing per-satellite validator checks catch this either -- confirmed both new tests have
    teeth (mapper test failed 1-chain-instead-of-3 before the fix; validator test failed
    0-issues-instead-of-2 with the check temporarily disabled, then reverted). Tests:
    `TiledAnimationToAchjMapperTests.Map_SatellitesFormNonRectangularFootprint_EachTileSurfacesAsItsOwnChainInsteadOfWrongFootprint`,
    `TsxAnimationValidatorTests.Validate_SatellitesFormNonRectangularFootprint_ReturnsIssuePerSatellite`.
  - **Real bug, confirmed with two probe tests first -- `GetChainNamesWithTsxIssues`'s chain-name
    correlation matched only a `TsxGroupIssue`'s `AnchorTileId`, which is correct for a
    lockstep-mismatch issue but wrong for a broken-`ParentId` issue (dangling/chained/backward/
    incomplete-footprint).** Those issues report `AnchorTileId` as "the tile `ParentId` names," not
    "the chain this issue is about" -- the actually-broken tile (`TileId`) is the one
    `TiledAnimationToAchjMapper.Map` makes its own independent chain in these cases, never a
    satellite of `AnchorTileId`'s chain. Two distinct failure modes confirmed: a **chained** `ParentId`
    (tile 2 -> tile 1 -> tile 0, where tile 1 is a legitimate satellite with no chain of its own) was
    silently dropped entirely -- `AnchorTileId=1` never matches any chain's entry tile id, so
    `GetChainNamesWithTsxIssues()` returned empty despite the validator reporting a real issue. A
    **backward** `ParentId` (tile 8 -> tile 9, where tile 9 is itself a real, perfectly consistent
    anchor with its own chain) flagged the *wrong* chain -- `AnchorTileId=9` coincidentally matches
    "ID:9"'s entry tile id, so the innocent chain got flagged while "ID:8" (the actually-broken one)
    never did. Fixed by matching a chain if its entry tile id equals *either* `AnchorTileId` or
    `TileId` -- catches the actually-broken chain in both cases, at the acceptable cost of also
    (correctly) flagging the referenced anchor's chain when it has one, since one of its would-be
    satellites failing to attach is worth surfacing there too. Tests:
    `ProjectManagerTsxValidationIssuesTests.GetChainNamesWithTsxIssues_BackwardParentId_IncludesTheActuallyBrokenChain`,
    `GetChainNamesWithTsxIssues_ChainedParentId_DoesNotSilentlyDropTheBrokenChain`.
  - **Doc defect, fixed on sight (not a behavior bug):** `AchjToTiledAnimationMapper.FrameDurationMs`
    had two consecutive `<summary>` XML doc tags (a leftover from an earlier edit); merged into one.
  - **Investigation area 1 (`TsxCompatibilityChecker`) -- safe, no gap.** Traced concretely:
    `CheckOpenCompatibility` calls `TsxWriter.Write(tileset, Stream.Null)` (the `Stream` overload),
    which never goes through `TryWritePatchedCore` (only the path-based `Write(tileset, string)`
    overload does) and never throws `InvalidOperationException` for any of this sweep's new guards
    (`Columns <= 0`, duplicate tile id) -- those guards live in `TiledAnimationToAchjMapper.Map`/
    `TsxAnimationValidator`/the sync classes, never in `TsxWriter`. So the compatibility checker
    can't throw an uncaught exception, but it also can't *detect* those corruption conditions --
    confirmed this is already documented and tested as intentional, not a gap:
    `LoadTsxProject`'s own doc comment and
    `ProjectManagerTsxProjectTests.LoadTsxProject_MapThrows_ThrowsAndLeavesProjectUnchanged` already
    establish that `Map` (called right after the compatibility check passes) is the layer that
    rejects these cases with a clear `InvalidOperationException`, and the load stays all-or-nothing
    either way.
  - **Investigation area 2 (`AppCommands.cs`) -- safe, no gap.** Read the full ~2300-line file.
    `_tsxTileset` is a private `ProjectManager` field with no public getter (grepped the whole tool
    for `TsxTileset`/`_tsxTileset` -- zero references outside `ProjectManager.cs` itself), so no
    external caller (including `AppCommands.cs`) can hold a stale reference to it across a
    `CloneForSave`-driven save. `AppCommands.cs` only ever reads current state through `_pm`'s public
    surface (`AnimationChainListSave`, `IsNativeTsxProject`, `FileName`) fresh each call, and every
    tsx-touching path (`OpenTsxWorkflowAsync`, `SaveCurrentAnimationChainList`,
    `SyncAssociatedTiledTilesets`) already has its own try/catch around the `ProjectManager` call.
  - **Investigation area 3 (`GetChainNamesWithTsxIssues` idempotence) -- safe, no gap.** Traced
    concretely: the method and everything it calls (`TsxAnimationValidator.Validate`,
    `MultiTileToTiledAnimationMapper.Map`) are pure reads over `_tsxTileset`/`AnimationChainListSave`/
    the tracking dictionaries -- nothing in the call chain mutates any of them, and
    `MultiTileToTiledAnimationMapper.MapChain`'s `perOffset` dictionary is rebuilt fresh with the
    same deterministic insertion order every call. Two calls with no intervening edit are
    byte-for-byte identical by construction; no test added (this is a structural guarantee of the
    code, not a subtle behavioral invariant worth pinning).
  - **Investigation area 4 (`CloneForSave` vs. the entry/satellite tracking dictionaries) -- safe, no
    gap.** Confirmed by reading `SaveTsxProject`'s exact order of operations:
    `MultiTileToTiledAnimationMapper.Map` (which reads the tracking dictionaries) runs *before*
    `CloneForSave` is even called, and the post-save commit-forward loop rebuilds the dictionaries
    from `mapped`'s `SourceChain` references, never from the tileset/clone at all. The two data
    structures never interact in either direction. No test added -- already exhaustively traced by
    this exact reasoning.
  - **Investigation area 5 (`CloneForSave` test coverage for a multi-tile/satellite scenario) --
    traced thoroughly, no gap found, no test added.** Confirmed `IntProperty.Clone()` is correct by
    reading DotTiled's actual v1.0.0 source (`new IntProperty { Name = Name, Value = Value }` -- a
    genuinely independent instance, since both fields are value/immutable types) rather than
    assuming. Separately confirmed a real, previously-unnoticed coverage gap: every multi-tile-group
    test in `NativeTsxProjectRoundTripTests.cs` calls `NativeTsxAnimationSync.Apply`/`TsxWriter.Write`
    directly, bypassing `ProjectManager`/`CloneForSave` entirely, so `CloneForSave` has in fact never
    been exercised by a successful save with satellites (only by the one failed-save
    `GetChainNamesWithTsxIssues_AfterSaveFails` test, which uses a satellite fixture but never
    reaches a successful `Apply`+`Write`). Did not add a test for it: traced that property-level
    cloning (vs. a hypothetical shallow/shared-reference clone) has **no observable difference in
    behavior on a successful save** -- the only place it would matter is a failed save leaving
    `_tsxTileset` mutated-but-unwritten, which is already covered (for the one field where in-place
    mutation vs. reassignment actually differs -- `Tile.Animation`) by the existing
    `GetChainNamesWithTsxIssues_AfterSaveFails_StillReflectsUnsavedOnDiskState` test. A test asserting
    "a successful multi-tile save through `ProjectManager` works" would have no discriminating power
    (it would pass identically whether or not properties are deep-cloned), so it would be padding,
    not a pinning test.

- [x] **Fresh-eyes pass #4.** Started from the task's two suggested targets, then read every file in
  scope end to end one more time. Found and fixed two new real, confirmed bugs (both in territory no
  prior pass touched -- cross-project-type reuse of one `ProjectManager` instance, and a validator
  check that didn't fully mirror its mapper counterpart's rule), confirmed one composition (two
  earlier fixes interacting) with a teeth-tested pinning test, and traced the two suggested targets
  plus a full re-read of the remaining files to "safe, no gap":
  - **Real bug, confirmed red first -- `ProjectManager.LoadAnimationChain` never cleared
    `_tsxTileset`/`_tsxEntryTileIdsByChain`/`_tsxSatelliteTileIdsByChain`, so opening a plain achx
    project after a native tsx project (in the same `ProjectManager` instance -- confirmed via
    `TabSwitchCacheTests.cs` that this instance is reused across `File > Open` calls and tab
    switches, never recreated per file) left `IsNativeTsxProject`/`TsxTileSize` reporting the
    *previous* tsx project's state.** Traced the severity precisely: `AppCommands.
    SaveCurrentAnimationChainList` branches on `IsNativeTsxProject` to choose `SaveTsxProject` vs.
    `SaveAnimationChainList`, so this would route a plain achx save through the tsx writer against a
    stale tileset. Fixed by clearing all three fields at the same commit point `LoadAnimationChain`
    already uses for `AnimationChainListSave`/`FileName`. Test:
    `ProjectManagerTsxProjectTests.LoadAnimationChain_AfterLoadTsxProject_ClearsNativeTsxState`.
    Traced a deeper, more severe manifestation of the same root cause (tab-switch cache restore,
    which bypasses `LoadAnimationChain` entirely and so isn't fixed by this change) but left it as a
    TODO above rather than expanding scope into `TabEditorCache.cs`/`TabController.cs`, which aren't
    in this sweep's declared file scope.
  - **Real bug, confirmed red first -- `TsxAnimationValidator`'s chained-ParentId check
    (`TryGetValidForwardAnchor` and the per-tile issue-reporting loop) only treated a satellite's
    referenced tile as "itself a satellite, not a true anchor" when that tile's *own* ParentId
    resolved to a real animated tile.** `TiledAnimationToAchjMapper.Map`'s actual rule
    (`trueAnchorTileIds`) is simpler and stricter: a tile is a true anchor only if it has *no*
    ParentId at all, regardless of whether that ParentId resolves to anything. So a tile whose own
    ParentId is dangling (set, but not resolving to any animated tile -- itself flagged separately as
    an orphaned-ParentId issue) was still wrongly accepted by the validator as a valid forward anchor
    for anything satellite-pointing at it, silently reporting zero issues for that satellite even
    though the mapper never actually folds it into a group -- same failure class as the already-fixed
    chained/backward/orphaned-ParentId gaps, just for the specific sub-case where the intermediate
    tile's own broken reference is *dangling* rather than *resolving to a real satellite*. Fixed both
    checks to require only "the referenced tile has a ParentId of its own," matching the mapper's
    simpler rule exactly. Test:
    `TsxAnimationValidatorTests.Validate_ChainedParentId_IntermediateTilesOwnParentIdIsDanglingRatherThanResolving_StillFlagsTheSatellite`.
  - **Composition check requested by the task (non-rectangular footprint + chained ParentId in the
    same group) -- already correct, confirmed with a teeth-tested pinning test, no source change.**
    Traced precisely: `IsAnchor` (which already excludes chained-ParentId tiles) runs *before*
    `tentativeSatellitesByAnchor` is built, so a chained-ParentId tile sitting at the exact grid cell
    that would complete a footprint's rectangle is never counted as filling that cell -- the
    completeness check correctly sees the gap and un-folds the group, while the chained tile
    independently surfaces as its own chain via the pre-existing chained-ParentId handling. Confirmed
    the test has teeth by temporarily broadening `IsAnchor` (dropping the chained-ParentId condition)
    and observing the assertion fail (3 chains instead of 4), then reverting. Test:
    `TiledAnimationToAchjMapperTests.Map_NonRectangularFootprintGapFilledByChainedParentIdTile_AllFourTilesSurfaceAsIndependentChains`.
  - **Task's target 1 (`GetChainNamesWithTsxIssues`'s either-id match false-positive risk) -- safe,
    no new gap beyond the one already accepted.** Traced concretely: tile ids are unique within a
    tileset (enforced at load by the duplicate-tile-id guard from fresh-eyes pass #2), so
    `AnchorTileId`/`TileId` always name one specific physical tile, and a chain's own computed entry
    tile id can only equal that value when the chain genuinely owns that exact tile -- there's no
    room for a numerically-coincidental match against a truly unrelated tile. The only additional
    angle found (two chains transiently computing the *same* not-yet-saved entry tile id mid-edit,
    before `SaveTsxProject`'s own `NativeTsxAnimationSync.Apply` collision guard would ever run)
    exists identically whether matching on `AnchorTileId` alone or on either id -- it's a property of
    correlating "any chain whose entry tile id equals a flagged real tile id," not something the
    either-id change introduced or worsened -- and it would (correctly, not confusingly) flag a chain
    that genuinely has a forthcoming collision problem, not an unrelated healthy one. Not added to
    TODO: too narrow/pre-existing to be worth tracking on its own, and it doesn't answer "did the
    either-id fix specifically introduce a new false positive" (it didn't).
  - **Task's target 2 (full `ProjectManager.cs` re-read for accumulated cross-patch inconsistency,
    specifically load-different-file-after-save) -- safe, no gap.** Traced `LoadTsxProject` on a
    second, different file after a prior `LoadTsxProject`+`SaveTsxProject`: all four tsx-specific
    fields (`_tsxTileset`, `AnimationChainListSave`, `_tsxEntryTileIdsByChain`,
    `_tsxSatelliteTileIdsByChain`) are computed into locals first and only committed together after
    `TiledAnimationToAchjMapper.Map` has already succeeded for the *new* file -- there is no partial
    reset or leftover-from-the-previous-file state possible, since every field is unconditionally
    reassigned in the same commit, not merged/updated in place. (This re-read is what surfaced the
    `LoadAnimationChain` bug above -- the gap wasn't in this reassignment pattern itself, but in the
    *other* load method, which has no equivalent reassignment for these fields at all.)
  - **Fresh re-read of `TsxWriter.cs`, `NativeTsxAnimationSync.cs`, `AchjToTiledAnimationMapper.cs`,
    `TilesetAnimationSync.cs`, `TiledTilesetSyncRunner.cs` end to end -- no new gaps found** beyond
    the two bugs above. `TsxWriter`'s `TopLevelEquals`/`TileContentEquals` field lists were checked
    field-by-field against `NativeTsxAnimationSync.CloneForSave`/`CloneTile`'s field lists for
    completeness (consistent); `NativeTsxAnimationSync.ValidateNoTileIdCollisions`,
    `TilesetAnimationSync.Apply`'s ownership/ordering, and `TiledTilesetSyncRunner.SyncAll`'s
    per-tsx isolation were all re-verified against the current source and found consistent with the
    DONE items that already cover them.
  - **Honest assessment: not a clean pass.** Two new real, confirmed bugs were found and fixed, plus
    one new TODO item describing a deeper (but out-of-declared-scope) manifestation of one of them.
    Per the stop condition, a pass #5 is needed; see the TODO items above for suggested starting
    points.

- [x] **`TabEditorCache`'s tab-switch cache restore bypassed `LoadTsxProject`/`LoadAnimationChain`
  entirely, leaking stale native-tsx state across tab switches (scope deliberately expanded for
  this item -- see "Files in scope" note above).** Real bug, confirmed red first in both directions:
  `TryActivateTabFromCache_TsxThenAchxThenBackToTsx_RestoresNativeTsxStateAndSaveWritesEdit` failed
  with `IsNativeTsxProject` still `false` after cache-restoring the tsx tab (the exact repro from
  this item's original writeup), and
  `TryActivateTabFromCache_AchxThenTsxThenBackToAchx_ClearsNativeTsxStateThenRestoresTsxOnReturn`
  failed with `IsNativeTsxProject` still `true` after cache-restoring the achx tab -- a second,
  previously-undocumented direction of the same bug (the achx tab wrongly inherits the *live* tsx
  tab's state, not just the reverse). Decision: **design option 1**, expose enough on
  `IProjectManager` for `TabEditorCache` to capture/restore the tsx-specific state, via an opaque
  snapshot rather than leaking `DotTiled.Tileset`/the tracking dictionaries' concrete types across
  the interface boundary -- keeps `ProjectManager`'s tsx fields `private` exactly as before, and
  `TabEditorCache`/`TabEntry` never need to know the snapshot's shape. Fixed:
  - `IProjectManager` gained `object? CaptureTsxState()` (returns `null` for an achx/achj project)
    and `void RestoreTsxState(object? state)` (clears all tsx state when `state` is `null`).
  - `ProjectManager` implements both via a private `sealed record TsxState(Tileset, EntryTileIdsByChain,
    SatelliteTileIdsByChain)` holding direct references to the existing fields at capture time --
    safe to share without cloning because `LoadTsxProject`/`SaveTsxProject` always replace these
    fields wholesale, never mutate them in place (same reasoning already documented on
    `_tsxEntryTileIdsByChain`). `RestoreTsxState(null)` reuses the exact same three-field reset
    `LoadAnimationChain` already does for its own state-leak fix earlier in this sweep.
  - `TabEntry` gained `object? CachedTsxState`; `TabEditorCache.CaptureFromProject`/`ApplyToProject`
    call `pm.CaptureTsxState()`/`pm.RestoreTsxState(tab.CachedTsxState)` alongside the existing
    `AnimationChainListSave`/`OnDiskCoordinateType` round-trip.
  - Eight `IProjectManager` implementations (7 test fakes across `AnimationEditor.Views.Tests` and
    `AnimationEditor.Core.Tests`, plus `ProjectManager` itself) needed the two new members added to
    compile; all are either a no-op stub (`CaptureTsxState() => null`, `RestoreTsxState` empty body)
    or a passthrough to a wrapped `ProjectManager` (`TabSwitchCacheTests`' `CountingProjectManager`).
  Tests (new file `TabSwitchCacheTsxTests.cs`):
  `TryActivateTabFromCache_TsxThenAchxThenBackToTsx_RestoresNativeTsxStateAndSaveWritesEdit` (also
  proves a save against the cache-restored tab writes a real edit to the correct tsx file, not a
  no-op against cleared state) and
  `TryActivateTabFromCache_AchxThenTsxThenBackToAchx_ClearsNativeTsxStateThenRestoresTsxOnReturn`.

- [x] **`ProjectManager._knownTextureSizes` has the identical "private per-load state
  `TabEditorCache` can't see" shape as the tsx fields just fixed, on the browser-wasm build
  specifically.** Real bug, confirmed red first --
  `TabSwitchCacheTextureSizeTests.TryActivateTabFromCache_SwitchBackAfterAnotherTabLoaded_SaveUsesThisTabsKnownTextureSizesNotTheOtherTabs`
  failed against the old code with `InvalidOperationException: Cannot save with
  CoordinateType=Pixel: texture size could not be resolved for: TexA.png` -- tab A is loaded with a
  known texture size for its own texture, tab B is loaded next with none, and switching back to tab
  A via `TryActivateTabFromCache` left `_knownTextureSizes` at tab B's (`null`) instead of tab A's,
  so `SaveAnimationChainList(Stream)`'s Pixel conversion had nothing to resolve `TexA.png` against.
  (Traced `GetTextureSizeInPixels`, the TODO's other named suspect, and found it does *not* read
  `_knownTextureSizes` at all -- it always reads a PNG header off disk via `FileName`'s directory --
  so only `SaveAnimationChainList(Stream)`/`SaveAnimationChainListAsync(Stream)` were actually
  affected.) Fixed with the exact same opaque-snapshot pattern as `CaptureTsxState`/`RestoreTsxState`,
  as a sibling pair rather than folding it into `TsxState` (unrelated concept -- texture sizes apply
  to achx/achj tabs too, not just native-tsx ones):
  - `IProjectManager` gained `object? CaptureTextureSizeState()` (returns the current
    `_knownTextureSizes` reference, or `null`) and `void RestoreTextureSizeState(object? state)`
    (restores it, or clears to `null` for anything that isn't the right dictionary type). No
    cloning needed -- same reasoning already documented on the tsx tracking dictionaries:
    `LoadAnimationChain` always replaces `_knownTextureSizes` wholesale, never mutates it in place.
  - `TabEntry` gained `object? CachedTextureSizeState`; `TabEditorCache.CaptureFromProject`/
    `ApplyToProject` call `pm.CaptureTextureSizeState()`/`pm.RestoreTextureSizeState(tab.
    CachedTextureSizeState)` alongside the existing `CachedTsxState` round-trip.
  - All 8 `IProjectManager` implementations (`ProjectManager` itself, `TabSwitchCacheTests`'
    `CountingProjectManager` passthrough, and 6 no-op test fakes across `AnimationEditor.Views.Tests`
    and `AnimationEditor.Core.Tests`) updated to match.
  Test: `TabSwitchCacheTextureSizeTests.
  TryActivateTabFromCache_SwitchBackAfterAnotherTabLoaded_SaveUsesThisTabsKnownTextureSizesNotTheOtherTabs`.

- [x] **Fresh-eyes pass #6.** Resolved pass #5's `ReferencedPngs` TODO (fixed, see its own DONE
  entry above -- the "never called on tab activation" severity assessment was wrong) and did a
  fresh, focused re-trace of `AppCommands.cs`'s tsx-adjacent command paths as directed. Found and
  fixed two new real, confirmed bugs, both in `AppCommands.cs` itself -- territory prior passes had
  only checked for *stale-reference* leaks (`_tsxTileset` held past its validity), never for
  *missing* tsx-awareness in a command that should have branched on it:
  - **Real bug, confirmed red first -- `ReloadAchxFromDisk` (the hot-reload handler wired to
    `IHotReloadWatcher.AchxChangedOnDisk`) unconditionally called `IProjectManager.
    LoadAnimationChain`, never `LoadTsxProject`, even when the changed file is a native tsx.**
    `AppCommands.SyncHotReloadWatcher` watches whatever path `IProjectManager.FileName` currently
    is with no extension check, and `TryActivateTabFromCache` calls it after restoring a tsx tab
    from the cache -- so a tsx tab, once visited, is watched exactly like an achx tab. Traced the
    consequence precisely rather than assuming a thrown exception: `AnimationChainListSave.
    FromString`'s hand-rolled XML parser (`ParseXml`) never validates the root element name, so
    parsing a tsx's `<tileset>` root as an achx does not throw -- it silently succeeds with an
    empty `AnimationChainListSave` (zero chains), wiping the tab's animation data with no error
    surfaced anywhere. Fixed by branching on the reloaded path's extension (`FilePath.Extension ==
    "tsx"`), mirroring the exact pattern `OpenProjectWorkflowAsync` already uses one screen away.
    Test: `AppCommandsHotReloadTsxTests.ReloadAchxFromDisk_NativeTsxPath_ReloadsAsTsxNotAsAnEmptyAchx`
    (failed with `IsNativeTsxProject` flipping to `false` and chains wiped to empty before the fix).
  - **Real bug, confirmed red first -- `SaveCurrentAnimationChainListAsync`'s ("Save As") file-type
    dropdown unconditionally offered only `achj`/`achx` choices, even for a native tsx project.**
    `SaveCurrentAnimationChainList(path)` already correctly routes a tsx project's save through
    `_pm.SaveTsxProject(target)` regardless of `target`'s extension, so letting the achj/achx
    choices through here would let a user save real Tiled-tileset-XML content under a misleadingly
    -named `.achx`/`.achj` file -- and nothing in `MainWindow.axaml`/`.axaml.cs` gates the Save As
    menu item off for a native tsx project, so this is reachable, not just theoretical. (The
    *default* extension was already correct by construction -- it falls back to whatever extension
    is already loaded, which is `tsx` for a tsx project -- only the dropdown's fixed choice list
    was wrong.) Fixed by branching the `FileTypeChoice` list on `_pm.IsNativeTsxProject`, offering a
    single `tsx` choice instead. Test:
    `AppCommandsSaveAsTests.SaveCurrentAnimationChainListAsync_NativeTsxProject_OffersOnlyTsxChoice`
    (failed, offering `["achj", "achx"]`, before the fix).
  - **Traced, not a bug: `SaveCurrentAnimationChainList`'s own tsx/achx branch, `TiledTilesetSyncRunner
    .SyncAll`'s per-tsx isolation, and `GetChainNamesWithTsxIssues`'s call sites -- all safe.** No
    other call site in `AppCommands.cs` calls `LoadTsxProject`/`SaveTsxProject`/`LoadAnimationChain`/
    `SyncAll`/`GetChainNamesWithTsxIssues` outside the ones already covered by this pass or prior
    ones; `CaptureTsxState`/`RestoreTsxState`/`CaptureTextureSizeState`/`RestoreTextureSizeState` are
    called only from `TabEditorCache`, never directly from `AppCommands.cs` (confirmed zero
    references). Traced whether `SyncAssociatedTiledTilesets` writing to an associated tsx file that
    happens to be open (and hot-reload-watched) in a *different* editor window could cause a
    surprising reload: it can, but confirmed this is the hot-reload watcher's own correct, intended
    behavior (an external process changed the file on disk, so the open tab should pick it up) -- not
    a gap, and now handled correctly by the `ReloadAchxFromDisk` fix above instead of misparsing.
    `AddAssociatedTiledTilesetViaDialogAsync` lets a user associate a Tiled tileset from within an
    already-native-tsx project's own tab; harmless since `SaveCurrentAnimationChainList` already
    skips `SyncAssociatedTiledTilesets` whenever `IsNativeTsxProject` is true (line 482), so the
    association is stored but never acted on -- a UI affordance that does nothing useful, not a
    functional bug, and not chased further.
  - **Honest assessment: not a clean pass, and a stronger one than pass #5.** Two new real,
    confirmed bugs were found (plus the `ReferencedPngs` TODO resolved), both surfaced specifically
    by the task's directed re-trace of `AppCommands.cs` rather than the `.Core`-only mapper/sync
    files most prior passes focused on -- confirming that file was a real gap in this sweep's
    coverage, not exhausted territory. Per the stop condition, a pass #7 is needed. The bug class
    found this pass (a command method silently missing a tsx/achx branch it should have had) is
    distinct from every prior pass's bug class (broken `ParentId` geometry, unchecked casts,
    reused-instance state leaks) -- worth another `AppCommands.cs`-focused look before assuming the
    command layer is now exhausted too.

- [x] **Fresh-eyes pass #7 -- full line-by-line read of `AppCommands.cs` end to end (not just
  re-tracing known tsx call sites, per the task's explicit instruction), plus a check of
  `TabController.cs`.** Found and fixed two new real, confirmed bugs, both in the exact bug class
  pass #6 introduced (a command silently missing a tsx-awareness branch that `OpenProjectWorkflowAsync`
  /`ReloadAchxFromDisk`/`SaveCurrentAnimationChainListAsync` already have) but in call sites pass #6's
  narrower re-trace never reached because they never call `LoadTsxProject`/`SaveTsxProject` directly --
  they bypass `IProjectManager` entirely for the fields that matter:
  - **Real bug, confirmed red first -- `NewFile` (File > New) never cleared `ProjectManager`'s
    private native-tsx state (`_tsxTileset` and its two tracking dictionaries).** It sets
    `AnimationChainListSave`/`FileName`/`OnDiskCoordinateType` directly instead of going through
    `LoadAnimationChain` (which already resets tsx state as part of its own earlier fix), so
    `IsNativeTsxProject` stayed `true` after File > New from an active tsx tab. Consequence traced
    concretely, not assumed: a subsequent Save As would offer only the `tsx` file-type choice
    (`SaveCurrentAnimationChainListAsync`'s branch on `IsNativeTsxProject`), and
    `SaveCurrentAnimationChainList` would route the brand-new, empty document through
    `SaveTsxProject` against the *stale* tileset instead of a plain achx/achj save -- the same
    "silent misparse/mishandle, not a crash" severity as every other bug in this class. Fixed with
    one line, `_pm.RestoreTsxState(null)`, reusing the exact reset seam `TabEditorCache`/
    `LoadAnimationChain` already use. Test:
    `AppCommandsNewFileTests.NewFile_AfterOpenTsxWorkflow_ClearsNativeTsxState`.
  - **Real bug, confirmed red first -- `CloseProject` (File > Close Project) had the identical
    gap**, for the identical reason (sets `AnimationChainListSave`/`FileName` directly, never calls
    `LoadAnimationChain`). Fixed the same way. Test:
    `AppCommandsCloseProjectTests.CloseProject_AfterOpenTsxWorkflow_ClearsNativeTsxState`.
  - **Every other command/method in the ~2300-line file traced and confirmed safe** against the
    question "does this correctly branch on `IsNativeTsxProject`/extension wherever it needs to, or
    does it silently mishandle a tsx project?": the open/load family
    (`OpenAchxWorkflowAsync`/`OpenTsxWorkflowAsync`/`OpenProjectWorkflowAsync`/`LoadAnimationChain`/
    `LoadAnimationChainFromParsed`/`FinishLoadIntoEditor`) already branches correctly or delegates to
    a `ProjectManager` method that already resets tsx state; the tab-cache family
    (`CaptureTabEditorState`/`TryActivateTabFromCache`/`ActivateTabContentAsync`/
    `RestoreTabSelection`/`RestoreSelection`) already round-trips tsx state via `TabEditorCache`
    (fixed in an earlier pass); the save family (`SaveCurrentAnimationChainList`,
    `SaveCurrentAnimationChainListAsync`, `SyncAssociatedTiledTilesets`,
    `AddAssociatedTiledTileset(ViaDialogAsync)`) already branches correctly (pass #6) or was already
    traced as a harmless no-op for a tsx project (pass #6); `ExportToPixiJsAsync` and
    `AdjustUVAfterResize` operate on the already-mapped abstract `AnimationChainListSave` model and
    have no tsx/achx-specific behavior to get wrong; every chain/frame/shape mutation command (add,
    delete, move, duplicate, flip, paste, cut, reorder, set-props, lock) mutates the same abstract
    model regardless of its tsx-or-achx origin and is picked up by the already-correct
    `OnAnimationChainsChanged` -> `SaveCurrentAnimationChainList` autosave path; the hot-reload
    family (`WireHotReloadWatcher`, `ReloadAchxFromDisk` -- fixed pass #6, `ReloadPngFromDisk`,
    `SyncHotReloadWatcher`, `GetReferencedAbsolutePngPaths`) is either already fixed or has no
    tsx-specific branch to add (PNG-list watching is format-agnostic by design). No "New Animation
    Chain" command exists separate from the traced `AddAnimationChain`/`AddNewAnimationChain`
    family, and none of them have a tsx-specific gap -- adding a chain to a tsx-derived ACLS is
    ordinary ACLS mutation, already covered by the save-routing fixes. No recent-files/MRU logic
    exists in this file at all (grepped for `Recent`/`MRU` -- zero matches); that lives elsewhere,
    out of this file's scope.
  - **`TabController.cs` -- safe, no gap.** `CaptureLeavingTab` delegates to
    `IAppCommands.CaptureTabEditorState`, which is already fully tsx-aware (captures via
    `TabEditorCache.CaptureFromProject`, fixed in an earlier pass); `EnsureCurrentDocumentHasTab`
    only reads `IProjectManager.FileName`/`AnimationChainListSave` to decide tab bookkeeping and
    never touches tsx-specific state. No "Close Tab"/"Close All Tabs" command exists in
    `AnimationEditor.Core` at all -- `TabManager.Close` (a different file, not in this sweep's
    scope) only removes the tab from its own list and reactivates whichever tab was previously
    active; it never calls `CaptureTabEditorState` for the tab being discarded, which is correct
    (there is nothing worth capturing for state about to be thrown away) rather than a gap.
  - **Honest assessment: `AppCommands.cs` now reads exhausted for this specific bug class.** This
    was a genuine full re-read (not a targeted re-trace), and it found real bugs in exactly the two
    places that share the shape "sets `ProjectManager` fields directly instead of going through one
    of the three already-audited choke points (`LoadAnimationChain`/`LoadTsxProject`/
    `TabEditorCache`)" -- both now fixed, and every other call site either already routes through
    one of those choke points or operates on the tsx-agnostic abstract model where no branch is
    needed. Unlike pass #6 (which found gaps precisely because it hadn't yet done a full read),
    this pass *was* the full read, and it surfaced exactly two hits before running out -- a real
    signal the file's coverage for "missing tsx branch" is now complete, not just quiet. The
    remaining risk in this bug class, if any, is more likely in the Avalonia `.App`/`.Views` UI
    layer (out of this sweep's declared scope) than in `AppCommands.cs` itself.

- [x] **Fresh-eyes pass #8 -- full line-by-line read of `ProjectManager.cs` end to end** (as opposed
  to the many prior passes' targeted re-traces of just its tsx-specific methods), per the exact
  approach that worked for `AppCommands.cs` in pass #7. Read and evaluated every member in the file
  against "does it correctly account for native-tsx state the way the already-fixed choke points
  do." Found and fixed one new real gap (a defense-in-depth guard, not a currently-reachable bug),
  and surfaced one new UI-layer TODO item (above) via a repo-wide grep this pass ran as part of the
  task's item 5.
  - **Method-by-method verdicts** (public/private members with any load-bearing logic; trivial
    pass-throughs and pure-static helpers with no state omitted):
    - `AnimationChainListSave`, `FileName`, `OnDiskCoordinateType` (properties) -- plain settable
      state, already covered by `TabEditorCache`'s direct round-trip (fresh-eyes pass #5's field
      audit); a direct external set on a tsx project is the UI-layer TODO above, not a
      `ProjectManager`-internal gap.
    - `TileMapInformationList` -- static, unrelated to tsx (traced pass #1).
    - `ReferencedPngs` -- already fixed (pass #5/#6).
    - `IsNativeTsxProject`, `TsxTileSize` -- pure computed properties over `_tsxTileset`, safe.
    - `ProjectFolderPath` -- session-wide by design, not per-load state (traced pass #5).
    - `LoadAnimationChain` -- already fixed (clears tsx state, `ReferencedPngs`); re-verified
      correct this pass, including its `TryLoadProjectFile`/no-`ProjectFile`-branch split.
    - `NormalizeCoordinatesToUv` -- static, no hidden state (task item 2); just seeds a cache from
      the `knownTextureSizes` parameter and delegates to `ConvertCoordinates`.
    - `SaveAnimationChainList(string)` / `(Stream)` / `SaveAnimationChainListAsync(Stream)` --
      **gap found and fixed, see below** (task item 3).
    - `IsJsonPath`, `RunWithDiskCoordinateConversion(Async)`, `BuildSeedCache`, `ConvertCoordinates`,
      `TryResolveTextureSize`, `GetTextureSizeInPixels`, `TryReadPngSize`,
      `NormalizeFrameTextureNames` -- all either static/stateless or read only `_knownTextureSizes`/
      `FileName`, no tsx-specific state involved.
    - `ResolveFilesPanelRoot`, `FindContentAncestor`, `TryLoadProjectFile` (task item 1),
      `FindMissingTextures`, `LoadTileMapInformation` -- achx/project-file/Files-panel concerns with
      no tsx interaction; a native tsx project never has an `AnimationChainListSave.ProjectFile`
      value (`TiledAnimationToAchjMapper.Map` never sets one), so `ResolveFilesPanelRoot`'s
      project-file branch is simply inert for a tsx project, not a gap.
    - `LoadTsxProject`, `SaveTsxProject` -- already exhaustively audited across every prior pass;
      re-read confirmed the all-or-nothing commit pattern still holds.
    - `CaptureTsxState`/`RestoreTsxState`/`CaptureTextureSizeState`/`RestoreTextureSizeState` --
      already fixed (pass #6-adjacent), re-verified correct.
    - `GetChainNamesWithTsxIssues`, `BuildTsxTilesetInfo` -- already audited (pass #3, #5's
      `ProjectManager`-layer agreement tests); re-read found no new gap.
  - **Real gap found and fixed (task item 3) -- `SaveAnimationChainList(string)`, its `Stream`
    overload, and `SaveAnimationChainListAsync(Stream)` had no guard against being called on a
    native tsx project.** `AppCommands.SaveCurrentAnimationChainList` is the only production call
    site that branches on `IsNativeTsxProject`, but it is not the only caller of these methods: a
    repo-wide grep for `SaveAnimationChainList`/`SaveAnimationChainListAsync` found
    `AnimationEditor.Browser\App.axaml.cs` calling the `Stream`/`Async` overloads directly, bypassing
    `AppCommands` entirely (confirmed not currently reachable there specifically, since the browser
    build never calls `LoadTsxProject` -- grepped, zero matches -- so `IsNativeTsxProject` can never
    be true on that build today). Without a guard, calling any of these three methods on a tsx
    project would silently write achx/achj-format content derived from the tsx's own
    `AnimationChainListSave` view, either corrupting the target path/stream or (worse, if pointed at
    the live `.tsx` file) replacing real Tiled tileset XML with achx XML. Same "defense-in-depth
    guard for a latent, not-currently-reachable-via-current-flow gap" shape as the already-fixed
    `Columns <= 0` guards in `TiledAnimationToAchjMapper`/`TsxAnimationValidator` (see the
    "Corrupt/negative or out-of-range values elsewhere" DONE entry above) -- fixed rather than left
    as a TODO, following that same precedent. Fixed with a new private
    `ProjectManager.ThrowIfNativeTsxProject()` helper called at the top of all three methods,
    throwing `InvalidOperationException` naming `SaveTsxProject` as the correct alternative.
    Confirmed the first draft of this test passed for the wrong reason (a fresh `pm` defaults
    `OnDiskCoordinateType` to `Pixel`, and the tsx fixture's texture PNG doesn't exist on disk, so
    `ConvertCoordinates`'s existing "texture size could not be resolved" guard threw first,
    masking the intended guard entirely) -- fixed by setting `OnDiskCoordinateType = UV` in each
    test (no texture-size resolution needed) and asserting the exception message names
    `SaveTsxProject`, so the tests fail for the intended reason. Tests:
    `ProjectManagerTsxProjectTests.SaveAnimationChainList_NativeTsxProjectLoaded_ThrowsInsteadOfWritingAchxFormatContent`,
    `SaveAnimationChainList_StreamOverload_NativeTsxProjectLoaded_ThrowsInsteadOfWritingAchxFormatContent`,
    `SaveAnimationChainListAsync_NativeTsxProjectLoaded_ThrowsInsteadOfWritingAchxFormatContent`.
  - **Task items 1, 2, 4 -- traced, no gap.** `TryLoadProjectFile` (item 1) is purely achx-side: it
    only ever runs from `LoadAnimationChain`'s `acls.ProjectFile` branch, which a native tsx project
    never populates, and it touches only `ReferencedPngs` (already covered). `NormalizeCoordinatesToUv`
    (item 2) is a stateless static helper with no fields of its own. Every other private field/method
    not yet named in a prior pass's DONE entry (`RunWithDiskCoordinateConversion(Async)`,
    `BuildSeedCache`, `TryResolveTextureSize`, `NormalizeFrameTextureNames`, `FindContentAncestor`)
    (item 4) only ever touches `_knownTextureSizes` (already tsx-independent by design -- texture
    sizes apply to achx/achj projects, tsx projects have no equivalent concept) or is pure/static; no
    tsx-awareness gap in any of them. No `WriteRecoveryFile`/`TryReadRecoveryFile`/`DeleteRecoveryFile`/
    `ApplySettings`/`SaveCompanionFileFor` methods exist in this file at all (grepped -- those live in
    `IoManager`/`AppSettings`, different files, out of this file's scope).
  - **Task item 5 -- external-caller-desync risk: real finding, but in the UI layer, not
    `ProjectManager.cs` itself (see new TODO item above).** Grepped the whole repo for every
    `.AnimationChainListSave =` and `.FileName =` assignment. Confirmed both of `AppCommands.cs`'s
    own two direct-assignment sites (`NewFile`, `CloseProject`) already call `RestoreTsxState(null)`
    immediately after (fixed in pass #7) -- no gap in `AppCommands.cs`. But the same grep surfaced
    several sites in `AnimationEditor.App`/`.Views`/`.Browser` (outside this sweep's declared file
    scope) that assign `AnimationChainListSave`/`FileName` directly with no equivalent
    `RestoreTsxState(null)` call -- the identical bug class pass #7 fixed twice in `AppCommands.cs`,
    reached through a different, out-of-scope layer. Not fixed here; see the new TODO item above for
    the specific sites and why a shared reset helper (not a per-site patch) is the right shape for
    that follow-up.
  - **Honest assessment: `ProjectManager.cs` reads exhausted for the "missing tsx-awareness branch"
    bug class specifically.** This was a genuine full read, not a targeted re-trace, and (like pass
    #7's equally thorough read of `AppCommands.cs`) it surfaced exactly one real gap before running
    out -- a defense-in-depth guard, not a live bug, and smaller in severity than every prior pass's
    findings. Every other member traced cleanly to either "already fixed by a prior pass" or "no
    tsx-specific behavior applies here." The remaining risk this pass could find is concentrated
    entirely in the Avalonia `.App`/`.Views`/`.Browser` UI layer (the TODO item above), matching pass
    #7's own prediction exactly -- not somewhere in `.Core` that a ninth pass of the same files is
    likely to find. A ninth `ProjectManager.cs`- or `AppCommands.cs`-focused pass is not recommended;
    the UI-layer TODO item is the highest-value next target if this sweep continues.

- [x] **UI layer (`AnimationEditor.App`/`.Views`/`.Browser`) had multiple direct
  `AnimationChainListSave =`/`FileName =` assignment sites that bypassed
  `AppCommands.NewFile`/`CloseProject` (both already call `RestoreTsxState(null)`, fixed in pass
  #7) -- the identical bug class, reached through a different code path (pass #8's TODO above).
  **Design chosen:** a single `IProjectManager.ResetToBlankDocument()` method (implemented on
  `ProjectManager`), rather than repeating the field list at every call site. It resets everything
  a reused `ProjectManager` instance can leak between documents/tabs: `AnimationChainListSave`
  (fresh empty), `FileName` (`null`), `OnDiskCoordinateType` (back to `Pixel`), and every native-
  tsx/texture-size/`ReferencedPngs` tracking field via the existing `RestoreTsxState(null)`/
  `RestoreTextureSizeState(null)` (`ReferencedPngs` had no existing round-trip helper, so
  `ResetToBlankDocument` clears it directly). It deliberately leaves `ProjectFolderPath` alone --
  that field is session-wide by its own doc comment, not per-document state -- and leaves tab/
  undo/selection state to the caller, since those differ per site (some preserve a caller-supplied
  selection, some reset unconditionally).
  - **`NewFile`/`CloseProject` refactored onto the same helper**, closing the audit gap the task
    asked for: neither previously reset `ReferencedPngs` or the texture-size-state field, both
    latent leaks of the same class already fixed for `LoadAnimationChain` (see pass #5's DONE
    entry). `NewFile` also silently changed `FileName` from `string.Empty` to `null` as a side
    effect of sharing the helper -- confirmed safe: every consumer in this codebase reads
    `FileName` via `string.IsNullOrEmpty`, never an exact-`null`/exact-`""` comparison (grepped),
    and `AppCommandsNewFileTests.NewFile_ClearsFileName` already asserts via `IsNullOrEmpty`.
  - **Sites fixed** (all via `_projectManager.ResetToBlankDocument()`/`projectManager.
    ResetToBlankDocument()`):
    - `MainWindow.axaml.cs`'s `ActivateUntitledTabContent` -- switching to an already-open
      Untitled tab while a native tsx tab was active left `IsNativeTsxProject` stuck true, since
      this path bypasses `TryActivateTabFromCache`/`RestoreTsxState` entirely. Confirmed reachable
      and real (not merely theoretical): a red-first UI test reproduced it end-to-end.
    - `MainWindow.axaml.cs`'s `CloseTabCore`'s "all tabs closed -- start fresh" branch -- same
      leak, reached by closing the last tab of a native tsx project.
    - `MainWindow.axaml.cs`'s `OpenAsNewUnsavedDocument` (shared by File > New and the crash-
      recovery-restore path) -- same leak, reached by File > New from an active tsx tab.
    - `AnimationEditor.Browser\App.axaml.cs`'s `CloseTab`'s "last tab closed" branch -- not
      reachable today (confirmed: the browser build never calls `LoadTsxProject` anywhere, grepped
      zero matches), fixed anyway for consistency with every other "start fresh" site so a future
      tsx-on-browser extension doesn't reintroduce the leak. Cheapest possible fix (a drop-in
      method-call swap), no meaningful cost to matching the pattern.
  - **Sites deliberately left unfixed:**
    - `HandleStartupAsync`'s two empty-state branches (the memory-probe branch and the
      no-recovery/no-CLI-arg/no-saved-tabs branch) -- **initially routed through
      `ResetToBlankDocument()` for "defense-in-depth consistency" per the task's suggestion, then
      reverted after this caused 5 real `AnimationEditor.App.Tests` failures**
      (`PngDropApplyTests`, `FramePixelCoordsMultiSelectTests` x2, `FrameTextureNameMultiSelectTests`).
      Root cause: several existing tests construct a `MainWindow` and pre-seed
      `ctx.ProjectManager.FileName`/`AnimationChainListSave` *before* `window.Show()` fires
      `OnOpened` -> `HandleStartupAsync` (fire-and-forget, but runs synchronously to completion in
      this branch since no `await` is reached first); the original code's fallback branch only
      ever reset `AnimationChainListSave`, deliberately leaving `FileName` alone, and those tests
      depend on that narrower behavior surviving startup. This is exactly the "verified vs.
      inferred" trap: the original per-site reachability read (a freshly-constructed
      `ProjectManager` has no prior tsx/`FileName` state, so the extra resets are a no-op in
      *production*) was correct but incomplete -- it didn't check *test* reachability, where
      state is deliberately pre-seeded ahead of the exact code path being widened. Reverted both
      branches to their original narrow form (`_projectManager.AnimationChainListSave = new
      AnimationChainListSave();`, `FileName`/tsx/texture-size/`ReferencedPngs` untouched). Full
      `AnimationEditor.App.Tests` suite confirmed green (985/985) after reverting. This vindicates
      the prior pass's original "likely unreachable, leave as-is" assessment -- confirmed correct,
      and it should not have been revisited without new evidence.
    - `AnimationTreeControl.axaml.cs`'s `AddAnimationChainAndBeginInlineRename` and its
      `MainWindow.axaml.cs` sibling -- confirmed low-risk, not fixed: both only assign
      `AnimationChainListSave = new AnimationChainListSave()` when it is already `null`, and
      `AnimationChainListSave`/`_tsxTileset` are always set together by every code path that sets
      either (`LoadTsxProject`, `RestoreTsxState`, `TabEditorCache.ApplyToProject`,
      `ResetToBlankDocument` itself) -- so `AnimationChainListSave is null` implies
      `IsNativeTsxProject` is already false, making this guard structurally unreachable while a
      tsx project is active. Also semantically different from a "start a fresh document" site
      (mid-session null-guard, not a document-identity reset), so it doesn't belong in
      `ResetToBlankDocument`'s call list even if it were reachable.
  - **Testable core, `ProjectManager.ResetToBlankDocument()`** (TDD: confirmed red against a
    temporary no-op stub, then green against the real implementation):
    `ProjectManagerResetToBlankDocumentTests.ResetToBlankDocument_AfterLoadTsxProject_ClearsNativeTsxState`,
    `ResetToBlankDocument_AfterLoadAchxWithProjectFileAndKnownTextureSizes_ClearsReferencedPngsAndTextureSizeState`.
  - **UI-layer verification: exercised through real `MainWindow`/window-level tests, not just
    eyeballed.** Each fixed site got a dedicated headless Avalonia test (`AnimationEditor.App.
    Tests`, using the `AvaloniaFact`/`ctx.CreateMainWindow()`/reflection-invoked-private-method
    pattern already established by `CloseLastTabTests`/`TabSwitchUndoTests`/`LoadTsxProjectTests`
    in that project), each confirmed red-first by temporarily reverting the corresponding
    production fix and observing the assertion fail for the intended reason, then re-confirmed
    green: `CloseLastTabTests.ClosingLastTsxTab_ClearsNativeTsxState`,
    `NewAndLoadResetTests.New_AfterOpeningTsxTab_ClearsNativeTsxState`,
    `ActivateUntitledTabTsxStateTests.ActivateTabAsync_SwitchBackToUntitledTabAfterViewingTsxTab_ClearsNativeTsxState`.
    The Browser `CloseTab` fix has no equivalent test (no `AnimationEditor.Browser` test project
    exists in this repo, confirmed by search) -- left as a thin, visually-reviewed wiring
    call-site swap, consistent with this being a not-currently-reachable defense-in-depth fix
    rather than a live bug.
  - **Full suite confirmation:** `AnimationEditor.Core.Tests` 2176/2176 (was 2174, +2 new),
    `AnimationEditor.Views.Tests` 144/144 (unchanged), `AnimationEditor.App.Tests` 988/988 (was
    985, +3 new), `AnimationEditor.Browser` builds clean (0 warnings/0 errors; no test project to
    run).

- [x] **Drag-and-drop silently ignored `.tsx` files (found mid-pass by fresh-eyes pass #9, which
  stalled before finishing -- see the completed pass #9 entry below for the rest of that pass).**
  `AchxDropProcessor.SelectAchxFiles` (the "OS file dropped onto the window" classifier) only
  recognized `.achx`/`.achj`, so dropping a native `.tsx` project onto the AnimationEditor window
  did nothing -- `OnWindowDrop` filtered it out before `LoadAnimationFileAsync` (which already
  dispatches correctly by extension via `OpenProjectWorkflowAsync`, same as `File > Open`) ever saw
  it. Real bug, confirmed red first (`ContainsAchx_TsxOnly_ReturnsTrue`/
  `SelectAchxFiles_TsxFile_IsIncludedAlongsideAchx` both failed against the original filter, then
  passed after adding the `tsx` extension check). Fixed in
  `tools/AnimationEditorAvalonia/src/AnimationEditor.Core/DragDrop/AchxDropProcessor.cs`;
  `MainWindow.axaml.cs`'s `OnWindowDrop` needed no logic change (just a variable-name/comment
  update), since it already delegates the actual open to the already-tsx-aware
  `LoadAnimationFileAsync`. Tests: `AchxDropProcessorTests.ContainsAchx_TsxOnly_ReturnsTrue`,
  `SelectAchxFiles_TsxFile_IsIncludedAlongsideAchx`. Full suite after this fix:
  `AnimationEditor.Core.Tests` 2178/2178, `AnimationEditor.App` builds clean.

- [x] **Fresh-eyes pass #9 (completed) -- finished the full line-by-line read of
  `MainWindow.axaml.cs` after the interrupted attempt above.** The interrupted attempt had already
  found/fixed the drag-and-drop bug (see previous entry) and confirmed direct `TabEntry`
  construction (category 6) was a non-issue; this continuation covered the five categories it never
  reached, plus a full re-read of everything in between (not just the interrupted agent's partial
  notes) since those notes weren't trusted as complete on their own:
  1. **Recent-files/MRU menu -- safe, no gap.** `RefreshRecentFiles`/`CreateNativeMenuActions`
     (macOS `NativeMenu`) and the `CurrentFileChanged`-driven `_appSettings.AddFile` both route
     through `LoadAnimationFileAsync` -> `AppCommands.OpenProjectWorkflowAsync`, already confirmed
     tsx-aware by prior passes; `AppSettingsModel.AddFile`/`RecentFiles` are plain path strings with
     no extension filtering, format-agnostic by construction.
  2. **Window/tab title staleness -- one real bug, confirmed red first, fixed.**
     `TitleBarHelper.BuildWindowTitle` itself is a pure `FileName`-string mapper with no tsx logic
     (safe), and every reachable transition already called `UpdateTitle()` explicitly
     (`ActivateUntitledTabContent`, the "all tabs closed" branch, `CloseProjectAsync`) or via the
     `CurrentFileChanged` event (`TryActivateTabFromCache`, `OpenProjectWorkflowAsync`,
     `SaveCurrentAnimationChainListAsync`'s successful-save path) -- except
     `OpenAsNewUnsavedDocument` (shared by `OnNewClick` and the crash-recovery restore branch),
     which reset `ProjectManager` state via `ResetToBlankDocument()` (a plain field reset, no event)
     and never called `UpdateTitle()` itself. `OnNewClick`'s own follow-up
     (`SaveCurrentAnimationChainListAsync`) only updates the title on a *successful* Save As --
     cancelling the dialog (the default behavior of `NullFileDialogService`, used by every test in
     this file's test class) returns early with no title update. Net effect: File > New from a
     named tsx (or achx) tab, with the Save As dialog cancelled, left the title bar showing the
     just-closed file's name even though `FileName`/`IsNativeTsxProject` had already reset
     underneath it -- not fixable-as-cheap by patching `OnNewClick` alone, since the same gap is
     reachable via the crash-recovery restore call site too. Fixed with one `UpdateTitle()` call
     inside `OpenAsNewUnsavedDocument` itself, right after its own reset, matching the pattern every
     other reset site already uses. Test:
     `NewAndLoadResetTests.New_AfterOpeningTsxTabAndCancelingSaveAs_TitleNoLongerShowsOldFileName`
     (failed with the title still containing `"Heroes.tsx"` before the fix).
  3. **Menu-item enable/disable gating on project type -- safe, no gap.** No menu item in this file
     is enabled/disabled based on `IsNativeTsxProject`; the only `IsNativeTsxProject`-gated UI is
     the property-inspector panel visibility toggles (`PropRectPanel`/`PropCirclePanel`/
     `PropTransformSection`/`PropColorSection`), all already correct. `MenuAssociateTiledTileset`
     (Associate Tiled Tileset) stays enabled for a tsx project and does nothing useful when clicked
     (already traced as a harmless no-op in pass #6, since `SaveCurrentAnimationChainList` skips
     `SyncAssociatedTiledTilesets` whenever `IsNativeTsxProject` is true) -- confirmed still the
     case, not re-litigated. `DoResizeTextureAsync` (Resize Texture) operates on a frame's texture
     PNG file directly, independent of achx/tsx schema, so it has no tsx-specific behavior to gate.
  4. **Crash-recovery restore -- safe, no gap; confirmed tsx projects structurally never
     participate in recovery, and that's correct, not a scope gap.** Traced `WriteRecoveryFile`'s
     one call site (`AppCommands.SaveCurrentAnimationChainList`'s `else` branch, which only fires
     when `_pm.FileName` is empty/null) against the fact that a native tsx project can only exist by
     opening a real `.tsx` file from disk -- there is no "blank tsx" concept reachable via File >
     New, so `IsNativeTsxProject` and "`FileName` is null" are mutually exclusive states in this
     codebase. Every edit to a *named* file (tsx or achx) autosaves immediately via the same method's
     `if` branch instead, so there is no unsaved-data window a crash could lose for a tsx project in
     the first place -- recovery exists specifically for the untitled-document case, which a tsx
     project can never be in. `TakeRecoveryFileContent`/`OpenAsNewUnsavedDocument`/
     `RecoveredDocumentBanner`'s dismiss handler have no format-specific logic of their own to get
     wrong.
  5. **Keyboard shortcut / command-palette dispatch -- safe, no gap.** No command palette exists in
     this file (grepped, zero matches). Every entry in `BuildHotkeyDefinitions`' hotkey registry
     delegates straight to an already-audited handler (`OnNewClick`, `LoadAsync`, `OnSaveClick`,
     `HandleCopyAsync`/`HandleCutAsync`/`HandlePasteAsync`/`HandleDuplicate`/`HandleDelete`,
     `_undoManager.Undo`/`Redo`, tree reorder/rename, panel zoom) -- none of the `Action` delegates
     implement inline project-state-touching logic that bypasses `AppCommands`. The `KeyDown`/`KeyUp`
     tunnel handlers in `WireKeyboard` are pure gesture-matching-and-dispatch with no tsx-adjacent
     logic of their own.
  - **Traced, not a bug: `SyncGridControlsToProject`'s tsx-forced grid lock has no `else` branch to
    revert when switching away from a tsx project.** While a native tsx tab is active, this method
    forces `SnapToGridCheck`/`GridSizeInput` to the tsx's own tile size (issue #1140, by design --
    see its own doc comment). Switching to an achx with no companion settings file yet (so
    `ApplyCompanionSettings` never fires) leaves those controls showing the previous tsx's
    tile-size/on values rather than resetting to any achx-specific default. Not fixed: this
    "sticky UI setting persists until an explicit companion-file override arrives" behavior already
    applies identically to achx-to-achx transitions with no companion file and predates the tsx
    feature entirely -- the tsx feature only changed how the value gets forced *while active*, not
    whether anything resets it on the way out. Since a plain achx-to-achx transition has the exact
    same "no reset without a companion file" shape, this isn't a tsx-awareness branch that's missing;
    it's pre-existing general UI behavior the tsx feature never needed to (and didn't) change. Purely
    cosmetic/UI (affects the wireframe's snap-to-grid increment only, never anything that reaches
    disk) -- no test added.
  - **Full re-read confirmed clean beyond the two items above:** `ActivateUntitledTabContent`, the
    "all tabs closed" branch, `OpenAsNewUnsavedDocument`'s tsx-state reset (aside from the missing
    `UpdateTitle()` call, now fixed), `HandleStartupAsync`'s crash-recovery/CLI-arg/saved-tabs
    branches, drag-and-drop via `AchxDropProcessor`, and every direct `TabEntry`/
    `AnimationChainListSave =`/`FileName =` assignment site in this file (five sites total, all
    previously audited: `ActivateUntitledTabContent`'s post-reset content assignment,
    `HandleStartupAsync`'s two deliberately-narrow memory-probe/no-recovery branches,
    `OpenAsNewUnsavedDocument`'s post-reset content assignment, and
    `AddAnimationChainAndBeginInlineRename`'s null-guard, which is structurally unreachable while a
    tsx project is active) all hold up against a fresh read, not just the interrupted agent's notes.
  - **Verdict: this is now a genuinely clean-for-this-bug-class pass.** One real, confirmed bug was
    found (title staleness) and fixed; every other category traced to "safe, no gap" with a concrete
    reason, not a shrug. Combined with the drag-and-drop fix the interrupted attempt already landed,
    fresh-eyes pass #9 is complete. Full suite: `AnimationEditor.Core.Tests` 2178/2178 (unchanged),
    `AnimationEditor.App.Tests` 989/989 (was 988, +1 new), `AnimationEditor.Views.Tests` builds
    clean.

- [x] **Fresh-eyes pass #10 -- end-to-end scenario walkthroughs instead of re-reading already
  well-trodden files, per the task's explicit instruction to try a genuinely different angle.**
  Traced 3-4 concrete multi-step user workflows through the actual current code (not synthetic
  edge cases), with a dedicated focus on Undo/Redo against the reference-keyed
  `_tsxEntryTileIdsByChain`/`_tsxSatelliteTileIdsByChain` tracking dictionaries -- an angle no
  prior pass in this sweep had checked. Found and fixed two real, confirmed bugs (one fully fixed,
  one confirmed-but-deferred to TODO with full reasoning), confirmed one scenario already safe with
  a teeth-tested pinning test, and did a full read of `TabEditorCache.cs`/`TabEntry.cs`/
  `TabController.cs`/`IProjectManager.cs` (the remaining files in/adjacent to "Files in scope" that
  hadn't had a dedicated full read, only incremental touches):
  - **Real bug, confirmed red first -- Undo of "Delete Animation" on a native-tsx chain relocates
    it instead of restoring it, for an owner-tile-not-its-own-first-frame chain.**
    `DeleteChainsCommand.Do()` removes the chain from `AnimationChainListSave.AnimationChains` and
    autosaves immediately (`AppCommands.SaveCurrentAnimationChainList` runs after every mutating
    command); `Undo()` re-inserts the *exact same* `AnimationChainSave` object at its original
    index and autosaves again. Traced why this breaks the identity hint despite both saves acting
    on the same object: `SaveTsxProject`'s post-save bookkeeping loop rebuilt
    `_tsxEntryTileIdsByChain`/`_tsxSatelliteTileIdsByChain` from scratch using only chains present
    in that save's `mapped` results (added by an earlier pass specifically so a genuinely-deleted
    chain's stale hint doesn't linger forever) -- so the chain's hint was purged during the
    Do()-triggered save (it's entirely absent from `AnimationChainListSave` at that moment), and by
    the time Undo() re-inserts the same object and saves again, no hint remains: the entry tile id
    gets recomputed fresh from frame[0], relocating a hand-authored "owner ≠ own frame[0]" chain
    away from its original tile and leaving that tile orphaned. Confirmed no command in
    `CommandsAndState` ever clones or replaces a chain/frame object across Undo/Redo (grepped for
    `.Clone()`/`DeepClone`/`new AnimationChainSave(`/`new AnimationFrameSave(` -- zero matches, and
    every `Add*Command`/`Delete*Command`/`Duplicate*Command` reviewed re-adds/re-removes the exact
    same reference), which is what makes a reference-keyed carry-forward fix sound. Fixed by having
    `SaveTsxProject`'s bookkeeping loop carry forward -- rather than drop -- hints for chains
    entirely absent from the current `AnimationChainListSave` (candidates for a later Undo/Redo
    reinsertion), while still dropping hints for chains that stay present but come back with zero
    frames (the pre-existing, still-tested zero-frame-chain guarantee is untouched, since that
    case's chain is never absent from the ACLS). Test:
    `ProjectManagerTsxProjectTests.SaveTsxProject_DeleteChainThenReinsertSameObjectAndSave_RestoresOriginalTileInsteadOfRelocating`.
  - **Real bug, confirmed but deferred to TODO (above) -- the frame-level sibling of the bug just
    fixed: Undo of "delete every frame in a chain" (`DeleteFramesCommand`) also relocates the
    chain, and the whole-chain fix above does not cover it.** Unlike whole-chain delete, the chain
    object here never leaves `AnimationChainListSave.AnimationChains` -- only its `Frames` list
    empties and refills -- so there is no "chain absent from the ACLS" signal to key a
    carry-forward decision on; the chain-present-with-null-`EntryTileId` state is indistinguishable
    (via chain-reference identity alone) from the already-tested, deliberately-different
    "clear frames then re-author with genuinely different geometry" case
    (`SaveTsxProject_AllFramesDeletedFromChain_...`), which requires the fresh recompute this
    scenario doesn't want. Confirmed real with a throwaway probe test (not kept, per this sweep's
    established practice for confirming-then-discarding a test that only pins a known, deferred
    gap) built on the same `OwnerNotFirstFrameFixtureXml` fixture. A correct fix needs a way to
    tell "same content, Undo-restored" apart from "different content, re-authored" -- e.g.
    fingerprinting the frame sequence by object reference (frames are never cloned across
    Undo/Redo either, per the same grep) alongside the dormant hint -- which means widening
    `_tsxEntryTileIdsByChain`'s value type and `MultiTileToTiledAnimationMapper.Map`'s
    `knownEntryTileIds` parameter type, a materially larger and riskier change than the whole-chain
    fix. Left as a TODO with full reasoning rather than folded into this pass; see the TODO item
    above for the complete writeup.
  - **Scenario: two DISTINCT tsx tabs switched back and forth repeatedly, editing and saving each
    in turn -- already correct, confirmed with a teeth-tested pinning test, no source change.**
    Every prior tab-switch-cache test in this sweep pairs one tsx tab with one achx tab; this was
    the untested tsx-vs-tsx combination. Traced why it already works: `CaptureTsxState` wraps the
    *current* `_tsxTileset`/tracking-dictionary instances in a fresh `TsxState` record at capture
    time, and `LoadTsxProject`/`SaveTsxProject` always reassign (never mutate in place) those
    fields, so two captures at different times necessarily hold independent object instances with
    no way to cross-contaminate. Confirmed the test has teeth by temporarily stubbing
    `CaptureTsxState` to always return `null` and observing it fail, then reverting. Test:
    `TabSwitchCacheTsxTests.TryActivateTabFromCache_TwoDistinctTsxTabsSwitchedBackAndForthRepeatedly_EachSavesOnlyItsOwnFile`.
  - **Scenario: drag-and-drop a tsx file while another (possibly-Untitled) tab is active -- safe,
    no gap, and structurally identical to File > Open, not a discrepancy between the two.** Traced
    precisely: `OnWindowDrop` and every File > Open path funnel through the same
    `LoadAnimationFileAsync`, which (a) captures the leaving tab's full state (including its undo
    history) via `_tabController.CaptureLeavingTab` before switching, and (b) registers any
    not-yet-tracked current document (including an Untitled one with in-memory-only content) as a
    background tab via `EnsureCurrentEditorContentHasTab` before opening the new file -- so nothing
    is discarded either way. This app has no general "unsaved changes, save first?" prompt at all
    for switching away from a tab (only for *closing* one) precisely because every named file
    autosaves on every edit and every Untitled tab's content is captured, not discarded, on
    switch-away -- confirmed this is consistent with fresh-eyes pass #9's own crash-recovery
    finding ("every edit to a *named* file... autosaves immediately... there is no unsaved-data
    window"). No gap to fix; no new test needed (this is the same mechanism `TabSwitchCacheTsxTests`
    and pass #9's title-staleness fix already exercise).
  - **`TabEditorCache.cs`/`TabEntry.cs`/`TabController.cs` -- full dedicated reads (all three had
    only been touched incrementally by prior passes' field-by-field additions, never read fully in
    one pass). `TabEditorCache.cs`/`TabEntry.cs`: no gap -- `Invalidate` only clears
    `CachedEditorModel`/`CachedDiskWriteTimeUtc`, leaving `CachedTsxState`/`CachedTextureSizeState`/
    `CachedReferencedPngs` stale, but confirmed harmless: `ApplyToProject` is only ever called after
    `HasFreshCache` returns `true`, which requires `CachedEditorModel != null` -- an invalidated tab
    always falls through to a full reload instead. `TabController.cs`: re-confirmed safe (already
    read fully in pass #7); `CaptureLeavingTab` atomically snapshots the undo stack and
    `CaptureTabEditorState` together in one method, so the two can never observe different
    "moments" of the same tab's state.**
  - **Real bug, confirmed red first, found while reading `TabManager.cs` (a neighbor of
    `TabController.cs`, surfaced by tracing `TabEntry.UndoSnapshot`'s only non-`TabController`
    write site) -- `TabManager.Rename` silently dropped `CachedTsxState`/`CachedTextureSizeState`/
    `CachedReferencedPngs` when replacing a `TabEntry`.** `Rename` builds a brand-new `TabEntry` and
    explicitly copies `CachedEditorModel`/`CachedOnDiskCoordinateType`/`CachedDiskWriteTimeUtc`/
    `UndoSnapshot` from the old instance, but the three tsx/texture-size/referenced-pngs cache
    fields (all added by *later* fixes in this same sweep) were never added to that copy list.
    Traced reachability precisely rather than assuming: `Rename`'s only caller
    (`MainWindow.axaml.cs`'s `CurrentFileChanged` handler) only invokes it to promote an Untitled
    tab to a real file path after a successful Save/Save-As -- and an Untitled tab can never carry
    native-tsx state (there is no "blank tsx" reachable via File > New, per pass #9's own finding),
    so this is not reachable in production today. Fixed anyway, matching this sweep's established
    "cheap, risk-free, defense-in-depth for a latent instance of an already-fixed leak class"
    precedent (e.g. the Browser `CloseTab` fix in the UI-layer `ResetToBlankDocument` entry above):
    a three-line copy-list addition with no behavior change on any currently-reachable path. Test:
    `TabManagerTests.Rename_CarriesForwardTsxAndTextureSizeAndReferencedPngsCache`.
  - **`IProjectManager.cs` -- full read, no gap.** Pure interface declaration with XML docs already
    audited for accuracy by prior passes; no behavior to get wrong.
  - **Honest assessment: not a clean pass, but the strongest signal yet that the "missing
    tsx-awareness branch" and "reused-instance state leak" bug classes (the shapes every pass since
    #5 has been finding) are giving way to a narrower, genuinely novel class: reference-keyed
    bookkeeping dictionaries whose lifecycle is not itself part of the undo/redo history, so
    replaying a command's own Do/Undo can desync them from the state the undo stack represents.**
    This pass deliberately avoided re-reading the heavily-trodden mapper/sync files and instead
    walked realistic multi-step scenarios end-to-end, and the two real bugs it found (both in the
    Undo/Redo-vs-tsx-identity-tracking interaction, confirmed as the most promising *novel* angle
    by the task itself) came from that shift in approach, not from finding new territory in
    already-audited files -- `TabEditorCache.cs`/`TabEntry.cs`/`TabController.cs`/
    `IProjectManager.cs` all read clean on a genuine first full pass. One of the two bugs found
    (the frame-level Undo case) was deliberately deferred to TODO rather than fixed, since a safe
    fix requires a materially larger change (widening a hint dictionary's value type across its
    parameter signature and every call site) than this pass's risk budget should absorb in one
    sitting -- a first for this sweep, which has fixed every previously-found bug in the same pass
    it was discovered in. This is not a clean pass by the stop condition's letter, but the *shape*
    of what it found (a deferred, larger design question rather than a quick localized fix) reads
    as the sweep approaching genuine exhaustion of the cheap, obviously-reachable bug supply in
    `AnimationEditor.Core`; a pass #11 aimed squarely at implementing the deferred TODO fix (rather
    than another scenario-walkthrough sweep) is the highest-value next step if this continues. Full
    suite: `AnimationEditor.Core.Tests` 2181/2181 (was 2178, +3 new: the whole-chain fix's test, the
    two-tsx-tabs pin, and the `TabManager.Rename` fix's test), `AnimationEditor.App.Tests` 989/989
    (unchanged), `AnimationEditor.Views.Tests` builds clean.

- [x] **`DeleteFramesCommand` Undo of "delete every frame in a chain" relocated the chain instead of
  restoring it -- the frame-level sibling of the whole-chain-delete fix, deferred by pass #10.**
  Real bug, confirmed red first --
  `SaveTsxProject_DeleteFramesThenUndoWithSameFrameObjectsAndSave_RestoresOriginalTileInsteadOfRelocating`
  failed against the old code (tile 5 left empty, animation relocated onto tile 6) before the fix,
  using the same `OwnerNotFirstFrameFixtureXml` fixture (tile 5 owns frames [6, 7]) as the
  whole-chain-delete test.

  **Design chosen: a dormant-hint side table keyed by frame-object-reference fingerprint, entirely
  inside `ProjectManager` -- not the wider `MultiTileToTiledAnimationMapper.Map`/
  `knownEntryTileIds` parameter-type change pass #10 sketched.** The simpler design worked: no
  `MultiTileToTiledAnimationMapper`/`TiledAnimationToAchjMapper`/`ProjectManagerTsxValidationIssuesTests`
  call site needed to change, because the fingerprint check only needs to decide, for one
  `SaveTsxProject` call, *which* hint value to feed into the existing `knownEntryTileIds`/
  `knownSatelliteTileIds` dictionaries -- it never needs those dictionaries' shape to carry the
  fingerprint themselves. Added:
  - `ProjectManager._tsxLastNonEmptyFramesByChain` (`Dictionary<AnimationChainSave,
    IReadOnlyList<AnimationFrameSave>>`) -- each chain's `Frames` contents as of the most recent
    save where it had a real entry tile id, i.e. the frame-object sequence that produced the
    current `_tsxEntryTileIdsByChain` value. Seeded on `LoadTsxProject` (so a chain that already
    has an on-disk hint can go dormant on the very first post-load save, not just the second),
    reset alongside the other tsx fields on an achx load, and round-tripped through
    `CaptureTsxState`/`RestoreTsxState` for the tab-switch cache.
  - `ProjectManager._tsxDormantHintsByChain` (`Dictionary<AnimationChainSave, DormantTsxHint>`,
    `DormantTsxHint` = `(uint EntryTileId, IReadOnlyDictionary<(int Dx, int Dy), uint> Satellites,
    IReadOnlyList<AnimationFrameSave> Frames)`) -- populated in `SaveTsxProject`'s post-save
    bookkeeping loop the moment a present chain's `EntryTileId` comes back null *and*
    `chain.Frames.Count == 0` (as opposed to null for some other mapper-skip reason, e.g. a
    texture-mismatch or misaligned-frame warning, which must not create a dormant hint). A chain
    left empty across several consecutive saves keeps reusing the same dormant entry (checked
    first) rather than losing it after the first "still empty" resave.
  - `SaveTsxProject` now builds one-off merged copies of `_tsxEntryTileIdsByChain`/
    `_tsxSatelliteTileIdsByChain` (only when at least one dormant hint actually matches -- no
    allocation on the common path) before calling `MultiTileToTiledAnimationMapper.Map`: for every
    chain with a dormant hint whose `Frames` list is reference-sequence-equal (same count, same
    object at each index -- `FramesSequenceEqual`) to the chain's *current* `Frames`, the dormant
    tile id (and satellite ids) win for that one save. A successfully revived hint flows back into
    `_tsxEntryTileIdsByChain`/`_tsxSatelliteTileIdsByChain` as an active hint via the same
    `mapped`-result loop that already existed -- no separate "un-dormant" step needed.

  **What distinguishes "reuse the dormant hint" from "genuinely cleared, don't reuse" (the
  already-shipped, still-correct `SaveTsxProject_AllFramesDeletedFromChain_...` guarantee):**
  frame-object *reference* identity, not value/content equality. `DeleteFramesCommand.Undo()`
  re-inserts the exact same `AnimationFrameSave` instances it removed (confirmed by reading
  `DeleteFramesCommand.cs`: `Undo()` re-inserts from `_removed`, the same array captured in
  `Do()`, never a clone) -- `FramesSequenceEqual`'s `ReferenceEquals` check matches, so the dormant
  hint revives. A user who clears a chain's frames and then re-authors it with brand-new
  `AnimationFrameSave` objects -- even ones with identical-looking coordinates, and even mapping to
  the same tile count -- produces a `Frames` list that fails the reference check, so the dormant
  hint is left unused and a fresh id is computed from geometry, exactly as before. Pinned with a
  new test built specifically to rule out a coincidental pass:
  `SaveTsxProject_DeleteFramesThenReauthorWithDifferentFrameObjectsAndSave_ComputesFreshTileNotStaleDormantHint`
  uses the *same* `OwnerNotFirstFrameFixtureXml` fixture as the fix's own positive test (tile 5,
  not tile 0), so a naive "any non-empty `Frames` after a clear reuses the last hint" bug couldn't
  slip through by accident the way it might have against the original `AllFramesDeletedFromChain`
  test's `PlainFixtureXml` (where the owner tile happens to equal frame 0's tile).

  **Redo symmetry, verified rather than assumed:** the task asked whether the same
  frame-reference-stability property holds for Redo, not just Undo. Read
  `DeleteFramesCommand.Redo()`: it re-removes frames via `_chain.Frames.Remove(frame)` over the
  same `_removed` array `Do()`/`Undo()` already use -- no clone, same objects. Added
  `SaveTsxProject_DeleteFramesUndoRedoUndoCycleWithSameFrameObjects_RestoresOriginalTileEveryTime`,
  which drives a full Do -> Undo -> Redo -> Undo cycle (four `SaveTsxProject` calls) and asserts
  tile 5 is correctly cleared after Redo and correctly restored after the second Undo. It passed
  without any additional source change: `ReferenceEquals`-based fingerprinting has no notion of
  "Undo" vs. "Redo" baked in, only "are these the same frame objects as when the hint went
  dormant," so the same mechanism covers both directions of the undo stack for free.

  Tests: `ProjectManagerTsxProjectTests.SaveTsxProject_DeleteFramesThenUndoWithSameFrameObjectsAndSave_RestoresOriginalTileInsteadOfRelocating`,
  `SaveTsxProject_DeleteFramesThenReauthorWithDifferentFrameObjectsAndSave_ComputesFreshTileNotStaleDormantHint`,
  `SaveTsxProject_DeleteFramesUndoRedoUndoCycleWithSameFrameObjects_RestoresOriginalTileEveryTime`.
  Full suite: `AnimationEditor.Core.Tests` 2184/2184 (was 2181, +3 new).

  **New plausible gap noticed, not yet added as a TODO item because it isn't yet confirmed real:**
  `AddFramesCommand`'s own Undo (removing frames it just added) and any other command that clears
  a chain to zero frames via a path other than `DeleteFramesCommand` would need the same
  `chain.Frames.Count == 0` condition to fire for dormant-hint capture to kick in at all -- which
  it does generically (the check is on the mapper result + live `Frames.Count`, not on which
  command caused it), so this is very likely already covered, not a gap. Not filed as a TODO
  item since no concrete repro was found or attempted -- flagging only as a "worth a quick
  confirming test if this area gets touched again" note, not a known bug.

- [x] **Fresh-eyes pass #11 -- audited every `IUndoableCommand` implementation in the codebase (not
  just `DeleteChainsCommand`/`DeleteFramesCommand`, the two that seeded this bug class) for the same
  "Undo/Redo desyncs tsx identity-tracking dictionaries" shape.** Found and fixed two new real,
  confirmed bugs (below) -- one a direct hit on the task's own item 5 (footprint-resize commands),
  the other a broader generalization discovered while investigating item 1 (rename) that turned out
  to have nothing to do with rename at all -- and traced every other command to a concrete "safe, no
  gap" verdict. Full command inventory (grepped `CommandsAndState/` for every `IUndoableCommand`
  implementation -- 33 files, no commands exist outside this directory):

  | Command | Verdict | Reasoning |
  |---|---|---|
  | `AddChainCommand` | Safe | Brand-new chain object; a first save always computes its entry tile fresh (no hint exists yet), so hint == natural value by construction -- an Add-Undo-Redo cycle has no discriminating outcome to test against. Undo (chain absent) is covered by the same reference-keyed absent-chain carry-forward already fixed for `DeleteChainsCommand`, which has zero command-specific branching. |
  | `RenameChainCommand` | Safe | `Name` is never a dictionary key anywhere in this subsystem (`MultiTileToTiledAnimationMapper`/`ProjectManager`'s hints are keyed by chain *object reference*); `NativeTsxAnimationSync.ApplyTile`'s `explicitName` is a one-way property write, never read back as an identity key. Already covered by an existing DONE entry (multi-tile-satellite rename); Undo just re-sets `Name` in place on the same object, trivially unaffected. |
  | `ReorderCommand<T>` (chains, frames, shapes) | Safe -- fixed one real gap it exposed indirectly (see below) | Rebuilds the list in place via `Clear()`+`Add()`, same objects, no membership change -- hints are keyed by reference/offset, never by list position. Frame-reorder specifically pinned with a teeth-tested test (`SaveTsxProject_ReorderFramesWithinChainAndSave_...`, confirmed red when hints are disabled). |
  | `DuplicateChainsCommand` | Safe | `AppCommands.CloneChainWithFlip` builds a brand-new `AnimationChainSave` + brand-new `AnimationCloneHelper.CloneFrame`-cloned frames (verified by reading it, not assumed) -- no shared references with the source, so Undo (removing the copy) can't touch the source's hint, and the copy's own first save computes fresh like `AddChainCommand`. |
  | `AddFrameCommand`, `AddFramesCommand`, `DuplicateFrameCommand`, `DuplicateFramesCommand` | Safe | Undo only removes the exact brand-new objects Do() just added -- these can never reference-match a `DormantTsxHint.Frames` snapshot (which only ever captures frames that existed *before* an add), so they can't spuriously revive or corrupt a dormant hint. The only way these reach `Frames.Count == 0` is undoing an add to an already-empty chain, which had no hint to begin with. |
  | `MoveFramesCommand` | Safe (traced, no test added) | A cross-chain move that empties the source chain then an Undo that restores the same frame objects in the same order exercises the exact same dormant-hint capture/revival code path already pinned by `DeleteFramesCommand`'s tests (the mechanism has no command-specific branching) -- would have no incremental discriminating power. |
  | `FrameRegionChangedCommand`, `BulkFrameRegionChangedCommand` | Real bug, fixed | See below -- the footprint-resize-then-undo satellite relocation (task item 5). |
  | `MoveFrameOffsetCommand`, `MoveFrameOffsetBulkCommand` | Safe | Only mutate `RelativeX`/`RelativeY`, which `AchjToTiledAnimationMapper.FrameRectPixels` (verified by reading it) never reads -- these fields are a rendering offset, unrelated to which physical tile a frame's rect maps to. |
  | `FlipCommand` | Safe | Mutates `FlipHorizontal`/`FlipVertical`/`FlipDiagonal`/`RelativeX`/`RelativeY`/shape offsets only; grepped the whole `Tiled/` folder for these flip flags -- the one hit (`AchjToTiledAnimationMapper.MapFrame`) is the achx-push path, which has no per-chain identity-hint dictionaries at all (already-established DONE entry: achx-push "simply follows wherever its geometry currently points to"), so there's no identity state for this command to desync. |
  | `SetFrameTextureNameCommand` | Real bug exposed (not this command's fault) | See below -- the mapping-abort generalization. |
  | `PasteChainsCommand` (incl. the `PasteChainsCut` composite) | Safe | `chains` always come from `ClipboardPayload.TryDeserialize` (fresh, deserialized objects) and `sourcesToRemove` are the original in-document references -- verified by reading the one call site (`MainWindow.axaml.cs`) -- so paste and the cut-delete never share a reference; behaves as two already-individually-safe operations (`AddChainCommand`-shaped add, `DeleteChainsCommand`-shaped remove) glued by `CompositeCommand`. |
  | `SetChainLockedCommand`, `SetChainLoopCommand` | Safe | `IsLocked`/`Loop` are never read anywhere in `Tiled/` (grepped, zero matches) -- no interaction with tsx sync at all. |
  | `AddAxisAlignedRectangleCommand`, `DeleteAxisAlignedRectangleCommand`, `AddCircleCommand`, `DeleteCircleCommand`, `DuplicateShapesCommand`, `PasteShapesCommand`, `MoveShapeCommand`, `ResizeShapeCommand`, `SetShapePropsCommand`, `BulkShapePropsCommand` | Safe | None of these touch `chain.Frames`/`AnimationChainListSave.AnimationChains`/frame rect coordinates at all (grepped `CommandsAndState/` for `.Frames.`/`AnimationChains.` -- none of these 10 files matched); they only mutate `frame.ShapesSave.Shapes`, which the Tiled sync pipeline never reads (grepped `Tiled/` for `Shape`/`Circle`/`Rectangle` -- one hit, an unrelated doc-comment word). |
  | `CompositeCommand` | Safe | Pure delegation to already-individually-audited children in order; introduces no bookkeeping of its own. |
  | `BulkFrameEditCommand` | Safe | Mutates `FrameLength`/`RelativeX`/`RelativeY`/UV coordinates/color fields via snapshot restore -- the UV-coordinate case is the same class as `FrameRegionChangedCommand` (already covered by the general footprint-resize fix below, since the fix lives in `ProjectManager`/the mapper, not in any specific command), and the rest are unrelated to tile identity. |

  **Real bug #1, confirmed red first (task item 5) -- a footprint-resize-then-undo relocates a
  multi-tile chain's satellite instead of restoring it.** `BulkFrameRegionChangedCommand.Do()`/
  `Undo()` (and `FrameRegionChangedCommand` for a 1-frame chain) mutate an existing frame's
  `Left`/`Top`/`Right`/`BottomCoordinate` in place -- no `AnimationFrameSave` object is added or
  removed and `chain.Frames.Count` never changes, unlike every previously-fixed case in this bug
  class. Shrinking a 2-wide footprint to 1-wide (dropping a satellite), then undoing back to the
  exact original rect, hit a version of the "owner isn't its own first frame" relocation bug -- but
  for the satellite: `SaveTsxProject`'s post-save bookkeeping unconditionally *replaced* (rather than
  merged into) a present chain's whole satellite-hint dictionary every save, so an offset absent from
  one save's footprint lost its hint outright instead of surviving for a later save where the
  footprint widens again. Fixed by merging each save's satellite results onto the existing hint
  dictionary, and by keeping the whole per-chain dictionary when a chain has frames but zero
  satellites this save -- mirroring how `_tsxEntryTileIdsByChain` already persists unconditionally
  for the life of a non-empty chain. No reference-fingerprint mechanism needed here (unlike the
  frame-count-zero case): a resize never touches frame-object identity or count, so there's no
  zero/dormant transition to gate revival on -- unconditional persistence is the correct, minimal
  fix. Test:
  `ProjectManagerTsxProjectTests.SaveTsxProject_ShrinkFootprintThenUndoWithSameFrameObjectsAndSave_RestoresSatelliteToOriginalTileInsteadOfRelocating`.

  **Real bug #2, confirmed red first -- a broader generalization found while investigating item 1
  (rename), unrelated to rename itself: ANY save whose mapping ABORTS with a warning while the chain
  still has frames silently dropped the chain's entry/satellite hints.** Tracing
  `SaveTsxProject`'s bookkeeping loop precisely (per this pass's "trace concretely, don't guess"
  instruction) surfaced a third, unhandled outcome alongside the two it already handled ("live hint"
  and "genuinely emptied to zero frames"): a chain that still has frames but whose
  `MultiTileToTiledAnimationMapper.MapChain` call hit any of its `Empty()`-with-warning call sites
  this save (mismatched texture name, misaligned/negative rect origin, footprint overflow, non-zero
  margin/spacing) fell through both branches, so nothing preserved its hints. The very next
  successful save then recomputed the entry tile fresh from frame[0], relocating an "owner isn't its
  own first frame" chain exactly like the bug class this whole sweep is themed around -- reachable by
  *any* command whose Do()/Undo() can drive a chain in and out of an abort condition without
  changing `Frames.Count` or frame identity, not one specific command. `SetFrameTextureNameCommand`
  (point a frame at the wrong texture, then back) is the simplest repro, but
  `FrameRegionChangedCommand`/`BulkFrameRegionChangedCommand` (drag a rect to a misaligned/
  non-tile-multiple size mid-gesture) and `MoveFramesCommand`/`AddFramesCommand`/
  `DuplicateFrameCommand` (moving or adding a frame with a mismatched texture into an existing chain)
  reach the identical code path. Fixed the general condition, not the one repro: a present chain with
  frames whose mapping aborted now keeps its existing entry/satellite hints and last-known-frames
  untouched, the same "transient, recoverable state" treatment already given to a chain absent from
  the ACLS. Test:
  `ProjectManagerTsxProjectTests.SaveTsxProject_MappingAbortsThenRecoversViaTextureNameFix_RestoresOriginalTileInsteadOfRelocating`.

  **Traced, pinned with a teeth-tested test, no source change -- frame reorder within a chain (task
  item 4).** `ReorderCommand<AnimationFrameSave>` (Reverse Chain, drag-to-reorder) changes which
  frame sits at index 0 -- the exact fallback `MapChain` uses when no hint exists -- but both
  `_tsxEntryTileIdsByChain` and `_tsxSatelliteTileIdsByChain` are keyed purely by chain/offset
  reference, never by list position, so an existing hint keeps winning regardless of reorder.
  Confirmed the test has teeth by temporarily forcing `SaveTsxProject` to pass `null` hints into
  `Map` and observing the anchor relocate, then reverting. Test:
  `SaveTsxProject_ReorderFramesWithinChainAndSave_EntryAndSatelliteTilesStayPutOnlyAnimationOrderChanges`.

  **Task item 2 (reorder chains at the ACLS level) -- traced, safe, no test added.** Same reasoning
  as the frame-reorder case one level up: `ReorderCommand<AnimationChainSave>` only rebuilds
  `AnimationChainListSave.AnimationChains`' order via `Clear()`+`Add()` on the same objects --
  nothing in this subsystem's identity tracking is position-keyed, confirmed by reading every
  dictionary's key type (`AnimationChainSave` object references throughout). No discriminating test
  possible beyond what the frame-level pin above already demonstrates for the identical mechanism.

  **New TODO item surfaced while implementing the mapping-abort fix (see TODO list above): a chain
  that is dormant AND whose refill also triggers a mapping abort loses its dormant hint outright**
  instead of staying dormant for a still-later save to revive. Narrow (requires stacking two
  separately-unusual states in succession) and not yet confirmed with a red test -- left as TODO
  rather than folded into this pass's fix, to avoid guessing at a fix shape for an unconfirmed
  scenario.

  Full suite: `AnimationEditor.Core.Tests` 2187/2187 (was 2184, +3 new: the satellite-footprint fix's
  test, the reorder-frames pin, and the mapping-abort fix's test).

  **Honest assessment: not a clean pass, and the strongest evidence yet that this bug class was not
  actually exhausted by pass #10's two fixes.** Two new real, confirmed bugs were found -- one a
  direct hit on a specifically-suggested target (footprint resize), the other a genuine
  generalization discovered by tracing code rather than guessing from a command's name, matching
  exactly the "fix the class, not the repro" principle: the mapping-abort bug has nothing to do with
  rename (what this pass was originally investigating when it was found) and instead applies
  uniformly across at least five different commands. Every other command in the 33-file inventory
  traced to a concrete, reasoned "safe" verdict -- no hand-waving "seems fine" verdicts. The bug class
  does **not** yet look exhausted: the mapping-abort fix's own TODO offshoot (dormant hint lost on an
  abort-while-refilling save) is a third, related-but-distinct gap in the same dormant/active hint
  bookkeeping this pass touched twice, and the fact that two passes in a row (#10 and #11) each found
  a "fix the obvious case, defer/discover a deeper related case" pattern suggests the
  `_tsxEntryTileIdsByChain`/`_tsxSatelliteTileIdsByChain`/`_tsxDormantHintsByChain` three-way state
  machine itself -- not any individual command -- is the part that keeps growing new edge cases. A
  pass #12 aimed at the TODO item above, or at a from-scratch re-derivation of that state machine's
  full transition table (live -> dormant -> revived -> aborted, and every pairwise combination), is
  more likely to find the next gap than another command-by-command command audit.

- [x] **Fresh-eyes pass #12 -- from-scratch derivation of `SaveTsxProject`'s full per-chain state
  transition table (Live / Dormant / Aborted / Absent / Never-had-a-hint), as recommended by pass
  #11.** Found and fixed one real, confirmed bug (the TODO item this pass resolves), and reduced the
  nominal "25 pairwise combinations" to a much smaller set of genuinely distinct cases by tracing a
  structural fact first: **"Aborted" and "Absent" are not separate stored buckets.** A chain's
  identity-tracking state, at rest between saves, only ever lives in one of three places --
  `_tsxEntryTileIdsByChain` (Live), `_tsxDormantHintsByChain` (Dormant), or neither (Never) -- and:
  - An **Aborted** outcome (mapping fails with a warning while `chain.Frames.Count > 0`) does not
    create a fourth bucket; the existing branches simply re-file the chain back into whichever
    bucket it already occupied (Live stays Live via the `stillActiveEntry` branch; Dormant now stays
    Dormant via this pass's fix, below). So "X -> Aborted -> Y" collapses to "X -> Y" for bucket
    purposes -- Aborted only matters as a same-save output label, never as next-save input state.
  - An **Absent** chain (missing from `AnimationChainListSave.AnimationChains` this save) doesn't go
    through the bucket-transition branches at all -- it's carried forward verbatim by the two
    unconditional pre-loops (one over `_tsxEntryTileIdsByChain`, one over `_tsxDormantHintsByChain`,
    both keyed on `!currentChains.Contains`), so whichever bucket it was in when it disappeared is
    exactly the bucket it's still in when/if it reappears. "X -> Absent -> Y" therefore also
    collapses to "X -> Y" once the carry-forward itself is confirmed unconditional and
    non-discriminating (confirmed by reading both loops -- neither branches on bucket identity).

  This leaves a genuine 3x3 core table over {Live, Dormant, Never} (Absent/Aborted are transparent
  wrappers around this core, verified separately below), all 9 cells traced against the actual
  branch logic in `SaveTsxProject`'s post-`mapped` loop:

  | Prev \ This save | -> Live (maps OK) | -> Dormant (Frames.Count==0) | -> Aborted (has frames, maps fail) |
  |---|---|---|---|
  | **Live** | OK (fresh recompute or hint reuse; pre-existing) | OK (`updatedEntries` not repopulated for this chain, so Live entry correctly drops as the chain demotes; pre-existing, `..._AllFramesDeletedFromChain_...`) | OK (`stillActiveEntry` branch keeps the hint; pass #11) |
  | **Dormant** | OK -- two sub-cases: reference-match revival via the pre-map injection (pre-existing, `..._DeleteFramesThenUndo...`), or no-match fresh recompute (pre-existing, `..._DeleteFramesThenReauthor...`) | OK (`stillDormant` re-used, not replaced with a new fingerprint; pre-existing) | **BUG, fixed this pass** -- neither the Live branch (`_tsxEntryTileIdsByChain` has no entry) nor the Dormant branch (`Frames.Count` isn't 0) fired, so the dormant hint was silently dropped. Fixed with a new `else if (_tsxDormantHintsByChain.TryGetValue(...))` branch that re-parks the same `DormantTsxHint` unchanged, mirroring the Live branch immediately above it in the same `if`/`else if` chain. Test: `SaveTsxProject_DormantChainRefillAlsoAbortsMapping_DormantHintSurvivesForLaterRevival` (confirmed red before the fix -- tile 5's animation came back empty instead of restored). |
  | **Never** | OK (fresh compute, brand-new chain; pre-existing, `AddChainCommand`) | OK (neither branch's `TryGetValue` succeeds -- correctly stays Never, no phantom dormant hint for a chain with nothing to preserve; pre-existing, matches the explicit doc-comment reasoning in the Dormant branch) | OK (no-op -- nothing to preserve, nothing preserved; pre-existing, e.g. a brand-new chain immediately given a bad texture name) |

  **Wrapper transitions verified separately** (Absent and Aborted layered on top of the 3x3 core, all
  confirmed either already-tested or newly pinned this pass):
  - `Live -> Absent -> Live` (delete then undo, still has frames): already tested,
    `SaveTsxProject_DeleteChainThenReinsertSameObjectAndSave_...` (pass #10).
  - `Dormant -> Absent -> Dormant -> Live` (delete a dormant chain, undo it back in still empty,
    then restore its original frames): **not previously tested** -- confirmed correct via the
    unconditional dormant carry-forward loop, and pinned with a teeth-tested test (temporarily
    disabled that loop's carry-forward with `if (false && ...)`, observed it fail, then reverted).
    Test: `SaveTsxProject_DormantChainDeletedThenReinsertedStillEmptyThenRevived_DormantHintSurvivesAbsence`.
  - `Live -> Aborted -> Absent` / `Dormant -> Aborted -> Absent`: not given dedicated tests -- both
    reduce structurally to `Live -> Absent` / `Dormant -> Absent` per the "Aborted re-files into its
    existing bucket" fact above, and the carry-forward loops don't discriminate on *how* a chain
    arrived in its bucket, only *which* bucket it's in -- no incremental discriminating power over
    the tests already listed.
  - `Absent -> Dormant` starting from a *Live* bucket (i.e. a chain reappears with zero frames after
    having been deleted while it still had a real tile): traced as structurally unreachable, not
    just untested -- no command in this codebase mutates `Frames` on a chain that isn't currently in
    `AnimationChainListSave.AnimationChains` (Undo/Redo only re-insert/remove the chain reference
    itself; nothing can clear its frames while it's absent), so a chain can never change bucket
    *during* its absence. No test added for an unreachable input.

  **Net result:** one real, confirmed bug (Dormant -> Aborted losing its hint, the exact TODO item
  pass #11 left behind) fixed with a three-line branch addition mirroring the existing Live-abort
  branch; one additional non-obvious-but-already-correct transition
  (Dormant -> Absent -> Dormant -> revival) pinned with a teeth-tested test that didn't exist before.
  Source change: `ProjectManager.cs`'s `SaveTsxProject` gained one `else if` branch (see diff/PR).
  Tests: `SaveTsxProject_DormantChainRefillAlsoAbortsMapping_DormantHintSurvivesForLaterRevival`,
  `SaveTsxProject_DormantChainDeletedThenReinsertedStillEmptyThenRevived_DormantHintSurvivesAbsence`.
  Full suite: `AnimationEditor.Core.Tests` 2189/2189 (was 2187, +2 new).

  **Honest assessment: the three-way state machine is now provably complete, not just "no new bugs
  found this pass."** Unlike passes #10-#11 (which each fixed one case and surfaced a new, unproven
  adjacent one), this pass didn't stop at finding gaps command-by-command -- it enumerated the full
  state space structurally, showed two of the nominal five "states" are not independent stored
  buckets at all (collapsing the real search space from 25 cells to 9 core cells + a small, fully
  reasoned set of wrapper cases), and traced every one of the resulting cells to a concrete verdict
  (7 pre-existing/correct, 1 real bug now fixed, 1 structurally unreachable). No cell was left as "not
  yet confirmed" or deferred. This is a stronger completeness claim than any prior pass in this sweep
  could make, precisely because it was derived from the state space itself rather than from guessing
  at plausible commands.

- [x] **Fresh-eyes pass #13 -- can achx-push and native-tsx collide when both target the same
  `.tsx` file?** A genuinely new angle: every prior pass audited each feature's own state machine in
  isolation; this one asked whether the two features' independent write paths interfere with each
  other. Traced concretely rather than assumed, and found two real, confirmed bugs (fixed) plus one
  larger structural gap that needs a design decision (moved to TODO above, not fixed here).
  - **Real bug, confirmed red first -- `TiledAnimationToAchjMapper.Map` (native-tsx's load side) had
    zero awareness of `achjSourceFile`, so opening a `.tsx` natively absorbed any achx-push-owned
    tile into the native project's own editable model as an independent "ID:{tileId}" chain** (every
    animated tile qualified as a chain candidate, regardless of who wrote it).
    `Map_TileOwnedByAchxPushSource_ExcludedFromNativeTsxModelEntirely` failed against the old code
    (`Assert.Empty` on `acls.AnimationChains` saw the achx-push tile's animation surfaced as its own
    chain). Consequence traced, not assumed: once absorbed, a user could rename/move/delete an
    animation that isn't this project's to own, and the next native-tsx save would either leave
    achx-push's `achjAnimationName`/`achjSourceFile` properties orphaned on a tile whose `Animation`
    native-tsx had since overwritten, or fight achx-push's own next sync over the same tile. Fixed by
    excluding any tile carrying `TilesetAnimationSync.SourceFilePropertyName` from
    `animatedTiles` entirely -- unlike every other "not a real anchor" case in this method (broken/
    orphaned/chained/backward `ParentId`, all *this project's own* broken references), an
    achx-push-owned tile belongs to a different owner outright, so it's excluded rather than
    surfaced as its own chain. Test:
    `TiledAnimationToAchjMapperTests.Map_TileOwnedByAchxPushSource_ExcludedFromNativeTsxModelEntirely`.
  - **Real bug, confirmed red first -- fixing the load-side absorption above would have made
    `NativeTsxAnimationSync.Apply`'s stale-tile-clearing strictly worse without a matching save-side
    fix.** Once an achx-push-owned tile is never absorbed into the model, it never appears in a native
    -tsx save's `results` -- and this class's own doc comment ("any previously-animated tile not
    represented in results is stale and gets cleared, full stop") means every native-tsx save would
    now unconditionally wipe that tile's animation, turning a race-condition-shaped bug into a
    guaranteed-every-save one. `Apply_TileOwnedByAchxPushSource_StaleClearingLeavesItUntouched`
    confirmed this red (before the fix, the achx-owned tile's `Animation` and tracking properties
    were cleared by a save with empty `results`). Fixed by excluding
    `IsAchxPushOwned` tiles from `previouslyAnimatedTileIds`, mirroring the load-side exclusion.
    Also added a symmetric loud-failure guard, `ValidateNoAchxPushOwnedTileClaimed`, for the case a
    native-tsx chain's own geometry newly computes the same tile id as an already achx-push-owned
    tile -- consistent with this codebase's established "two independent claimants, don't silently
    pick a winner" precedent (the existing `ValidateNoTileIdCollisions` between two native-tsx
    chains). Test:
    `Apply_ChainGeometryClaimsAchxPushOwnedTile_ThrowsInsteadOfSilentlyOverwriting`. Both tests
    confirmed red before the fix, green after.
  - **Traced but deliberately not fixed this pass -- `SaveTsxProject`'s load-time snapshot has no
    re-read/merge against disk, so a concurrent achx-push write (new tile, or an edit to a tile the
    snapshot doesn't know about) can still be silently dropped from the file the next time the
    native-tsx tab saves.** The two fixes above close the gap for a tile achx-push already owned *at
    the moment the native-tsx project loaded* (the common, easily-reachable case: associate then
    open, or open then associate then switch tabs and save without ever touching the associated
    tsx's tab again). They do **not** close the gap for a tile achx-push writes *while* the native-tsx
    tab is already open and loaded -- `ProjectManager.SaveTsxProject` never re-reads the file, and
    `TsxWriter.Write`'s output (patch or full-rewrite) is driven entirely by the in-memory
    `Tileset.Tiles` list with no fallback to what's on disk, so a tile invisible to that stale
    snapshot is invisible to every check this pass added and gets silently omitted from the written
    file -- true data loss, not just a stale warning or a thrown exception. This is a materially
    larger fix (re-reading and merging on every save, or refusing the two features' coexistence
    outright) with real product tradeoffs, not a small patch -- added as a TODO item above with three
    candidate responses laid out, rather than guessed at unilaterally.
  - **Confirmed reachable via the app's own UI, not just hand-editing.** Re-checked
    `AddAssociatedTiledTilesetViaDialogAsync`/`AddAssociatedTiledTileset` (`AppCommands.cs`,
    previously traced in pass #6 for a *different* question -- whether associating from within an
    already-native-tsx project's own tab does anything, concluded "harmless no-op" because
    `SaveCurrentAnimationChainList` skips `SyncAssociatedTiledTilesets` for a native-tsx project).
    That conclusion still holds for *that* question, but is orthogonal to this one: nothing in either
    method, nor in `LoadTsxProject`/`TsxCompatibilityChecker`, checks whether the `.tsx` path being
    associated (from an *achx* tab) is currently open natively in a *different* tab, or vice versa.
    The dialog is a plain file picker with no such awareness either direction -- confirming the
    scenario is reachable through two entirely ordinary actions (open a `.tsx` directly in one tab;
    in a different tab, associate a `.tiledsync` pointing an achx at that same path), not a
    contrived/hand-edited-XML edge case like most of this sweep's other findings.
  - **Honest assessment: a fixed pass with a real deferred-design-question residue, not a clean
    pass.** Two real, confirmed bugs found and fixed (both TDD, both had to be fixed together --
    fixing only the load side would have actively regressed the save side), plus one larger
    structural gap identified, traced to a concrete root cause (a long-lived in-memory session vs. a
    stateless-per-call sync competing for one file with no coordination), and left as a TODO with
    named candidate responses rather than guessed at. This is the first pass in the sweep to find a
    bug class at the *intersection* of the two major features rather than within either one's own
    state machine -- everything analyzed to reach it (`TilesetAnimationSync`, `NativeTsxAnimationSync`,
    `TiledAnimationToAchjMapper`, `TsxWriter`, `ProjectManager.SaveTsxProject`,
    `AppCommands.AddAssociatedTiledTileset*`) had already been individually audited by earlier
    passes, but never against each other. Full suite: `AnimationEditor.Core.Tests` 2192/2192 (was
    2189, +3 new).

- [x] **Resolved the pass #13 coexistence TODO: refuse native-tsx/achx-push coexistence outright
  (candidate (b)), rather than the re-read-and-merge fix (candidate (a)) or documenting-and-accepting
  (candidate (c)).** Product decision made explicitly (not a unilateral engineering call): a `.tsx`
  can never be both a native-tsx project and an achx-push target at the same time. Blocked in both
  directions:
  - **Direction 1 -- opening a `.tsx` natively while it already has a `.tiledsync` association.**
    New static `Tiled/TiledSyncAssociationScanner.FindAssociationsTargeting(tsxPath)` recursively
    scans the tsx's own directory and subdirectories for every `.tiledsync` file, resolves each
    one's relative `TiledTilesetPaths` against its own folder, and returns the absolute achx/achj
    path (inferred from the `.tiledsync` file's own name; falls back to the `.tiledsync` path itself
    if no sibling `.achx`/`.achj` is found on disk) for every match. `ProjectManager.LoadTsxProject`
    calls it first and throws `InvalidOperationException` before touching `_tsxTileset` or any other
    field if any match is found -- same all-or-nothing invariant the method's doc comment already
    promised for its other two throw conditions (unsupported construct, corrupt tsx). Deliberately
    NOT added to `IIoManager`/`IoManager` (unlike `GetAssociatedTiledTilesetPaths`, which is
    achx-keyed and DOES belong there): `ProjectManager` has no `IIoManager` dependency today (its
    constructor takes none, and ~50 call sites do `new ProjectManager()`), and it already does its
    own direct `Directory.EnumerateFiles` scanning elsewhere (`FindMissingTextures`'s PNG fallback
    scan) -- a static scanner class matches that existing precedent
    (`Tiled/TsxCompatibilityChecker.cs` is the same "static class ProjectManager calls directly, no
    DI" shape) without adding constructor-injection blast radius to every `new ProjectManager()`
    call site for a single guard. **Known scope limit, stated in the class's own doc comment**: an
    achx/achj living outside the tsx's own directory tree (a sibling or parent folder, reached via
    the plain file-picker `AddAssociatedTiledTilesetViaDialogAsync` already uses) is not found by
    this scan -- narrower than a perfect search, but consistent with how every other relative path in
    this file format is already resolved from the achx's own folder, and matches the ordinary case of
    an achx/achj and its tsx colocated in one content folder.
  - **Direction 2 -- associating a `.tiledsync` with a `.tsx` that's currently open natively in
    another tab.** `AppCommands`/`ProjectManager` have no visibility into other tabs (`ProjectManager`
    only tracks its own current/active project; tab awareness lives in `TabManager`, which
    `AppCommands` has no reference to, and threading it in would mean a constructor change touching
    ~10 `new AppCommands(...)` call sites). Used the same host-supplied-delegate seam this class
    already has for exactly this kind of "Core needs UI-owned state it doesn't have" gap (see
    `CanvasDefaultTexturePath`): new `IAppCommands.IsTsxPathOpenAsNativeProject` (`Func<string,
    bool>?`), null by default (permissive -- "not open anywhere" -- matching
    `CanvasDefaultTexturePath`'s own null-means-no-info convention so a host that hasn't wired tab
    awareness keeps associating exactly as before). `AppCommands.AddAssociatedTiledTileset` throws
    `InvalidOperationException` when the delegate reports the target tsx open; the delegate itself is
    wired in `MainWindow.axaml.cs`'s `WireAppCommands()`, checking both the active tab
    (`_projectManager.IsNativeTsxProject` + `FileName` match) and every backgrounded tab
    (`TabEntry.CachedTsxState != null` for a path match) since a native-tsx tab's own
    identity-tracking state only lives in one of those two places depending on whether it's currently
    active. `AddAssociatedTiledTilesetViaDialogAsync` (the fire-and-forget UI entry point invoked via
    `_ = ...` with no caller-side try/catch) catches the exception and re-raises it through the
    existing `TiledSyncFailed` event instead of letting it become a silently-swallowed unobserved
    task exception -- reusing the event this class already fires for the sibling "corrupt
    `.tiledsync`" failure mode, which `MainWindow` already displays via `UpdateTiledSyncStatus`, so no
    new UI wiring was needed for display, only for supplying the tab-awareness delegate itself.
  - Tests (`AnimationEditor.Core.Tests`): `ProjectManagerTsxProjectTests.
    LoadTsxProject_TsxAlreadyAssociatedViaTiledSync_ThrowsInsteadOfSilentlyCoexisting`,
    `LoadTsxProject_TsxAssociatedFromAchjInstead_ThrowsInsteadOfSilentlyCoexisting` (direction 1,
    both confirmed red before the fix -- `Assert.Throws` saw no exception); happy-path guards
    `LoadTsxProject_NoAssociationAnywhere_OpensNormallyAsBefore`,
    `LoadTsxProject_TiledSyncAssociatesADifferentTsx_OpensNormally` (an unrelated or
    differently-targeted `.tiledsync` must not false-positive). `AppCommandsTiledSyncTests.
    AddAssociatedTiledTileset_TargetTsxOpenAsNativeProjectElsewhere_ThrowsInsteadOfSilentlyCoexisting`,
    `AddAssociatedTiledTilesetViaDialogAsync_TargetTsxOpenAsNativeProjectElsewhere_
    RaisesTiledSyncFailedInsteadOfAssociating` (direction 2, both confirmed red before the fix --
    the interface member didn't exist yet, a compile-time red); happy-path guards
    `AddAssociatedTiledTileset_DelegateNotWired_AssociatesNormally`,
    `AddAssociatedTiledTileset_DelegateSaysNotOpenElsewhere_AssociatesNormally`.
  - Files changed: new `Tiled/TiledSyncAssociationScanner.cs`; `ProjectManager.cs` (`LoadTsxProject`
    guard + updated `<exception>` doc); `CommandsAndState/IAppCommands.cs`/`AppCommands.cs`
    (`IsTsxPathOpenAsNativeProject` property, `AddAssociatedTiledTileset` guard,
    `AddAssociatedTiledTilesetViaDialogAsync` try/catch); `AnimationEditor.App/MainWindow.axaml.cs`
    (`WireAppCommands()` wires the delegate using `_projectManager`/`_tabManager`, both already
    fields on this class). `IIoManager`/`IoManager`/`BrowserIoManager` untouched -- deliberately not
    part of the abstraction for either direction, per the reasoning above.
  - Full suite: `AnimationEditor.Core.Tests` 2200/2200 (was 2192, +8 new). `AnimationEditor.App`,
    `AnimationEditor.Views`, and `AnimationEditor.Browser` all still build clean (full
    `AnimationEditorAvalonia.slnx` build, 0 warnings/0 errors); `AnimationEditor.App.Tests`
    989/989.
  - **Honest assessment of residual scope -- this closes the two reachable-via-ordinary-UI paths
    pass #13 identified, but not the full lost-update window.** A `.tiledsync` file created or
    hand-edited on disk (bypassing `AddAssociatedTiledTilesetPath`/`AddAssociatedTiledTileset`
    entirely -- e.g. by another tool, a teammate's commit, or manual editing) while a `.tsx` is
    *already* open as a native-tsx tab is not caught until that tab's next `LoadTsxProject` call --
    there is no live re-check while a tab sits open, only at open/associate time. This is narrower
    than the two flows pass #13 confirmed reachable via ordinary UI (both of which this fix closes
    completely), but it is a real, if awkward-to-hit, gap: the association is invisible to the
    already-open tab for the rest of that session, and a save from that tab would still write over
    ground achx-push might independently claim once its own association is eventually read. Not
    fixed here -- doing so would mean either polling/watching `.tiledsync` files for every open
    native-tsx tab (a new, always-on file-watch responsibility with its own cost/complexity) or
    re-validating on every `SaveTsxProject` call (which starts to resemble candidate (a)'s
    re-read-and-merge shape, the option this pass deliberately did not choose). Left as a known,
    narrower residual rather than folded into this fix silently.

- [x] **Fresh-eyes pass #14 -- extended the "cross-feature interaction" angle pass #13 opened to
  other pairs of features/subsystems sharing a `.tsx` file, plus a broader "what haven't we tried"
  scan.** One real gap found and pinned with a teeth-tested test (a degenerate case of the
  coexistence guard itself, not a new bug class); every other angle traced to a concrete "safe, no
  gap" verdict:
  - **Hot-reload vs. a currently-open native-tsx tab -- safe, no gap.** `AppCommands.
    ReloadAchxFromDisk` (fixed pass #6 to branch on extension) calls `_pm.LoadTsxProject(filePath)`
    for a `.tsx` path -- the exact same method the coexistence guard lives in, not a parallel path
    that could miss it -- so a hot-reload-triggered reload of an already-open native-tsx tab
    inherits the guard for free. Traced why this can't spuriously false-positive on an ordinary
    external edit (e.g. a teammate's commit, or hand-editing the tsx in Tiled itself): the guard
    only checks for a `.tiledsync` file targeting the path, which is disk state independent of *why*
    the tsx file itself changed. `LoadTsxProject`'s existing all-or-nothing commit pattern also means
    a hot-reload that legitimately hits the guard (because a `.tiledsync` was concurrently created,
    hand-edited, or restored via version control while the tab sat open -- the same residual gap
    pass #13's fix already documented as known-and-accepted) fails cleanly via the existing
    `HotReloadFailed` event, leaving the tab's in-memory state untouched rather than half-applied.
    No new test added -- this is the residual gap pass #13 already named, observed from a different
    trigger (hot-reload) rather than a new one.
  - **Coexistence-guard stress tests (task's angle 2) -- three sub-cases traced, one real
    (degenerate, not previously covered) case found and pinned:**
    - **A `.tsx` associating itself while it is the CURRENTLY ACTIVE native-tsx tab -- real,
      previously-uncovered case; already correctly blocked by existing code, confirmed with a new
      teeth-tested test rather than assumed.** Every existing coexistence test for direction 2
      (`AppCommandsTiledSyncTests`) stubs `IsTsxPathOpenAsNativeProject` by hand; none exercised the
      *real* delegate `MainWindow.WireAppCommands()` wires (comparing `_projectManager.FileName`/
      `_tabManager.Tabs` against the target path), and none tried the specific case of a tab
      associating *itself*. Without this check, a self-referential `.tiledsync` would get written
      next to the tsx's own file, permanently locking it out of ever being reopened natively again
      (every future `LoadTsxProject` call would see its own association and refuse to open) --
      reachable via ordinary UI (the "Associate Tiled Tileset" file picker has no filter excluding
      the currently-open file). Added
      `NativeTsxSelfAssociationTests.AddAssociatedTiledTileset_TargetIsTheCurrentlyOpenTsxItself_ThrowsInsteadOfWritingSelfReferentialTiledSync`
      (`AnimationEditor.App.Tests`, exercising the real `MainWindow` wiring via `LoadAnimationFileAsync`
      + `AddAssociatedTiledTileset`, not a stub). Confirmed the test has teeth: temporarily disabling
      *both* of the delegate's branches (the active-tab check and the backgrounded-tabs check) made
      it fail with "No exception was thrown"; disabling only the active-tab check left it passing,
      because the backgrounded-tabs check (`_tabManager.Tabs.Any(t => t.Path == target && t.
      CachedTsxState != null)`) independently also catches this case -- the just-opened tab is
      already tab-tracked with a populated `CachedTsxState` by the time `AddAssociatedTiledTileset`
      runs, so both branches redundantly guard the same scenario here (harmless belt-and-suspenders,
      not a gap -- reverted both temporary edits after confirming).
    - **A `.tiledsync` pointing at a tsx that gets renamed/moved on disk -- traced, safe, not a
      guard evasion.** AnimationEditor has no in-app rename/move for `.tsx` files (grepped for
      `RenameFile`/`MoveFile`/`File.Move(` -- the only hits are unrelated `IoManager`/`RecycleBin`
      code); a rename can only happen externally. After an external rename, the `.tiledsync`'s
      relative path still names the *old* filename, which no longer exists -- so `TiledTilesetSyncRunner
      .SyncAll` fails with the already-covered "tsx missing from disk" failure mode
      (`StatusBarTests.TiledSyncStatus_ShowsFailureDetail_AfterAssociatedTsxMissingFromDisk`), not a
      coexistence conflict, and the *new*-named file opens natively without any (correctly absent)
      association. The reverse -- a different, unrelated tsx later reusing the freed old filename --
      is correctly (conservatively) treated as still-associated by path, matching every other
      path-identity assumption already made throughout this file format. No test added: not a new
      behavior, just confirms path-based identity fails safe in both directions.
    - **Two different open native-tsx tabs whose paths could resolve to the same file via spelling
      differences -- traced, safe by construction, not reachable.** `TabManager.OpenOrFocus`/
      `FindTab` (the single path both `File > Open` and drag-drop funnel through) dedupe via the same
      `FilePath` equality (case-insensitive, slash-normalized, relative-vs-absolute-resolved) the
      coexistence guard itself uses -- so two tabs both pointing at the same file via different
      ordinary spellings can't coexist as distinct `TabEntry` instances in the first place; opening
      the "second" spelling just focuses the existing tab. `FilePath` does not resolve symlinks (no
      filesystem-level canonicalization anywhere in this class), so two *symlinked* paths to one
      underlying file would evade both this dedup and the coexistence guard identically -- a
      pre-existing, engine-wide `FilePath` limitation (not introduced by, or unique to, this guard),
      consistent with every other path-identity assumption in this codebase. Not filed as a new TODO:
      fixing it would mean adding filesystem-level path canonicalization to `FilePath` itself, a
      change with a far wider blast radius than this sweep's scope, for a scenario with no evidence
      of being hit in practice.
  - **Multiple Tiled tileset associations from one achx file (task's angle 3) -- safe, no gap.**
    Grepped every call site of `IIoManager.AddAssociatedTiledTilesetPath`: the only production caller
    is `AppCommands.AddAssociatedTiledTileset`, which runs the `IsTsxPathOpenAsNativeProject` guard
    unconditionally before delegating -- there is no direct-to-`IoManager` bypass anywhere in
    production code (only test fixtures call `IoManager.AddAssociatedTiledTilesetPath` directly, to
    seed fixture state without exercising `AppCommands`). Confirmed the guard therefore fires once
    per association, not once per achx: `AddAssociatedTiledTilesetViaDialogAsync`'s file picker
    (`PickOpenFileAsync`, not a multi-select variant) only ever returns one path per invocation, and
    associating a second or third tsx to the same achx means a second or third full user-initiated
    "Associate Tiled Tileset" action, each independently running the guard.
  - **Export path (task's angle 4) -- safe, no gap, matches an existing pass #7 finding.**
    `AppCommands.ExportToPixiJsAsync` reads only `_pm.AnimationChainListSave` (the already-mapped
    abstract model, identical in shape whether it came from a tsx or achx load) and `_pm.
    GetTextureSizeInPixels`/`_pm.FileName`'s directory for texture resolution -- no tsx/achx-specific
    branch exists or is needed, consistent with pass #7's "`ExportToPixiJsAsync`... operate[s] on the
    already-mapped abstract `AnimationChainListSave` model and have no tsx/achx-specific behavior to
    get wrong." Grepped for any other export feature (`Export` across `AnimationEditor.Core`) --
    PixiJS is the only one.
  - **Honest assessment: the cross-feature-interaction angle is now much thinner than pass #13's
    haul, but not conclusively exhausted.** This pass found one real (if degenerate/redundant-fix)
    gap and confirmed it with a from-scratch test using the *real* production wiring rather than a
    stub -- itself a small process improvement, since every prior coexistence test in this file
    stubbed the delegate and so could never have caught a wiring mistake in `MainWindow.axaml.cs`
    itself. Every other angle traced to a concrete, reasoned "safe" verdict grounded in a specific
    mechanism (shared `FilePath` equality, single guarded call site, single export path, pre-existing
    missing-file failure mode) rather than a shrug. Confidence the cross-feature angle is *fully*
    exhausted is moderate, not high: pass #13 found its bugs by asking "do these two features'
    independent state machines fight over one file," and this pass's stress-tests of the *same* guard
    pass #13 just added found only a redundant-coverage case, not a new hole -- suggesting the
    specific achx-push/native-tsx pairing is genuinely settling, but the sweep has now only checked
    that one pairing against itself twice in a row (passes #13 and #14) rather than trying a
    genuinely different pairing (e.g. hot-reload vs. tab-cache, or the recovery-file system vs. tab
    switching) with the same rigor. A pass #15 that picks a *different* pair of subsystems --
    or, per the stop condition, a second consecutive "nothing new" pass on this exact pairing --
    is the more informative next step than a third stress-test of the same guard.
  Test: `AnimationEditor.App.Tests.NativeTsxSelfAssociationTests.
  AddAssociatedTiledTileset_TargetIsTheCurrentlyOpenTsxItself_ThrowsInsteadOfWritingSelfReferentialTiledSync`.
  Full suite: `AnimationEditor.Core.Tests` 2200/2200 (unchanged -- no `.Core` source change this
  pass), `AnimationEditor.App.Tests` 990/990 (was 989, +1 new), full
  `AnimationEditorAvalonia.slnx` build 0 warnings/0 errors.

- [x] **Frame resize in a native-tsx chain (#1155, PR #1155).** A handle drag on one frame left its
  siblings at the old size, so the chain failed the mapper's footprint check and its tile animation
  was skipped on save. `FrameFootprintSync.ComputeSiblingMatches` now applies the dragged frame's
  per-edge delta to every sibling (any of the 8 grow/shrink directions), recorded in the same undo
  command. Tests: `FrameFootprintSyncTests`, `WireframeHandleDragTests.HandleDrag_StretchingFrameInNativeTsxProject_*`.

- [x] **Entry-tile ownership transfer on a left/top frame-0 resize (#1156, PR #1157).** Resizing
  frame 0's left or top edge moves its top-left cell; the pinned entry-tile hint then pointed at a
  cell that was now a satellite (collision, `ValidateNoTileIdCollisions` threw) or outside the
  footprint. `ProjectManager._tsxEntryHintOriginFrames` tracks the frame each hint was derived
  from; the hint transfers only when that frame is still in the chain with its cell on a different
  tile, so a reorder or add/remove keeps the pin. A save-then-reload matrix (21 drags x 3 owner
  provenances) found three more: satellite hints keyed by the old origin survived a transfer and
  collided; a fresh satellite sat at the frame's physical cell instead of next to the owner tile,
  which the loader reads it back from, so growing a hand-authored chain wrote an unattachable
  satellite; a loaded owner already at frame 0's cell was pinned like a hand-authored one. Tests:
  `TsxFrameResizeRoundTripTests` (71), `TsxEntryTileOwnershipTransferTests`,
  `MultiTileToTiledAnimationMapperTests`.

- [x] **Fresh-eyes pass #15 -- the resize/drag edit path as a whole: every code path that changes
  a frame's size, through autosave, into `SaveTsxProject`.** Pass #14 asked for a different
  subsystem pairing; #1155 had just added footprint propagation to exactly one of the three
  size-changing paths, so the question was whether the other two had the same hole. They did.
  - **Inspector Width/Height (`AppCommands.SetFramePixelRegion`) -- real gap, fixed.** Typing a
    width for one frame of a native-tsx chain left its siblings unchanged; the autosave that runs
    on every edit then skipped the chain with a footprint warning. Siblings now take the same
    width/height (keeping their own X/Y) inside the same `BulkFrameEditCommand`; an X/Y-only edit
    propagates nothing, and an achx project is untouched. Test:
    `AppCommandsSetFramePixelRegionTsxTests` (4; two red before the fix).
  - **Bulk handle drag (`WireframeControl` commit for `_bulkHandleDragStarts`) -- real gap,
    fixed.** Reachable only with a selection mixing chain nodes and individual frames (two chain
    nodes alone drag every frame of both, which stays consistent), but the commit site had no
    propagation at all. Both bulk commit sites now funnel through `CommitBulkHandleDrag`, which
    uses a new many-frames `FrameFootprintSync.ComputeSiblingMatches` overload (per chain, first
    size-changed dragged frame sets the delta; dragged frames are never siblings). Tests:
    `FrameFootprintSyncTests.ComputeSiblingMatches_ManyResized*`,
    `WireframeHandleDragTests.BulkHandleDrag_StretchingOneFrameOfEachChainInNativeTsxProject_*`
    (red before the fix).
  - **Chain drag, single-frame move, inspector X/Y -- safe.** A move changes no footprint; the
    owner tile follows frame 0's cell via #1157's transfer (`TsxFrameResizeRoundTripTests`
    "move" rows and `Resize_MoveOnlyFrameOne_OwnerStays`).
  - **Mid-drag autosave -- safe.** Frame coordinates mutate live during a drag but
    `FrameRegionChanged` (the autosave trigger) fires only on commit, after propagation. A
    partial state reaching a save is covered by `Resize_OnlyFrameZeroGrownLeft_WarnsAndKeepsOwnerUntilSiblingsMatch`.
  - **Off-grid drops -- safe (correcting an earlier note).** `MainWindow.SyncGridControlsToProject`
    forces the wireframe grid on at the tsx's own tile size for a native-tsx project and refuses
    to let it be turned off or resized, so a handle drag always lands on tile boundaries.
  - **One UX residual, not data loss, not fixed here:** a tile collision thrown by
    `SaveTsxProject` is swallowed by `SaveCurrentAnimationChainList`'s bare `catch` into
    `MarkSaveFailed`, so the user never sees which two chains collided (fixed in pass #16).
  Full suite: `AnimationEditor.Core.Tests` 2323, `AnimationEditor.App.Tests` 999,
  `AnimationEditor.Views.Tests` 146, `DocScreenshots` 6, all green.

- [x] **Fresh-eyes pass #16 -- chain-level operations in a native-tsx project (duplicate, paste,
  new chain over owned cells) and what the user sees when the save refuses.** One real gap,
  fixed; the rest traced safe.
  - **Duplicate chain always fails the save, silently -- real gap, fixed at the reporting
    layer.** A copy animates the same cells as its source, so it claims the same tile and
    `ValidateNoTileIdCollisions` refuses the whole save (correct: a Tiled tile carries one
    animation). But `SaveCurrentAnimationChainList`'s bare `catch` turned that into a red "Auto
    Save Failed" status with no reason, and every later autosave kept failing the same way until
    the copy was moved -- with nothing telling the user that. New `IAppCommands.SaveFailed`
    carries the exception message; desktop and browser show it as a toast ("Auto save failed --
    Tiled tile 0 would be claimed by both ..."). Also covers disk/permissions failures on achx
    saves, which were equally silent. Undoing the duplicate makes the next autosave succeed
    again. Not changed: the duplicate itself. Auto-assigning the copy a free owner tile (the
    hand-authored unrelated-owner pattern) would let two chains share cells, but the copy would
    then stay pinned there forever once the user moves its frames; a deliberate feature, not a
    sweep fix. Test: `AppCommandsSaveFailedTests` (2).
  - **Paste chain / new chain over owned cells -- same class, same fix.** Both reach the same
    collision throw and now the same toast.
  - **Shrink-then-regrow satellite hint vs. a chain placed on the vacated tile -- safe.** The
    kept `(1,0)` hint is a real physical-cell conflict once the chain regrows, so refusing (now
    with a reason) is right.
  - **Redo of a transferring resize -- safe.** `BulkFrameRegionChangedCommand.Redo` re-applies
    the same rects as `Do`; the transfer is a pure function of the rects
    (`TsxFrameResizeRoundTripTests.Resize_GrowLeftThenRevert_*` covers the reverse).
  - **Hot-reload of a tsx with origin-tracked chains -- safe.** `LoadTsxProject` replaces every
    chain object and re-seeds `_tsxEntryHintOriginFrames` from the file.
  Full suite: `AnimationEditor.Core.Tests` 2325, `AnimationEditor.App.Tests` 999,
  `AnimationEditor.Views.Tests` 146, `DocScreenshots` 6, all green; Browser builds clean.

- [x] **Fresh-eyes pass #17 -- achx-model data the tsx format can't hold (flips, sprite offset,
  color, collision shapes, non-looping chains), and every way a user can still create it in a
  native-tsx project.** One real gap, fixed; the earlier snapping note corrected (see pass #15).
  - **Silently dropped on save -- real gap, fixed at the save boundary.** The inspector hides the
    transform/color/shape panels for a tsx project (#1140), but the frame context menu still
    offers "Add Rectangle"/"Add Circle" (`TreeMenuPlanBuilder`), the chain menu "Duplicate
    flipped" (`HandleDuplicateChainsFlip`), the Loop toggle (#1120), and paste from an achx tab
    all still produce that data. `MultiTileToTiledAnimationMapper` reads only rects and durations,
    so it vanished on save and on the next reload -- a shape sat in the tree until then. Rather
    than gate each entry point (and miss the next one), `SaveTsxProject` now appends one
    `TsxLossyDataCheck` warning per chain naming the kinds it dropped ("chain "Walk": collision
    shape, flip can't be stored in a .tsx and was not saved."), through the existing
    "Saved, but not every change applied" toast. The chain itself is still written. Tests:
    `TsxLossyDataCheckTests` (14), `ProjectManagerTsxProjectTests.SaveTsxProject_FrameCarriesCollisionShape_*`.
  - **Not done: gating those entry points.** Hiding "Add Rectangle" etc. for a tsx project would
    be friendlier than a warning after the fact, but it is per-entry-point work and the boundary
    check already guarantees nothing is lost without notice. Worth a small follow-up issue.
  Full suite: `AnimationEditor.Core.Tests` 2340, `AnimationEditor.App.Tests` 999,
  `AnimationEditor.Views.Tests` 146, `DocScreenshots` 6, all green.

- [x] **Fresh-eyes pass #18 -- the load side against what real Tiled writes: every root/image/
  tile attribute and element a 1.9-1.12 tileset can carry, through an edit-and-save, in both the
  patch-in-place path and the full-rewrite fallback.** Two real losses and one silent-wrongness,
  all fixed; one validator gap closed.
  - **Tiled 1.9 `class="..."` on tiles -- real loss, fixed.** Tiled 1.9 saved a tile's class as
    `class` (1.10 went back to `type`, all DotTiled reads). An edited tile is regenerated from the
    model, so the class vanished from any 1.9-era animated tile the moment its animation was
    touched, and a class-only tile whose animation was removed was deleted as "empty". New
    `TsxLoader.LoadTileset` (now the only tsx read path: `ProjectManager`, `TsxWriter`'s
    original load, `TiledTilesetSyncRunner`) folds `class` into `Tile.Type`; a regenerated tile
    writes `type`, exactly what Tiled itself does on resave. Tests:
    `NativeTsxForeignContentRoundTripTests.*Tiled19Class*` (2, red first).
  - **`backgroundcolor` on the root -- real loss in the fallback, fixed.** DotTiled has no slot
    for it; the patch path reuses the original root text so it survived there, but the full
    rewrite (taken for a file the writer can't slice per line, e.g. minified) dropped it.
    `TsxWriter.Write(path)` now copies any root attribute it doesn't model from the original
    file. Test: `EditAndSave_MinifiedSingleLineFile_KeepsEveryForeignAttributeAndElement` (red
    first); its Tiled-formatted twin proves the patch path keeps a kitchen-sink of root/image/
    grid/tileoffset/property-of-every-type/tile class+probability content byte-for-byte.
  - **Margin/spacing tilesets -- silent wrong geometry, now refused at open.** Neither mapper
    accounts for margin/spacing (`TiledAnimationToAchjMapper` places tile N at col*tilewidth;
    the native save path never even passed them to `MultiTileToTiledAnimationMapper`), so such a
    file opened with every frame rect shifted off its pixels and no way to save. Real support is a
    feature (multi-tile frames would span the gap pixels); `TsxCompatibilityChecker` refuses with
    a reason, like wangsets. Test: `CheckOpenCompatibility_TilesetWithMarginOrSpacing_*`.
  - **Frame id past the tile count -- validator gap, closed.** A hand-edited frame beyond
    `tilecount` loaded as an off-image rect and could never be saved, with nothing at open saying
    why. `TsxAnimationValidator` now reports it. Test: `Validate_FrameReferencesTileBeyondTileCount_ReturnsIssue`.
  - **Safe:** `trans` is rewritten as `#aarrggbb` (Tiled reads both), self-closing tags gain a
    space, the fallback reorders `<image>` before `<tileoffset>` -- all cosmetic; per-tile
    object layers, wangsets, transformations were already refused up front.
  Full suite: `AnimationEditor.Core.Tests` 2347, `AnimationEditor.App.Tests` 999,
  `AnimationEditor.Views.Tests` 146, `DocScreenshots` 6, all green.

- [x] **Fresh-eyes pass #19 -- the load side, continued: tileset shapes Tiled can produce that
  aren't a plain single-image atlas with full metadata.** One regression (mine, from #1157)
  fixed, two misleading failures turned into clear refusals.
  - **`<image>` without width/height -- opening threw, fixed.** The loader already fell back to
    the grid's extent (columns*tilewidth by rows*tileheight), but `ProjectManager.
    BuildTsxTilesetInfo` passed a null texture size to the save path, which dereferenced it on the
    first UV->pixel conversion ("Nullable object must have a value"). Before #1157 that only broke
    the save; #1157's open-time mapping (origin seeding) turned it into a file that wouldn't open
    at all. The save path now uses the loader's own `GetTextureSize`, so the conversion inverts the
    load exactly. Test: `EditAndSave_ImageWithoutWidthAndHeight_OpensAndSavesUsingTheGridSize`
    (red first).
  - **Image-collection tileset -- refused with the right reason.** A one-image-per-tile tileset
    (`columns="0"`, no shared `<image>`) is a legitimate Tiled file with no tile grid for any of
    this editor's math; the only error it hit was the mapper's "Columns=0 isn't a valid tile-grid
    width", which called a valid file corrupt. `TsxCompatibilityChecker` now refuses it by name.
    The columns=0-with-an-image corrupt case still takes the old path. Test:
    `CheckOpenCompatibility_ImageCollectionTileset_ReturnsBlockingReason`.
  - **Animated tile itself past the tile count -- validator gap, closed** (pass #18 covered only
    the frames it references). Test: `Validate_AnimatedTileItselfBeyondTileCount_ReturnsIssue`.
  - **Safe:** an `<image source="../art/x.png">` outside the tsx folder resolves through the same
    relative-path join every achx uses; BOM/CRLF handling is already covered by `TsxWriterTests`.
  Full suite: `AnimationEditor.Core.Tests` 2350, `AnimationEditor.App.Tests` 999,
  `AnimationEditor.Views.Tests` 146, `DocScreenshots` 6, all green.
