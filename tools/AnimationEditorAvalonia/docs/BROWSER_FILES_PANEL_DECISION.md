# Decision: Files panel, "This File" scope (#655, Phase 12)

Status: **Shipped.** TDD'd new `AnimationEditor.Views` control, wired as the sidebar's third tab.
Verified live in a real Chrome tab: the bundled sample's referenced texture shows with a correct
thumbnail, selected/theme styling matches the other two tabs, zero console errors.

## Rebuild, not port

Desktop's `FilesPanelControl` needs a real `Window` (for its file dialogs) and a real disk folder
scan (its "Project" scope walks the `.achx`'s folder via `PngFolderScanner`) — neither survives the
browser. Same reasoning `BROWSER_TREE_INSPECTOR_DECISION.md` used for tree/inspector in Phase 1:
**rebuild**, don't try to port.

Scoped to desktop's **"This File"** mode specifically (added post-Phase-5 in issue #615), not a
cumulative session-wide texture dump. `AnimationEditor.Core.Data.TextureListBuilder
.GetAvailableTextures(AnimationChainListSave?)` already returns exactly the active tab's
referenced texture names — pure, sorted, de-duplicated, zero disk access, zero new Core code
needed. Paired with the already-existing `ThumbnailService.GetFullImageThumbnail` for previews.

## New control: `TextureListPanel`

`AnimationEditor.Views/Controls/TextureListPanel.axaml(.cs)`. Public surface:

- `InitializeServices(AnimationChainListSave? acls, ThumbnailService thumbnailService)` — call once
- `SetAnimationChainList(AnimationChainListSave? acls)` — call again for every subsequent tab/file
  change
- `TextureNames` (`IReadOnlyList<string>`) and `EmptyLabel`/`TextureItems` — test-visible surface,
  same `x:FieldModifier="Public"` convention as `InspectorControl`

**No single event fires for "the loaded file changed"** the way
`ISelectedState.SelectionChanged` does for shape selection, so `SetAnimationChainList` is re-pushed
explicitly at the same points the browser already refreshes `AnimationTreeControl`: `CloseTab`,
`SwitchToTab`, the Open Folder and drag-drop load handlers, and `ApplicationEvents
.AnimationChainsChanged` (covers a new/deleted frame changing which textures are referenced,
without a tab switch). One real wiring snag hit while doing this: `CloseTab`/`SwitchToTab` are
local functions declared *earlier* in `BuildView` than the natural place to construct
`textureListPanel` (next to the other sidebar tabs) — C# local functions can capture a
later-declared local, but only once it's definitely assigned before any possible invocation, so the
compiler flagged `CS0841`/`CS0165` until the declaration + `InitializeServices` call moved up next
to `animationTree`'s own initialization (the `TabItem`/UI construction stayed in its original spot,
just referencing the already-initialized variable).

## TDD

`TextureListPanelTests.cs` written and confirmed failing to compile (`TextureListPanel` didn't
exist yet) before implementing: empty-state (no ACLS), sorted-and-deduplicated texture list, and
`SetAnimationChainList` correctly replacing the list on a simulated tab switch (including back to
`null`, restoring the empty state). 4 new tests, all passing after implementation; 29 pre-existing
Views tests unaffected.

## Verification

- [x] `dotnet build tools/AnimationEditorAvalonia/AnimationEditorAvalonia.slnx` — 0 warnings/errors
- [x] `dotnet test` on all 3 suites — Core 1584, Views 33 (4 new), App 679 — all green
- [x] `dotnet publish -c Release` for `AnimationEditor.Browser.csproj` — exit 0
- [x] Live Chrome: Files tab shows "player.png" with a correctly-rendered color thumbnail; tab
      selection/foreground styling matches Inspector/History; theme toggle recolors it correctly;
      zero console errors across load, tab-click, and theme-toggle
