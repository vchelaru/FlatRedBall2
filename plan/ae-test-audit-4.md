# AnimationEditor test audit 4 (#1678, #1684, #1685, #1679)

Snapshot, 2026-11-07. Same method as `plan/sequence-test-audit-2.md` and `plan/sequence-test-audit-3.md`: break one production line, run only the test classes that should catch it, record whether one failed. Mutations ran against main at 95055386. Not maintained: re-run before trusting it later.

## Verdict

- 147 behaviors audited by mutation. 102 failed a test. 45 survived: 23 were unpinned and now have a test that fails against the same mutation (RED against the mutation, GREEN on the real code), 22 are equivalent, redundant or cannot be staged headlessly.
- Per PR (audited / caught / pinned / left): #1678 50 / 39 / 9 / 2, #1684 37 / 32 / 5 / 0, #1685 22 / 12 / 5 / 5, #1679 38 / 19 / 4 / 15.
- Riskiest gaps found: the shipped "Flip Diagonally" menu-click assertion ran over a chain with no frames, so it passed for any action (D02); the Duplicate submenu's Horizontal and Vertical items were never clicked (D09, D10); no naming test had a source whose frames already carry the diagonal flag, so four terms of the direction matrix were unpinned (N15, N17, N20); the inspector's circle, polygon and texture name boxes were never checked to commit on a tab switch (P07 to P09); pressing inside the open rename box was never checked to leave it open (P15); the tree rename's place in the left tab's undo history was never checked for the open-another-file path (P20, T19); the commit that Undo, Redo and Back do for the macOS system menu, which raises no window press, was untested (T13 to T15); the diagonal chain flip's undo label, `FramesFlipped` pose and tree and wireframe refreshes were untested (A19, A20, B05, B06); a native tsx project refusing a flipped duplicate still renamed it was untested (C11); the Flip Diagonal icon was only checked to exist, not to be the diagonal one (E03).
- Bugs found: none. The diagonal-flip direction mapping agrees with `FlipScaleCalculator.ComputeMatrix` for every combination of H, V and D toggles on unflipped, H, D and H+D sources (the new theory cases were derived from a separate calculation of the display matrices, not from the code under test).

## Behaviors audited

Strength: **outcome** = a test asserts the observable result.

### #1678 Flip Diagonal in the chain menu and Duplicate (`ChainFlip`, `FlipChainDiagonally`, `FlipCommand.FlipFrame`)

| Id | Behavior broken | Result | Test |
|---|---|---|---|
| A01 | FlipFrame H: FlipHorizontal not toggled | caught | FlipTransitionTests |
| A02 | FlipFrame H: RelativeX not negated | caught | FlipTransitionTests |
| A03 | FlipFrame V: FlipVertical not toggled | caught | AppCommandsChainTests |
| A04 | FlipFrame V: RelativeY not negated | caught | AppCommandsChainTests |
| A05 | FlipFrame D: FlipDiagonal not toggled | caught | AppCommandsChainTests |
| A06 | FlipFrame D: negate rule uses & instead of ^ | caught | AppCommandsChainTests |
| A07 | FlipFrame D: negate rule never negates | caught | AppCommandsChainTests |
| A08 | FlipFrame D: negate rule always negates | caught | AppCommandsChainTests |
| A09 | FlipFrame D: negated swap loses a sign | caught | AppCommandsChainTests |
| A10 | FlipFrame D: plain swap does not swap | caught | AppCommandsChainTests |
| A11 | FlipFrame shapes: diagonal uses Mirror instead of Transpose | caught | AppCommandsChainTests |
| A12 | FlipFrame shapes: Transpose gets H/V arguments swapped | caught | AppCommandsChainTests |
| A13 | FlipFrame shapes: Mirror horizontal flag wrong | caught | AppCommandsChainTests |
| A15 | FlipFrame shapes: Mirror skipped for non-diagonal | caught | AppCommandsChainTests |
| A16 | FlipFrame shapes: Transpose skipped for diagonal | caught | AppCommandsChainTests |
| A17 | Toggle: FlipFrame always uses Horizontal (axis ignored) | caught | AppCommandsChainTests |
| A18 | Toggle: FramesFlipped 'before' pose taken after the flip | caught | FlipTransitionTests |
| A19 | Description: diagonal label changed | UNPINNED -> pinned | FlipChainDiagonally_IsOneUndoEntry_NamedForTheAxisAndTheFrameCount |
| A20 | PoseOf: FlipDiagonal not recorded | UNPINNED -> pinned | FlipChainDiagonally_RaisesFramesFlipped_WithTheDiagonalFlagInBothPoses |
| B01 | FlipChainDiagonally: locked check removed | caught | AppCommandsChainTests |
| B02 | FlipChainDiagonally: native-tsx check removed | caught | AppCommandsTsxAchxOnlyEditsTests |
| B03 | FlipChainDiagonally: flips Horizontal axis | caught | AppCommandsChainTests |
| B04 | FlipChainDiagonally: only first frame | caught | AppCommandsChainTests |
| B05 | FlipChainDiagonally: tree node not refreshed | UNPINNED -> pinned | FlippingAChain_RefreshesItsTreeNode_AndTheWireframe (also pins H and V) |
| B06 | FlipChainDiagonally: wireframe not refreshed | UNPINNED -> pinned | same |
| C01 | Clone: Horizontal skipped | caught | AppCommandsChainTests |
| C02 | Clone: Vertical skipped | caught | AppCommandsChainTests |
| C03 | Clone: Diagonal skipped | caught | AppCommandsChainTests |
| C04 | Clone: Horizontal applied after Diagonal (order H,V,D -> V,D,H) | survived (equivalent) | the baked offset is a function of the final flags (final matrix times the inverse of the starting one), so toggling in a different order ends in the same data |
| C05 | Clone: Vertical applied after Diagonal (order H,V,D -> H,D,V) | survived (equivalent) | same |
| C06 | Clone: Vertical flag tests Diagonal bit | caught | AppCommandsChainTests |
| C07 | Clone: Diagonal branch applies Vertical | caught | AppCommandsChainTests |
| C08 | Clone: flips the source instead of the copy | caught | ChainFlipNamingTests |
| C09 | DuplicateChains: native-tsx flip not cleared | caught | AppCommandsTsxAchxOnlyEditsTests |
| C10 | DuplicateChains: native-tsx clears only Horizontal | caught | AppCommandsTsxAchxOnlyEditsTests |
| C11 | DuplicateChains: name uses a Horizontal flip when the refused flip is None (native tsx would rename) | UNPINNED -> pinned | FlippingTheCopyOfANativeTsxChain_IsRefused_SoItsDirectionWordsStayAsTheyWere |
| D01 | Menu: Flip Diagonally item omitted | caught | TreeMenuPlanBuilderTests |
| D02 | Menu: Flip Diagonally calls FlipChainVertically | UNPINNED -> pinned | TheFlipDiagonallyMenuItem_FlipsEveryFrameOfTheChain_AndOnlyDiagonally (the shipped click assertion ran over a chain with no frames, so it passed for any action) |
| D03 | Menu: Flip Diagonally icon is FlipVertical | caught | TreeMenuPlanBuilderTests |
| D04 | Menu: Flip Diagonally label changed | caught | TreeMenuPlanBuilderTests |
| D05 | Menu: Flip Diagonally shown on native tsx / multi-select (outside the guard) | caught | TreeMenuPlanBuilderTests |
| D06 | Menu: Duplicate > Flip Diagonal passes Horizontal | caught | TreeMenuPlanBuilderTests |
| D07 | Menu: Duplicate > Flip Diagonal passes Horizontal|Diagonal | caught | TreeMenuPlanBuilderTests |
| D08 | Menu: Duplicate > Flip Diagonal icon is FlipHorizontal | caught | TreeMenuPlanBuilderTests |
| D09 | Menu: Duplicate > Flip Vertical passes Diagonal | UNPINNED -> pinned | TheDuplicateSubmenu_EachItemRequestsOnlyItsOwnAxis_AndOriginalRequestsNone |
| D10 | Menu: Duplicate > Flip Horizontal passes Vertical | UNPINNED -> pinned | same |
| D11 | Menu: Duplicate > Flip Diagonal label changed | caught | TreeMenuPlanBuilderTests |
| E01 | MainWindow: Duplicate flip wiring ignores the picked flip (passes Horizontal) | caught | ChainMenuScenarioTests |
| E02 | MainWindow: HandleDuplicateChainsFlip drops the flip | caught | ChainMenuScenarioTests |
| E03 | TreeMenuRenderer: FlipDiagonal icon maps to IconFlipH | UNPINNED -> pinned | IconAssetPath_EachFlipIcon_UsesItsOwnAxisIcon (the existing test only checks every icon file exists) |

A14 (the whole shape loop skipped) did not compile as a mutation (unreachable code is an error) and is covered by A11 to A16.

### #1684 direction-word swap for diagonal duplicates (`ChainFlipNaming`)

| Id | Behavior broken | Result | Test |
|---|---|---|---|
| N01 | Rename: Distinct removed (every frame counts as its own answer) | caught | ChainFlipNamingTests |
| N02 | Rename: no frames maps like a diagonal-less flip | caught | ChainFlipNamingTests |
| N03 | Rename: one distinct answer ignores the frames' flags | caught | ChainFlipNamingTests |
| N04 | Rename: mixed frames keep the diagonal | caught | ChainFlipNamingTests |
| N05 | Rename: mixed frames rename nothing | caught | ChainFlipNamingTests |
| N06 | Rename: mixed frames strip Horizontal instead of Diagonal | caught | ChainFlipNamingTests |
| N07 | Rename: mixed frames also strip Vertical | UNPINNED -> pinned | Duplicate_VerticalAndDiagonal_WhenFramesDisagree_FallsBackToTheVerticalRename |
| N08 | Rename: mixed frames also strip Horizontal | caught | ChainFlipNamingTests |
| N09 | TransformOf: before-matrix ignores the starting flags | caught | ChainFlipNamingTests |
| N10 | TransformOf: Horizontal toggle dropped | caught | ChainFlipNamingTests |
| N11 | TransformOf: Vertical toggle dropped | caught | ChainFlipNamingTests |
| N12 | TransformOf: Diagonal toggle dropped | caught | ChainFlipNamingTests |
| N13 | TransformOf: Vertical toggle flips h | caught | ChainFlipNamingTests |
| N14 | TransformOf: Horizontal toggle flips v | caught | ChainFlipNamingTests |
| N15 | Matrix a: second term subtracted | UNPINNED -> pinned | Duplicate_FromADiagonallyFlippedSource_RenamesByTheChangeTheToggleMakesToTheDisplay (no test had a source with FlipDiagonal set) |
| N16 | Matrix b: second term subtracted | caught | ChainFlipNamingTests |
| N17 | Matrix c: second term subtracted | UNPINNED -> pinned | same |
| N18 | Matrix d: second term subtracted | caught | ChainFlipNamingTests |
| N19 | Matrix a: first term subtracted | caught | ChainFlipNamingTests |
| N20 | Matrix d: first term subtracted | UNPINNED -> pinned | same |
| N21 | Direction: Down is (0,-1) | caught | ChainFlipNamingTests |
| N22 | Direction: Up is (0,1) | caught | ChainFlipNamingTests |
| N23 | Direction: Left is (1,0) | caught | ChainFlipNamingTests |
| N24 | Word: Right and Left swapped | caught | ChainFlipNamingTests |
| N25 | Word: Down and Up swapped | caught | ChainFlipNamingTests |
| N26 | Regex: lowercase guard removed | caught | ChainFlipNamingTests |
| N27 | Regex: Up dropped | caught | ChainFlipNamingTests |
| N28 | Regex: Down dropped | caught | ChainFlipNamingTests |
| N29 | Regex: Left dropped | caught | ChainFlipNamingTests |
| N30 | Regex: Right dropped | caught | ChainFlipNamingTests |
| N31 | Regex: ignores case | caught | ChainFlipNamingTests |
| N32 | Regex: a digit after the word blocks the match | UNPINNED -> pinned | Duplicate_ADirectionWordFollowedByADigit_IsStillRenamed |
| N33 | Regex: an uppercase letter after the word blocks the match | caught | ChainFlipNamingTests |
| N34 | Regex: the word must start the name or follow a non-letter | caught | ChainFlipNamingTests |
| N35 | AppCommands: Rename reads the copy's frames (already flipped) instead of the source's | caught | ChainFlipNamingTests |
| N36 | AppCommands: Rename gets no frames | caught | ChainFlipNamingTests |
| N37 | AppCommands: Rename ignores the flip | caught | ChainFlipNamingTests |

### #1685 ANIMATIONS tree and inspector rename commits on leave

| Id | Behavior broken | Result | Test |
|---|---|---|---|
| P01 | CommitPendingRename: tree commit not part of it | caught | AchxRenameLeaveScenarioTests |
| P02 | Window pointer handler: not registered | caught | AchxRenameLeaveScenarioTests |
| P03 | Window pointer handler: Bubble instead of Tunnel | survived (not stageable) | only differs when a press itself begins a rename (a label double-tap), which the harness cannot raise; see Not testable |
| P04 | Window pointer handler: skips handled presses | survived (equivalent) | the handler is a Tunnel handler on the window, the root of the tunnel, so nothing can have handled the press before it |
| P05 | Inspector commit: chain name box dropped | caught | AchxRenameLeaveScenarioTests |
| P06 | Inspector commit: rect name box dropped | caught | AchxRenameLeaveScenarioTests |
| P07 | Inspector commit: circle name box dropped | UNPINNED -> pinned | SwitchingTabs_WhileTypingTheInspectorCircleName_CommitsTheName |
| P08 | Inspector commit: polygon name box dropped | UNPINNED -> pinned | SwitchingTabs_WhileTypingTheInspectorPolygonName_CommitsTheName |
| P09 | Inspector commit: texture name box dropped | UNPINNED -> pinned | SwitchingTabs_WhileTypingTheInspectorTextureName_CommitsTheName |
| P10 | Inspector commit: rect box applies circle | caught | AchxRenameLeaveScenarioTests |
| P11 | Tree commit: inspector commit not run | caught | AchxRenameLeaveScenarioTests |
| P12 | Tree commit: open rename boxes not committed | caught | AchxRenameLeaveScenarioTests |
| P13 | EditingTreeNodes: children not searched | caught | AchxRenameLeaveScenarioTests |
| P14 | EditingTreeNodes: roots not searched | caught | AchxRenameLeaveScenarioTests |
| P15 | Pointer handler: a press inside the open rename box also commits | UNPINNED -> pinned | PressingInsideTheOpenRenameBox_KeepsTheRenameOpen |
| P16 | Pointer handler: commit runs synchronously, not posted | survived (not stageable) | same as P03: sync and posted differ only for a press that begins a rename |
| P17 | Pointer handler: posted commit ignores that the focus-loss commit already ran | survived (equivalent) | the second commit repeats the first: same name is a no-op, a refused name shows the same banner again, and the tree is not focusable so AnimTree.Focus() moves nothing |
| P18 | Pointer handler: re-reads the open boxes at post time | caught | AchxRenameLeaveScenarioTests |
| P19 | CaptureLeaving: tree commit dropped | caught | AchxRenameLeaveScenarioTests |
| P20 | CaptureLeaving: tree commit after the snapshot | UNPINNED -> pinned | LeavingByOpeningAFile_PutsTheRenameInTheLeftTabsHistory (the tab click path commits earlier on its own press, hiding the order) |
| P21 | Closing: tree commit dropped | survived (redundant) | closing the active tab goes through CaptureLeaving first, which commits; a hidden tab cannot hold an open rename |
| P22 | Commit hotkey path: inspector name not committed on a hotkey | caught | SequenceRenameLeaveScenarioTests |

P21b (the same line as P19) is left out of the count.

### #1679 sequence rename commit on leave

| Id | Behavior broken | Result | Test |
|---|---|---|---|
| T01 | Track box commit is applied at once, not deferred | caught | SequenceRenameLeaveScenarioTests |
| T02 | RenamingTrack always null | caught | SequenceRenameLeaveScenarioTests |
| T03 | CommitRename: inverted guard | caught | SequenceRenameLeaveScenarioTests |
| T04 | CommitTrackRename: always refocuses | survived (not stageable) | focus hand-off after a commit; no headless click target tells the focus owner apart (audit 2, RC1 and CP4) |
| T05 | CommitTrackRename: never refocuses | survived (not stageable) | same |
| T06 | CommitTrackRename.Apply: document identity guard dropped | survived (not stageable) | guards a document switch between a focus-loss press and its posted commit; the harness pumps the dispatcher after every press |
| T07 | CommitTrackRename.Apply: sequence index guard dropped | survived (not stageable) | guards a sequence deleted inside the same dispatcher turn as the press |
| T08 | CommitTrackRename.Apply: track index guard dropped | survived (not stageable) | guards a track deleted inside the same dispatcher turn as the press |
| T09 | CommitTrackRename.Apply: rename selects its sequence | caught | SequenceRenameLeaveScenarioTests |
| T10 | CommitTrackRename: deferred flag ignored (always at once) | caught | SequenceRenameLeaveScenarioTests |
| T11 | RenameTrack: keepSelection ignored (always selects) | caught | SequenceDocumentSelectionTests |
| T12 | RenameTrack: never selects | caught | SequenceDocumentSelectionTests |
| T13 | Undo: pending rename not committed | UNPINNED -> pinned | NativeMenuUndoAndRedo_WhileRenaming_CommitFirst (the in-window menu click commits through its own press; the macOS system menu raises none) |
| T14 | Redo: pending rename not committed | UNPINNED -> pinned | NativeMenuRedo_WhileRenaming_CommitsFirst_SoTheNewRenameReplacesTheRedo |
| T15 | Back/Forward: pending rename not committed | UNPINNED -> pinned | NativeMenuBack_WhileRenaming_CommitsTheRename |
| T16 | Window closing: pending rename not committed | caught | SequenceRenameLeaveScenarioTests |
| T17 | Hotkey: pending rename not committed | caught | SequenceRenameLeaveScenarioTests |
| T18 | Sequence CaptureLeaving: commit dropped | caught | SequenceRenameLeaveScenarioTests |
| T19 | Sequence CaptureLeaving: commit after the snapshot | UNPINNED -> pinned | LeavingByOpeningAFile_PutsTheRenameInTheLeftTabsHistory (sequence) |
| T20 | Sequence Closing: commit dropped | survived (redundant) | same as P21 |
| T21 | Window pointer handler not registered | caught | SequenceRenameLeaveScenarioTests |
| T22 | ApplySequenceRename: document identity guard dropped | survived (not stageable) | same as T06 |
| T23 | ApplySequenceRename: index guard dropped | survived (not stageable) | guards a row removed inside the same dispatcher turn as the press |
| T24 | HasPendingRename: ignores sequence rows | caught | SequenceRenameLeaveScenarioTests |
| T25 | HasPendingRename: ignores the track box | caught | SequenceRenameLeaveScenarioTests |
| T26 | CommitPendingRename: track rename not committed | caught | SequenceRenameLeaveScenarioTests |
| T27 | CommitPendingRename: list rows not committed | caught | SequenceRenameLeaveScenarioTests |
| T28 | CommitListRename: box not abandoned first | survived (equivalent) | the second commit repeats the first: same name is a no-op, a refused name shows the same banner again |
| T29 | CommitListRename: item still editing after the commit | survived (equivalent) | the next press finds the row still marked editing and clears it in the no-box branch; the box itself is already closed (ARefusedName_OnLeavingByCtrlS passes on both) |
| T30 | CommitListRename: no-box branch leaves the row editing | survived (not stageable) | the row's container is virtualized away; needs a list long enough to scroll the renamed row out |
| T31 | Pointer handler: ignores which pane is visible | survived (equivalent) | the Achx pane never has an open sequence rename while it is visible: leaving commits |
| T32 | Pointer handler: a press inside the box also commits | caught | SequenceRenameLeaveScenarioTests |
| T33 | Pointer handler: no pending check | survived (equivalent) | only skips a posted lambda that finds nothing to do |
| T34 | Pointer handler: list commit ignores that the box closed meanwhile | survived (equivalent) | a closed row falls through to the no-box branch, which only clears the flag |
| T35 | Pointer handler: track commit ignores which track was open | survived (not stageable) | needs the posted commit to run after a later press began another track rename; the harness pumps the dispatcher after every press (DoubleClickingAnotherTrackHeader passes on both) |
| T36 | Pointer handler: track commit dropped | caught | SequenceRenameLeaveScenarioTests |
| T37 | Row box commit applied at once, not posted | caught | SequenceRenameClickAwayScenarioTests |
| T38 | Row box commit: keepSelection dropped | caught | SequenceRenameClickAwayScenarioTests |

Edge cases checked with a test that passed on first run (no mutation needed, they pin the rule): double-clicking another track header while a track rename is open commits the first and opens the second (`DoubleClickingAnotherTrackHeader_WhileRenamingATrack_CommitsTheFirst_AndOpensTheSecond`); a refused sequence name committed by Ctrl+S shows the banner and closes the box (`ARefusedName_OnLeavingByCtrlS_ShowsTheBanner_AndClosesTheRenameBox`); a press on an inspector box while a tree rename is open commits it and the box keeps the focus (`PressingAnInspectorBox_WhileRenamingInTheTree_CommitsTheRename_AndTheBoxKeepsTheFocus`).

## Equivalent, redundant or not stageable

C04, C05, P04, P17, P21, T20, T28, T29, T31, T33, T34 are equivalent or redundant; the tables say why. P03, P16, T04 to T08, T22, T23, T30, T35 cannot be staged headlessly.

## Not testable headlessly

| Surface | Why | Extracted core |
|---|---|---|
| A press that itself begins a rename (label double-tap) racing the window press handler (P03, P16) | text labels are not hit-testable, so the harness cannot raise the label double-tap | none |
| The posted commit running after a *later* press or a document change (T06, T22, T35) | the harness pumps the dispatcher after every press, so a posted callback never straddles two presses | the guards are one-line comparisons |
| Index guards for a sequence, row or track removed inside one dispatcher turn (T07, T08, T23) | no input removes one between a press and its posted callback | none |
| Focus hand-off after a rename commit (T04, T05) | no headless click target tells the focus owner apart (audit 2, RC1 and CP4) | the selection rules are tested |
| Rename of a row scrolled out of the list (T30) | needs a virtualized list long enough to recycle the container | none |
| The macOS system menu itself | AppKit; the actions it calls are tested through `CreateNativeMenuActions()` | `NativeMenuBuilder` |

## Finding left alone

Order of the toggles in `CloneChainWithFlip` does not matter (C04, C05) because the stored offsets are a function of the final flags; the comment "the same as pressing each flip button in turn" and `ChainFlip`'s remark that axes apply in Horizontal, Vertical, Diagonal order describe an order that has no observable effect. Left as is.
