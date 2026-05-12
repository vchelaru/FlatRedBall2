# Per-Frame Shapes (Hitboxes / Hurtboxes)

Animation frames can carry shape definitions (`AARect`, `Circle`, `Polygon`) which the engine reconciles onto the parent entity as the animation plays. The intended use is hitboxes and hurtboxes that come and go with specific frames of an attack, parry, or i-frame window.

## The ownership rule (load-bearing — read this before authoring)

A shape **name** is *owned* by an `AnimationChainList` iff that name appears in any `AnimationFrame.Shapes` entry of any `AnimationChain` in that chainlist. Ownership is at the **chainlist** level — not per-chain, not per-frame.

Each frame, the engine reconciles the chainlist's owned set:

- Listed in this frame → shape is enabled (`IsVisible = true`, registered for default collision) and values are applied.
- In the owned set but **not** listed in this frame → shape is hidden (`IsVisible = false`, removed from default collision). The instance persists for reuse; it isn't destroyed.

Shapes whose names are not in *any* chainlist's owned set are **never touched**. Body colliders are safe by absence — adding animation to an existing entity does not silently strip the body, no flag required.

## Naming rules

- Names are required and non-empty. An empty name throws at load time (.achx) or apply time (code-built).
- Names must be unique within a single frame.
- The match key is **name only**. If a frame says shape `"Sword"` is a Polygon and the entity already has a `"Sword"` rectangle, that's a type mismatch — throws at apply time.

## AutoCreateShapes

Default `true` on `AnimationChainList`. When a frame names a shape that doesn't yet exist on the entity, the engine instantiates it and attaches it. This is the ergonomic code path — no need to pre-declare every hitbox in `CustomInitialize`.

Set `chainList.AutoCreateShapes = false` to make missing shapes a runtime error instead. Useful when you want typo detection.

## Cross-chain switching

Within one chainlist, switching chains (`Attack` → `Idle` in `Combat.achx`) Just Works: `Idle`'s frames don't list `Sword`, the chainlist still owns `Sword`, so reconciliation hides it on the first frame of `Idle`.

## Cross-chainlist switching (deliberate non-cleanup)

Each chainlist's ownership is scoped to itself. Switching from `Combat.achx` (owns `Sword`) to a `Movement.achx` (owns nothing) leaves `Sword` in whatever state it was last left in — `Movement.achx` does not own it. **Keep related chains in one .achx file** to avoid this. The escape hatch (a `HideOwnedShapes()`-style API) is deferred until a real use case appears.

## Collision-relationship wiring

Animation-created shapes are **not** auto-registered with any collision relationship. Look the shape up by name on the entity (or its `Children`) after the chain has played at least once, and add it to your `CollisionRelationship` like any other shape. Predictable, matches how user-added shapes work.

## Code authoring shape data

Per-frame shape entries are typed:

- `AnimationAARectFrame` — `Name`, `RelativeX/Y`, `Width`, `Height`
- `AnimationCircleFrame` — `Name`, `RelativeX/Y`, `Radius`
- `AnimationPolygonFrame` — `Name`, `RelativeX/Y`, `Points` (Vector2 array)

Add them to `AnimationFrame.Shapes`. The shapes themselves on the entity are the regular `AARect` / `Circle` / `Polygon` types — the `Animation*Frame` types are just per-frame data carriers.

## .achx authoring

Frame shapes round-trip via the `<ShapesSave>` element on each `<Frame>` (`<AARectSaves>`, `<CircleSaves>`, `<PolygonSaves>`). Rectangle dimensions in the .achx use FRB1's half-extent `ScaleX`/`ScaleY` and are doubled into `Width`/`Height` at load time. See `achx-authoring.md` for the surrounding schema.
