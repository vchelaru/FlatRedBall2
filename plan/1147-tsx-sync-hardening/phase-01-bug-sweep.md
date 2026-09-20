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

- [ ] **`TabEditorCache`'s tab-switch cache restore bypasses `LoadTsxProject`/`LoadAnimationChain`
  entirely, so it can't apply the `LoadAnimationChain` fix from fresh-eyes pass #4 (see DONE) --
  switching between a cached tsx tab and a cached achx tab can still leave stale native-tsx state.**
  Confirmed by reading (not yet reduced to a failing test -- the fix touches files outside this
  sweep's declared "Files in scope" list, so left as a TODO rather than expanding scope
  mid-sweep). `ProjectManager` is a single instance reused across tabs (see `TabSwitchCacheTests.cs`);
  `TabEditorCache.ApplyToProject`/`CaptureFromProject` (`AnimationEditor.Core/Models/TabEditorCache.cs`)
  round-trip a tab's editor state through `IProjectManager`'s public surface only
  (`AnimationChainListSave`, `OnDiskCoordinateType`, `FileName`) -- there is no public surface at all
  for `_tsxTileset`/`_tsxEntryTileIdsByChain`/`_tsxSatelliteTileIdsByChain`, so a tab switch that hits
  the cache (`TryActivateTabFromCache`, when `TabEditorCache.HasFreshCache` is true -- i.e. every
  switch back to a tab already visited once, since the file on disk hasn't changed) never touches
  those fields at all, regardless of whether the tab being switched to/from is a tsx or achx project.
  Concretely: open tsx tab A, open achx tab B (first visit to each goes through
  `OpenProjectWorkflowAsync` -> `LoadTsxProject`/`LoadAnimationChain`, so `_tsxTileset` is set for A's
  visit and -- after this pass's fix -- cleared for B's), switch back to tab A via
  `TryActivateTabFromCache` (fresh cache, no reload) -- `_tsxTileset` is still whatever
  `LoadAnimationChain` last left it (cleared, from loading B), not tab A's tileset, so
  `IsNativeTsxProject` now wrongly reports `false` while tab A is active and its
  `AnimationChainListSave` is actually tab A's tsx-derived chains. A full fix needs `IProjectManager`
  to expose enough surface for `TabEditorCache` to capture/restore the tsx-specific state per tab
  (or an explicit tsx/achx flag + cached tileset on `TabEntry`), which touches `IProjectManager.cs`,
  `TabEditorCache.cs`, and possibly `TabEntry.cs`/`TabController.cs` -- none of which are in this
  sweep's declared scope.

- [ ] **Fresh-eyes pass #5 needed**: pass #4 found two new real, confirmed bugs (fixed, see DONE) plus
  the TODO above, so per the phase doc's stop condition ("finds nothing new to add, twice in a row")
  this phase is not yet exhausted -- this was not a clean pass. Suggested starting points for pass
  #5: the achx-push mapper's per-frame (not per-chain) skip semantics in `AchjToTiledAnimationMapper.
  MapFrame` -- `ChainMappingResult.EntryTileId` is always the *first non-skipped* frame's tile id, so
  a chain whose frame 0 is skipped (texture mismatch, size mismatch, etc.) but has other valid frames
  writes its animation starting from frame 1's tile instead of frame 0's; traced this pass as
  probably pre-existing/intentional single-cell achj-push behavior (not part of the native-tsx
  identity-preservation bug class this sweep targets), but not chased to a firm conclusion -- worth
  a deliberate look. Also worth another look: whether `TsxWriter`'s patch-mode `TopLevelEquals` should
  compare `Tileset.Properties` order-sensitively or order-insensitively (currently order-sensitive
  via `PropertiesEqual`'s `Zip` -- a hand-reordered-but-otherwise-identical tileset-level properties
  list would be seen as "different," falling back to a full rewrite; likely harmless since a full
  rewrite is always a safe fallback, but not explicitly traced this pass).

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
