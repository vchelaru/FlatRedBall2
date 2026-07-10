# Decision: tab-strip visual polish + context menus (#654, Phase 11)

Status: **Shipped.** Verified live in a real Chrome tab: active/inactive tab styling, disabled
close button on the sole tab, tab context menu (including a real "Open in New Browser Tab" that
opened and loaded a fresh instance cleanly), and "Copy Full Path" on the header filename all
confirmed working with zero console errors.

## Scope shipped

1. **Border-based tab styling**, matching desktop's `TabStrip` exactly: `BgActive` background for
   the active tab (transparent otherwise), 1px `LineBrush` right border, `Ink`/`InkMid` label
   foreground for active/inactive.
2. **Close button** (✕) on every tab, disabled when it's the only open tab (mirrors desktop's
   `TabBarBorder.IsVisible = tabs.Count > 1` intent — the browser keeps the strip visible always,
   but disables the one action that would leave zero tabs open via that specific control).
3. **Tab context menu**: "Open in New Browser Tab" (see remap decision below) + "Close Tab"
   (disabled under the same single-tab condition as the close button).
4. **Middle-click to close**, matching desktop.
5. **`CloseTab`**, mirroring desktop's exact fallback shape: closes via `TabManager.Close` (already
   unit-tested at the Core level in `TabManagerTests.cs` — next-tab/previous-tab/null-if-empty
   logic isn't new here), then either activates whatever `TabManager` picked as the next tab, or —
   if `ActiveTab` becomes `null` (last tab closed) — resets to a blank `AnimationChainListSave`,
   same as desktop's "all tabs closed, start fresh" branch.
6. **Header filename context menu** (Phase 8's branded header bar): "Copy Full Path" via
   `IClipboard`. "Open Containing Folder" is dropped — no filesystem to open.

**Explicitly not built:** drag-to-reorder tabs. Desktop's version is real ceremony (pointer
capture, a floating ghost-label overlay drawn on a `Canvas`, drop-index computation) that the
roadmap's Phase 11 scope — "visual polish + context menus" — doesn't call for. Left for a future
phase if reordering turns out to matter in practice.

## Remap decision: "Detach to New Window" → "Open in New Browser Tab"

Desktop's `Detach to New Window` re-opens the file in a fresh native window — no browser
equivalent (there's no OS-level "detach this pane" concept for a web page). Implemented as
`Avalonia.Platform.Storage.ILauncher.LaunchUriAsync(new Uri(Program.PageUrl))`, obtained via
`TopLevel.GetTopLevel(control).Launcher` — confirmed via Avalonia's own XML docs
(`ILauncher.LaunchUriAsync(System.Uri)` in `Avalonia.Base.xml`) rather than decompiling, then
verified by trying it and letting the compiler confirm the exact access path (`TopLevel.Launcher`
compiled clean on the first attempt).

`Program.PageUrl` is captured once from `args[0]` (`location.href`, already passed in for
`HttpClient.BaseAddress`) and reused here — this reopens the same app URL as a genuinely fresh
instance (its own WASM runtime, its own `BuildView()`, loading the bundled sample from scratch),
**not true state transfer**. Verified live: right-clicking the tab, clicking the menu item, opened
a new Chrome tab that loaded and rendered the full app correctly with zero console errors.

**Open judgment call, not resolved here** (per the roadmap): whether the new tab should inherit
`localStorage` theme state or start fully fresh. It currently inherits — `localStorage` is shared
across tabs of the same origin by default, and nothing in this phase special-cases that. Revisit
if it turns out to matter.

## Verification

- [x] `dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx` — 0 warnings/errors
- [x] `dotnet test` on all 3 suites — Core 1584, Views 29, App 679 — unchanged, all green (no
      Core/Views changes this phase; `TabManager.Close` was already tested before this phase)
- [x] `dotnet publish -c Release` for `AnimationEditor.Browser.csproj` — exit 0
- [x] Live Chrome: tab shows `BgActive` styling with a dimmed/disabled close button (single-tab
      state); right-click context menu shows "Open in New Browser Tab" (enabled) and "Close Tab"
      (disabled); clicking "Open in New Browser Tab" opened and cleanly loaded a fresh instance in
      a new tab, zero console errors; "Copy Full Path" on the header filename fired with zero
      console errors
- [ ] Actual tab-*close* interaction (closing one of 2+ tabs) — not exercisable in this session
      without a second tab, which requires the native folder picker (same category of residual gap
      as Phase 1's "Open Folder needs a human", already documented). `CloseTab`'s own logic is
      verified by construction: it's the identical pattern to desktop's already-working
      `CloseTab`, built on `TabManager.Close`, which already has direct Core-level test coverage.
