---
name: automation-mode
description: Automation mode in FlatRedBall2. Use when an external agent (AI or script) needs to drive a running game: stepping frames, injecting input, querying entity state, or forcing entity values over stdin/stdout. Covers EnableAutomationMode, the NDJSON command protocol, reflection-based entity introspection, and optional RegisterStateProvider for derived state.
---

# Automation Mode in FlatRedBall2

Automation mode lets an external process control a running game via NDJSON (newline-delimited JSON) over stdin/stdout — one command per line in, one response per line out. The primary use case is AI agents that need to observe and interact with the game the way Playwright interacts with a browser.

**Debug builds only.** `EnableAutomationMode()` is a no-op in Release. This is intentional — automation mode exposes internal state and allows arbitrary value forcing.

## Setup

```csharp
// Game.Initialize, after base.Initialize():
FlatRedBallService.Default.EnableAutomationMode();
```

The call itself does nothing unless `--frb-auto` is present in the command-line args. Ship the call unconditionally; the flag controls activation.

```
dotnet run -- --frb-auto
```

That's the entire required wiring. Entities and their public properties are auto-discovered — no per-property registration.

## Command Protocol

Each command is a JSON object terminated by `\n`. Each response is a JSON object on its own line, always containing `ok` and `frame`.

| Command | JSON |
|---------|------|
| Step N frames | `{"cmd":"step"}` or `{"cmd":"step","count":5}` |
| Key down/up | `{"cmd":"input","type":"key","key":"Space","down":true}` |
| Gamepad button | `{"cmd":"input","type":"gamepad","player":0,"button":"A","down":true}` |
| Gamepad axis | `{"cmd":"input","type":"axis","player":0,"axis":"LeftStickX","value":0.8}` |
| Query active screen | `{"cmd":"query","target":"screen"}` |
| Query all entities | `{"cmd":"query","target":"entities"}` |
| Query one entity type | `{"cmd":"query","target":"Player"}` |
| Force a value | `{"cmd":"set","entity":"Player","prop":"X","value":100.0}` |
| Quit | `{"cmd":"quit"}` |

### Frame stepping

The game loop gates each frame on pending step commands. Without a `step` the game does not advance — it returns from `Update` early and suppresses Draw each tick.

Commands process **in the order they were sent**. A query that follows a step observes the post-step frame. This makes a recorded NDJSON file fully reproducible — replay produces the same responses regardless of how fast the reader pumps lines.

A `step count:N` produces one response per frame (N total), each carrying the resulting frame number.

### Input injection

Synthetic state replaces MonoGame hardware polling. The injected state persists across frames until explicitly changed — send `"down":false` to clear.

Key names resolve via `Enum.Parse<Keys>()` — use MonoGame's `Keys` enum names verbatim (`Space`, `W`, `Left`, `LeftShift`). Same for gamepad buttons (`Buttons` enum) and axes (`GamepadAxis` enum: `LeftStickX`, `LeftStickY`, `RightStickX`, `RightStickY`, `LeftTrigger`, `RightTrigger`).

Input commands produce no response — query if you need confirmation. `WasKeyPressed` style inputs require the down state to span at least one stepped frame between the down and up commands; combine `input down:true` → `step` → `input down:false` to register a press.

## Querying Entities (Zero Config)

`query target:"<EntityTypeName>"` returns a snapshot list of every live instance of that type. The type name is the simple C# type name — `PlayerShip`, not `ShmupSpace.Entities.PlayerShip`.

`query target:"entities"` returns every factory keyed by type name, plus any registered providers, in one object.

Snapshots include public instance properties whose type is in a small allowlist: numeric primitives, `bool`, `string`, enums (serialized as their name), `Vector2`, `Vector3`, and `Color` (split to `R/G/B/A`). Reference types like `Sprite`, `Layer`, and the engine itself are skipped — no recursion, no circular-ref blowups. Properties whose getter throws are skipped silently.

A property the agent expects but doesn't see in the snapshot is almost always either non-public, of a non-allowlisted type, or throws on read.

## Setting Values (Zero Config)

`set entity:"Player" prop:"X" value:50.0` looks up the factory for `Player`, takes the first live instance, and assigns via reflection. `value` is always parsed as `double` and converted to the target property's type (including `int`, `float`, enums by ordinal). Errors are specific: missing factory, no live instances, non-existent property, non-writable property.

Boolean/string forcing isn't supported — `value` is numeric only. Register a custom setter if you need that.

## Custom Providers and Setters (Optional)

Use these only for state that *isn't* a plain entity property — typically derived values like `Score`, `Lives`, `current wave`, or a curated projection that hides internals.

```csharp
Engine.RegisterStateProvider("hud", () => new { Score = _score, Lives = _lives });
Engine.RegisterValueSetter("Player", "Health", v => player.Health = (int)v);
```

A registered name takes precedence over reflection — handy when you want a concise view rather than the full property dump.

Providers and setters live on the screen that registered them and disappear on screen exit. The agent can detect a screen change with `query target:"screen"`.

## Gotchas

- **stdout is the protocol channel.** Any `Console.WriteLine` in game code corrupts the NDJSON stream. Use `System.Diagnostics.Debug.WriteLine` for diagnostics — also the project's code-style rule.
- **Type names, not lowercase aliases.** `query target:"Player"` works; `query target:"player"` returns `unknown query target` unless you explicitly registered a `"player"` provider.
- **Screen-scoped providers.** Custom providers registered in `GameScreen.CustomInitialize` don't exist while on `TitleScreen`. Reflection-based entity queries work only for types whose factories have been created — i.e., on the screen that owns them.
- **`quit` calls `Game.Exit()`.** If the game is not yet initialized (e.g. in tests), the call is swallowed silently.
