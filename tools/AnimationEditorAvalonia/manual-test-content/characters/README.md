# Documentation-screenshot fixture: `characters.png`

A real, good-looking sprite sheet for the `animation-editor-screenshots` skill to load
into scenarios, instead of a synthetically-generated solid-color placeholder square.

`characters.png` — 736×128, a 32×32 grid (23 columns × 4 rows):

- Row 0 (y=0): bald/monk character, full-width walk cycle
- Row 1 (y=32): knight character, full-width walk cycle
- Row 2 (y=64): rogue/ninja character, full-width walk cycle
- Row 3 (y=96): small slime — only the first ~3 columns are populated, the rest of the row is empty

**Every frame built from this sheet must crop to exactly one 32×32 cell (e.g. `(0,0,32,32)` for the first monk frame) unless a scenario explicitly calls for a different crop.** Don't crop mid-cell or span multiple cells by default — a short run of consecutive whole cells in a row makes a walk-cycle chain.
