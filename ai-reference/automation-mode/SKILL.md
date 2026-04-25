---
name: automation-mode
description: Automation mode in FlatRedBall2. Use when an external agent (AI or script) needs to drive a running game: stepping frames, injecting input, querying game state, or forcing entity values over stdin/stdout. Covers EnableAutomationMode, the NDJSON command protocol, RegisterStateProvider, and RegisterValueSetter.
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

## Command Protocol

Each command is a JSON object terminated by `\n`. Each response is a JSON object on its own line, always containing `ok` and `frame`.

| Command | JSON |
|---------|------|
| Step N frames | `{"cmd":"step"}` or `{"cmd":"step","count":5}` |
| Key down/up | `{"cmd":"input","type":"key","key":"Space","down":true}` |
| Gamepad button | `{"cmd":"input","type":"gamepad","player":0,"button":"A","down":true}` |
| Gamepad axis | `{"cmd":"input","type":"axis","player":0,"axis":"LeftStickX","value":0.8}` |
| Query screen | `{"cmd":"query","target":"screen"}` |
| Query all providers | `{"cmd":"query","target":"entities"}` |
| Query named provider | `{"cmd":"query","target":"player"}` |
| Force a value | `{"cmd":"set","entity":"Player","prop":"X","value":100.0}` |
| Quit | `{"cmd":"quit"}` |

Responses:
```
{"ok":true,"frame":42}
{"ok":true,"frame":42,"result":{"screen":"GameScreen"}}
{"ok":false,"frame":42,"error":"unknown target: foo"}
```

### Frame stepping

The game loop gates each frame on pending step commands. Without a `step` command the game does not advance — it returns from `Update` early and suppresses Draw each tick. The OS window stays alive; only game logic is paused.

`step` with `count` queues multiple frames: `{"cmd":"step","count":60}` lets 60 frames run before the gate closes again.

### Input injection

Input commands inject synthetic state at the `InputManager` level, replacing MonoGame hardware polling for that frame. The injected state persists across frames until explicitly changed — sending `"down":false` clears it.

Key names resolve via `Enum.Parse<Keys>()` — use MonoGame's `Keys` enum names verbatim (`Space`, `W`, `Left`, `LeftShift`). Same for gamepad buttons (`Buttons` enum: `A`, `B`, `Start`, `LeftShoulder`) and axes (`GamepadAxis` enum: `LeftStickX`, `LeftStickY`, `RightStickX`, `RightStickY`, `LeftTrigger`, `RightTrigger`).

Input commands do not produce a response — they are applied silently on the next frame. If you need confirmation, send a `query` after.

## State Registration

Game code registers named providers for queries and named setters for forced values. Nothing is auto-discovered.

```csharp
// In CustomInitialize or after entity creation:
Engine.RegisterStateProvider("player", () => new { player.X, player.Y, player.Health });
Engine.RegisterValueSetter("Player", "X", v => player.X = (float)v);
```

`query target:"player"` → `{"ok":true,"frame":N,"result":{"X":100,"Y":200,"Health":3}}`

`set entity:"Player" prop:"X" value:100` → calls the registered setter with `100.0` as a `double`.

`query target:"entities"` returns all registered providers in one object.

Providers are called on the game thread between frames — the returned object is serialized with `System.Text.Json`. Anonymous types serialize cleanly; avoid types that reference MonoGame objects with circular references.

## Gotchas

- **stdout is the protocol channel.** Any `Console.WriteLine` in game code will corrupt the NDJSON stream. Use `System.Diagnostics.Debug.WriteLine` for all diagnostic output (this is the project's code style rule too). The engine itself follows this.
- **Query results reflect the previous frame.** Queries are processed at the start of `Update`, before this frame's logic runs. If you step then query, you see the state as of the stepped frame — which is what you want for observe-decide-act loops.
- **Providers and setters must be registered before the agent queries/sets them.** There is no deferred lookup — an unrecognized target returns `{"ok":false,"error":"unknown target: ..."}`.
- **`set` only accepts numeric values.** The `value` field is parsed as `double`. Boolean or string forcing is not supported — register a numeric proxy (0/1) if needed.
- **`quit` calls `Game.Exit()`.** If the game is not yet initialized (e.g. in tests), the call is swallowed silently.
