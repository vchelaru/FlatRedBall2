# Platformer Config JSON — Design Notes

Design record for the platformer config JSON (coefficients). Coefficients are landed; this file preserves the rationale and settled decisions.

See also: `TODOS.md` section "PlatformerConfig JSON — Coefficients (Landed)".

---

## Motivation

- Externalize `PlatformerValues` coefficients into a JSON file per platformer entity.
- Canonical application of the `content-boundary` skill: coefficients are human-tunable; they don't belong in C#.

## Settled decisions

### Animation — not engine-managed

FRB1's `AnimationController` / `PlatformerAnimationController` mapped behavior states to animation chains via a layered priority system. **FRB2 does not port this.** The controller was primarily solving an FRB1/Glue problem (generated code coexisting with custom code). Without a code generator, the abstraction adds indirection for no benefit — the equivalent if-statement or pattern match is shorter, more readable, and directly debuggable.

The engine focuses on excellent primitives instead:
- `PlayAnimation` is idempotent (calling with the same chain doesn't restart)
- `PlatformerBehavior.DirectionFacing` exposes facing for suffix logic
- `AnimationFinished` event handles non-looping transitions

See the `platformer-movement` skill for the recommended animation pattern.

### Schema shape (one file per platformer entity)

```json
{
  "movement": {
    "ground": { "MaxSpeedX": 160, "Gravity": 1500, "minJumpHeight": 48 },
    "air":    { "MaxSpeedX": 100, "Gravity": 1500 }
  }
}
```

- Single loader entry point: `PlatformerConfig.FromJson(path)`.

### Movement slots are fixed, not user-named profiles

Movement slot names are **hardcoded** and map 1:1 to `PlatformerBehavior` fields:

- `movement.ground` → `PlatformerBehavior.GroundMovement`
- `movement.air` → `PlatformerBehavior.AirMovement`
- `movement.afterDoubleJump` → the not-yet-wired double-jump slot (FRB1 had this)

This was initially designed as "arbitrary user-named profiles (Walk/Run/Ice)" — corrected when user pointed out FRB2 already has the fixed-slot model (`GroundMovement`/`AirMovement`) and FRB1 used Ground/Jump/AfterDoubleJump. If a game wants "ice physics," it swaps the whole `PlatformerValues` assignment in game code — not an engine-level concern.

### Coefficient DTO is nullable-field

- Each movement slot deserializes into a DTO where every field is nullable.
- Omitted fields fall back to `PlatformerValues` **struct defaults** (not base-profile inheritance — simpler, and inheritance loses value when there are only three fixed slots).

### Jump config: two mutually-exclusive input modes

Per movement slot:

- **Derived (preferred, intuitive):** `minJumpHeight` + optional `maxJumpHeight` → loader calls `PlatformerValues.SetJumpHeights(minHeight, maxHeight)`. `Gravity` must be resolved first (ordering constraint).
- **Raw (escape hatch):** `JumpVelocity` + `JumpApplyLength` + `JumpApplyByButtonHold` direct-set.
- Loader **errors** if both modes are specified in the same slot.

See `src/Movement/PlatformerValues.cs:18` for the existing `SetJumpHeights` function.

## Resolved questions

- ~~**Hot-reload support**~~ — promoted to its own TODO in `TODOS.md` ("PlatformerConfig Hot-Reload (File Watch)", Priority: Soon).

## Implementation status

1. ~~DTO + nullable-field coefficient types~~ — **Done.** `MovementSlot` in `src/Movement/PlatformerConfig.cs`.
2. ~~`PlatformerValues` JSON loader + resolver (derived-vs-raw jump modes)~~ — **Done.** `PlatformerConfig.FromJson` / `FromJsonString` + `PlatformerConfigExtensions.ApplyTo`. SlopesSample and AutoEvalCoinHopperSample converted. Template at `.claude/templates/PlatformerConfig/player.platformer.json`.

Animation controller, animation JSON section, and RegisterState API were **intentionally dropped** — see "Animation — not engine-managed" above.

## Key source references

- `src/Movement/PlatformerConfig.cs` — DTOs (`PlatformerConfig`, `PlatformerMovementConfig`, `MovementSlot`) and `FromJson`/`FromJsonString` loader
- `src/Movement/PlatformerConfigExtensions.cs` — `ApplyTo` extension method (config → behavior wiring)
- `src/Movement/PlatformerValues.cs:18` — existing `SetJumpHeights(minHeight, maxHeight?)` function
- `src/Movement/PlatformerBehavior.cs:14` — `GroundMovement` / `AirMovement` fixed slots
- `.claude/templates/PlatformerConfig/player.platformer.json` — commented template for new platformer entities
- `samples/SlopesSample/Player.cs` — converted to JSON; has hand-rolled `UpdateFacingChain` and `AddLeftFacingVariants` (this is the expected user-code animation pattern)
- `.claude/skills/content-boundary/SKILL.md` — the philosophy this feature embodies
