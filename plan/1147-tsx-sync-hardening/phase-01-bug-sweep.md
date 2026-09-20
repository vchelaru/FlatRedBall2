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

- [ ] **Fresh-eyes pass #1**: once the above are done, do a dedicated pass (self or subagent)
  re-reading every file in scope end to end asking "what haven't we tried yet" — new categories to
  consider: concurrent edits (two `ProjectManager` instances / two AnimationEditor windows open on
  the same tsx), very large tilesets (performance, not just correctness), non-Latin/unicode chain
  names round-tripping through the `Name` property, and the achj (JSON) vs achx (XML) serialization
  paths for anything this phase touches.

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
