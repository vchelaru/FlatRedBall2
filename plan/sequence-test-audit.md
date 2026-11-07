# Sequence mode test audit

Snapshot, 2026-11-07. Mutations ran against main at 4b0e1972; the new tests were re-run green after merging main at 87764749 (which brought #1597, #1608, #1609, #1610, #1611, not audited here). Not maintained: re-run the mutations before trusting it later.

## Verdict

- 168 behaviors audited by mutation (break the production line, run only the relevant tests). 123 failed a test. 45 survived.
- Of the 45: 9 are equivalent or redundant (listed below), 1 is open, 35 were unpinned and now have a test that fails against the same mutation.
- Riskiest gaps found: a loop count overriding the chain's own Loop flag (the existing test's chain had Loop on, so it never ran the override); no sequence test used a ping-pong chain; empty or blank sequence/track names were accepted; selection survived switching sequence by keyboard; no test for a corrupt .seqj, a refused source removal, or reopening a closed tab.
- Bugs found (not fixed, tests-only PR): see Bugs.

## Coverage by PR

Strength: **outcome** = asserts the observable result; **weak** = only that it does not throw; **none** = unpinned.

| PR | Behaviors | Pinned by | Strength |
|---|---|---|---|
| 1587 pane per tab kind, hotkey scopes | scope refusal per pane, Delete/Space routing | TabKindScenarioTests, SequenceTabScenarioTests | outcome (A01, A02, A05-A09) |
| 1588 menu scoping | achx/document items disabled per pane, Undo/Redo enablement | TabKindScenarioTests::OnAPngTab_*, SequenceTabScenarioTests | outcome |
| 1589 format, timing, loader | cut, tie, float loops, missing chain, edge-drag magnets, conflict markers, UV conversion | SequenceTimelineTests, SequenceListSaveTests, AnimationFileLoaderTests | outcome; ping-pong, WrapTime<0, min loops, nearer-magnet, Length+missing were none (now outcome) |
| 1590 open .seqj | tab open/close/restore, corrupt file, document cache | SequenceTabScenarioTests | corrupt file and reopen were none (now outcome) |
| 1591 library | AddSources/RemoveSource refusals, chip drag, banner | SequenceDocumentTests, SequenceLibraryScenarioTests | refusal banner was none (now outcome) |
| 1592 timeline editing | one undo entry per gesture, Escape, drag lanes, hit-test, zoom/scroll clamps | SequenceTimelineScenarioTests, TimelineLayoutTests, TimelineViewportTests | zoom/scroll clamps, later-chain-wins, cut-edge were none (now outcome) |
| 1593 preview/playback | scene Z order, flip, loop override, clock, Space | SequenceSceneTests, SequenceClockTests, SequencePreviewScenarioTests | loop override was weak (wrong fixture); paused Advance none; tick clamp still none |
| 1594 folder menu, New Sequence File, convert | convert visibility, separators, naming | ProjectFolderConvertRuleTests, ProjectTreeMenuLayoutTests, NewSequenceFileNamingTests, ProjectPanelControlTests | outcome (M93-M103, V01-V04 all caught) |
| 1595 chip outline | pixel corners | DocScreenshots LibraryChipOutlineTests | outcome (pixel), not mutation-run |
| 1596 inspector, Go to Definition | field commit = one undo, refused values, error paths | SequenceInspectorScenarioTests, SequenceDocumentInspectorEditTests | outcome |
| 1598 library collapse | collapse state, no undo entry, persistence | CollapsedSourceSetTests, SequenceLibraryCollapseScenarioTests | outcome |
| 1599, 1607 navigation | coalescing, suppression, push/clear forward, capacity, validity | NavigationHistoryTests, NavigationCoalescingTests, NavigationTrackerTests, two scenario files | outcome; boundary, Redo, chords, dead sequence index were none (now outcome) |
| 1600 rename, sequence inspector | inline rename, validation | SequenceInlineRenameScenarioTests, SequenceDocumentTests | empty/blank/trim names were none (now outcome) |
| 1601, 1602 Go to Definition | menu, focus, error banner | SequenceLibraryScenarioTests, GoToDefinitionFocusScenarioTests | outcome |
| 1603 list menu | menu follows the right-clicked row | SequenceListMenuScenarioTests, SequenceListMenuLayoutTests | outcome |
| 1604 version, unknown fields | round trip at every level, version rules | SequenceListSaveTests | clobber guard, BOM, non-object root were none (now outcome) |
| 1605 docs | n/a | | |
| 1606 (merged; its files untouched) | TimelineDropPlan | TimelineDropPlanTests | outcome (M62 caught) |

## Bugs

1. **A `.seqj` with a wrong-typed field crashes the open path.** `{ "sequences": [ { "name": 5 } ] }` throws `InvalidOperationException` from `SequenceListSave.ReadSequence` (`GetValue<string>` on a number). `SequenceTabPane.LoadAsync` only catches `IOException`, `UnauthorizedAccessException` and `JsonException`, so the exception escapes `OpenAsync` instead of showing the "Could not open" banner. The same applies to `x`, `y`, `z`, `start`, `loops` and the booleans. Repro: write that file into a project and open it; a corrupt-JSON file is handled, this is not. **Fixed:** every typed read goes through `SeqjObject`, which throws `JsonException` naming the property.
2. **Fixed meanwhile, not a finding:** the doc gap that nothing surfaced `IsFromNewerVersion` (now #1610), and Delete with only a sequence selected doing nothing (now #1608).

## Not testable headlessly

| Surface | Extracted core | Could still be extracted |
|---|---|---|
| OS drag-drop of files into the library | `SequenceDocument.AddSources`; the test raises the same DragEvent the OS would | none |
| Chip drag cursor, timeline edge cursor (#1597, #1609, merged after the mutation runs, not audited) | `TimelineDropPlan`, `TimelineLayout.HitTest`, `TimelineCursor` | n/a |
| Timeline, preview and chip pixels | `TimelineLayout`, `TimelineDropPlan`, `SequenceScene`; chip outline has a pixel test | `TickStep` and ruler label formatting in `SequenceTimelineControl.Render` |
| Playback tick clamp (`MaxTickSeconds` in `SequenceTabPane.OnTick`) | `SequenceClock`, `TickClock` | none: the clamp is now `SequenceClock.AdvanceTick` (A26 pinned) |
| Status bar "Auto Save Failed" for a .seqj | `SequenceDocument.SaveError` (tested) | `SequenceTabPane.Status` label choice (not mutation-run) |

## Equivalent or redundant survivors

M04 dead cut in `ActiveAt`; M105 same exception type either way; A04 unreachable behind A02; A27 first tick stops a detached clock; A38 sub-threshold wiggle drops nothing; N12, N13, N14, N16 the pointer press already opens the navigation action.

## Mutations

M = Common/Core, A = App, N = navigation, V = Views. `caught` shows the first failing test; `UNPINNED -> pinned` shows the test added in this PR. M33 did not compile and is not counted.

| Id | Behavior broken | Result | Test |
|---|---|---|---|
| M01 | cut rule (PlayedLength clamps to next start) | caught | L]
[xUnit.net 00:00:00.37]     AnimationEditorCommon.Tests.SequenceT.. |
| M02 | tie rule (list order breaks equal starts) | caught | ecified pattern.

Failed!  - Failed:     1 |
| M03 | float loops (fractional loop count keeps its fraction) | caught | s matched the specified pattern.

Failed!  - Failed:     1 |
| M04 | ActiveAt cut by next chain | survived (equivalent) | ActiveAt's own cut is dead code (the latest started chain always has next.Start > time) |
| M05 | edge drag magnet to whole cycle | caught | Failed!  - Failed:     1 |
| M06 | edge drag magnet to next chain start | caught | led:     1 |
| M07 | edge drag minimum loops | UNPINNED -> pinned | SequenceTimelineTests.LoopsForEdgeDrag_BeforeTheStart_NeverShrinksBelowTheMinimum |
| M08 | edge drag: magnet chooses the nearer of cycle and next | UNPINNED -> pinned | SequenceTimelineTests.LoopsForEdgeDrag_CycleAndNextChainBothInReach_PullsToTheNearerOne |
| M09 | WrapTime loop wrap | caught | tched the specified pattern.

Failed!  - Failed:     1 |
| M10 | WrapTime negative time clamps to 0 | UNPINNED -> pinned | SequenceTimelineTests.WrapTime_WrapsOrHoldsAtTheEnd_AndNeverGoesNegative |
| M11 | Length: missing chain contributes nothing/uses cut | UNPINNED -> pinned | SequenceTimelineTests.Length_AMissingChainCutsTheOneBeforeIt_AndNoChainsIsZero |
| M12 | ping-pong cycle length doubles (sequence-specific tests only) | UNPINNED -> pinned | SequencePingPongAndLoopTests (4 tests; no sequence test used ping-pong before) |
| M13 | RemoveSource refusal while a chain uses it | caught | RemoveSource_Unused_RemovesIt_UsedByAChain_IsRefused |
| M14 | RemoveSource usage check is case-insensitive | UNPINNED -> pinned | SequenceDocumentAuditTests.RemoveSource_ChainUsesItWithDifferentCase_IsStillRefused |
| M15 | AddSources refuses non-achx/achj files | caught | AddSources_SeqjAndDuplicate_AreRefused_TheRestIsAdded |
| M16 | AddSources refuses duplicate sources | caught | AddSources_SeqjAndDuplicate_AreRefused_TheRestIsAdded |
| M17 | AddSources: refused files do not block the rest | caught | AddSources_SeqjAndDuplicate_AreRefused_TheRestIsAdded |
| M18 | Edit skips undo entry when nothing changed | caught | TrackEdits_SettingTheCurrentValue_RecordsNothing |
| M19 | CommitGesture records nothing when unchanged | caught | Gesture_LiveEditsCommitAsOneUndoStep_AndAnUnchangedGestureRecordsNot.. |
| M20 | CancelGesture restores the model | caught | CancelGesture_RestoresTheModelWithoutAnUndoStep |
| M21 | RenameSequence rejects empty name | UNPINNED -> pinned | SequenceDocumentAuditTests.RenameSequence_EmptyOrBlank_IsRefusedWithNoUndoEntry |
| M22 | RenameSequence rejects duplicate name | caught | RenameSequence_DuplicateName_IsRefused |
| M23 | RenameSequence trims | UNPINNED -> pinned | SequenceDocumentAuditTests.RenameSequence_SurroundingSpaces_AreTrimmed_... |
| M24 | RenameTrack rejects empty name | UNPINNED -> pinned | SequenceDocumentAuditTests.RenameTrack_EmptyOrBlank_IsRefusedWithNoUndoEntry |
| M25 | RenameTrack rejects duplicate name | caught | DeleteChainDeleteTrackAndRenameTrack_AreUndoable_AndADuplicateTrackN.. |
| M26 | SetChainStart refuses negative | caught | 10000000000000001) |
| M27 | SetChainLoops refuses zero | caught | 0) |
| M28 | PlaceChain clamps negative start to 0 | UNPINNED -> pinned | SequenceDocumentAuditTests.PlaceChain_NegativeStart_IsClampedToZero |
| M29 | AddSequence unique name suffix | UNPINNED -> pinned | SequenceDocumentAuditTests.PlaceChain_OnTheNewTrackRowThreeTimes_... / AddSequence_..._GivesSequence3 (mutation hit the track-name loop; sequence loop checked as M29b) |
| M30 | LocateDefinition: missing source error | caught | LocateDefinition_MissingSourceOrChain_ReturnsAnErrorAndNoPath |
| M31 | LocateDefinition: missing chain error | caught | LocateDefinition_MissingSourceOrChain_ReturnsAnErrorAndNoPath |
| M32 | Restore (undo/redo) re-saves the file | caught | PlaceChain_OnATrackAndOnTheNewTrackRow_EachIsOneUndoStepAndSaves |
| M34 | SaveToDisk reports failure through SaveError | UNPINNED -> pinned | SequenceDocumentAuditTests.SaveToDisk_WhenTheFolderIsGone_ReportsSaveError... |
| M35 | ResolveTexturePath: relative to source folder | caught | ResolveTexturePath_ResolvesAgainstTheSourcesFolder_NotTheSeqjs |
| M36 | AddSources stored relative to the seqj | caught | AddSources_FileInAnotherFolder_StoresItRelativeAndLoadsItsChains_AsO.. |
| M37 | SnapStart never before 0 | caught | SnapStart_RoundsToTheStep_AndNeverGoesBeforeZero |
| M38 | Blocks: missing chain placeholder length | caught | Blocks_CutChainKeepsItsFullLength_AndAMissingChainIsAOneSecondPlaceh.. |
| M39 | IsCut tolerance for float rounding | caught | Blocks_EndingAFloatRoundingPastTheNextStart_IsNotCut |
| M40 | HitTest: cut tail wins in the tail strip | caught | HitTest_BodyEdgeAndCutTail |
| M41 | HitTest: later chain wins (drawn on top) | UNPINNED -> pinned | TimelineAuditTests.HitTest_WhereOneChainEndsAndTheNextStarts_TheLaterChainWins |
| M42 | HitTest: missing chain cannot be edge-resized | caught | Blocks_CutChainKeepsItsFullLength_AndAMissingChainIsAOneSecondPlaceh.. |
| M43 | HitTest: a cut chain's body edge is not resizable | UNPINNED -> pinned | TimelineAuditTests.HitTest_ACutChainsBodyEnd_IsNotAResizeEdge_ButItsTailEndIs |
| M44 | magnet is fixed pixels at any zoom | caught | EdgeDragLoops_MagnetIsAFixedPixelDistanceAtAnyZoom |
| M45 | ZoomAround clamps the zoom | UNPINNED -> pinned | TimelineAuditTests.ZoomAround_PastEitherLimit_StaysWithinTheLimits |
| M46 | ScrollHorizontally never before 0 | UNPINNED -> pinned | TimelineAuditTests.ScrollHorizontally_BeforeTheStart_StopsAtZero |
| M47 | ScrollVertically clamped to content | UNPINNED -> pinned | TimelineAuditTests.ScrollVertically_IsKeptWithinTheContent_... |
| M48 | RowAt: new-track row | caught | RowAt_MapsRulerLanesNewTrackRowAndBelow_WithVerticalScroll |
| M49 | Clock: non-looping stops at the end | caught | Advance_PastTheEndWithoutLoop_StopsAtTheEnd |
| M50 | Clock: Play at end restarts | caught | Play_AtTheEnd_StartsAgainFromZero |
| M51 | Clock: Advance does nothing when paused | UNPINNED -> pinned | TimelineAuditTests.Advance_WhilePaused_DoesNotMoveThePlayhead |
| M52 | Scene: Z order | caught | At_TwoTracksFromTwoSources_DrawsBothInZOrderWithTheirOwnTextures |
| M53 | Scene: flip mirrors offset | caught | Sample_AFlippedTrack_MirrorsTheFrameOffsetAndFlipAcrossTheTrack |
| M54 | Scene: loop count wraps into a cycle | UNPINNED -> pinned | SequencePingPongAndLoopTests.At_ReferenceLoops_OverrideAChainWhoseOwnLoopFlagIsOff (the old test's chain had Loop on, so it never exercised the override) |
| M55 | Scene: texture resolved through the source | caught | At_TwoTracksFromTwoSources_DrawsBothInZOrderWithTheirOwnTextures |
| M56 | CollapsedSourceSet case-insensitive | caught | IsCollapsed_IgnoresCase_LikeTheDocumentsSourceLookup |
| M57 | CollapsedSourceSet.Load does not raise Changed | caught | Load_ReplacesTheKeys_WithoutRaisingChanged |
| M58 | CollapsedSourceSet.Prune raises Changed only when dropped | caught | Prune_DropsSourcesNoLongerListed_KeepsTheRest_AndRaisesChangedOnlyWh.. |
| M59 | SourceTitle backslash | caught | achj") |
| M60 | SourceTitle dotdot fallback | UNPINNED -> pinned | TimelineAuditTests.Format_ADotSegmentIsNotAFileName |
| M61 | List menu: blank space offers only Add | caught | OnBlankSpace_OffersOnlyAddSequence |
| M62 | DropPlan: header region is not a drop target | caught | For_OverTheHeaderOrPastTheRightEdge_TargetsNothing |
| M63 | unknown properties kept on a track | caught | RoundTrip_UnknownPropertiesAtEveryLevel_Survive |
| M64 | unknown properties kept on a chain reference | caught | RoundTrip_UnknownPropertiesAtEveryLevel_Survive |
| M65 | unknown properties kept on a sequence | caught | RoundTrip_UnknownPropertiesAtEveryLevel_Survive |
| M66 | unknown properties kept at top level | caught | RoundTrip_UnknownPropertiesAtEveryLevel_Survive |
| M67 | unknown entry cannot clobber a known key | UNPINNED -> pinned | SequenceListSaveTests.ToJsonString_AnUnknownEntryNamedLikeAKnownField_DoesNotReplaceIt |
| M68 | newer version written back, not downgraded | caught | FromJsonString_HigherVersion_LoadsKeepsNumberAndFlagsIt |
| M69 | version below 1 rejected | caught | "0") |
| M70 | BOM allowed in FromJsonString | UNPINNED -> pinned | SequenceListSaveTests.FromJsonString_LeadingByteOrderMark_IsAccepted |
| M71 | flipHorizontal written only when set | caught | ToJsonString_UnknownPropertiesWrittenAfterKnownOnes |
| M72 | unknown values are cloned (stored node reusable) | caught | UnknownProperties_AreIndependentOfTheSourceDocument |
| M73 | root must be an object | UNPINNED -> pinned | SequenceListSaveTests.FromJsonString_RootThatIsNotAnObject_Throws |
| M74 | IsFromNewerVersion boundary | caught | FromJsonString_NoVersion_LoadsAsOneAndSavesWithVersionOne |
| M75 | missing 'loops' defaults to 1 | caught | FromJsonString_MissingOptionalFields_UsesDefaults |
| M76 | version written first | caught | ToJsonString_NewFile_WritesVersionOneFirst |
| M77 | new navigation clears forward | caught | ANewUserMove_AfterGoingBack_DropsForward |
| M78 | repeat of newest entry ignored | caught | Push_SameAsTop_IsIgnored |
| M79 | history capacity trims oldest | caught | Push_PastCapacity_DropsTheOldest |
| M80 | Back skips invalid entries | caught | Back_WhenNoEntryIsValid_ReturnsNull_AndLeavesForwardAlone |
| M81 | Back remembers current for Forward | caught | ANewUserMove_AfterGoingBack_DropsForward |
| M82 | coalescing: a pause earns a new frame entry | caught | ShouldRecord_FrameToFrameAfterAPause_Records |
| M83 | coalescing: frame-only change is the same target | caught | ShouldRecord_FrameToFrameOfOneChain_CollapsesWhileTheUserKeepsMoving |
| M84 | coalescing: first move (no previous) is recorded | caught | ShouldRecord_FrameToFrameWithNoEarlierChange_Records |
| M85 | coalescing: quiet window boundary is inclusive | UNPINNED -> pinned | NavigationCoalescingTests.ShouldRecord_FrameMoveExactlyAtTheQuietWindow_... |
| M86 | Suppress cancels the open user action | caught | Suppress_StartedInsideAnOpenAction_CancelsThatAction |
| M87 | suppression open at action end records nothing | caught | Suppress_StopsAMoveInsideItFromPushing |
| M88 | ending the last suppression restarts the frame run | caught | AfterSuppress_AFrameMoveStartsAFreshRunInsteadOfJoiningTheOldOne |
| M89 | run measured from the previous change | caught | ACoalescedRun_KeepsExtendingWhileTheUserKeepsMoving |
| M90 | nested actions: only the outermost compares | caught | ActionsOverlappingOutOfOrder_PushOnceWhenTheLastOneEnds |
| M91 | dead places are not recorded | caught | UserAction_LeavingAPlaceThatNoLongerExists_PushesNothing |
| M92 | unchanged location records nothing | caught | UserAction_ThatChangesNothing_PushesNothing |
| M93 | convert visibility: existing .achj hides convert | caught | AchxWhoseAchjAlreadyExists_IsNotConvertible |
| M94 | convert visibility: bin/obj excluded | caught | AchxOnlyUnderBinOrObjInsideTheFolder_IsNotConvertible |
| M95 | convert visibility: sibling folder with a shared prefix is not inside | caught | AchxOutsideTheFolder_IsNotConvertible |
| M96 | folder menu: Convert group left out when nothing to convert (no dangling separator) | caught | Folder_WithNothingToConvert_DropsTheConvertGroupEntirely |
| M97 | file menu: Convert only on .achx | caught | seqj") |
| M98 | folder menu: New Sequence File offered | caught | Folder_WithNothingToConvert_DropsTheConvertGroupEntirely |
| M99 | new seqj name: only other .seqj collide | caught | SuggestSequenceFileName_NameTaken_AddsTheFirstFreeNumber |
| M100 | new seqj name: typed .seqj extension stripped | caught | ResolveSequence_AddsTheExtension_AndAcceptsATypedOne |
| M101 | new seqj name: collision refused | caught | ResolveSequence_TakenName_IsRejectedRatherThanSuffixed |
| M102 | new seqj name: empty refused | caught | "") |
| M103 | new seqj name: invalid chars refused | caught | ResolveSequence_InvalidCharacters_AreRejected |
| M104 | AnimationFileLoader: conflict markers refused | caught | Load_ConflictMarkers_ThrowsInvalidData |
| M105 | AnimationFileLoader: missing file reported | survived (equivalent) | File.ReadAllText throws FileNotFoundException anyway (message differs) |
| M106 | AnimationFileLoader: converts to UV | caught | Load_PixelFile_ReturnsUvModelAndRemembersPixelOnDisk |
| M107 | AnimationFileLoader: reports on-disk coordinate type | caught | Load_PixelFile_ReturnsUvModelAndRemembersPixelOnDisk |
| M108 | scanner: .seqj recognised case-insensitively | caught | ScanAsync_SeqjFile_IsIncludedWithoutAnimationFrames |
| A01 | seqj pane refuses Achx-scoped hotkeys and menu items | caught | OnASequenceTab_AchxOnlyMenuItemsAndHotkeysAreOff_UndoRedoSaveStayOn |
| A02 | png pane refuses Document-scoped hotkeys | caught | OnAPngTab_DocumentMenuItemsAreDisabled_AndComeBackOnTheDocumentTab |
| A03 | seqj Delete routes to the timeline selection | caught | DeleteKey_RemovesTheSelectedChain_NotTheHiddenAchxSelection |
| A04 | png Delete leaves the hidden document alone | survived (equivalent) | PNG pane refuses Document scope, so Delete never reaches it (A02 guards the real rule) |
| A05 | menu scoping: Save/Undo/Redo/History are Document-scoped | caught | OnASequenceTab_AchxOnlyMenuItemsAndHotkeysAreOff_UndoRedoSaveStayOn |
| A06 | menu scoping: achx items disabled off an achx tab | caught | OnASequenceTab_AchxOnlyMenuItemsAndHotkeysAreOff_UndoRedoSaveStayOn |
| A07 | hotkey dispatch honours the pane's scope | caught | OnAPngTab_Undo_LeavesTheHiddenDocumentAlone |
| A08 | Undo/Redo menu items disabled when the pane has no document | caught | OnAPngTab_DocumentMenuItemsAreDisabled_AndComeBackOnTheDocumentTab |
| A09 | Delete hotkey is Document scope | caught | DeleteKey_RemovesTheSelectedChain_NotTheHiddenAchxSelection |
| A10 | Go to Definition error path shows the banner | caught | GoToDefinition_MissingSourceOrChain_ShowsTheBanner_AndOpensNothing |
| A11 | collapsed sources persisted per file | caught | Collapsed_SurvivesAddingASecondSource_AndUndoOfTheAdd_AndMakesNoUndo.. |
| A12 | collapsed entry dropped when none collapsed | caught | Collapsed_IsWrittenToSettingsPerFile_AndForgottenOnceTheSourceIsRemo.. |
| A13 | loop toggle sets the sequence flag | caught | LoopToggle_SetsTheSequencesLoopFlagAsOneUndoStep |
| A14 | inline sequence rename shows refusal banner | caught | RenamingASequenceToATakenName_ShowsTheBanner_EscapeAndTheMenuStillWork |
| A15 | one undo entry per gesture, not per pointer move | caught | DraggingAChain_MovesItInTimeAsOneUndoStep_AndCtrlZPutsItBack |
| A16 | Escape cancels the drag | caught | Escape_DuringADrag_PutsTheChainBackWithNoUndoEntry |
| A17 | a finished drag commits one undo entry | caught | DraggingAChain_MovesItInTimeAsOneUndoStep_AndCtrlZPutsItBack |
| A18 | double-click a chain = Go to Definition | caught | GoToDefinition_DoubleClickAndMenu_ReachTheSourceToo_AndTheSequenceTa.. |
| A19 | switching sequence clears the timeline selection | UNPINNED -> pinned | SequenceAuditScenarioTests.ArrowingToAnotherSequence_ClearsTheTimelineSelection_... |
| A20 | selection dropped when its chain disappears | UNPINNED -> pinned | SequenceAuditScenarioTests.UndoingTheDropOfASelectedChain_DropsItsSelection |
| A21 | clicking empty timeline space clears the selection | caught | InvalidValues_AreRefusedWithABanner_AndTheFieldGoesBack |
| A22 | ruler press scrubs | caught | OutsideItsChain_ATracksSpriteIsHidden |
| A23 | dragging a chain to another lane moves it there | caught | DraggingAChainToAnotherLane_MovesItToThatTrack_AndCtrlZPutsItBack |
| A24 | Delete removes the selected track | caught | DeleteKey_WithATrackSelected_DeletesTheTrack_AndCtrlZBringsItBack |
| A25 | track header menu Delete Track | caught | TrackHeaderMenu_RenamesAndDeletesATrack_EachUndoable |
| A26 | playback tick is clamped after a stall | UNPINNED -> pinned | SequenceClockTests.AdvanceTick_AfterALongStall_MovesNoFurtherThanTheCap (clamp extracted into `SequenceClock.AdvanceTick`) |
| A27 | leaving the tab stops playback | survived (self-correcting) | a detached sequence has length 0, so the first tick stops the clock |
| A28 | corrupt seqj reports and does not open | UNPINNED -> pinned | SequenceAuditScenarioTests.OpeningACorruptSeqj_ShowsTheBanner_AndOpensNoTab |
| A29 | selecting another sequence stops playback | UNPINNED -> pinned | SequenceAuditScenarioTests.SwitchingToAnotherSequence_StopsPlayback_AtZero |
| A30 | edit shortening the sequence reseeks the playhead | UNPINNED -> pinned | SequenceAuditScenarioTests.AnEditThatShortensTheSequence_PullsThePlayheadBackToItsEnd |
| A31 | selecting brings the inspector forward unless History/Shortcuts | caught | ClickingAnotherSequence_ShowsItsFields_AndBringsTheInspectorForward_.. |
| A32 | AddSequence selects the new sequence | caught | AddSequence_SavesTheFile_AndCtrlZTakesItBack |
| A33 | AddSources refusals reach the banner | caught | DroppingFiles_AddsTheAchxAsOneUndoStep_RefusesTheSeqj_AndOpensNoTab |
| A34 | RemoveSource refusal reaches the banner | UNPINNED -> pinned | SequenceAuditScenarioTests.RemovingASourceAChainStillUses_IsRefusedWithTheBanner |
| A35 | closing a seqj tab forgets its document | UNPINNED -> pinned | SequenceAuditScenarioTests.ClosingAndReopeningASeqjTab_ReadsTheFileFromDiskAgain |
| A36 | library: missing source header disables Go to Definition | caught | AMissingSourcesHeader_DisablesGoToDefinition_ButKeepsRemove |
| A37 | library: collapsing hides chips | caught | ChevronAndTitle_CollapseAndExpandASource_KeepingItsHeader |
| A38 | library: a drag that never crossed the threshold is not a drag | survived (equivalent) | a sub-threshold wiggle ends outside the timeline, so nothing drops |
| N01 | nav: Ctrl/Alt chords are not navigation input | UNPINNED -> pinned | NavigationAuditScenarioTests.DuplicateThenUndoAndRedo_... / IsNavigationInputKey_... |
| N02 | nav: Undo records no history | caught | Undo_RestoringASelection_RecordsNothing |
| N03 | nav: Redo records no history | UNPINNED -> pinned | NavigationAuditScenarioTests.DuplicateThenUndoAndRedo_RecordNothing_... |
| N04 | nav: mouse Back button = back | caught | -1) |
| N05 | nav: Back/Forward do not record themselves | caught | MouseBackAndForward_AfterGoToDefinition_ReturnToTheChainOnEachTab |
| N06 | nav: a closed tab whose file is gone is skipped | caught | Back_ToAClosedTabWhoseFileIsGone_IsSkipped |
| N07 | nav: a deleted/renamed chain is not a place to return to | caught | Undo_RestoringASelection_RecordsNothing |
| N08 | nav: a removed sequence is not a place to return to | UNPINNED -> pinned | NavigationAuditScenarioTests.Back_PastASequenceThatWasUndoneAway_SkipsIt |
| N09 | nav: restore re-selects the frame | caught | ArrowingThroughFrames_IsOneEntry_ThenTheNextChainIsAnother |
| N10 | nav: restore re-selects the timeline chain/track | caught | SequenceTimelineClicks_RecordTrackAndChainSelections |
| N11 | nav: capture includes the selected timeline chain | caught | SequenceTimelineClicks_RecordTrackAndChainSelections |
| N12 | nav: pointer release is a user action | survived (redundant) | pointer press already opens the action; only programmatic paths differ |
| N13 | nav: tab switch is a user action | survived (redundant) | same |
| N14 | nav: closing the active tab is a history entry | survived (redundant) | same |
| N15 | nav: Back/Forward menu enablement | caught | Undo_RestoringASelection_RecordsNothing |
| N16 | nav: Go to Definition is a history entry | survived (redundant) | same |
| N17 | nav: opening a file is a history entry | caught | OpeningAFile_RecordsTheTabItLeft |
| V01 | folder menu: groups separated by a divider | caught | FolderMenu_SeparatesNewItemsFromConvertAndReveal |
| V02 | folder menu: convert checked against the right-clicked folder | caught | FolderMenu_NoAchxAnywhereUnderIt_HidesConvertAndItsSeparator |
| V03 | folder menu: convert checked against the right-clicked folder (dogfood) | caught | FolderMenu_HoldingOnlyASequence_OffersNoConvert |
| V04 | folder menu: separators (dogfood) | caught | FolderMenu_HoldingOnlyASequence_OffersNoConvert |
| A39 | collapsing a source makes no undo entry | caught | ChevronAndTitle_CollapseAndExpandASource_KeepingItsHeader |
| N18 | nav: lone modifier keys are not navigation input | UNPINNED -> pinned | NavigationAuditScenarioTests.IsNavigationInputKey_CountsPlainKeys_ButNotChordsOrLoneModifiers |
