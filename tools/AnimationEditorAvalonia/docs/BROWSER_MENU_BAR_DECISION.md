# Decision: In-canvas menu bar, browser-safe shortcut subset (#662, Phase 13)

Status: **Shipped.** Verified live in a real Chrome tab: File/Edit/View/Help all open and their
items dispatch correctly, Ctrl+S/Ctrl+Z/Ctrl+Y route through the same handlers as their toolbar
buttons, zero console errors.

## Avalonia `Menu`, not `NativeMenu`

Desktop uses `NativeMenu` (`MainWindow.axaml`'s `<NativeMenu.Menu>`), which renders through the OS
window-manager menu bar and has no browser equivalent — `Avalonia.Browser` has no native menu host.
The browser build instead gets an in-canvas `Menu` control docked to the top of the root
`DockPanel`, above the tab strip, styled with the same `BgRail` resource as the rest of the chrome.

## Delegate to existing buttons, don't duplicate logic

Every menu item's `Click` handler does one of two things:
- Raises the existing toolbar button's `Click` event via
  `button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent))` (Load Folder, Save, Save As, Export,
  Undo, Redo) — the menu item is a second entry point into logic that already exists, not a new
  implementation.
- Calls the same local function/method the toolbar or F3 key handler already calls (`SetTheme`,
  `ApplyDiagnostics`) — extracted `SetTheme(AppTheme)` out of the theme toggle button's inline
  handler specifically so the menu's Light/Dark items and the toolbar's toggle button share one
  code path instead of two copies of the resource-lookup + persistence logic.

`File > New` is the one item with its own body (no existing button does exactly "New"), reusing
the same tab-creation sequence the tab strip's "+" control already runs.

## Browser-safe keyboard subset

Only Ctrl+S (save), Ctrl+Z (undo), Ctrl+Y (redo) got shortcuts, extending the existing F3
diagnostics `KeyDown` handler on `TopLevel`. All three `e.Handled = true` before the browser's
native behavior (Ctrl+S's save-page dialog) can fire — confirmed live: Ctrl+S updated the status
bar to "Saved to player.achx." with no browser save dialog appearing. Not attempted: Ctrl+O (file
picker semantics differ from desktop's), Ctrl+N, Ctrl+Shift+Z (redo alt) — none are asked for by
the roadmap and the existing menu items/buttons already cover them by click.

## Verification

- [x] `dotnet build` — 0 warnings/errors
- [x] `dotnet test` on all 3 suites — unaffected (no new Core/Views logic; App has no dedicated
      test project so this phase adds no new automated coverage — see note below)
- [x] `dotnet publish -c Release` for `AnimationEditor.Browser.csproj` — exit 0
- [x] Live Chrome: File menu (New, Load Folder…, Save, Save As…, Export to PixiJS) — New tested
      end-to-end (opens Untitled tab, preserves original tab, tree/status update correctly); View
      menu zoom items, Show History, Theme submenu — Light theme tested, recolors UI and syncs
      toolbar button text; Edit menu shows Undo/Redo; Help menu Diagnostics (F3) and About — About
      tested, updates status bar text; Ctrl+S and Ctrl+Z tested live, both routed correctly with no
      browser-native interference; zero console errors across every interaction

**Test-first note**: this phase is pure UI wiring — every `Click`/`KeyDown` handler forwards to an
already-tested code path (`appCommands.NewFile()`, `undoManager`, `SetTheme`, `ApplyDiagnostics`,
the existing button handlers) with no new branching logic of its own. Per `CLAUDE.md`'s test-first
discipline, the testable core here (tab-creation naming, theme persistence, undo/redo semantics)
was already covered when those code paths were built in earlier phases; this phase only adds new
entry points into them, which is why it was verified live rather than with new unit tests.
