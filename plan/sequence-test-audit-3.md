# Sequence mode test audit 3 (#1682)

Snapshot, 2026-11-07. Round 3 of `plan/sequence-test-audit.md` and `plan/sequence-test-audit-2.md`: PR #1682 ("a non-looping chain plays once and holds its last frame", squash f8afd500) merged after audit 2's mutation runs and was never mutated. Mutations ran against main at 581c9ace. Not maintained: re-run the mutations before trusting it later.

## Verdict

- 45 behaviors audited by mutation (break the production line, run only the relevant test classes), plus R05 added once the tests existed. 23 of the 45 failed a test. 22 survived: 3 are equivalent (below), 19 were unpinned and now have a test that fails against the same mutation (RED against the mutation, GREEN on the real code). One more mutation of the first pass (R04, the "held" line added twice) is left out of the count: it failed trivially and says nothing.
- Riskiest gaps found: the hold marker (`IsHold`) and its tolerance had no test for a looping chain or for a chain played exactly one cycle; the Loop flag read when a right-edge drag starts (`_dragRepeats`) was untested, so the edge could snap to second-cycle frame boundaries of a held chain, or fail to snap on a looping one; the Loop flag in the filmstrip and event caches was untested (a Loop flag flipped under an open sequence kept drawing the old cells and flags); the "held" hover line was untested for a ping-pong return and for a merged sliver group, and its position in the list was unpinned; the inspector's "Cycles" label and tooltips were untested.
- Bugs found: none. Scene, filmstrip, event row, hover card and edge magnets agree for Reverse and non-Reverse ping-pong holds, Loops below 1 with Loop off, a hold cut by the next chain, a tie at the same start, a hold whose last frame has no length, the Loop flag toggled under an open sequence, and a block of 1e9 cycles.

## Behaviors audited

Strength: **outcome** = a test asserts the observable result.

| Id | Behavior broken | Result | Test |
|---|---|---|---|
| H01 | scene: wraps the local time into a cycle again (the pre-1682 rule) | caught | SequencePingPongAndLoopTests, SequenceSceneTests |
| H02 | scene: clamps to one cycle for every chain, looping ones too | caught | At_ReferenceLoops_OnALoopingChain_RestartItEachCycle |
| H03 | ChainClock: wraps even when Loop is off | caught | SequencePingPongAndLoopTests |
| H04 | ChainClock: holds even when Loop is on | caught | ChainClockTests.Locate_Looping_WrapsPastTheEndBackToTheFirstFrame |
| H05 | ChainClock: a held chain's sample keeps the 1 us nudge | caught | SequenceSceneTests |
| H06 | ChainClock: a looping chain is treated as ended too | caught | SequenceSceneTests |
| F01 | filmstrip: a non-looping chain draws every cycle | caught | Cells_ANonLoopingChain_HoldsItsLastFrameAsOneStretchedCellAfterItsFirstCycle |
| F02 | filmstrip: hold also applied to a looping chain (cut mid-cycle) | UNPINNED -> pinned | Cells_ALoopingChainCutMidCycle_HasNoHold_AndItsClippedLastCellKeepsTheWindowOfItsFullWidth |
| F03 | filmstrip: hold when played equals one cycle (`>=`) | UNPINNED -> pinned | Cells_ANonLoopingChainPlayedExactlyOneCycle_HasNoHold_WhateverTheRoundingOfTheNumbers |
| F03b | filmstrip: hold without the 1e-9 tolerance | UNPINNED -> pinned | same |
| F03c | filmstrip: hold tolerance on the wrong side | caught | Cells_ANonLoopingChainPlayedExactlyOneCycle_HasNoHold |
| F04 | filmstrip: held visit not marked `IsHold` | caught | Cells_ANonLoopingChain_HoldsItsLastFrameAsOneStretchedCellAfterItsFirstCycle |
| F05 | filmstrip: held visit end not stretched | caught | same |
| F06 | filmstrip: held unit end not stretched | caught | same |
| F07 | filmstrip: held unit natural end not stretched (sprite aligned as in a narrow cell) | UNPINNED -> pinned | Cells_AHeldCell_IsAWideCell_SoItsSpriteLeftAlignsAtTheFramesStart |
| F08 | filmstrip: hold goes on the first visit of the last cell, not the last | UNPINNED -> pinned | Cells_ANonLoopingOneFramePingPong_HoldsOnlyTheShowingThatEndsTheCycle |
| F09 | filmstrip: hold skipped when there are no cells | survived (equivalent) | `units` cannot be empty here: the first visit always starts before `played` |
| F10 | filmstrip: `loop` defaults to false | caught | Cells_RepeatThePatternEachCycle_AndTheLastCycleIsClippedAtThePlayedEnd |
| E01 | events: a non-looping chain fires every cycle | caught | Occurrences_ANonLoopingChain_FiresOnlyDuringItsFirstCycle_NotDuringTheHold |
| E02 | events: a non-looping chain fires a second cycle | caught | same |
| E03 | events: `For` ignores `loop` | UNPINNED -> pinned | For_ANonLoopingChain_GroupsOnlyTheFirstCycle (production only calls `Occurrences` and `Group`; `For` is a test-side convenience) |
| E04 | events: `Occurrences` defaults `loop` to false | caught | Occurrences_AOneFrameLoop_FiresEachCycle_ButAOneFramePingPongOnlyOnce |
| E05 | events: `For` defaults `loop` to false | UNPINNED -> pinned | For_ANonLoopingChain_GroupsOnlyTheFirstCycle |
| R01 | hover card: no "held" line | caught | At_HeldTail_SaysItIsHeld_AndItsDurationCoversTheWholeHold |
| R02 | hover card: no "held" on a ping-pong return hold | UNPINNED -> pinned | At_TheHeldReturnOfAPingPong_SaysItIsHeld_AfterTheHalf |
| R03 | hover card: no "held" inside a merged sliver group | UNPINNED -> pinned | At_AHeldTailInsideAMergedGroup_ListsTheFramesThenSaysHeld |
| R05 | hover card: "held" inserted before the half or merged lines (added after the first pass) | RED check only | the same two tests (exact line lists) |
| S01 | edge magnets: frame marks past the first cycle offered to a chain that does not repeat | caught | ResizeEdge_OfAChainThatDoesNotRepeat_OffersFrameBoundariesOnlyInItsFirstCycle |
| S02 | edge magnets: first-cycle mark at exactly the cycle end dropped (`<`) | survived (equivalent) | frame boundaries are strictly inside a cycle, so no mark sits on the cycle end |
| S03 | edge magnets: first-cycle limit measured from time 0, not the block start | UNPINNED -> pinned | ResizeEdge_OfAChainThatDoesNotRepeatAndStartsLater_StillOffersFrameBoundariesInItsFirstCycleOnly |
| S04 | edge magnets: limit is two cycles | caught | ResizeEdge_OfAChainThatDoesNotRepeat_OffersFrameBoundariesOnlyInItsFirstCycle |
| S05 | edge magnets: `repeats` inverted | caught | same |
| S06 | edge magnets: `repeats` defaults to false | caught | ResizeEdge_SnapsToTheOwnWholeCycles_AndTheFrameBoundaries |
| D01 | Cycles error text reverts to "Loops must be above 0." | caught | SetChainLoops_ZeroNegativeOrNotFinite_IsRefusedWithNoUndoEntry |
| A01 | filmstrip cache key drops the Loop flag | UNPINNED -> pinned | TurningALoopFlagOffUnderAnOpenSequence_RedrawsItsFilmstripAsOneCycleAndAHold |
| A02 | filmstrip renderer does not pass the Loop flag | caught | ANonLoopingBlock_DrawsOneCycle_ThenItsLastFrameHeldToTheBlockEnd |
| A03 | event occurrence cache key drops the Loop flag | UNPINNED -> pinned | TurningALoopFlagOffUnderAnOpenSequence_DropsTheEventFlagsOfTheRepeats |
| A04 | event marker layout cache key drops the Loop flag | UNPINNED -> pinned | same |
| A05 | timeline does not pass the Loop flag to the event occurrences | caught | ANonLoopingChain_FlagsOnlyItsFirstCycle_NotTheHeldLastFrame |
| A06 | edge drag always assumes the chain repeats | UNPINNED -> pinned | DraggingTheRightEdge_SnapsToAFrameBoundaryInASecondCycle_OnlyWhenTheChainRepeats |
| A07 | edge drag always assumes the chain does not repeat | UNPINNED -> pinned | same |
| A08 | edge drag does not hand `repeats` to `ResizeEdge` | UNPINNED -> pinned | same |
| A09 | drag state `_dragRepeats` not reset at the end of a drag | survived (equivalent) | every drag start assigns it before any use |
| A10 | inspector label reads "Loops" | UNPINNED -> pinned | TheInspectorsChainField_IsCalledCycles_AndExplainsWhatALoopFlagOffDoes |
| A11 | inspector field tooltip removed | UNPINNED -> pinned | same |
| A12 | inspector label tooltip removed | UNPINNED -> pinned | same |

Edge cases checked with a test that passed on first run (no mutation needed, they pin the rule): Reverse and Reverse + ping-pong holds (frame 0 and the last frame, with their elapsed time), Loops below 1 with Loop off (no hold, the block simply ends), a hold cut by the next chain (held until the next starts, then the next plays), two chains starting together (the earlier plays zero seconds, a non-looping one too), a hold whose last frame has no length (the strip stretches it, as the scene holds frame n-1), a Loop flag flipped on the shared chain under an open sequence (scene, filmstrip cells, event flags all follow), a block of 1e9 cycles (non-looping: one cycle and a hold, looping: the cell cap), ping-pong, Reverse and one-frame event holds (no event fires in the hold), the cycle marks still offered inside a hold.

## Equivalent or redundant survivors

F09, S02, A09. The table says why.

## Not testable headlessly

| Surface | Extracted core | Could still be extracted |
|---|---|---|
| The pixels of the hold (Skia filmstrip op) | `FilmstripLayout.Cells`, pixel test `SequenceHoldTailTests` (DocScreenshots) already samples the cell | the op replay itself |
| The hover card's pixels | `FilmstripReadout.At`, the card lines asserted through the hover scenario | none |

## Finding left alone

The undo entries and the edge-drag description still say "Set Loops of 'X' to N" while the inspector now says "Cycles". The label is user visible in History and the tests assert it. Renaming it is a wording call, not a bug; not changed here.
