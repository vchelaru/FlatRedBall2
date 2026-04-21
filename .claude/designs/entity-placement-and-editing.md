# Entity Placement & Editing — Design Debate

Summary of a discussion on how FRB2 should let users define entity visuals and place entity instances into levels. No decision reached; this document captures the tradeoffs so we can pick up later.

## Background

FRB1 had a full in-game editor: the game itself was hijacked to become the editor, so entities, animations, and particles all rendered live with real assets. Powerful, but brittle and maintenance-heavy, and still not as capable as dedicated tools like Gum for focused editing.

FRB2 currently uses **Tiled** for level data, including designer-placed spawn markers via `TileMap.CreateEntities<T>` (see `design/TODOS.md` — Designer-Placed Spawn Markers, landed). This is stable and low-maintenance, but limiting.

## Concrete Pain Points With Tiled Today

1. **No "entity" concept.** Everything mixes into tilesets; finding things is awkward.
2. **Fixed tile size.** Can't place an arbitrary-sized visual to represent a real-world entity.
3. **Origin pain.** Already had to work around tile origin when creating entities from tiles.
4. **No entity editor.** Can't draw a collision rect on an entity, compose one entity out of multiple sprites, or define visual hierarchy.
5. **Custom rules/validation.** Tiled has a JS scripting API but it's obscure; in practice we don't use it.
6. **Visual drift.** When an entity's in-game look changes, Tiled's representation doesn't update — placeholders diverge from reality.

Partial mitigations worth noting before committing to a tool switch:
- Tiled tilesets support per-tile alignment (top-left, center, bottom-center). Pushing origin into the tileset definition may address #3 without code workarounds.
- Tiled Classes on tiles address some of #1, though not fully.

## The Four Options Considered

### 1. FRB1 game-as-editor (revive)
Highest fidelity. Real animations, real sizes, custom validation, cross-entity references — all live.
- **Cost:** The exact maintenance nightmare FRB2 was built to escape. Rebuilding a scene editor is a years-long sustained investment (Unity/Godot/Unreal all have one, and each is huge).
- **Verdict:** Not seriously on the table.

### 2. Tiled + targeted gap-fills (status quo plus)
Keep Tiled. Solve the real gaps with the smallest possible additions:
- CLI validator that runs over TMX + entity reflection at build time
- Startup-time sanity checks that throw with clear messages
- `--spawn-at=<marker>` launch flag for fast iteration
- Investigate Tiled tile alignment for origin (#3)

- **Cost:** Cheapest. Doesn't fix #1, #2, #4, #6 — those are Tiled-shaped limits, not missing features.
- **Verdict:** Viable if the pain is tolerable. Underpowered for the long-term vision.

### 3. Gum as entity editor + Tiled/LDtk for level placement (split-tool architecture)
Gum already does multi-sprite visual composition, hierarchy, states, and hot-reload. That's exactly the entity-definition half.

- **Gum = entity definition** (what a Turret looks like: multi-part composition, states, animations)
- **Tiled/LDtk = level placement** (where Turrets go; per-instance typed fields like `patrolRadius=100`)

Level editor only needs a placeholder icon per entity type — full WYSIWYG composed visuals were never realistic in a level editor without building one ourselves.

- **What Gum would need:** An "entity definition" concept distinct from a UI component, OR accept that Gum owns the visual and entity fields (`Health`, `Worth`) live in code. Plus exposing pivot/origin and collision rect alongside the visual. Additive, not a rewrite.
- **Cost:** Two tools in the pipeline (two importers, two hot-reload paths, two sets of docs). Requires sustained Gum tool work alongside its UI duties.
- **Verdict:** Strongest on fidelity-per-dollar if Gum can carry the entity-definition role without drifting from its UI focus.

### 4. LDtk (replace Tiled)
LDtk is a dedicated 2D level editor designed around the entity concept.

| # | Pain | LDtk |
|---|------|------|
| 1 | No "entity" concept | Entities are first-class with typed fields (int, float, enum, entity-ref, tile, color, array) |
| 2 | Fixed-size representations | Entity has its own width/height; visual is a tile, colored rect, or cropped tileset tile |
| 3 | Origin pain | Pivot is a first-class property on entity definitions (0–1 in both axes) |
| 4 | Multi-sprite composition | ❌ Still one visual per entity. Scene-editor territory. |
| 5 | Custom validation | ❌ No in-editor scripting. Same ceiling as Tiled in practice — validate externally. |
| 6 | Visual drift | ✅ Better — entity visual references a tileset tile, so PNG updates flow through |

LDtk directly fixes 1, 2, 3, 6. Lateral on 5. Does **not** fix 4 — multi-sprite composition stays a scene-editor problem, which is what makes option 3 (Gum) attractive for that specific pain.

- **Cost:** Smaller ecosystem than Tiled; single-maintainer project; JSON format evolves and must be pinned; migration throws away current Tiled integration work (though see below).
- **Key unknown:** MonoGame.Extended's Tiled loader **reportedly** also supports LDtk. If true, tile/collision layer work transfers "for free" and only the entity layer needs custom integration. Worth verifying — "supposedly" is doing a lot of work in that sentence.
- **Verdict:** The right middle ground if #4 is not a hard requirement.

## The Real Architectural Question

The options reduce to **how far we push each pain**:

- #4 (multi-sprite composition) is **scene-editor territory**. Only Gum (option 3) or FRB1-revival (option 1) address it in-editor. If it's not a firm requirement, the field narrows to options 2 and 4.
- #5 (custom validation) is **unsolved by every tool** in practice. Best answered outside the editor regardless of choice — CLI validator or startup checks.
- #1, #2, #3, #6 are the cluster LDtk directly addresses. If these are the dominant pains (and arguably they are), LDtk alone may be the cheapest answer.

## Recommended Next Step — Two Afternoons of Hands-On

Stop debating, start validating. Two small experiments in parallel:

1. **Verify LDtk loads via MonoGame.Extended.** Half an afternoon. Load a toy LDtk file, render a tile layer, confirm collision-layer-equivalent works. If yes, migration cost drops meaningfully.
2. **Mock up a "Turret" in Gum** with a pivot and a collision rect alongside its visual. Does it feel natural, or are we fighting the tool? Answers whether option 3 is real or a trap.

Outcomes:
- Both work → option 3 (Gum + LDtk split) is viable.
- Only LDtk works → option 4 alone, accept #4 stays unsolved.
- Neither works cleanly → option 2, Tiled plus gap-fills.

Decisions like this are cheaper with two hours of hands-on than two more weeks of discussion.

## Open Questions

- Is #4 (multi-sprite composed entities) a near-term need, or speculative?
- How many current Tiled-based samples would need migration if we switch?
- Does Gum's maintainer (effectively the same person as FRB2's) have bandwidth to evolve Gum toward the entity-definition role?
- If we split Gum + level-editor, does visual drift between Gum compositions and the level-editor's placeholder icon create a worse problem than today's Tiled drift?
