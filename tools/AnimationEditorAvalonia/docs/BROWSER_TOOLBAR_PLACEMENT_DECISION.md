# Browser toolbar placement parity

**Issue:** PR #647 follow-up — user screenshot showed three stacked global toolbar rows
below the menu bar; desktop distributes the same controls across context-specific rails.

**Decision:** Remove the global `toolbar` / `editToolbar` / `viewToolbar` rows from
`App.axaml.cs` and place controls where `MainWindow.axaml` puts them.

## Mapping (before → after)

| Control | Was | Now (desktop parity) |
|---|---|---|
| Open Folder, Save, Export, Theme | Top file row | File / View menus only |
| Add Animation | Edit toolbar | Footer below animations tree |
| Add Frame / Rect / Circle / Delete | Edit toolbar | Edit menu (+ tree context menus from Phase 9) |
| Undo / Redo | Edit toolbar | History tab header (icon buttons) + Edit menu |
| Move / Magic Wand | Edit toolbar | Wireframe toolbar pill |
| Grid + size | View toolbar | Wireframe toolbar |
| Wireframe zoom | View toolbar | Wireframe toolbar |
| Onion Skin / Guides / Interpolate | View toolbar | Preview toolbar |
| Preview zoom | View toolbar | Preview toolbar |
| Diagnostics | View toolbar | Help menu only |
| Reload changed textures | File toolbar (hidden) | Edit menu (enabled when pending) |

## Layout shape

- **Canvas column:** `RowDefinitions="Auto,*,4,*"` — wireframe toolbar → wireframe → splitter → preview block.
- **Preview block:** `RowDefinitions="Auto,*,52"` — preview toolbar → canvas → timeline/transport.
- **Animations panel:** tree `*` + `+ Add Animation` footer with top border.
- **History tab:** 30px undo/redo header rail + scrollable list (matches desktop `HistoryTab`).

## Test plan

Browser-only wiring — no new unit tests (established convention for `App.axaml.cs` glue).
Verify in live Chrome:

1. No global toolbar rows under the tab strip.
2. Wireframe toolbar above wireframe canvas; preview toolbar above preview canvas.
3. `+ Add Animation` at bottom of left animations panel.
4. Undo/Redo icons in History tab header; Edit menu items work.
5. Grid toggle/size and zoom controls affect the correct viewport.
6. File/Edit/View/Help menus still reach all former toolbar actions.
