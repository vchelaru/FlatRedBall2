# AnimationEditor test audit 5 (#1681, #1689, #1690)

Snapshot, 2026-11-07. Same method as `plan/ae-test-audit-4.md`: break one production line, run only the test classes that should catch it (`NativeMenu*` for the menu, the navigation and sequence classes for the rest), record whether one failed. Mutations ran against main at ea5bbb4a. Not maintained: re-run before trusting it later.

## Verdict

- 92 mutations. Production: 60 on `NativeMenuBuilder`/`CreateNativeMenu` (#1689), 10 on the Back/Forward menu wiring (#1681), 5 on the Cycles label (#1690). Test side: 17 on the parity helper itself.
- Per target (audited / caught / pinned / equivalent): menu mirror 60 / 31 / 24 / 5, Back/Forward 10 / 10 / 0 / 0, Cycles label 5 / 3 / 2 / 0, parity helper 17 / 16 / 1 / 0. 27 survivors were unpinned and now have a test that fails against the same mutation (RED against the mutation, GREEN on the real code).
- #1681's own production code (`NativeMenuActions` Back/Forward members, the hand-built View items) was deleted by #1689 hours later, so its behaviors were audited through what replaced them: Back/Forward enabled state, gestures from the hotkey registry, Click direction, app scope. All 10 were already caught.
- Riskiest gaps found: the parity test never looked at a native item's command, so a leaf with no command, a submenu with one, or a command that can run while the in-window item is disabled all passed (US9, plus the new helper checks); nothing followed a later header, gesture, toggle-type or checked change (IM2, IM4 to IM6); the empty-to-filled and filled-to-empty switch of Open Recent (action item vs submenu) was untested (IM7, US3, US5, US8); a rebuild that left the old native items subscribed (a leak) was invisible to every test (MM3, MM4, MM6 to MM8, ID1 to ID3, US6); a rebuild that did not clear the native list was masked by `Single()` lookups (MM2); `ToMacGesture` was only run on gestures with no extra modifier (G4); the checkmark resync after the system flips its own was untested (AC10); activating a non-toggle item flipping its checkmark was untested (AC6a); the Cycles label's culture independence was untested (S3, S4).
- The parity helper had a dead branch: `NativeMenuItemSeparator` derives from `NativeMenuItem`, so "native item is a separator" never fired (the drift still showed as a header difference). Fixed.
- **Bug found and fixed:** a recent file whose name has an underscore (`my_walk.achx`) showed as `mywalk.achx` in Open Recent, in the window and now in the macOS bar (the old hand-built bar showed it right). `RefreshRecentFiles` set the raw file name as a header, and a header treats `_x` as an access key. It now doubles underscores. Test: `OpenRecent_ShowsAFileNameWithAnUnderscoreVerbatim` (RED before, GREEN after).
- **Issue filed, not fixed:** #1692, a window made by Detach to New Window never gets a native menu (`RegisterNativeMenu` runs only for the first window, and `MainWindow` hides the in-window menu on macOS). Not verified on a Mac. Already filed elsewhere: #1688.

## Behaviors audited

### #1689 `NativeMenuBuilder` (mirror of the in-window menu)

| Id | Behavior broken | Result | Test |
|---|---|---|---|
| HT1 | HeaderText: access-key underscores kept | caught | NativeMenuParityScenarioTests |
| HT2 | HeaderText: access-key letter dropped with the underscore | caught | NativeMenuParityScenarioTests |
| HT3 | HeaderText: non-string header fallback dropped | survived (equivalent) | every in-window header is a string |
| G1 | ToMacGesture: no gesture becomes a gesture | caught | NativeBar_HasAnItemForEveryInWindowMenuItem |
| G2 | ToMacGesture: Ctrl never moved to Cmd | caught | EditGestures_ComeFromTheHotkeyRegistry |
| G3 | ToMacGesture: Ctrl kept alongside Cmd | caught | EditGestures_ComeFromTheHotkeyRegistry |
| G4 | ToMacGesture: other modifiers dropped | UNPINNED -> pinned | ToMacGesture_MovesCtrlToCmd_AndKeepsTheKeyAndEveryOtherModifier (no in-window gesture mixes Ctrl with Shift or Alt) |
| G5 | ToMacGesture: Ctrl becomes Shift | caught | EditGestures_ComeFromTheHotkeyRegistry |
| G7 | ToMacGesture: any modifier moved to Cmd (Alt too) | caught | ViewMenu_HasBackAndForward |
| G8 | ToMacGesture: key replaced | caught | EditGestures_ComeFromTheHotkeyRegistry |
| AC1 | Command can always execute | caught | BackAndForward_FollowTheHistory |
| AC2 | Command can never execute | caught | BackAndForward_FollowTheHistory |
| AC3 | RaiseCanExecuteChanged does nothing | survived (equivalent) | see below |
| AC4 | CopyState does not raise CanExecuteChanged | survived (equivalent) | see below |
| AC5 | Activate: disabled guard removed | caught | OnAPngTab_NativeMenuUndo_LeavesTheHiddenDocumentAlone |
| AC6a | Activate: flips the checkmark of a non-toggle item | UNPINNED -> pinned | ActivatingAnItemWithoutACheckmark_LeavesItsCheckedStateAlone |
| AC6b | Activate: never flips a toggle | caught | EnableHotReload_ShowsAndTogglesItsCheckedState |
| AC7 | Activate: flips after Click instead of before | caught | EnableHotReload_ShowsAndTogglesItsCheckedState |
| AC8 | Activate: flip is a no-op | caught | EnableHotReload_ShowsAndTogglesItsCheckedState |
| AC9 | Activate: Click not raised | caught | 9 tests |
| AC10 | Activate: native checkmark not resynced after Click | UNPINNED -> pinned | AfterActivating_TheNativeCheckmarkMatchesTheInWindowOne_EvenIfTheSystemFlippedItMeanwhile |
| MM1 | MenuMirror: source CollectionChanged not subscribed | caught | NewInWindowItemAddedLater_AppearsInTheNativeBar |
| MM2 | Rebuild: native items not cleared | UNPINNED -> pinned | AddingAndRemovingInWindowItems_LeavesNoStaleOrDuplicateNativeItems (the old test found the new item with `Single()`, which a duplicated list still satisfies) |
| MM3 | Rebuild: old child mirrors not disposed | UNPINNED -> pinned | RebuildingAMenu_ReleasesTheNativeItemsItDropped |
| MM4 | Rebuild: child mirror not tracked for disposal | UNPINNED -> pinned | ASubmenuThatEmpties_ReleasesTheItemsItDropped |
| MM5 | Rebuild: separators dropped | caught | NativeBar_HasAnItemForEveryInWindowMenuItem |
| MM6 | MenuMirror.Dispose: CollectionChanged not unsubscribed | UNPINNED -> pinned | RebuildingAMenu_ReleasesTheNestedItemsOfTheSubmenusItDropped |
| MM7 | MenuMirror.Dispose: children not disposed | UNPINNED -> pinned | same |
| MM8 | DisposeChildren: list not cleared | UNPINNED -> pinned | RebuildingAMenu_ReleasesTheNativeItemsItDropped |
| IM1 | ItemMirror: PropertyChanged not subscribed | caught | UndoAndCopy_AreGreyedOutLikeTheInWindowMenu |
| IM2 | Header change not followed | UNPINNED -> pinned | AHeaderChange_IsFollowed_WithItsAccessKeyUnderscoreDropped |
| IM3 | IsEnabled change not followed | caught | UndoAndCopy_AreGreyedOutLikeTheInWindowMenu |
| IM4 | IsChecked change not followed | UNPINNED -> pinned | TheDiagnosticsCheckmark_FollowsTheF3Hotkey (the old toggle test passed because Activate resyncs the checkmark itself) |
| IM5 | InputGesture change not followed | UNPINNED -> pinned | AGestureChange_IsFollowed_OnTheMacCommandKey |
| IM6 | ToggleType change not followed | UNPINNED -> pinned | AToggleTypeChange_IsFollowed |
| IM7 | child collection changes not followed (empty to filled) | UNPINNED -> pinned | OpenRecent_WhenItEmptiesAndFillsAgain_SwitchesBetweenAnActionAndASubmenu |
| IM8 | CopyState before UpdateSubmenu | survived (equivalent) | see below |
| IM9 | CopyState skipped in the constructor | caught | 19 tests |
| CS1 | CopyState: header not copied | caught | 19 tests |
| CS2 | CopyState: enabled not copied | UNPINNED -> pinned (parents only) | ADisabledSubmenuItem_IsGreyedInTheBar; for a leaf the command's CanExecute carries the state, so it stays equivalent |
| CS3 | CopyState: gesture not copied | caught | ViewMenu_HasBackAndForward |
| CS4 | CopyState: toggle type not copied | caught | EnableHotReload_ShowsAndTogglesItsCheckedState |
| CS5 | CopyState: checked not copied | caught | EnableHotReload_ShowsAndTogglesItsCheckedState |
| CS6 | CopyState: gesture copied without the Mac mapping | caught | EditGestures_ComeFromTheHotkeyRegistry |
| US1 | single-child menu is not a submenu | caught | NativeBar_HasAnItemForEveryInWindowMenuItem (Open Recent with one file) |
| US2 | native submenu recreated on every child change | UNPINNED -> pinned | AddingAChildToASubmenu_KeepsTheSameNativeSubmenu_AndItStillForwardsOpeningOnce |
| US3 | command not cleared when a submenu appears | UNPINNED -> pinned | OpenRecent_WhenItEmptiesAndFillsAgain_SwitchesBetweenAnActionAndASubmenu |
| US4 | Opening not subscribed | caught | OpeningTheFileMenu_RefreshesCloseProjectFolderEnabled |
| US5 | Opening not unsubscribed when the children go | UNPINNED -> pinned | same as US3 |
| US6 | submenu mirror not disposed when the children go | UNPINNED -> pinned | ASubmenuThatEmpties_ReleasesTheItemsItDropped (also checks the old native menu is released) |
| US7 | `_submenu` not nulled when the children go | caught | OpenRecent_FollowsTheRecentFilesAfterTheBarIsBuilt |
| US8 | native Menu not cleared when the children go | UNPINNED -> pinned | same as US3 |
| US9 | command also set on submenu parents | UNPINNED -> pinned | the parity helper now reports a submenu with a command (29 tests fail against the mutation) |
| US10 | command never set | caught | 10 tests |
| OS1 | opening raises Click instead of SubmenuOpened | survived (equivalent) | see below |
| OS2 | opening raises nothing | caught | OpeningTheFileMenu_RefreshesCloseProjectFolderEnabled |
| ID1 | ItemMirror.Dispose: PropertyChanged not unsubscribed | UNPINNED -> pinned | the three release tests |
| ID2 | ItemMirror.Dispose: Items.CollectionChanged not unsubscribed | UNPINNED -> pinned | same |
| ID3 | ItemMirror.Dispose: submenu not disposed | UNPINNED -> pinned | RebuildingAMenu_ReleasesTheNestedItemsOfTheSubmenusItDropped |
| W1 | CreateNativeMenu builds from an empty menu | caught | 19 tests |

### #1681 Back/Forward through the current wiring

| Id | Behavior broken | Result | Test |
|---|---|---|---|
| NV1 | in-window Back click navigates forward | caught | BackAndForwardCommands_Navigate |
| NV2 | in-window Forward click navigates back | caught | BackAndForwardCommands_Navigate |
| NV3 | Back enabled follows CanGoForward | caught | 15 tests |
| NV4 | Forward enabled follows CanGoBack | caught | 5 tests |
| NV5 | enabled state not refreshed on a history change | caught | 7 tests |
| NV6 | enabled state not initialised | caught | 4 tests |
| NV8 | Back gesture takes Forward's hotkey | caught | ViewMenu_HasBackAndForward |
| NV9 | Forward gesture takes Back's hotkey | caught | ViewMenu_HasBackAndForward |
| NV10 | Back/Forward not app-scoped (disabled on a PNG or sequence tab) | caught | 4 tests |
| NV11 | menu gestures never applied | caught | ViewMenu_HasBackAndForward |

NV7 (the refresh in the `finally` of `NavigateHistoryAsync`) was not mutated.

### #1690 "Set Cycles of" in undo and History text

| Id | Behavior broken | Result | Test |
|---|---|---|---|
| S1 | Core label reverts to Loops | caught | EditingAChain_StartAndLoops_AreUndoable |
| S2 | edge-drag label reverts to Loops | caught | DraggingTheRightEdge_SnapsToCyclesAndTheNextChain |
| S3 | edge-drag label number uses the current culture | UNPINNED -> pinned | DraggingTheRightEdge_NamesTheCycles_WithADotWhateverTheCulture (de-DE set on the UI thread, which is not the test's thread) |
| S4 | Core label number uses the current culture | UNPINNED -> pinned | SetChainLoops_NamesTheCycles_WithADotWhateverTheCulture (Core.Tests) |
| S5 | edge-drag label names Start | caught | DraggingTheRightEdge_SnapsToCyclesAndTheNextChain |

Nothing else in `src` still says "Loops" in user-visible text (`grep`); the inspector label and the refusal text were audited in `plan/sequence-test-audit-3.md` (A10, D01).

### The parity helper (`NativeMenuParity`), by mutating it

Each drift kind is now injected into a built bar and the helper must report it; each helper check was then removed to prove its drift test notices.

| Id | Helper check removed | Result | Test |
|---|---|---|---|
| P01 | header | caught | ParityCheck_ReportsAWrongHeader |
| P02 | enabled | caught | ParityCheck_ReportsAWrongEnabledState |
| P03 | toggle type | caught | ParityCheck_ReportsAWrongToggleType |
| P04 | checked | caught | ParityCheck_ReportsAWrongCheckedState |
| P05 | gesture | caught | ParityCheck_ReportsAGestureThatDriftedFromTheInWindowMenu (existing) |
| P06 | missing submenu | caught | ParityCheck_ReportsAMissingSubmenu |
| P07 | no recursion into submenus | caught | 13 tests |
| P08 | extra native item | caught | ParityCheck_ReportsAnExtraNativeItem_InASubmenuAndOnTheBar |
| P09 | missing native item at the end | UNPINNED -> pinned | ParityCheck_ReportsAnItemMissingFromTheEndOfAMenu (the old test removed Save mid-list, which shows up as header differences) |
| P10 | separator where an item belongs | caught | ParityCheck_ReportsASeparatorWhereAnItemBelongs_... (the branch was dead before; fixed) |
| P11 | item where a separator belongs | caught | same |
| P12 | stops at the shorter list | caught | ParityCheck_ReportsAnExtraNativeItem_... |
| P13 | submenu with a command (new check) | caught | ParityCheck_ReportsALeafWithoutACommand_ASubmenuWithOne_AndALeafWithASubmenu |
| P14 | leaf with a submenu (new check) | caught | same |
| P15 | leaf without a command (new check) | caught | same |
| P16 | command CanExecute differs from enabled (new check) | caught | ParityCheck_ReportsACommandThatCanExecuteWhileTheInWindowItemIsDisabled |
| P17 | oracle keeps access-key underscores | caught | 29 tests |

The built bar is also compared after the active tab changes kind (achx to PNG to sequence and back) and for a window with no tab, so the enabled state of the scoped items is checked in every state, not only the achx one.

## Equivalent, and what cannot be staged headlessly

- **AC3, AC4, CS2 (leaves), IM8:** a leaf's enabled state is carried twice: `Native.IsEnabled` is set directly in `CopyState`, and `ActivateCommand.CanExecute` reads the same flag. Dropping either path alone changes nothing a test can see (dropping both, or making CanExecute constant, is caught: AC1, AC2, CS2 on a parent). IM8 (order of `UpdateSubmenu` and `CopyState`) matters only if CanExecute differed from the flag.
- **HT3:** every in-window header is a string.
- **OS1:** raising Click on an in-window parent item makes Avalonia open its submenu, which raises `SubmenuOpened` itself, and the parent's own Click handlers do not see the event. Leaves never open a submenu. So the mutation cannot be told apart for any item that has a submenu, and only those forward an opening.
- **Not testable headlessly:** the AppKit menu bar itself; key-equivalent dispatch (Cmd+Z reaching the command); AppKit flipping a checkmark (simulated in AC10's test) and raising `Opening` (raised by reflection, `NativeMenuTestSupport.RaiseOpening`); `App.RegisterNativeMenu` and `NativeMenu.SetMenu` on the real window (needs the desktop lifetime; `CreateNativeMenu` is covered by W1); a native menu on a detached window (#1692). The Mac head's use of Alt+Left and Alt+Right as key equivalents (Option+arrow is word navigation in text fields on a Mac) could not be judged here.
- Leak tests use `WeakReference` plus `GC.Collect`, with the lookup in a `NoInlining` helper so no local keeps the item alive. They are the only way to see a dropped item still being updated by the in-window one.
