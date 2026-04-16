# Platformer Config JSON — In-Flight Design Notes

This file captures the in-progress design of the unified platformer config JSON (animations + coefficients, bundled). Delete when the feature lands.

See also: `TODOS.md` section "Platformer Config JSON (Animations + Coefficients, Bundled)".

---

## Motivation

- Externalize both platformer animation state→chain mappings AND `PlatformerValues` coefficients into one JSON file per platformer entity.
- Canonical application of the `content-boundary` skill: coefficients and mappings are human-tunable; they don't belong in C#.
- Replaces two previously-separate TODOs ("Platformer Animation Support" and "JSON-Driven PlatformerValues") with a single bundled work item.

## Settled decisions

### Schema shape (one file per platformer entity)

```json
{
  "leftSuffix": "Left",
  "rightSuffix": "Right",

  "movement": {
    "ground": { "MaxSpeedX": 160, "Gravity": 1500, "minJumpHeight": 48 },
    "air":    { "MaxSpeedX": 100, "Gravity": 1500 }
  },

  "animations": {
    "states": {
      "Idle": "Idle",
      "Walk": "Walk",
      "Jump": "Jump",
      "Fall": "Fall"
    }
  }
}
```

- All three top-level sections (`suffixes`, `movement`, `animations`) are **optional** — a file can provide any subset.
- Single loader entry point, provisionally `PlatformerConfig.FromJson(path)`.

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

### Facing suffix convention

- Defaults: `leftSuffix: "Left"`, `rightSuffix: "Right"` — matches FRB1 editor output.
- Rationale: users won't be migrating FRB1 projects, but they likely will use FRB1's editor, which emits Left/Right-suffixed chains.
- Either suffix can be set to `""` if the user authored a different convention.
- `SlopesSample` currently uses `"Left"`-only with base-name-is-right. Will need to update when feature lands — either rename chains or set `rightSuffix: ""`.

### Animation selection approach: Option 3 (hybrid)

Three options were weighed:

1. String expressions in JSON (parser required, ~150 lines)
2. Enum-only predefined states (no parser, but custom states require engine change)
3. **Built-in enum + user-registered C# predicates** — chosen

In option 3:
- Engine ships predicates for a small set of built-in states; user's JSON maps them to chain names.
- Custom states (`WallSlide`, `DoubleJump`, etc.) are added via `controller.RegisterState(name, priority, predicate)` in game code, then mapped in JSON.
- Rationale for option 3 over option 1: most custom states coincide with new mechanics that need C# anyway, so forcing a predicate into C# isn't a content-boundary violation. Dodges the parser.

### Built-in animation states (minimum)

Ship predicates for these four only:

| State | Predicate (tentative) |
|-------|-----------------------|
| `Idle` | `IsOnGround && VelocityX == 0` |
| `Walk` | `IsOnGround && VelocityX != 0` |
| `Jump` | `!IsOnGround && VelocityY > 0` |
| `Fall` | `!IsOnGround && VelocityY <= 0` |

`Run`, `Land`, `Duck` are **not** built-in — documented as registration recipes in the skill file so users can copy-paste. Keeps the controller lean and avoids coupling it to input systems, thresholds, or frame counters.

### Priority system: flat int priorities with named constants

Not layered (FRB1 had "AnimationLayers" but — pending confirmation from user — they appeared to be a Glue editor grouping, not a runtime semantic difference).

```csharp
public static class BuiltInPriorities
{
    public const int Idle = 100;
    public const int Walk = 200;
    public const int Jump = 300;  // Jump and Fall share; predicates are mutually exclusive
    public const int Fall = 300;
}
```

- Gaps of 100 leave room to inject: `WallSlide` at `Fall + 10`, `Sprint` at `Walk + 10`, etc.
- Per frame: evaluate all registered predicates, pick highest-priority match whose mapping exists in the JSON.
- Tiebreaker: registration order (stable sort).

## Open questions / not yet decided

1. **FRB1 AnimationLayers semantics** — did layers do anything at runtime beyond priority ordering? Specifically, could layers run *in parallel* (e.g., body layer + weapon layer both playing on different sprites)? If yes, that's a separate parallel-blending feature — probably out of scope for this pass, but worth confirming. **Waiting on user.**

2. **Can `RegisterState` override built-in predicates?** E.g., user calls `RegisterState("Walk", ...)` with their own predicate. Tentatively yes (last registration wins), but not explicitly decided.

3. **Hot-reload support** for the JSON — nice-to-have, not in scope for v1.

4. **Where the skill file lives** — new skill (`platformer-config`?) or a section added to existing `platformer-movement` / `animation` skills. Deferred until closer to landing.

## Implementation order (once open questions resolve)

1. DTO + nullable-field coefficient types
2. `PlatformerValues` JSON loader + resolver (derived-vs-raw jump modes)
3. Animation section loader
4. `PlatformerAnimationController` (built-in states + registration API + priority evaluation)
5. Graduate `AddLeftFacingVariants` helper out of `SlopesSample` (generalize to mirror any direction)
6. SlopesSample integration — retire hand-rolled `UpdateFacingChain`; this is the end-to-end validation
7. Skill file

## Tasks

Active tasks (7-14) in the task list map to the sections above. Task #7 (condition strategy) is resolved — option 3 chosen.

## Key source references

- `src/Movement/PlatformerValues.cs:18` — existing `SetJumpHeights(minHeight, maxHeight?)` function
- `src/Movement/PlatformerBehavior.cs:14` — `GroundMovement` / `AirMovement` fixed slots
- `samples/SlopesSample/Player.cs:94-129` — current hand-rolled facing convention (Left-suffix only) and `AddLeftFacingVariants` helper to graduate
- `.claude/skills/content-boundary/SKILL.md` — the philosophy this feature embodies
