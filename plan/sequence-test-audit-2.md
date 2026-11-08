# Sequence mode test audit 2

Snapshot, 2026-11-07. Mutations ran against main at e04f2e7d (#1674), after the first audit (`plan/sequence-test-audit.md`, #1613). Not maintained: re-run the mutations before trusting it later.

## Verdict

- 402 behaviors audited by mutation (break the production line, run only the relevant tests). 317 failed a test. 85 survived.
- Of the 85 (the late PRs 1675 to 1679, merged while this ran, are in the table too): 14 are equivalent or redundant (listed below), 66 were unpinned and now have a test that fails against the same mutation, 5 are still open because the surface cannot be staged headlessly.
- Riskiest gaps found: the guard that makes Ctrl+X do nothing when neither the list nor the timeline has the focus (without it, Cut would delete the timeline selection while a button held the focus); Edit > Cut and Edit > Duplicate menu clicks were not routed by any test; Ctrl+X, Ctrl+D and Delete typed into a text box were never checked (only Ctrl+C and Ctrl+V were); a paste anchored on a selected chain, and a track paste below several selected chains or headers, were never exercised with more than the trivial case; a refused source in a paste never reached the banner in any test; the snap tie between a dragged chain's start and end, and the own-cycle mark at the chain's start, had no test; Escape cancelling a Ctrl rubber band, the chip-drop Alt bypass, and zoom around the lane edge had no test.
- Test discipline: every merged PR in scope shipped tests, but they pin the main path and skip boundaries (ties, one-sided clamps, exact edges, defaults, repeats). Whether the tests came before the code cannot be read from squash merges. One test was wall-clock dependent and would fail on a slow runner (fixed, see Hygiene).
- Bugs found: none in production code. 33 hostile clipboard envelopes, weird loop and start values (0, negative, 1e30, 1e9 on an empty chain) and zoom extremes threw nothing and hung nothing.

## Coverage by PR

Strength: **outcome** = asserts the observable result; **weak** = only that it does not throw; **none** = unpinned. Ids are the mutations below.

| PR | Behaviors | Pinned by | Strength |
|---|---|---|---|
| 1597, 1609 chip drag and edge resize cursors (not in audit 1) | stock cursor ids, hover shapes | DragCursorsTests | outcome; non-Windows hover shapes were none (DC5) |
| 1611 filmstrip of frames (not in audit 1) | to-scale cells, slivers, ping-pong merge, window centering, placement | FilmstripLayoutTests, FilmstripPlacementTests | outcome; repeat of one frame, cut on a boundary, reversed UVs, no-texture width, longest-frame ties, cell and visit edges were none (FL03, FL04, FL09, FL12, FL14, FL16, FL17, FL20) |
| 1612 filmstrip hover card | card text, merged list, cap | FilmstripReadoutTests, SequenceFilmstripHoverScenarioTests | outcome; deduplicated merged list was none (FR3) |
| 1627 malformed fields | every typed read, newer-version tick clamp | SequenceListSaveMalformedTests, SequenceMalformedOpenScenarioTests, SequenceClockTests | outcome (J01-J12) |
| 1629 undo selects the changed sequence | diff rule, restore | SequenceDocumentSelectionTests, SequenceSelectionRestoreScenarioTests | outcome (D38-D43) |
| 1630 achx tab that fails to load | dropped like a seqj tab | AchxCorruptOpenScenarioTests | outcome (AC1) |
| 1631 event row | cycles, ping-pong mirror, cut tail, merge, cards | EventMarkersTests, EventReadoutTests, SequenceEventRowScenarioTests | outcome; reversed ping-pong, marker right edge, unnamed event, negative duration, merged marker time, 8-event cap were none (EV04, EV09, EV12, EV13, EV15, ER3) |
| 1634 audit gaps, ruler | tick step, labels | TimelineRulerTests | outcome; half-second labels were none (W18) |
| 1636 new track click | header column only, selects, renames | SequenceNewTrackScenarioTests | outcome (NT1-NT3) |
| 1638 fixed-width numbers | readout, zoom field | CountingNumberWidthTests (shaped text) | outcome; drop pill font is none and not testable cheaply |
| 1639 play-all, open in tab | gate rule, timer, persistence | LibraryChipPlaybackTests, SequenceLibraryAutoplayScenarioTests | outcome (H01-H04, LB2-LB4) |
| 1640 source and texture reload | stamp rule, reload on show | SequenceSourceReloadTests, SequenceTextureReloadScenarioTests, FilmstripRendererTextureReloadTests | outcome; stamp length and write time were none (R03, R04); preview redraw after a texture swap is none (PL1, open) |
| 1641 scrollbars | mapping, binder | TimelineScrollBarsTests, SequenceTimelineScrollBarScenarioTests | outcome; negative and overshoot clamps were none (B04, B06) |
| 1642 track reorder | slot, Z renumbering, gesture | TrackReorderTests, SequenceDocumentTrackReorderTests, SequenceTrackReorderScenarioTests | outcome (T01-T12, G14) |
| 1643 track eye | saved flag, scene, eye hit | SequenceTrackVisibleTests, SequenceSceneTests, SequenceTrackEyeScenarioTests | outcome (V01-V05, W13, W14, G10); dimming was none (DIM1, G27) |
| 1646 copy qualified name | format, menu | QualifiedNameTests, SequenceListMenuScenarioTests | outcome (QN1) |
| 1647 middle-drag pan | sign, clamp, no selection change | TimelineViewportTests, SequenceMiddlePanScenarioTests | outcome (W06, W07, G11) |
| 1650 zoom presets | stepping, clamp, wheel | TimelineZoomPresetTests, WheelZoomPresetTests, SequenceTimelineZoomScenarioTests | outcome; StepZoom clamp and the pivot over the headers were none (P06, G13) |
| 1654 selected-sequence memory | defaults, restore | SequenceSelectionMemoryTests, SequenceSelectionRestoreScenarioTests | outcome (Y01-Y07, X20) |
| 1656 sprite hidden outside its chain | scene rule | SequenceSceneTests | outcome |
| 1657, 1674 timeline performance | culling, lane heights, atlas, layers, F3 | TimelineCullingTests, SequenceTimelineRenderBudgetScenarioTests | outcome; zero-length block, outline slack, event-row lane height, F3 reaching the timeline, hidden-track dimming were none (K06-K08, F3, DIM1, G27) |
| 1658 Add Sequence rename | rename starts, unique names | SequenceAddRenameScenarioTests, NameGeneratorTests | outcome (AR1, N01-N07); "2b" is not a number was none (N05) |
| 1659 chip hover outline | outline pixels | LibraryChipOutlineTests | outcome (pixels); not mutation-run |
| 1660 unified snapping | thinning, priorities, Alt, both ends, marks | TimelineSnapTests, TimelineSnapTargetsTests, TimelineDropPlanTests, SequenceTimelineSnapScenarioTests | outcome; start-beats-end tie, own-start mark, cut neighbour of the second dragged chain, chip-drop Alt were none (S10, S17, S25, G06) |
| 1661 live preview | refresh after every change | SequencePreviewLiveScenarioTests | outcome; texture-swap redraw none (PL1) |
| 1663 sequence duplicate, copy, paste | envelope, placement, names | SequenceClipboardTests, SequenceDuplicateAndPasteTests, SequenceDuplicateCopyPasteScenarioTests | outcome (C20-C24, D33-D37); version 0 was none (C21) |
| 1666 remember zoom | clamp, forget at 100% | TimelineZoomMemoryTests, SequenceZoomRestoreScenarioTests | outcome (Z01-Z06, X21) |
| 1668 flaky flip test | clock frozen | FlipAnimationScenarioTests | not mutated |
| 1669 multi-select, move, copy, cut, paste, duplicate | selection rules, clamping, envelopes, track mapping, routing | TimelineSelectionTests, TimelineMarqueeSelectorTests, TimelineMultiMoveTests, TimelineClipboardTests, SequenceDocumentTimeline*Tests, six scenario files | outcome for the main rules; envelope version 0, non-path sources, half a track list, default visible, created-track name, negative paste time, default track name, refusal once per file, distinct names for several pasted or duplicated tracks, duplicate shift of a cut chain, anchor by selected chain, track paste below the lowest selection, refusal banner, Cut without a surface, text-box keys, Edit > Cut and Duplicate, Escape on a band were none |
| 1671 click on empty lane seeks | rule, gesture | TimelineEmptyClickTests, SequenceTimelineClickPlayheadScenarioTests | outcome (E01-E04, G22, G25) |
| 1673 marquee performance | binary search, throttle, selection layer | TimelineMarqueeSelectorTests, SequenceTimelineMarqueePerformanceScenarioTests, TimelineSelectionLayerTests | outcome; exact-edge touches were none (Q03, Q04); the scenario count depended on the wall clock (fixed) |
| 1675 sequence rename click-away | commit waits for the press, keeps the clicked selection, redo selects | SequenceRenameClickAwayScenarioTests, SequenceDocumentSelectionTests | outcome (SD1, SD2); redo of a keep-selection rename was asserted vacuously (the selection was already there): SD3 now has a test; the focus hand-off is open (RC1) |
| 1676 drag a chain onto the new-track row | plan, tracks made, names, Z, description | TimelineNewTrackMoveTests, SequenceTimelineMoveToNewTrackScenarioTests | outcome (NM1-NM9, DP1, DP2, MV1-MV3) |
| 1677 popups above the selection and playhead | layer order | TimelinePopupLayerTests (pixels) | outcome (PU1) |
| 1678 Flip Diagonal menu | not sequence mode | not audited | n/a |
| 1679 leaving an open rename commits it | tab switch, library click, close, hotkeys, track and list boxes | SequenceRenameLeaveScenarioTests | outcome (CP1-CP3, CP6, CP8); focus hand-off and the same-document guard are open (CP4, CP5, RC1) |
| 1680 skill signpost | docs | n/a | n/a |

## Bugs

None in production code. Hygiene finding, fixed: see below.

## Hygiene: timing-dependent tests

- **Fixed.** `SequenceTimelineMarqueePerformanceScenarioTests` asserted the rubber band raised `SelectionChanged` between 1 and 6 times. The control throttles it to one per 33 ms on the real clock, so a runner that takes more than about 200 ms for the 60 moves raises more and fails. The test now holds the throttle open (reflection on the field, since the throttle takes ticks, not a clock) and asserts exactly 2: the first change and the flush. The throttle's own timing rules are pinned by ticks in `TimelineMarqueeSelectorTests` (Q06, Q07, Q08b).
- Not changed, cannot fail on a slow runner: `SequenceNewerVersionScenarioTests` waits 9 s of real time past the 8 s banner (slow, not flaky; the banner never comes back early). Negative checks that wait a fixed time (`LeavingTheSequenceTab_WhilePlaying_StopsTheClock`, 300 ms) can only pass early on a slow runner, never fail.
- Not changed, can fail only if a runner stalls past a generous timeout: positive `WaitUntilAsync` polls of 2 to 5 s on real timers (library autoplay, preview playhead, texture reload); `Play_WithLoop_WrapsBackToTheStartAndKeepsPlaying` needs to sample a 80 ms window of each lap but retries every lap for 3 s.
- Not changed, theoretical: the fixed 150 ms settles after fire-and-forget clipboard work (`CtrlAsync` in the copy and paste scenario files). The headless clipboard completes within one dispatcher turn, so a stall could skip the pump but has not. The new tests wait for the observable outcome instead.

## Not testable headlessly

| Surface | Extracted core | Could still be extracted |
|---|---|---|
| OS drag and drop of files into the library | `SequenceDocument.AddSources`; the test raises the same DragEvent | none |
| Win32 cursor calls (`DragCursors.SetOsCursor`) | `StockCursorId`, `ForHover` | none |
| Skia ops (`FilmstripOp`, `LaneBackdropOp`, atlas, block paint) | `FilmstripLayout`, `FilmstripPlacement`, `TimelineCulling`, `TimelineLayout`; render counters; pixel tests for dimming and the selection outline | the op replay itself |
| Real timers (playback tick, chip timer, zoom ease, banner, F3 sampling) | `SequenceClock.AdvanceTick`, `SettleZoomAnimation`, `BurstRate`, `SelectionChangeThrottle` | the `DispatcherTimer` wrappers |
| Drop pill font | none | the typeface choice could be a function of `mono` and the theme |
| Stale-document guard after the async clipboard read (X17) | none | an `IClipboard` seam on the pane |
| Focus hand-off when a rename commits by click-away (RC1, CP4), same-document guard on a deferred track rename (CP5) | the selection rules are tested | the focus rule is one bool in the pane; no headless click target distinguishes it |
| Preview redraw after a texture swap on disk (PL1) | `ThumbnailService.PathInvalidated` is tested | a draw counter on the preview control |

## Equivalent or redundant survivors

L03, L15, Q02, C25, C26, D16, D40, FP02, G08, G15, X19, AR2, LB1, CP7. The table says why each is dead or covered by another path.

## Mutations

Q, S, L, M, C, D, R, T, V, J, K, B, Z, Y, P, N, H, W, E, EV, FL, FP, ER, FR: Common and Core. X, G, HK, AC, NT, QN, AR, LB, SB, DC, PL, F3, MQ, DIM: App (HK are the hotkey and menu scope mutations; K are the culling ones). `caught` shows the first failing test where the run recorded one; `-` means it was not recorded. Three mutations hung the test host (P01, P02, W08: infinite loops) and count as caught. Mutations that did not compile or apply (J11, Q08, X10) are not counted. Equivalent entries say why.

| Id | Behavior broken | Result | Test |
|---|---|---|---|
| S01 | snap: Thin keeps higher-priority kind when crowded | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S02 | snap: Thin gap is twice the snap distance | caught | Thin_EqualPriority_KeepsTheEarlierTime |
| S03 | snap: Thin drops a target crowding from the right | caught | Thin_TwoTargetsCloserThanTwiceTheDistance_KeepsTheHigherPriority |
| S04 | snap: Thin drops a target crowding from the left | caught | Thin_EqualPriority_KeepsTheEarlierTime |
| S05 | snap: bypass (Alt) disables Edge snapping | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S06 | snap: bypass (Alt) on Move returns free start | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S07 | snap: Edge ignores targets before minTime | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S08 | snap: Edge never goes below minTime | caught | Edge_IgnoresTargetsBelowTheMinimum_AndNeverReturnsLessThanIt |
| S09 | snap: Move considers the chain END snapping | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S10 | snap: Move tie goes to the start | UNPINNED -> pinned | Move_StartAndEndEquallyFarFromTheirTargets_TheStartSnapWins |
| S11 | snap: Move end-target skipped if it would put start before 0 | caught | Move_AnEndSnapThatWouldStartBeforeZero_IsNotOffered |
| S12 | snap: Move grid rounds to GridStep | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S13 | snap: grid only at low zoom | caught | GridIsUsable_OnlyWhileOneStepIsNarrow |
| S14 | snap: Nearest tie goes to higher priority | caught | Edge_EquidistantTargets_HigherPriorityWins |
| S15 | snap: reach is SnapPixels in pixels (zoom-aware) | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S16 | snap: CycleMarks stride thins narrow cycles | caught | CycleMarks_NarrowCycles_OfferEveryNthMarkFromTheStart_WhateverThePointerIsAt |
| S17 | snap: CycleMarks counted from chain start (k>=1) | UNPINNED -> pinned | CycleMarks_NearTheChainsOwnStart_NeverOffersTheStartItself |
| S18 | snap: FrameMarks empty when cycle narrower than 2x snap | caught | FrameMarks_OnlyWhenACycleIsWideEnoughToHoldThem |
| S19 | snap: FrameMarks offsets within cycle | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S20 | snapTargets: dragged chain offers no edges | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S21 | snapTargets: chain cut by dragged offers no played end | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S22 | snapTargets: cut chain offers its full end | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S23 | snapTargets: playhead is a target | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S24 | snapTargets: zero is a target | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S25 | snapTargets: multi-drag cut check uses ALL dragged starts | UNPINNED -> pinned | SnapTargetsOthers_ANeighbourCutByTheSecondOfSeveralDraggedChains_OffersNoPlayedEnd |
| S26 | snapTargets: frame boundaries skip ping-pong repeated end frame | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S27 | snapTargets: ResizeEdge offers own cycle marks | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| S28 | snapTargets: ResizeEdge offers frame marks | caught | TimelineSnapTests, TimelineSnapTargetsTests |
| L01 | selection: Ctrl toggles chain off when selected | caught | - |
| L02 | selection: Shift range across track by start time | caught | - |
| L03 | selection: Shift on other track selects only clicked | survived (equivalent) | falls back to SelectChain, same result |
| L04 | selection: Shift range keeps the anchor as primary | caught | - |
| L05 | selection: MakePrimary only when selected | caught | - |
| L06 | selection: Ctrl toggles track off | caught | - |
| L07 | selection: Shift tracks range | caught | - |
| L08 | selection: Ctrl+A with tracks selects all tracks | caught | - |
| L09 | selection: Ctrl+A first press = primary's track only | caught | - |
| L10 | selection: Ctrl+A with none selects every chain | caught | - |
| L11 | selection: Retain drops vanished chains | caught | - |
| L12 | selection: Retain drops tracks past count | caught | - |
| L13 | selection: Remap shifts later chain indices after delete | caught | - |
| L14 | selection: Remap only counts same-track removals | caught | - |
| L15 | selection: set holds chains or tracks never both (chains win) | survived (equivalent) | private path, no caller hands over both sets |
| L16 | selection: chains kept in document order | caught | - |
| L17 | selection: Replace reports no change when same | caught | - |
| L18 | selection: Clear when empty returns false | caught | - |
| L19 | selection: SelectChains primary honoured only when selected | UNPINNED -> pinned | SelectChains_PrimaryThatIsNotInTheSet_FallsBackToTheLastChain |
| L20 | marquee: block overlap right edge inclusive/min 1px | UNPINNED -> pinned | Marquee_ReachesAZeroLengthChain_ThatIsDrawnOnePixelWide |
| L21 | marquee: rect in any corner order | caught | - |
| L22 | marquee: lane vertical overlap test | caught | - |
| Q01 | marqueeSel: Ctrl base selection kept | caught | - |
| Q02 | marqueeSel: unchanged returns false (no redraw) | survived (equivalent) | SelectChains already reports no change; the early return only saves an allocation |
| Q03 | marqueeSel: binary search left bound | UNPINNED -> pinned | MarqueeSelector_ABandEdgeExactlyOnAChainsEdge_TouchesThatChain_OnBothSides |
| Q04 | marqueeSel: binary search right bound | UNPINNED -> pinned | MarqueeSelector_ABandEdgeExactlyOnAChainsEdge_TouchesThatChain_OnBothSides |
| Q05 | marqueeSel: lane vertical skip | caught | - |
| Q06 | throttle: raises at most once per interval | caught | - |
| Q07 | throttle: Flush reports held request | caught | - |
| M01 | multimove: time clamp keeps earliest chain >= 0 | caught | - |
| M02 | multimove: time clamp uses the EARLIEST chain | caught | - |
| M03 | multimove: lane clamp top | caught | - |
| M04 | multimove: lane clamp bottom | caught | - |
| M05 | multimove: lane clamp uses highest occupied lane | caught | - |
| M06 | multimove: lane clamp uses lowest occupied lane | caught | - |
| M07 | multimove: empty origins no-op | UNPINNED -> pinned | MultiMovePlan_NothingDragged_MovesNothing |
| Q08b | throttle: Reset re-arms the first-request-immediate | caught | - |
| C01 | tlclip: copied chains rebased to earliest start | caught | - |
| C02 | tlclip: copied sources written absolute | caught | - |
| C03 | tlclip: foreign 'kind' rejected silently | caught | - |
| C04 | tlclip: version < 1 rejected | UNPINNED -> pinned | TimelineClipboard_VersionZero_IsADamagedCopy |
| C05 | tlclip: sources must be strings | UNPINNED -> pinned | TimelineClipboard_SourcesThatAreNotPaths_AreADamagedCopy |
| C06 | tlclip: chainTracks count must equal chain count | caught | - |
| C07 | tlclip: chain track index must be in range | caught | - |
| C08 | tlclip: legacy envelope (no track info) accepted | caught | - |
| C09 | tlclip: only one of sourceTracks/chainTracks = damaged | UNPINNED -> pinned | TimelineClipboard_TrackListWithoutTheChainToTrackList_IsADamagedCopy |
| C10 | tlclip: copied track 'visible' default true when absent | UNPINNED -> pinned | TimelineClipboard_ACopiedTrackWithNoVisibleField_IsVisible |
| C11 | tlclip: wrong-typed optional track field = damaged | caught | - |
| C12 | tlclip: chains lane/name written per distinct lane | caught | - |
| C13 | tlclip: tracks envelope needs 'tracks' array | caught | - |
| C14 | tlclip: used sources listed once case-insensitively | caught | - |
| C20 | seqclip: foreign kind rejected | caught | - |
| C21 | seqclip: version < 1 rejected | UNPINNED -> pinned | SequenceClipboard_VersionZero_IsADamagedCopy |
| C22 | seqclip: 'sequence' must be an object | caught | - |
| C23 | seqclip: sources must be strings | caught | - |
| C24 | seqclip: copy writes sources absolute | caught | - |
| C25 | seqclip: UNC path counts as absolute | survived (equivalent) | Path.Combine returns a rooted second path on Windows; differs only on Linux |
| C26 | seqclip: drive path counts as absolute | survived (equivalent) | same |
| D01 | paste: no selection -> land by track name | caught | - |
| D02 | paste: selected track -> relative lanes | caught | - |
| D03 | paste: lane past last track -> created track | caught | - |
| D04 | paste: out-of-range selected track is no anchor | UNPINNED -> pinned | PasteChains_WhenTheSelectedTrackIsNoLongerThere_LandsByTrackName |
| D05 | paste: legacy envelope created track Z = count; new = source Z | caught | - |
| D06 | paste: created track gets unique name | UNPINNED -> pinned | PasteChains_ThatMakeATrackWhoseNameIsTaken_GiveItAFreeName |
| D07 | paste: created track keeps Visible | caught | - |
| D08 | paste: created track keeps FlipHorizontal | caught | - |
| D09 | paste: created track keeps X/Y | caught | - |
| D10 | paste: negative paste time clamped to 0 | UNPINNED -> pinned | PasteChains_AtANegativeTime_PutsTheEarliestChainAtZero |
| D11 | paste: chains offset by paste time | caught | - |
| D12 | paste: result ids point at the new chain slots | caught | - |
| D13 | paste: legacy default track name when envelope has none | UNPINNED -> pinned | PasteChains_FromACopyWithNoTrackInformationOrName_MakesATrackNamedTrack |
| D14 | paste: new source added to file | caught | - |
| D15 | paste: non-achx source refused | caught | - |
| D16 | paste: self-referencing .seqj source refused | survived (equivalent) | the .achx/.achj check already refuses a .seqj |
| D17 | paste: refusal reported once per file | UNPINNED -> pinned | PasteChains_OfTwoChainsFromTheSameRefusedSource_ReportsItOnce |
| D18 | paste: reference source rewritten relative to this file | caught | - |
| D19 | paste: already listed source reused (no duplicate) | caught | - |
| D20 | pasteTracks: inserted after the given track | caught | - |
| D21 | pasteTracks: no anchor -> appended at end | caught | - |
| D22 | pasteTracks: names of pasted tracks stay unique among themselves | UNPINNED -> pinned | PasteTracks_ThatShareAnUnnumberedAndANumberedName_GetDistinctNames |
| D23 | dupTracks: copies inserted after the LAST selected | caught | - |
| D24 | dupTracks: copy names unique among themselves | UNPINNED -> pinned | DuplicateTracks_OfTwoTracksWhoseCopiesWouldCollide_GetDistinctNames |
| D25 | dupChains: result ids count earlier copies on the same track | caught | - |
| D26 | dupChains: copies shifted by the selection length | caught | - |
| D27 | timelineDup: shift = latest FullEnd - earliest Start | UNPINNED -> pinned | DuplicateChains_OfAChainThatIsCutByTheNextOne_ShiftsByItsFullLength |
| D28 | timelineDup: shift from earliest Start | caught | - |
| D29 | deleteChains: highest index first | caught | - |
| D30 | deleteTracks: highest index first | caught | - |
| D31 | showAllTracks sets every track visible | caught | - |
| D32 | setTrackVisible writes the flag | caught | - |
| D33 | copySeq/dupSeq: duplicate placed right after original | caught | - |
| D34 | pasteSeq: inserted after selected sequence | caught | - |
| D35 | pasteSeq: no selection -> after the last | caught | - |
| D37 | dupSeq: unique 'copy' name | caught | - |
| D38 | undo selection: edit selects the sequence it changed | caught | - |
| D39 | undo selection: ChangedSequence returns the differing index | caught | - |
| D40 | undo selection: count change names no sequence | survived (equivalent) | every edit that changes the sequence count names its selection |
| D41 | undo selection: unchanged document names no sequence | caught | - |
| D42 | undo restores selected track | caught | - |
| D43 | TakeTrackSelection is one-shot | caught | - |
| R01 | reload: unchanged stamp is skipped | caught | - |
| R02 | reload: Changed raised only when something changed | caught | - |
| R03 | reload: stamp includes length | UNPINNED -> pinned | ReloadChangedSources_RewrittenToAnotherLengthUnderTheSameTimestamp_IsSeenByItsLength |
| R04 | reload: stamp includes write time | UNPINNED -> pinned | ReloadChangedSources_RewrittenToTheSameLength_IsSeenByItsNewTimestamp |
| R05 | reload: deleted file reloads as missing | caught | - |
| T01 | reorder: dropping in own slot is a no-op | caught | - |
| T02 | reorder: dropping just below own slot is a no-op | caught | - |
| T03 | reorder: moving down lands at slot-1 | caught | - |
| T04 | reorder: ascended check tolerates equal Z | caught | - |
| T05 | reorder: non-ascending Z renumbered to lane | caught | - |
| T06 | reorder: moved Z clamped between neighbours | caught | - |
| T07 | reorder: low neighbour bound | caught | - |
| T08 | reorder: high neighbour bound | caught | - |
| T09 | reorder: slot picked by lane midpoint | caught | - |
| T10 | reorder: below last lane = append slot | caught | - |
| T11 | reorder: MoveTrack no-op returns null with no undo entry | caught | - |
| T12 | reorder: Z assigned from plan to moved tracks | caught | - |
| V01 | visible: written only when false | caught | - |
| V02 | visible: default true when absent | caught | - |
| V03 | visible: not an unknown property (no duplicate key) | caught | - |
| V04 | visible: false is written | caught | - |
| V05 | scene: hidden track draws nothing | caught | - |
| J01 | seqj: Bool rejects non-bool | caught | - |
| J02 | seqj: Float rejects non-finite / non-number | caught | - |
| J03 | seqj: Double rejects non-finite / non-number | caught | - |
| J04 | seqj: Int rejects non-int (fraction/string) | caught | - |
| J05 | seqj: String rejects non-string | caught | - |
| J06 | seqj: Objects item must be an object | caught | - |
| J07 | seqj: Objects value must be an array | caught | - |
| J08 | seqj: Strings value must be an array | caught | - |
| J09 | seqj: Strings item must be a string | caught | - |
| J10 | seqj: PositiveInt rejects < 1 | caught | - |
| J12 | seqj: null value is wrong type (not absent) | caught | - |
| K01 | culling: lane above ruler culled | caught | - |
| K02 | culling: lane below view culled | caught | - |
| K03 | culling: block left of header culled | caught | - |
| K04 | culling: block right of view culled | caught | - |
| K05 | culling: uses the longer of played/full (cut tail visible) | caught | - |
| K06 | culling: zero-length block is at least 1px | UNPINNED -> pinned | IsBlockVisible_ZeroLengthBlockJustLeftOfTheLanes_IsStillDrawnOnePixelWide |
| K07 | culling: selected outline slack | UNPINNED -> pinned | IsBlockVisible_BlockEndingJustLeftOfTheLanes_StillShowsItsSelectedOutline |
| K08 | culling: uses per-lane height (event rows) | UNPINNED -> pinned | IsBlockVisible_TallLaneWhoseBottomIsStillBelowTheRuler_IsTrue |
| E01 | emptyClick: only on empty lane | caught | - |
| E02 | emptyClick: modifier click does not move playhead | caught | - |
| E03 | emptyClick: a drag (marquee) does not move playhead | caught | - |
| E04 | emptyClick: time clamped through ScrubTime | caught | - |
| B01 | scrollbars: header width excluded from view | caught | - |
| B02 | scrollbars: end margin past sequence | caught | - |
| B03 | scrollbars: maximum covers a scrolled-past value | caught | - |
| B04 | scrollbars: value never negative | UNPINNED -> pinned | ScrollBars_NegativeScroll_ReadsAsZero |
| B05 | scrollbars: vertical max = content - control | caught | - |
| B06 | scrollbars: vertical value clamped | UNPINNED -> pinned | ScrollBars_NegativeScroll_ReadsAsZero |
| B07 | scrollbars: vertical viewport excludes ruler | caught | - |
| B08 | scrollbars: thumb fraction | caught | - |
| B09 | scrollbars: CanScroll | caught | - |
| Z01 | zoomMemory: 100% forgets the entry | caught | - |
| Z02 | zoomMemory: unchanged value reports no change | caught | - |
| Z03 | zoomMemory: remembered percent clamped to limits | caught | - |
| Z04 | zoomMemory: default is 100 | caught | - |
| Z05 | zoomMemory: restore clamps a stale/edited value | caught | - |
| Z06 | zoomMemory: rounding | caught | - |
| Y01 | selMemory: out-of-range index ignored | caught | - |
| Y02 | selMemory: first sequence = default, forgotten | caught | - |
| Y03 | selMemory: unchanged name reports no change | caught | - |
| Y04 | selMemory: stored by name (survives reorder) | caught | - |
| Y05 | selMemory: empty list restores -1 | caught | - |
| Y06 | selMemory: unknown name restores first | caught | - |
| Y07 | selMemory: nothing stored restores first | caught | - |
| P01 | presets: step up goes to next greater preset | caught | - |
| P02 | presets: step down goes to next smaller preset | caught | - |
| P03 | presets: float drift snapped to preset | caught | StepToNextPreset_ZoomIn_FloatDriftBelowPreset_AdvancesNotStalls |
| P04 | presets: stops at the top preset | caught | StepToNextPreset_ZoomIn_AtMaxPreset_ClampsToMax |
| P05 | presets: stops at the bottom preset | caught | StepToNextPreset_ZoomOut_AtMinPreset_ClampsToMin |
| P06 | presets: StepZoom clamps to min/max | UNPINNED -> pinned | StepZoom_NeverLeavesTheZoomLimits_EvenWhenAPresetIsOutsideThem |
| N01 | names: Unique numbering starts at 2 | caught | - |
| N02 | names: case-insensitive collision | caught | - |
| N03 | names: ForCopy strips a trailing number (Idle 2 -> Idle 3) | caught | - |
| N04 | names: ForCopy unused name stays | caught | - |
| N05 | names: only an all-digit suffix is a number | UNPINNED -> pinned | ForCopy_ANameEndingInADigitThenALetter_IsNotNumbered |
| N06 | names: nothing before the number stays whole | caught | - |
| N07 | names: Unique skips taken numbers | caught | - |
| H01 | chipPlay: needs 2+ frames | caught | - |
| H02 | chipPlay: collapsed source never plays | caught | - |
| H03 | chipPlay: hover plays when play-all off | caught | - |
| H04 | chipPlay: play-all plays without hover | caught | - |
| EV01 | events: cycles repeat (ceil of played/cycle) | caught | Occurrences_AFrameEnteredBeforeThePlayedEnd_FiresEvenIfItIsCutShort |
| EV02 | events: cut tail drops later occurrences | caught | Occurrences_AFrameThatStartsAtOrPastThePlayedEnd_DoesNotFire |
| EV03 | events: ping-pong return pass flagged IsReturn | caught | Occurrences_APingPong_MirrorsOnTheReturnHalf_AndTheTurnaroundAndLoopPointAddNothing |
| EV04 | events: reverse flips IsReturn | UNPINNED -> pinned | Occurrences_AReversedPingPong_MarksItsFirstHalfAsTheReturn |
| EV05 | events: first showing fires at once | caught | Occurrences_AOneFrameLoop_FiresEachCycle_ButAOneFramePingPongOnlyOnce |
| EV06 | events: repeated end frame (ping-pong) fires once | caught | Occurrences_AOneFrameLoop_FiresEachCycle_ButAOneFramePingPongOnlyOnce |
| EV07 | events: markers merge within marker width | caught | For_IsOccurrencesThenGroup |
| EV08 | events: marker merge boundary exact | caught | Group_ExactlyAMarkerWidthApart_DoesNotMerge |
| EV09 | events: MarkerAt right edge exclusive | UNPINNED -> pinned | MarkerAt_JustPastTheMarkerWidth_FindsNothing |
| EV10 | events: MarkerAt left edge inclusive | caught | MarkerAt_FindsTheFlagUnderAPixel_AndNullElsewhere |
| EV11 | events: ping-pong cycle length doubles | caught | Occurrences_APingPong_MirrorsOnTheReturnHalf_AndTheTurnaroundAndLoopPointAddNothing |
| EV12 | events: unnamed event reads as empty string | UNPINNED -> pinned | FramesOf_AnEventWithNoName_ReadsAsAnEmptyName |
| EV13 | events: negative duration counted as 0 | UNPINNED -> pinned | Occurrences_AFrameWithANegativeDuration_TakesNoTime |
| EV14 | events: zero-length chain has no events | caught | Occurrences_NoEventsOrNoLength_IsEmpty |
| EV15 | events: Group keeps occurrence order & time | UNPINNED -> pinned | Group_AMergedMarker_IsPlacedAtItsFirstOccurrence |
| FL01 | film: cycles repeat to the played length | caught | Cells_ACutChainDrawsOnlyUpToThePlayedEnd |
| FL02 | film: ping-pong end frames shown twice merge to one cell | caught | CellAt_FindsTheCellUnderAnXOffset_AndVisitAtTheShowingUnderATime |
| FL03 | film: same frame twice in a row w/o ping-pong stays 2 cells | UNPINNED -> pinned | Cells_AOneFrameLoopWithoutPingPong_DrawsOneCellPerCycle |
| FL04 | film: merged unit keeps natural end | UNPINNED -> pinned | Cells_AMergedPingPongTurnaround_CountsBothShowingsWhenChoosingTheLongestFrame |
| FL05 | film: sliver threshold exact at 3px | caught | Cells_AFrameOfExactlyThreePixelsStandsAlone |
| FL06 | film: sliver accumulates to the threshold | caught | Cells_FramesUnderThreePixelsMergeIntoOneCellThatDrawsItsLongestFrame |
| FL07 | film: leftover slivers join the last cell | caught | Cells_SliversGroupUntilThreePixels_AndALeftoverJoinsThePreviousGroup |
| FL08 | film: merged cell shows its longest frame | caught | Cells_FramesUnderThreePixelsMergeIntoOneCellThatDrawsItsLongestFrame |
| FL09 | film: longest tie keeps the first | UNPINNED -> pinned | Cells_FramesOfEqualLengthInOneCell_ShowTheFirst |
| FL10 | film: narrow cell centers the sprite | caught | - |
| FL11 | film: sprite scaled to cell height by aspect | caught | Cells_AWideCellLeftAlignsTheSpriteAtTheFramesStart |
| FL12 | film: no texture -> square placeholder | UNPINNED -> pinned | Cells_AFrameWithNoTexture_IsAsWideAsTheCellIsTall |
| FL13 | film: diagonal flip swaps sprite w/h | caught | FramesOf_ReadsDurationsAndSpriteSizesFromTheUvRegions |
| FL14 | film: reversed UVs still measure width | UNPINNED -> pinned | FramesOf_UvsStoredRightToLeft_StillMeasureAPositiveWidth |
| FL15 | film: last visit cut at the played length | caught | Cells_ACutChainDrawsOnlyUpToThePlayedEnd |
| FL16 | film: CellAt right edge exclusive | UNPINNED -> pinned | CellAt_TheExactRightEdgeOfTheLastCell_IsNotInIt |
| FL17 | film: VisitAt boundary belongs to next visit | UNPINNED -> pinned | VisitAt_ExactlyWhereOneShowingEnds_IsTheNextShowing |
| FL18 | film: IsMerged only for 2+ frames | caught | Cells_AFrameOfExactlyThreePixelsStandsAlone |
| FL19 | film: IsReturn flag on ping-pong back pass | caught | Cells_PingPongMarksTheWayBackAsReturn_AndAReversedOneMarksItsFirstHalf |
| FL20 | film: end cut by played even mid-visit | UNPINNED -> pinned | Cells_ACutThatEndsExactlyOnAFrameBoundary_DrawsNoCellForTheNextFrame |
| FP01 | placement: rounds half away from zero | caught | Place_FractionalScale_CopiesWholePixelsOnly |
| FP02 | placement: no sprite -> HasSprite false | survived (equivalent) | the empty-window check below it drops the sprite too |
| FP03 | placement: sprite fully outside the cell is not drawn | caught | Place_SpriteOutsideTheCell_HasNoSprite |
| FP04 | placement: sprite clipped to the cell left | caught | Place_CellNarrowerThanTheSprite_CopiesTheWindowUnderIt |
| FP05 | placement: sprite clipped to the cell right | caught | Place_CellNarrowerThanTheSprite_CopiesTheWindowUnderIt |
| FP06 | placement: source offset is the clipped-off part | caught | Place_CellNarrowerThanTheSprite_CopiesTheWindowUnderIt |
| W01 | viewport: lane offsets accumulate event-row heights | caught | For_BelowATallLane_TargetsTheNextTrack_AndItsEventRowStillBelongsToTheTallOne |
| W02 | viewport: lane past the list is a base lane | caught | IsBlockVisible_BlockInsideTheLanes_IsTrue |
| W03 | viewport: new-track row sits below the last lane | caught | Chains_AnyCornerOrder_GivesTheSameResult |
| W04 | viewport: RowAt new-track row height | caught | For_BelowATallLane_TargetsTheNextTrack_AndItsEventRowStillBelongsToTheTallOne |
| W05 | viewport: ContentHeight includes the new-track row | caught | LaneTop_FollowsEachTracksOwnHeight_AndTheNewTrackRowIsAFullBaseLane |
| W06 | viewport: Pan horizontal sign/clamp | caught | Pan_MovesTheContentWithThePointer_OnBothAxesAtOnce |
| W07 | viewport: Pan vertical sign | caught | Pan_MovesTheContentWithThePointer_OnBothAxesAtOnce |
| W08 | viewport: zoom for slider rounds to 3 places | caught | - |
| W09 | viewport: SetPixelsPerSecond clamps | caught | SetPixelsPerSecond_KeepsTheLeftEdgeAndClamps |
| W10 | viewport: Changed raised on scroll | caught | Changed_FiresForEveryMutatorThatMovesTheView |
| W11 | viewport: Changed raised on lane heights | caught | Changed_FiresForEveryMutatorThatMovesTheView |
| W12 | layout: only a block with events makes its lane tall | UNPINNED -> pinned | LaneHeights_SeveralBlocksWithEventsOnOneTrack_AddOneEventRow |
| W13 | layout: eye hit box x | caught | IsOverEye_IsTrueOnTheEyeInTheTopBandAndFalseOnTheNameOrLowerInTheLane |
| W14 | layout: eye hit box y | caught | IsOverEye_IsTrueOnTheEyeInTheTopBandAndFalseOnTheNameOrLowerInTheLane |
| W15 | layout: TrackStarts last = block count | caught | TrackStarts_GivesWhereEachTracksBlocksBegin_AndTheTotalLast |
| W16 | layout: event row below tail strip is not a resize edge | caught | HitTest_InATallLanesEventRow_HitsTheBlockBody_AndNeverACutTail |
| W17 | ruler: tick step from 60px | caught | - |
| W18 | ruler: fractional labels below 1s steps | UNPINNED -> pinned | FormatTick_HalfSecondSteps_KeepTheirFraction |
| W19 | ruler: fallback step | caught | - |
| W20 | dropPlan: Alt bypass reaches snapping | caught | For_Bypass_IgnoresTargetsAndTheGrid |
| W21 | dropPlan: snapped target is reported | caught | For_NearAnotherChainsEnd_StartsExactlyThere_AndNamesTheTarget |
| W22 | dropPlan: snap targets passed through | caught | For_NearAnotherChainsEnd_StartsExactlyThere_AndNamesTheTarget |
| X01 | paste anchor: selected header is the anchor | caught | CtrlV_OntoTheSelectedTrack_PutsTheEarliestChainAtThePlayhead_AndSelectsWhatWasPasted |
| X02 | paste anchor: first selected chain's track | UNPINNED -> pinned | CtrlV_WithAChainSelectedOnAnotherTrack_PastesOntoThatChainsTrack |
| X03 | paste tracks goes below the lowest selected chain | UNPINNED -> pinned | CtrlV_OfACopiedTrack_WithChainsSelected_InsertsBelowTheLowestSelectedChainsTrack |
| X04 | paste tracks goes below the lowest selected header | UNPINNED -> pinned | CtrlV_OfACopiedTrack_WithHeadersSelected_InsertsBelowTheLowestSelectedHeader |
| X05 | paste selects what was pasted (chains) | caught | CtrlV_OntoTheSelectedTrack_PutsTheEarliestChainAtThePlayhead_AndSelectsWhatWasPasted |
| X06 | paste selects what was pasted (tracks) | caught | CopyingAndPastingTracks_InsertsCopiesAfterTheSelectedTrack_WithFreeNames_AndSelectsThem |
| X07 | paste reports refusals through the banner event | UNPINNED -> pinned | CtrlV_OfChainsFromASourceThatCannotBeOne_ShowsTheRefusalOnTheBanner |
| X08 | paste lands at the playhead | caught | CtrlV_OntoTheSelectedTrack_PutsTheEarliestChainAtThePlayhead_AndSelectsWhatWasPasted |
| X09 | duplicate selects the copies (chains) | caught | CtrlD_DuplicatesTheSelectedChains_RightAfterTheSelection_AndSelectsTheCopies_AsOneUndoEntry |
| X11 | cut copies BEFORE removing | caught | CtrlX_OnTheTimeline_CopiesThenRemovesTheChains_AsOneUndoEntry |
| X12 | cut removes the timeline selection | caught | CtrlX_OnTheTimeline_CopiesThenRemovesTheChains_AsOneUndoEntry |
| X13 | cut with no surface does nothing | UNPINNED -> pinned | CtrlX_WithTheFocusOnNeitherTheListNorTheTimeline_RemovesNothing |
| X14 | Delete on the list vs timeline by focus | caught | DeleteKey_RemovesTheSelectedChain_NotTheHiddenAchxSelection |
| X15 | menu clipboard follows the focus held before the menu opened | caught | EditMenuCopyThenPaste_OnTheFocusedList_CopiesAndPastesTheSelectedSequence |
| X16 | CanCopyPaste false with no focused surface | caught | EditMenuCopyAndPaste_AreEnabledOnlyWhileTheListOrTheTimelineIsFocused_OnASeqjTab |
| X17 | paste ignores a stale document (tab switched meanwhile) | UNPINNED (open) | stale-document guard after the async clipboard read; the headless clipboard completes synchronously, so the race cannot be staged |
| X18 | paste of a damaged copy shows the banner | caught | TextThatIsNotAnEnvelope_PastesNothing_AndShowsNoBanner_ADamagedOneShowsTheError |
| X19 | paste sequence stops playback | survived (equivalent) | selecting the pasted sequence already stops playback |
| X20 | selected sequence remembered on change | caught | DeletingTheSelectedSequence_RemembersTheOneThatTookItsPlace |
| X21 | timeline zoom remembered on change | caught | EachTab_KeepsItsOwnZoom_WhenSwitchingBetweenTwoFiles |
| X22 | changed sources reload when the tab is shown | caught | ASourceEditedWhileItsTabWasOpen_ShowsItsNewChainsWhenTheSequenceTabIsShownAgain |
| G01 | Ctrl+click on a chain toggles it | caught | ASelection_SurvivesARefreshAndAnUndo_WhereItsChainsStillExist |
| G02 | Shift+click on a chain extends | caught | ShiftClick_SelectsTheChainsBetween_OnTheSameTrack |
| G03 | Ctrl+click on a header toggles | caught | CtrlClickAndShiftClick_OnTrackHeaders_SelectTracks_AndDropTheChains |
| G04 | Shift+click on a header extends | caught | CtrlClickAndShiftClick_OnTrackHeaders_SelectTracks_AndDropTheChains |
| G05 | Alt bypasses snapping during move/resize | caught | DraggingTheRightEdge_WithAltHeld_DoesNotSnap |
| G06 | Alt bypasses snapping for a chip drop | UNPINNED -> pinned | DraggingAChipOntoALane_WithAltHeld_FollowsThePointerFreely |
| G07 | pressing a selected chain keeps a multi-selection | caught | DraggingOneSelectedChain_MovesTheWholeSelectionByTheSameTime_AsOneUndoEntry |
| G08 | edge drag resizes only the pressed chain | survived (equivalent) | pressing an edge collapses the selection to that chain first |
| G09 | body drag moves every selected chain | caught | DraggingOneSelectedChain_MovesTheWholeSelectionByTheSameTime_AsOneUndoEntry |
| G10 | eye toggles visibility both ways | caught | AllTracksHidden_PreviewsEmpty_WithoutErrors_AndTheSequenceKeepsItsLength |
| G11 | middle-drag pans in the drag direction | caught | MiddleDraggingFromAChain_PansTimeAndLanesTogether_AndChangesNothingElse |
| G12 | Ctrl+wheel zooms; plain wheel scrolls | caught | CtrlWheelAtTheLimits_StaysClamped |
| G13 | zoom pivot clamps to lane area | UNPINNED -> pinned | CtrlWheelOverTheTrackHeaders_ZoomsAroundTheLeftEdgeOfTheLanes |
| G14 | track drag commits one MoveTrack | caught | DraggingAHeaderUpOverATallLane_LandsInTheGapTheLineShowed |
| G15 | moved track stays selected | survived (equivalent) | the undo hand-off (TakeTrackSelection) selects the moved track anyway |
| G16 | gesture commits one undo entry | caught | DraggingAChainToAnotherLane_MovesItToThatTrack_AndCtrlZPutsItBack |
| G17 | multi-move description counts the chains | caught | DraggingOneSelectedChain_MovesTheWholeSelectionByTheSameTime_AsOneUndoEntry |
| G18 | Ctrl+A selects all (not while renaming/dragging) | caught | CtrlA_SelectsTheChainsOfTheSelectedTrack_ThenEveryChain_AndEscapeClears |
| G19 | Escape cancels a rubber band to its base | UNPINNED -> pinned | Escape_DuringACtrlRubberBand_PutsTheSelectionBackToWhatItWasBeforeTheBand |
| G20 | Escape clears selection when idle | caught | CtrlA_SelectsTheChainsOfTheSelectedTrack_ThenEveryChain_AndEscapeClears |
| G21 | Ctrl band adds to the selection | caught | CtrlRubberBand_AddsToTheSelectionAlreadyMade |
| G22 | plain press on empty space clears selection | caught | ClickingEmptyLaneSpace_ClearsTheSelection |
| G23 | press in the header column starts no band | UNPINNED -> pinned | PressingTheRulerCornerAboveTheHeaders_AndDraggingOverChains_SelectsNothing |
| G24 | band drag threshold | caught | ClickingEmptyLane_ClearsTheSelection_AndASmallWobbleStillCountsAsAClick |
| G25 | empty click seeks only when unmodified | caught | CtrlClickAndShiftClick_OnEmptyLane_DoNotMoveThePlayhead |
| G26 | selection summary names the count | caught | DraggingTheTopHeaderToTheBottom_MovesItToTheFront_AsOneUndoStep_AndKeepsItSelected |
| G27 | hidden track's chain outline dimmed | UNPINNED -> pinned | TheSelectedOutlineOfAHiddenTracksBlock_IsDimmedToo |
| G28 | Retain after rebuild drops removed chains | caught | UndoingTheDropOfASelectedChain_DropsItsSelection |
| ER3 | event card: lists at most 8 events then an ellipsis | UNPINNED -> pinned | EventReadout_MoreThanEightEventsInOneMarker_ListsTheFirstEightThenAnEllipsis |
| FR3 | film card: merged frames listed once each | UNPINNED -> pinned | FilmstripReadout_AMergedCellThatShowsOneFrameTwice_ListsThatFrameOnce |
| DC5 | cursor: hover cursor for resize on non-Windows | UNPINNED -> pinned | ForHover_GivesEachTimelineCursorItsOwnShape_AndNothingForTheDefault |
| NM1 | new-track move: only the dragged chains leave their tracks | caught | Apply_OneChain_LeavesItsTrackEmptyAndMakesATrackNamedAfterTheChain |
| NM2 | new-track move: chains are removed from their old track | caught | Apply_OneChain_LeavesItsTrackEmptyAndMakesATrackNamedAfterTheChain |
| NM3 | new-track move: one new track per source track | caught | Apply_ChainsFromTwoTracks_MakeOneNewTrackPerSourceTrack_InLaneOrder_KeepingTheirOrder |
| NM4 | new-track move: track named after its first chain | caught | Apply_ANameThatIsTaken_GetsANumber |
| NM5 | new-track move: blank chain name falls back to Track | caught | Apply_AChainWithNoName_MakesATrackCalledTrack |
| NM6 | new-track move: unique track name | caught | Apply_ANameThatIsTaken_GetsANumber |
| NM7 | new-track move: Z puts the track on top | caught | Apply_ChainsFromTwoTracks_MakeOneNewTrackPerSourceTrack_InLaneOrder_KeepingTheirOrder |
| NM8 | new-track move: result lists the new track indices | caught | Apply_ChainsFromTwoTracks_MakeOneNewTrackPerSourceTrack_InLaneOrder_KeepingTheirOrder |
| NM9 | new-track move: empty source tracks skipped | caught | ASelectionInOneTrack_GetsOneNewTrackHoldingAllOfIt |
| DP1 | move drop plan: only over the new-track row | caught | ForMove_OverALaneOrBelowTheNewTrackRow_IsNone |
| DP2 | move drop plan: width is the chain's full length in pixels | caught | ForMove_OverTheNewTrackRow_TargetsANewTrackAtTheGivenStart |
| SD1 | rename click-away leaves the selection alone | caught | RenameSequence_KeepSelection_LeavesTheSelectionAlone_ButUndoAndRedoStillSelectTheRenamedOne |
| SD2 | selectOnDo=false skips the select on the first run | caught | RenameSequence_KeepSelection_LeavesTheSelectionAlone_ButUndoAndRedoStillSelectTheRenamedOne |
| SD3 | redo of such an edit still selects | UNPINNED -> pinned | RenameSequence_KeepSelection_RedoStillSelectsTheRenamedOne_WhereverTheSelectionHasGone |
| MV1 | release on the new-track row makes the track(s) | caught | AMissingChain_MovesToo_WithItsOneSecondPlaceholderAsTheGhost |
| MV2 | plan shown only while over the new-track row | caught | AMissingChain_MovesToo_WithItsOneSecondPlaceholderAsTheGhost |
| MV3 | move description names the new track | caught | DraggingAChainToTheNewTrackRow_MakesATrackForIt_AsOneUndoEntry_AndSelectsIt |
| RC1 | click-away rename waits for the press and keeps its selection | UNPINNED (open) | a click-away commit refocusing the list; every click target the headless run can reach takes the focus from the list anyway |
| PU1 | popups draw in their own layer above the selection | caught | TheHoverCard_DrawsOverASelectedChainsOutline |
| CP1 | leaving a tab commits an open rename into that tab's history | caught | OpeningAnotherFile_WhileRenamingASequence_CommitsTheRename |
| CP2 | closing the window commits an open rename | caught | ClosingTheWindow_WhileRenamingASequence_CommitsTheRename |
| CP3 | a press that moves no focus commits an open rename | caught | ClickingALibraryChip_WhileRenamingASequence_CommitsTheRename_AndKeepsTheSelection |
| CP4 | timeline rename: Enter keeps the focus, click-away does not pull it | UNPINNED (open) | same focus hand-off as RC1, for the track rename box |
| CP5 | track rename belongs to the sequence it began in | UNPINNED (open) | needs the document replaced between the press and the deferred commit; no headless gesture does that |
| CP6 | timeline rename: focus-loss commit deferred past the press | caught | ClickingAnotherSequence_WhileRenamingATrack_CommitsTheRename_AndSelectsTheClickedRow |
| CP7 | list rename: the box is abandoned before it is applied (no second commit) | survived (equivalent) | a second commit finds the name unchanged and records no undo entry |
| CP8 | timeline rename commits with the pending list renames | caught | ClosingTheWindow_WhileRenamingATrack_CommitsTheRename |
| HK01 | hotkey copy ignored while a text box has focus | caught | CtrlC_AndCtrlV_InTheRenameBox_EditTheText_NotTheSequences |
| HK02 | hotkey cut ignored while a text box has focus | UNPINNED -> pinned | DeleteCutAndDuplicate_InTheTrackRenameBox_EditTheText_NotTheTimeline |
| HK03 | hotkey paste ignored while a text box has focus | caught | CtrlC_AndCtrlV_InTheRenameBox_EditTheText_NotTheSequences |
| HK04 | hotkey duplicate ignored while a text box has focus | UNPINNED -> pinned | DeleteCutAndDuplicate_InTheTrackRenameBox_EditTheText_NotTheTimeline |
| HK05 | Delete ignored while a text box has focus | UNPINNED -> pinned | DeleteCutAndDuplicate_InTheTrackRenameBox_EditTheText_NotTheTimeline |
| HK06 | PNG pane refuses document-scoped hotkeys | caught | OnAPngTab_DocumentMenuItemsAreDisabled_AndComeBackOnTheDocumentTab |
| HK07 | seqj pane accepts only App and Document scopes | caught | OnASequenceTab_AchxOnlyMenuItemsAndHotkeysAreOff_UndoRedoSaveStayOn |
| HK08 | achx pane clipboard menu items stay enabled | caught | OnAPngTab_DocumentMenuItemsAreDisabled_AndComeBackOnTheDocumentTab |
| HK09 | PNG pane clipboard menu items disabled | caught | EditMenuCopyAndPaste_OnAPngTab_AreDisabled |
| HK10 | menu Copy/Cut/Paste/Duplicate enablement from pane | caught | EditMenuCopyAndPaste_AreEnabledOnlyWhileTheListOrTheTimelineIsFocused_OnASeqjTab |
| HK11 | menu Paste routes to the pane | caught | EditMenuCopyThenPaste_OnTheFocusedList_CopiesAndPastesTheSelectedSequence |
| HK12 | menu Cut routes to the pane | UNPINNED -> pinned | EditMenuCut_WithTheTimelineFocused_RemovesTheSelectedChain |
| HK13 | menu Duplicate routes to the pane | UNPINNED -> pinned | EditMenuDuplicate_WithTheTimelineFocused_DuplicatesTheSelectedChain |
| HK14 | menu Copy routes to the pane | caught | EditMenuCopyThenPaste_OnTheFocusedList_CopiesAndPastesTheSelectedSequence |
| HK16 | copy on the list copies a sequence | caught | CtrlC_ThenCtrlV_OnTheFocusedList_PutsTheSequenceOnTheClipboardAsJson_AndPastesACopyAfterIt |
| HK17 | duplicate on the list duplicates a sequence | caught | CtrlD_WithTheSequenceListFocused_StillDuplicatesTheSequence |
| AC1 | achx tab failing to load on tab switch is dropped (#1630) | caught | Restoring_AnAchxTabBehindTheActiveOne_ThatFailsToLoad_ShowsTheBannerWhenSelected_AndDropsIt |
| NT1 | new-track click only in the header column | caught | ClickingTheNewTrackRowLane_AddsNothing |
| NT2 | new track is selected | caught | ClickingNewTrack_AddsAnEmptyTrack_SelectsIt_AndUndoRedoKeepIt |
| NT3 | new track opens its name for editing | caught | ClickingNewTrack_OpensItsNameForEditing_WithTheDefaultSelected_AndTypingEnterRenamesIt |
| QN1 | copy qualified name puts file + name on the clipboard | caught | CopyQualifiedName_FromARowMenu_CopiesTheRightClickedSequenceInItsFile |
| AR2 | Add Sequence stops playback | survived (equivalent) | selecting the new sequence already stops playback |
| LB2 | playback timer stops when nothing plays | caught | CollapsedSources_DoNotTickTheirChips_UntilExpanded |
| LB4 | collapsed source's chips do not play | caught | CollapsedSources_DoNotTickTheirChips_UntilExpanded |
| SB1 | scrollbar thumb change scrolls the timeline (horizontal) | caught | HorizontalBar_DraggingTheThumb_ScrollsTheTimelineToThatTime |
| SB2 | scrollbar thumb change scrolls the timeline (vertical) | caught | VerticalBar_WhenTheLanesOverflow_ShowsAndDraggingItScrollsTheLanes |
| SB3 | vertical bar hidden when lanes fit | caught | VerticalBar_WhenEveryLaneFits_IsHidden |
| SB4 | horizontal bar disabled when nothing to scroll | caught | HorizontalBar_SequenceShorterThanTheView_IsDisabled |
| SB5 | scrollbar refresh on view change | caught | HorizontalBar_FollowsTheWheel_AndTheThumbReachesTheEndAtTheSequenceEnd |
| AR1 | Add/Duplicate Sequence open the new name for editing | caught | ATakenName_ShowsTheBanner_AndKeepsTheDefault |
| LB1 | detached library plays no chips | survived (equivalent) | leaving the tab stops the chips another way |
| LB3 | play-all click reports to the pane (persisted) | caught | PlayAllToggle_IsWrittenToSettings_AndClearedWhenToggledOff |
| LB3b | play-all click is written to settings | caught | PlayAllToggle_IsWrittenToSettings_AndClearedWhenToggledOff |
| DIM1 | hidden track's blocks drawn dimmed | UNPINNED -> pinned | ABlockOnAHiddenTrack_IsDrawnDimmerThanTheSameBlockOnAVisibleOne |
| ER1 | event card: frame is 1-based of the chain's count | caught | At_OneEvent_NamesItItsFrameAndItsTime |
| ER2 | event card: return pass labelled | caught | At_AMirroredReturnEvent_SaysItIsTheReturn |
| ER4 | event card: unnamed event shown as (unnamed) | caught | At_AnUnnamedEvent_ReadsAsUnnamed |
| ER5 | event card: time is block start + occurrence time | caught | At_OneEvent_NamesItItsFrameAndItsTime |
| FR1 | film card: frame number is 1-based | caught | At_MergedSliverGroup_ListsItsRealFrames |
| FR2 | film card: return/forward labels | caught | At_DoubledPingPongCell_SaysWhichHalfTheShowingIs |
| FR4 | film card: merged list capped with an ellipsis | caught | At_MergedGroupOfManyFrames_ListsTheFirstEightThenAnEllipsis |
| FR5 | film card: duration is the visit's length | caught | At_MergedSliverGroup_ListsItsRealFrames |
| DC1 | cursor: reorder = N-S arrow | caught | StockCursorId_ReorderIsSizeNS |
| DC2 | cursor: pan = all-directions | caught | StockCursorId_PanIsSizeAll |
| DC3 | cursor: resize = W-E arrow | caught | StockCursorId_ResizeHorizontalIsSizeWE_DefaultIsArrow |
| DC4 | cursor: chip copy = hand, refused = no | caught | StockCursorId_CopyIsHand_NotAllowedIsNo |
| PL1 | live preview: changed texture redraws the preview | UNPINNED (open) | a texture replaced on disk redraws the preview; the preview exposes no draw counter or pixel hook to see it |
| F3 | F3 toggles the timeline diagnostics with the other panels | UNPINNED -> pinned | F3_TogglesTheTimelineDiagnosticsWithTheOtherPanels |
