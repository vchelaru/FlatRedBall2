# Automation Mode — Design Notes

Stdin/stdout protocol for external agents (AI or otherwise) to control a running FRB2 game: advance frames, inject input, query state, and force values. Analogous to Chrome DevTools Protocol for browsers or LSP for editors, but over stdin/stdout to avoid network ports, firewall prompts, and client library dependencies.

---

## Motivation

- AI agents need a way to interact with a game the same way Playwright interacts with a browser: run a frame, observe state, decide on next input.
- HTTP/WebSocket would work but triggers Windows firewall prompts and requires client library imports — real friction for automation setups.
- stdin/stdout just works: no ports, no permissions, no `using` statements. Works in CI, containers, and anywhere `dotnet run` works.
- Precedent: LSP (Language Server Protocol) uses exactly this transport for the same reasons.

---

## Activation

Game opts in with one call, typically in `Game1.Initialize`:

```csharp
FlatRedBallService.Default.EnableAutomationMode();
```

`EnableAutomationMode` detects a `--frb-auto` flag in `Environment.GetCommandLineArgs()` internally — game code doesn't need to parse args. If the flag is absent, the call is a no-op. This means the game ships with the call in place and automation mode only activates when the flag is passed.

Launch:

```
dotnet run -- --frb-auto
```

---

## Protocol

Newline-delimited JSON (NDJSON) over stdin/stdout. One command per line in, one response per line out. Commands and responses are both valid JSON objects terminated by `\n`.

### Commands (stdin → game)

| Command | Example |
|---------|---------|
| Advance N frames | `{"cmd":"step"}` or `{"cmd":"step","count":60}` |
| Key input | `{"cmd":"input","type":"key","key":"Space","down":true}` |
| Gamepad button | `{"cmd":"input","type":"gamepad","player":0,"button":"A","down":true}` |
| Gamepad axis | `{"cmd":"input","type":"axis","player":0,"axis":"LeftX","value":0.8}` |
| Cursor (screen) | `{"cmd":"input","type":"cursor","x":120,"y":80,"primary":true}` |
| Cursor (world)  | `{"cmd":"input","type":"cursor","x":0,"y":0,"space":"world","primary":true}` |
| Query screen state | `{"cmd":"query","target":"screen"}` |
| Query all entities | `{"cmd":"query","target":"entities"}` |
| Query named entity | `{"cmd":"query","target":"entity","name":"Player"}` |
| Force a value | `{"cmd":"set","entity":"Player","prop":"X","value":100.0}` |
| Quit | `{"cmd":"quit"}` |

### Responses (game → stdout)

```json
{"ok":true,"frame":42}
{"ok":true,"frame":102,"result":{"screen":"GameScreen","entities":42}}
{"ok":false,"error":"entity 'Plyr' not found"}
```

All responses include the current `frame` count so agents can verify temporal ordering.

### Error handling

Unknown commands and malformed JSON return `{"ok":false,"error":"..."}` — the game keeps running. An unrecognized `cmd` is not fatal.

---

## Frame Stepping

In automation mode the game loop gates each `Update` call on a `SemaphoreSlim`. The background stdin reader releases the semaphore when a `step` command arrives.

```
main thread:          reader thread:
  Update blocks         readline → parse
  ...                   cmd == "step" → semaphore.Release()
  Update runs           ...
  Draw runs
  write response
  Update blocks again
```

The gate is only active in automation mode — normal play is unaffected.

### Message pump concern

A truly blocking wait starves the OS message pump (window appears frozen/crashed). The gate should use a short-polling loop (`semaphore.Wait(16ms)`) that also calls `game.SuppressDraw()` or equivalent while waiting, rather than a bare blocking `Wait()`. This keeps the window alive between steps.

---

## State Query Model

Two options, not yet decided:

**Option A — Explicit registration (preferred)**

Game code registers named providers:

```csharp
engine.RegisterStateProvider("player", () => new { player.X, player.Y, player.Health });
```

Agents query by name: `{"cmd":"query","target":"player"}`. Clean, typed, no reflection. Requires game code to opt in per-entity but gives agents predictable, documented endpoints.

**Option B — Reflection-based scan**

Engine walks all live entities and reflects public properties. No game-code changes needed. Risk: fragile (renames break agent scripts), noisy (exposes internals agents don't care about), potential perf hit if entity count is large.

Recommendation: start with Option A. Option B can be added later as a convenience layer on top.

---

## Screenshot Endpoint

A `{"cmd":"screenshot"}` command that returns a base64-encoded PNG of the current framebuffer would let agents "see" the game — making visual regression and play-testing possible without a vision model interpreting the window. This requires rendering to an offscreen surface or reading back the backbuffer via `GraphicsDevice.GetBackBufferData`. Worth doing once the core step/query loop is working.

---

## Channel Decision — stdout sharing & diagnostics

**Status:** decided (resolves the former "stdout collision" open question). Issue #436.

**Context.** Protocol responses and any game-code `Console.WriteLine` share one stdout stream. A strict line-by-line JSON reader chokes on a non-JSON line, so logging *can* corrupt the protocol — but whether it does depends on the reader and on whether game code writes to stdout at all. It's a possibility, not a certainty; the original skill note ("any `Console.WriteLine` corrupts the stream") overstated it as inevitable.

**Options considered.**

- **A — Share stdout, document a best-effort filter.** The reader skips lines that don't parse as JSON; every response starts with `{`, which acts as a de-facto prefix. Zero code change. Cost: a game log line that itself starts with `{` still collides, and a partial `Console.Write` (no trailing newline) can prepend a fragment onto a response line, silently dropping it. Best-effort, not framing.
- **B — Explicit prefix on every response** (e.g. `@@FRB@@{...}`). Same mechanism as A with a less collision-prone marker. Cost: every consumer must implement the same framing, and it still doesn't fix partial-write splicing.
- **C — Redirect `Console.Out` to stderr on activation.** The protocol keeps a private handle to the real stdout; game `Console.WriteLine` is repointed at stderr, so it physically can't reach the protocol channel. Robust, needs no reader cooperation. Cost: it silently changes where game logging lands *while `--frb-auto` is active* — a hidden behavior change, and tooling that treats "stderr has content" as failure trips on it. The log-to-file variant dodges the stderr footgun but adds machinery.
- **D — Dedicated transport** (named pipe / localhost socket). The protocol gets its own channel; stdout is left entirely to the game — no collision, no redirect. Cost: rendezvous (agree on a pipe name / port) plus connection lifecycle that the free stdin/stdout inheritance gave us for nothing.

**Decision.** Default to **A**: stdout transport with documented best-effort `{`-line filtering, diagnostics kept off stdout by convention (`Debug.WriteLine` / stderr — also the code-style rule).

**Fallback (if A proves inadequate).** If best-effort filtering causes real problems in practice — recurring collisions, or a consumer such as another engine reusing this protocol needing a hard guarantee — escalate to **D**, a dedicated transport (named pipe / localhost socket) that gives the protocol its own channel and leaves stdout entirely to the game. D is the designated escape hatch, not B or C: it dominates both on robustness (no shared stream at all, no `Console.Out` redirect, no reader cooperation). It's cheap to reach for because `AutomationMode` already takes injectable `TextReader`/`TextWriter` (today `Console.In`/`Console.Out`); a pipe's streams drop into the same seam with no change to protocol logic. Not built now because it's a speculative transport with no current consumer (`design/TODOS.md`: no speculative items) — add it when a real need lands.

**Why not B or C.** B over A buys little for FRB2's own use, where we own both ends, and still taxes every external consumer. C is the most robust single-stream option, but the rug-pull / stderr-as-error surprise isn't worth it while A is adequate.

---

## Release Guard — decided

**Status:** decided — yes, guarded (resolves the former "security / accidental activation" open question).

Automation mode exposes internal entity state and arbitrary value-forcing, so it must never be reachable in a shipped build. The entire automation block in `src/FlatRedBallService.cs` is wrapped in `#if DEBUG`; the `#else` branch compiles `EnableAutomationMode`, `RegisterStateProvider`, and `RegisterValueSetter` to empty no-op stubs. A Release build therefore cannot activate automation even if a game ships the `EnableAutomationMode()` call and is launched with `--frb-auto`.

---

## Open Questions

1. **State registration API shape.** `RegisterStateProvider(string name, Func<object> provider)` vs. a typed generic `RegisterStateProvider<T>(string name, Func<T> provider)` with JSON serialization. Generic is cleaner but adds reflection at serialization time — may not matter given this is dev-only.

2. **Input injection model.** Inject at the `InputManager` level (fake `GamePadState` / `KeyboardState`) vs. at the `FlatRedBallService.Input` abstraction layer. The abstraction layer is cleaner for FRB2 game code; injecting at MonoGame's state level also covers raw `Keyboard.GetState()` calls in `Game1.Update`.

3. **Frame timing in step mode.** Stepped frames use whatever `GameTime` MonoGame provides. Should step mode synthesize a fixed `GameTime` (e.g., always 16.67ms per frame) so physics is deterministic regardless of wall-clock time?
