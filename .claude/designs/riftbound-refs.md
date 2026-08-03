# Riftbound — Engine Reference & Implementation Guide

> **Companion docs:** [Design Document](riftbound-design.md) | [Implementation Plan](riftbound-tasks.md)

This document bridges the Riftbound design and task plan to the FlatRedBall2 engine. It maps every game system to concrete engine APIs, flags stubs/gaps, and provides the implementation path agents should follow.

**Read this before writing any Riftbound code.**

---

## Skills to Load Per Task

Not every skill is needed for every task. Load only what's relevant:

| Task | Skills to Invoke |
|------|-----------------|
| Project setup | `sample-project-setup`, `engine-overview`, `gumcli` |
| Screen flow (Title, Battle, Overworld) | `screens`, `engine-overview` |
| Character/Enemy entities | `entities-and-factories`, `physics-and-movement` |
| ATB combat system | `timing`, `input-system`, `entities-and-factories` |
| Battle UI (HP bars, menus) | `gum-integration` (+ `gumcli` if using project mode) |
| Damage/elements | No skill needed — pure game logic |
| Overworld movement | `top-down-movement`, `input-system`, `collision-relationships` |
| Overworld map/tiles | `shapes`, `levels`, `collision-relationships` |
| Camera follow on overworld | `camera` |
| Pet system | `timing`, `entities-and-factories` |
| Crafting UI | `gum-integration` |
| Dialogue system | `gum-integration`, `input-system` |
| Save/load | No skill needed — pure C# serialization |
| Paths/patrol routes | `path-and-pathfollower` |

---

## Critical Engine Rules (Non-Obvious)

These are the rules agents MUST know. Violating any of these causes bugs or crashes.

### 1. Entity Creation — ALWAYS Use Factory
```csharp
// CORRECT
var enemy = Engine.GetFactory<Enemy>().Create();

// WRONG — Engine is null, CustomInitialize never called
var enemy = new Enemy();
```
**Why:** `Factory<T>.Create()` injects `Entity.Engine` (internal set) before calling `CustomInitialize`. Direct construction leaves `Engine` null and throws `InvalidOperationException` on any engine access.

### 2. Engine Is Null in Constructor
```csharp
// WRONG — crashes
public Enemy() { _input = Engine.InputManager.Keyboard; }

// CORRECT — Engine is available here
public override void CustomInitialize() {
    _input = Engine.InputManager.Keyboard;
}
```
**Applies to:** All entity setup — input creation, shape creation, content loading.

### 3. Shapes Default Invisible
```csharp
var rect = new AxisAlignedRectangle();
rect.IsVisible = true; // MUST set explicitly
```
Sprites default `IsVisible = true`. Shapes default `IsVisible = false`. The design doc mentions colored rectangles for prototyping — always set `IsVisible = true`.

### 4. Y+ Is Up
World space Y increases upward. This affects:
- Gravity is **negative** AccelerationY
- Screen top has **higher** Y values
- Grid/level parsing: row 0 = top of screen = highest Y value
- Gum UI coordinates are **separate** (screen pixels, Y-down)

### 5. Frame Loop Order
```
Screen transition check
  → Input polling
  → Gum UI update
  → Physics update (pos += vel*dt + acc*dt²/2, vel += acc*dt, vel -= vel*drag*dt)
  → Collision resolution
  → Entity CustomActivity(deltaSeconds)
  → Screen CustomActivity(deltaSeconds)
  → Draw
```
**Combat implication:** ATB gauge advancement and turn resolution belong in `CustomActivity`, which runs after collision. This is correct for a battle screen.

### 6. MoveToScreen Is Deferred
```csharp
MoveToScreen<BattleScreen>(s => s.EnemyGroupId = "goblin_pack");
// Code here STILL RUNS this frame — the transition happens next frame
```
When transitioning Overworld → Battle, set battle parameters via the configure callback.

### 7. Screen Cleanup Is Automatic
When `MoveToScreen` executes, all entities, factories, collision relationships, and Gum elements from the current screen are destroyed automatically. No manual cleanup needed in `CustomDestroy` unless you have external resources (file handles, etc.).

### 8. Create Input Objects Once
```csharp
// WRONG — allocates every frame
public override void CustomActivity(float delta) {
    var input = new KeyboardInput2D(...);
}

// CORRECT — create in CustomInitialize, use in CustomActivity
private I2DInput _movement;
public override void CustomInitialize() {
    _movement = new KeyboardInput2D(Engine.InputManager.Keyboard, ...);
}
```

### 9. Collision After Physics, Before CustomActivity
The `CollisionOccurred` event fires during collision resolution phase. Entity `CustomActivity` runs after. This means:
- In `CollisionOccurred`: entities are already separated/bounced
- In `CustomActivity`: you can read post-collision state safely
- For overworld enemy contact → battle transition: detect in `CollisionOccurred`, trigger `MoveToScreen` there

### 10. Platformer Uses BounceOnCollision, NOT MoveFirstOnCollision
Not directly relevant to Riftbound (JRPG, not platformer), but if any jumping/platforming segments exist:
```csharp
// CORRECT for platformer-style collision
.BounceOnCollision(0f, 1f, 0f)

// WRONG — doesn't zero velocity into surface
.MoveFirstOnCollision()
```
For overworld top-down collision with walls, `MoveFirstOnCollision()` is fine.

---

## System-by-System Implementation Mapping

### ATB Combat System
**Design:** FFIV-style real-time ATB with speed-based gauge fill, 4-person party, front/back row.

**Engine APIs:**
- `FrameTime.DeltaSeconds` for gauge advancement (`timing` skill)
- `Screen.CustomActivity(float deltaSeconds)` as the battle loop tick
- Entities for combatants (characters + enemies) via `Factory<T>` (`entities-and-factories` skill)
- `Engine.InputManager.Keyboard` / `.GetGamepad(0)` for menu navigation (`input-system` skill)

**Implementation notes:**
- ATB is pure game logic. No engine system handles turn order — build it from scratch.
- Speed toggle (2x/4x): multiply `deltaSeconds` passed to ATB system, NOT the real delta. The UI/input should still run at normal speed.
- The battle screen is a `Screen` subclass. All battle state lives on the screen instance.
- **Do NOT use physics/velocity for combatant positioning in battle.** Battle positions are fixed layout slots. Set X/Y directly.

**Damage formula from design doc:**
```
Physical: (STR * 2 - DEF) * ability_multiplier * random(0.9, 1.1), min 1
Magical:  (MAG * 2 - RES) * ability_multiplier * random(0.9, 1.1), min 1
```
Note: Design doc uses STR/DEF/MAG/RES. Tasks doc uses ATK/DEF/MAG/MDEF. **Standardize on the design doc names: STR, MAG, DEF, RES.**

### Battle UI
**Design:** HP/MP bars, ATB gauges, action menus, target selection, speed toggle indicator.

**Engine APIs:**
- Gum Forms controls (`gum-integration` skill): `StackPanel`, `Panel`, `Label`, `Button`
- `Add(element)` attaches Gum to screen-space (HUD layer)
- Gum coordinates are screen pixels, Y-down — independent of world camera

**Implementation notes:**
- **Ask Gum mode first** (`gumcli` skill): code-only is simplest for Phase 1 prototype. Project mode can come later.
- HP/MP bars: colored `Panel` inside a container `Panel`, width set as percentage
- ATB gauge: same pattern — Panel width = gaugePercent
- Action menu: `StackPanel` of `Button` controls, shown when character's ATB is full
- Target selection: highlight enemies with a cursor indicator (shape overlay or Gum element)
- **Namespace warning:** Use `Gum.Forms.Controls`, NOT `MonoGameGum.Forms.Controls` (obsolete)
- **Do NOT call `AddToRoot()`** — use `Add(element)` on the screen

### Overworld Movement
**Design:** Top-down, run by default, visible enemies, collision with terrain.

**Engine APIs:**
- `TopDownBehavior` + `TopDownValues` (`top-down-movement` skill)
- `KeyboardInput2D` / `GamepadInput2D` for movement input (`input-system` skill)
- `.Or()` to merge keyboard + gamepad
- `AddCollisionRelationship` with `.MoveFirstOnCollision()` for terrain (`collision-relationships` skill)
- `CameraControllingEntity` for camera follow (`camera` skill)

**Implementation notes:**
- Run by default = set `MaxSpeed` to run speed. No walk toggle needed initially.
- `TopDownBehavior.Update(entity, time)` must be called from `CustomActivity` **after collision resolution** (it already runs after collision in the frame loop).
- Visible enemies: separate entity type with simple patrol AI (can use `PathFollower` from `path-and-pathfollower` skill).
- Enemy contact → battle: `AddCollisionRelationship<Player, OverworldEnemy>()` as trigger-only (no fluent modifier), handle in `CollisionOccurred` to call `MoveToScreen<BattleScreen>`.

### Overworld Maps & Tiles
**Design:** Tile-based maps with 5 locations per world, interiors, terrain collision.

**CRITICAL GAP:** Tiled integration (`TiledMapLayerRenderable`, `TiledCollisionGenerator`) is **stubbed**. Cannot load `.tmx` files.

**Workaround options (pick one):**
1. **Entity-based maps** — Build maps from positioned entities and shapes. Most aligned with current engine. Use `TileShapeCollection` for terrain collision grids.
2. **String grid levels** — Use the `levels` skill pattern: define maps as string arrays, parse into entities/shapes. Good for prototyping.
3. **Custom tile renderer** — Write a minimal tile renderer using `Sprite` with `SourceRectangle` for tilesheets. More work but closer to traditional JRPG look.

**Recommendation for Phase 1-2:** Use option 2 (string grids) for prototyping, migrate to option 3 for polish. `TileShapeCollection` works now for collision grids regardless of rendering approach.

**TileShapeCollection setup:**
```csharp
var tiles = new TileShapeCollection();
tiles.GridSize = 16; // or whatever tile size
tiles.X = startX;
tiles.Y = startY;
tiles.AddTileAtCell(col, row); // adds collision rectangle
```

### Camera
**Design:** Scrolling overworld with camera following player.

**Engine APIs:**
- `CameraControllingEntity` for automatic following (`camera` skill)
- Supports deadzone, smooth/constant/immediate approach, screen shake
- `Camera.TargetWidth` / `Camera.TargetHeight` define visible world area
- `DisplaySettings` for window resolution and zoom

**Implementation notes:**
- `CameraControllingEntity` must be created via `Factory` (needs Engine injection)
- Set `Map` property to an `AxisAlignedRectangle` defining world bounds (prevents camera from scrolling past map edges)
- For battle screen: camera is static (no following entity needed). Just set position.
- `PrepareWindow<TFirstScreen>()` call goes in `Game1` constructor for initial window size

### Pet System
**Design:** 3 care stats (Satiety, Training, Bond), active nourishment loop, combat abilities, death/redemption.

**Engine APIs:**
- `FrameTime.DeltaSeconds` for stat decay (`timing` skill)
- Pet as Entity attached to character entity (`entities-and-factories` skill)
- Pet gauge in battle: same pattern as ATB gauge (float incremented by delta)
- Pet UI: Gum Forms for care screen (`gum-integration` skill)

**Implementation notes:**
- Pet is a **data model** more than a visible entity during combat. In battle, the pet is represented by UI (gauge + ability buttons). On overworld, the pet could be a small sprite following the owner.
- Stat decay: cooldown gate pattern from `timing` skill. Decrement stats by rate * deltaSeconds.
- Training minigames: separate `Screen` or overlay within the pet care menu. Use `timing` for the 30-second timer and `input-system` for player actions.
- Redemption quests: mini-dungeons loaded as separate screens. Can reuse overworld movement + battle systems.

### Crafting System
**Design:** One-screen UI, batch craft, material tracker, no failure, infinite materials.

**Engine APIs:**
- Gum Forms: `StackPanel` for recipe list, `Label` for material counts, `Button` for craft action (`gum-integration` skill)
- Layout: `Panel` as container, anchor/dock for positioning (`gum-integration/references/layout.md`)

**Implementation notes:**
- Crafting is 100% UI + data. No engine-specific concerns.
- Data model: `Recipe` (materials list + output), `Inventory` (dictionary of material → count), `RecipeBook` (discovered recipes list)
- Filter buttons at top of recipe list, craftable-now highlight
- Material tracker: store pinned recipe ID, show markers on minimap (UI overlay)

### Dialogue System
**Design:** Branching text, character portraits, text log.

**MUST BUILD FROM SCRATCH.** No built-in dialogue system.

**Engine APIs:**
- Gum `Label` for text display, `Panel` for dialogue box (`gum-integration` skill)
- `StackPanel` for dialogue choices
- `Engine.InputManager.Keyboard.WasKeyPressed(Keys.Space)` to advance (`input-system` skill)

**Implementation notes:**
- Dialogue data: JSON files with nodes (text + speaker + portrait + next/choices)
- Text box: Gum `Panel` anchored to bottom of screen, `Label` inside for text
- Portrait: Gum element with texture (or colored rectangle for prototype)
- Branching: each node has optional `choices` array, each choice points to next node ID
- Text log: append every displayed line to a `List<string>`, viewable from menu

### Save/Load System
**Design:** Save anywhere, autosave, 20+ slots.

**MUST BUILD FROM SCRATCH.** No built-in serialization.

**Implementation notes:**
- Use `System.Text.Json` for serialization (built into .NET, no extra dependency)
- Save data: party roster, active party, inventory, quest flags, current screen + position, pet stats, discovered recipes, bestiary, bond progress
- Save location: `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)/Riftbound/saves/`
- Autosave: call save logic at screen transitions and rest points
- Save slots: numbered JSON files (`save_01.json` through `save_20.json` + `autosave.json`)

### Audio
**Design:** Music per world, battle themes, boss themes, sound effects.

**CRITICAL GAP:** `AudioManager` methods **throw `NotImplementedException`**.

**Workaround:** Use MonoGame APIs directly:
```csharp
// Sound effects
var sfx = Content.Load<SoundEffect>("sfx_hit");
sfx.Play();

// Music
var song = Content.Load<Song>("bgm_overworld");
MediaPlayer.Play(song);
MediaPlayer.IsRepeating = true;
MediaPlayer.Volume = 0.7f;
```
**Note:** `Content` here is MonoGame's `ContentManager`, not FRB2's. Access via `Game.Content` or pass through from `Game1`. Audio files must go through the MGCB content pipeline (`.mgcb` file).

**Recommendation:** Defer audio to Phase 4 (Polish). The game is fully playable without it. When implemented, wrap MonoGame APIs in a simple `AudioService` class.

### Animation
**Design:** Character battle animations, pet animations, ability effects.

**CRITICAL GAP:** `Sprite.PlayAnimation` is a **no-op**. ACHX format not implemented.

**Workaround:** Custom frame-based animation:
```csharp
// Simple sprite sheet animation using SourceRectangle
private float _animTimer;
private int _currentFrame;
private Rectangle[] _frames; // pre-computed source rectangles

public override void CustomActivity(float delta) {
    _animTimer += delta;
    if (_animTimer >= frameDuration) {
        _animTimer = 0;
        _currentFrame = (_currentFrame + 1) % _frames.Length;
        _sprite.SourceRectangle = _frames[_currentFrame];
    }
}
```
**Recommendation:** For Phase 1, use colored shapes (rectangles/circles) for all entities. No animation needed. Phase 2+ can add sprite sheets with the pattern above.

---

## Stub Systems — Do NOT Attempt to Use

These exist in the engine API but are non-functional:

| System | Status | What Happens |
|--------|--------|-------------|
| `AudioManager` | All methods throw `NotImplementedException` | Crash at runtime |
| `Sprite.PlayAnimation` | No-op | Silently does nothing |
| `DebugRenderer` | All draw methods are no-ops | Silently does nothing |
| `TiledMapLayerRenderable` | Stub | Cannot render tile maps |
| `TiledCollisionGenerator` | Stub | Cannot generate collision from Tiled |

---

## Design Doc Corrections

Issues found comparing design doc against engine reality:

### 1. Stat Naming Inconsistency
- **Design doc** (Section 3): STR, MAG, DEF, RES, SPD, LCK
- **Tasks doc** (Milestone 1.2): ATK, DEF, MAG, MDEF
- **Resolution:** Use design doc names everywhere: **STR, MAG, DEF, RES, SPD, LCK**

### 2. Element Names vs Tasks Doc
- **Design doc** (Section 3): Steam/Glitch/Aether + Fire/Ice/Lightning (6 elements, world-themed)
- **Tasks doc** (Milestone 1.2): "Fire/Ice/Lightning + Light/Dark/Void"
- **Resolution:** Use design doc names: **Steam/Glitch/Aether + Fire/Ice/Lightning**. The design doc's world-themed element triangle is more interesting and ties into the three-world aesthetic.

### 3. Grief Debuff Percentage
- **Design doc** (Section 5): -15% to all stats
- **Tasks doc** (Milestone 1.4): -25% to all stats
- **Resolution:** Use design doc value: **-15%**. -25% feels too punishing for a system meant to not feel like punishment.

### 4. Technical Notes Section Accuracy
The design doc's Section 10 is mostly correct but understates what's available:
- It says "2D sprite rendering for characters, tilesets, and UI elements" — tileset rendering is actually **stubbed** (Tiled integration doesn't work)
- It doesn't mention `TopDownBehavior` which is the exact API for overworld movement
- It doesn't mention `CameraControllingEntity` for camera following
- It doesn't mention `TileShapeCollection` which works for collision grids even without Tiled rendering
- It doesn't mention `PathFollower` which is useful for NPC/enemy patrol routes

### 5. MP Regeneration Inconsistency
- **Design doc** (Section 3, Ability System): "MP regenerates slowly in combat (1% per turn) and fully restores after battle"
- **Design doc** (Section 8, QoL table): "Post-battle full heal (MP) — MP fully restores after every battle"
- **Tasks doc** (Milestone 1.3): "Post-battle MP restore (10-20%)"
- **Resolution:** Design doc is internally consistent (1% per turn in-battle + full restore after battle). Tasks doc is wrong — should be **full MP restore after battle**, not 10-20%.

---

## Gum Mode Decision

Before writing ANY UI code, the implementing agent MUST decide the Gum mode. This is a one-time decision that affects all subsequent UI work.

**Recommended for Riftbound:** Start with **code-only mode** (Mode 1) for Phase 1. This avoids tool dependencies and is fastest to iterate on. Switch to **project + codegen** (Mode 3) in Phase 2+ when the UI stabilizes and there are many screens to build.

**If using code-only mode**, no `gumcli` invocation is needed. Just use `Gum.Forms.Controls` directly.

**If switching to project mode later**, invoke the `gumcli` skill at that point for setup.

---

## Content Pipeline Setup

The MGCB content pipeline is needed for:
- Textures (sprite sheets, portraits, tilesets) — `.png` → `.xnb`
- Fonts — `.spritefont` → `.xnb`
- Audio (when implemented) — `.wav`/`.mp3`/`.ogg` → `.xnb`

**Critical:** The sample project MUST include `.config/dotnet-tools.json` with `dotnet-mgcb` registered. Without this, the build fails with "Cannot find a manifest file" error. See `sample-project-setup` skill.

For Phase 1 (shapes only, no textures), the content pipeline is minimal. But set it up correctly from the start to avoid pain later.

---

## Architecture Patterns to Follow

### Data-Driven Design
With 22 characters, 22 pets, hundreds of abilities, enemies, and recipes, **nothing should be hardcoded**. Use JSON data files:

```
Data/
  characters.json    — all 22 character definitions (stats, growth, abilities)
  enemies.json       — enemy definitions per area
  abilities.json     — all ability definitions (damage, element, cost, target)
  pets.json           — pet definitions (abilities per tier, care rates)
  recipes.json        — crafting recipes (materials, output, category)
  dialogue/           — per-scene dialogue files
  levels/             — per-area map definitions
```

Load with `System.Text.Json.JsonSerializer.Deserialize<T>()`.

### Event Bus
Cross-system communication (pet death → grief debuff, quest complete → recipe unlock) should use a simple pub/sub event bus, not direct references between systems.

```csharp
// Simple event bus pattern
public class GameEvents {
    public event Action<Pet> PetDied;
    public event Action<string> QuestCompleted;
    public event Action<Character, int> BondLevelUp;

    public void OnPetDied(Pet pet) => PetDied?.Invoke(pet);
    // etc.
}
```

### Battle Engine Isolation
The ATB system, damage calculator, and AI should be testable without a running game. Keep them as plain C# classes that take data in and return results. The `BattleScreen` wires them to entities and UI but the logic is independent.

---

## Phase 1 Prototype — What Gets Built With What

| Component | Engine Feature | Skill | Visual |
|-----------|---------------|-------|--------|
| Battle characters | Entity + Factory | `entities-and-factories` | Colored rectangles |
| Battle enemies | Entity + Factory | `entities-and-factories` | Colored rectangles (different colors) |
| ATB gauges | float + DeltaSeconds | `timing` | Gum Panel (width = %) |
| HP/MP bars | float | — | Gum Panel (width = %) |
| Action menu | Gum StackPanel + Buttons | `gum-integration` | Text buttons |
| Damage numbers | Gum Label | `gum-integration` | Floating text |
| Turn resolution | Pure game logic | — | — |
| Pet gauge | float + DeltaSeconds | `timing` | Gum Panel |
| Pet care screen | Gum Forms | `gum-integration` | Buttons + labels |
| Screen flow | Screen subclasses | `screens` | — |
| Test overworld | Entity + TopDownBehavior | `top-down-movement` | Colored rectangle |
| Terrain collision | TileShapeCollection | `shapes`, `collision-relationships` | Visible rectangles |

No textures, no audio, no animations in Phase 1. Shapes and Gum text only.
