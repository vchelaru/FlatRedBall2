# Solitaire Sample — Plan & Progress

> **Resume hint for a fresh Claude session:** read this file end-to-end first. Phase 1 is done and committed to disk; the next blocker is the user authoring two Gum elements in the Gum editor (see "Next action" at the bottom).

## Goal

Add a full sample (`samples/Solitaire/`, **not** under `samples/auto/`) implementing **Klondike Solitaire, draw 3**. Goals:

- Demonstrate **Gum project files** (`.gumx`) loaded from disk — the first non-auto sample to do so. Existing samples use code-only Gum.
- Demonstrate a **content-rich Gum component with multiple state categories** (Card with Suit × Rank × Facing).
- Ship for **both desktop (DesktopGL, net10.0) and Blazor WASM (KNI BlazorGL, net8.0)** as a dual-target project from day one.
- Serve as an AI-usability test: card games force drag/drop, rule validation, and state-driven UI — areas the engine has not been exercised in before.

The build is staged so each phase compiles and runs before the next is added.

## Decisions locked in

| Topic | Choice |
|---|---|
| Variant | Klondike, **draw 3** |
| Card art source | Placeholders (text + colored rect) authored in Gum; user can swap to Sprites later |
| Card states | Three state categories on `CardGum`: **Suit** (Spades/Hearts/Clubs/Diamonds), **Rank** (Ace…King), **Facing** (Up/Down). Each category modifies a different child element so they compose. |
| Gum mode | **gumcli mode 3** — project + codegen, strongly-typed access |
| Project name | `Solitaire` (no `Sample` suffix; matches PlatformKing/ShmupSpace) |
| Canvas pattern | Pattern A (stretch-to-viewport, locked aspect) — engine default |
| Resolution | 1280×720 design res, locked aspect |

## Project layout (mirrors PlatformKing)

```
samples/Solitaire/
  Solitaire.slnx                          ✅ created
  PLAN.md                                 ✅ this file
  Solitaire.Common/                       net8.0;net10.0
    Solitaire.Common.csproj               ✅
    Game1.cs                              ✅ loads GumProject/GumProject.gumx
    Screens/
      GameScreen.cs                       ✅ Phase-1 placeholder (code-only green felt)
    Cards/                                ⏳ Phase 3 (model)
    Entities/                             ⏳ Phase 4 (CardEntity)
    Content/GumProject/
      GumProject.gumx                     ✅ created via `gumcli new --template empty`
      Components/Controls/                ✅ all Forms controls (ButtonStandard, etc.)
      Components/Elements/                ✅ Divider, Icon, etc.
      Behaviors/                          ✅ ButtonBehavior, etc.
      Standards/                          ✅
      Screens/GameScreenGum.gusx          ⏳ user must author in Gum editor
      Components/CardGum.gucx             ⏳ user must author in Gum editor
  Solitaire.Desktop/                      net10.0
    Solitaire.Desktop.csproj              ✅
    Program.cs                            ✅
    Content/Content.mgcb                  ✅ (minimal stub)
    .config/dotnet-tools.json             ✅ (dotnet-mgcb 3.8.4.1)
  Solitaire.BlazorGL/                     net8.0
    Solitaire.BlazorGL.csproj             ✅ Pattern-A wiring + Copy targets
    Program.cs                            ✅
    wwwroot/index.html                    ✅
    wwwroot/Content/.gitignore            ✅
    Properties/launchSettings.json        ✅ ports 50490/50491
```

`Solitaire.Common.csproj` mirrors `PlatformKing.Common.csproj` (multi-target, KNI/MONOGAME defines, conditioned XNA packages, `ProjectReference` to `src/FlatRedBall2.csproj`) plus a wildcard `<Content Include="Content\GumProject\**" CopyToOutputDirectory="PreserveNewest" />`.

The Desktop and BlazorGL heads follow `frb-skills/multiplatform-conversion/SKILL.md` verbatim: Desktop links Common's `Content/GumProject/**` into its output; BlazorGL uses a `BeforeTargets="GenerateStaticWebAssetsManifest;AssignTargetPaths"` `<Copy>` target to land the same files in physical `wwwroot/Content/`.

## Implementation phases

### ✅ Phase 1 — Bootstrap (DONE)

Both heads `dotnet build` clean (0 warnings, 0 errors). The Gum project loads via `EngineInitSettings.GumProjectFile = "GumProject/GumProject.gumx"`. A code-only `ColoredRectangleRuntime` placeholder fills the screen with felt-green (`(11,102,35)`) until Phase 2's authored `GameScreenGum.gusx` replaces it.

### ✅ Phase 2 — Author CardGum + GameScreenGum (DONE)

User authored `Screens/GameScreenGum.gusx` and `Components/CardGum.gucx` (with three state categories: Suit, Rank, Facing) in the Gum editor. `gumcli codegen` generated 45 elements including `Solitaire.Screens.GameScreenGum` and `Solitaire.Components.CardGum`. `GameScreen.cs` now does `Add(new GameScreenGum())`. Both Desktop (net10.0) and BlazorGL (net8.0) build clean.

**Friction encountered:** `gumcli check`/`codegen` failed with "Could not get the default state for type Arc / Canvas in either the default or through plugins" because gumcli's headless project loader didn't register the Apos Shapes / Canvas plugins. Runtime path was unaffected (MonoGameGum init registers them). User patched gumcli; rebuild + retry succeeded.

**Skill update:** `frb-skills/gum-integration/SKILL.md` Mode-3 section incorrectly claimed generated classes are named `<ElementName>Runtime`; gumcli actually generates them with the bare element name (so `MainMenuScreenGum` → class `MainMenuScreenGum`, not `MainMenuScreenRuntime`). Section corrected.

### Phase 2 archive — original spec (kept for reference)

User opens the Gum editor on `samples/Solitaire/Solitaire.Common/Content/GumProject/GumProject.gumx` and authors:

1. **`Screens/GameScreenGum.gusx`** — one full-bleed `ColoredRectangle` named `Felt`, color `(11,102,35)`. Just enough to register the screen; play-field anchors come in Phase 4.

2. **`Components/CardGum.gucx`** — children:
   - `Background` — white `ColoredRectangle`, ~80×112
   - `RankText` — `Text`, top-left, default "A"
   - `SuitIcon` — `Text`, center, default "♠"
   - `Back` — blue `ColoredRectangle`, fills card

   State categories on `CardGum`:
   - **Suit** (4 states): Spades, Hearts, Clubs, Diamonds. Vary `SuitIcon.Text` (♠ ♥ ♣ ♦) and color (red for Hearts/Diamonds, black otherwise — apply to both `RankText.Color` and `SuitIcon.Color`).
   - **Rank** (13 states): Ace, Two…Ten, Jack, Queen, King. Vary only `RankText.Text` ("A", "2"…"10", "J", "Q", "K"). Optionally vary `Background.Color` for J/Q/K so face cards are visually distinct now.
   - **Facing** (2 states): Up (`Back.Visible=false`), Down (`Back.Visible=true`).

After save, Claude runs `gumcli codegen-init` then `gumcli codegen` to generate strongly-typed C# classes.

**Validation step before authoring all 13×4=52 states:** Author Spades + Ace + Up first, plug into `GameScreen`, confirm the three categories compose (each modifies disjoint child variables so applying state A then state B in a different category should not undo A). If they don't compose, fall back to the third planning option (pip cards drawn from code, J/Q/K as authored states).

### ✅ Phase 3 — Game model + rules + xUnit tests (DONE)

`Solitaire.Common/Cards/`: `Suit` (+ `IsRed` extension), `Rank` (Ace=1…King=13), `Card`, `Deck` (Fisher-Yates shuffle on `System.Random` — uses `System.Random` rather than `GameRandom` so the model has zero engine dependency, easier to test in isolation), `Pile` base + `StockPile`/`WastePile`/`FoundationPile`/`TableauPile`, `Rules` static, `GameState` with `Deal(Random)`, `DrawThree()` (recycles waste → stock face-down when stock is empty), and `IsWon`.

**Deviation from original plan:** tests live in `samples/Solitaire/Solitaire.Tests/` rather than `tests/FlatRedBall2.Tests/Cards/`. Putting sample tests in the engine-test project would require a `ProjectReference` from engine tests onto a sample (backwards coupling); a sample-local test project is the standard separation. 25 tests pass (`dotnet test samples/Solitaire/Solitaire.Tests/`).

### Phase 3 archive — original spec (kept for reference)

Pure C# in `samples/Solitaire/Solitaire.Common/Cards/`:

- `enum Suit { Spades, Hearts, Clubs, Diamonds }` + `IsRed()` extension
- `enum Rank { Ace=1, Two…King=13 }`
- `class Card { Suit; Rank; bool IsFaceUp; }`
- `class Deck` — 52 cards, `Shuffle(GameRandom)`
- `class Pile` (base) → `StockPile`, `WastePile`, `FoundationPile` (one per suit), `TableauPile`
- `static class Rules` — `CanPlaceOnTableau` (alternating color, descending; empty accepts King), `CanPlaceOnFoundation` (same suit, ascending from Ace)
- `class GameState` — 1 stock, 1 waste, 4 foundations, 7 tableaus. `Deal()`, `DrawThree()` with empty-stock recycle.

xUnit tests in `tests/FlatRedBall2.Tests/Cards/{RulesTests.cs, GameStateTests.cs}` for `Rules` + `GameState.DrawThree` recycling. Pure logic, no engine — easy confidence wins.

### ✅ Phase 4 — Layout and rendering (DONE — pending visual playtest)

`Solitaire.Common/Entities/CardEntity.cs` wraps `CardGum`, exposes `Card? Model { get; set; }` whose setter drives the three CardGum state categories (Suit/Rank/Facing). `GameScreen.CustomInitialize` builds a `Factory<CardEntity>`, deals a fresh `GameState`, and spawns one entity per card placed at world coords:

- Card: 80×112; column spacing 105; FaceDownOffset 12; FaceUpOffset 28
- World coords are centered (default camera 1280×720, Y+ up); top row at y=260, tableau row at y=100 stacking down (decreasing Y)
- Stock at x=-540, waste at -435, foundations at 220/325/430/535, tableaus at -540 + col*105
- All 52 cards spawn even when stacked (foundations empty → no spawn there at deal-time)

Both Desktop (net10.0) and BlazorGL (net8.0) build clean.

### Phase 4 archive — original spec (kept for reference)

- `CardEntity : Entity` wraps `CardGum`, exposes `Card Model { get; set; }`. Setter applies Suit/Rank/Facing state via codegen-typed setters.
- `GameScreen` constants for card size, tableau spacing, foundation x-offsets, stock/waste anchors — derived from 1280×720 canvas.
- `GameScreen` builds and positions one `CardEntity` per card. Tableau cards stack vertically (smaller offset for face-down, larger for face-up).

### ⏳ Phase 5 — Mouse/touch input

Mouse/touch only (no keyboard, no gamepad — Solitaire idiom).

- Click stock → draw 3 to waste.
- Click empty stock → recycle waste.
- Click+drag a card or face-up tableau run → release on target → `Rules` validates → move on success / snap back on failure.
- Double-click → auto-send to foundation if legal.

`InputManager.Mouse.Position` works on both backends. Touch on Blazor maps to mouse events automatically (verify with browser touch emulation).

### ⏳ Phase 6 — Win + restart overlay

`WinOverlayGum` component with "You Win!" text + Forms `ButtonStandard`. Detect 52-cards-in-foundation. Button click → `GameState.Deal()` + rebuild `CardEntity` instances.

### Phase 7 (optional polish; not must-ship)

Tween card moves, auto-flip newly-exposed tableau top, win animation cascade.

## Friction encountered (and how it was resolved)

### `gumcli new` default `forms` template crashes

`gumcli v2026.4.5.1` throws `InvalidOperationException: Forms template resource not found: 'Gum.ProjectServices.Templates.FormsTemplate.EventExport.gum_events.json'`. The Gum source `Tools/Gum.ProjectServices/Templates/FormsTemplate/manifest.txt` lists the file but the file itself isn't on disk, so it isn't embedded.

**Workaround used:** `gumcli new --template empty Content/GumProject/GumProject.gumx`. Despite the name, `empty` still copies all the Forms components/behaviors/standards under the project — the only thing skipped is the missing `EventExport/gum_events.json` step.

Issue write-up at `design/gumcli-forms-template-issue.md` for the user to file in the Gum repo.

### Hand-authored `.gusx` doesn't deserialize cleanly

`GumProject.gumx` is `<Version>2</Version>` (`AttributeVersion`). The format detector `IsElementContentCompact` checks for `<Variable ` (note the **space** — attribute form) vs `<Variable>` (long form). Long-form files fall through to default `XmlSerializer`, but `ScreenSave.Name` ends up null, causing `ObjectFinder.EnableCache` to throw `"The Gum project includes a screen with a null name"`.

**Workaround:** don't hand-author `.gusx`/`.gucx` at all. The Gum editor saves in correct compact format. This is the right split anyway — content authoring is a human task per `frb-skills/content-boundary/SKILL.md`.

## Reused engine APIs

- `FlatRedBallService.Default` (only static)
- `EngineInitSettings.GumProjectFile` — path is **relative to MonoGame Content root**, do **not** prepend `Content/`
- `Screen` lifecycle: `CustomInitialize`, `CustomActivity`, `CustomDestroy`
- `Entity` with `internal set Engine` injection — `Factory<CardEntity>` for spawning
- `Screen.Add(IRenderable)` for Gum visuals
- `InputManager.Mouse` for input (both backends)
- `GameRandom` for deck shuffling

## Skills already loaded (re-load on resume)

In order of relevance to remaining phases:

1. `frb-skills/gumcli/SKILL.md` — codegen workflow rules; remember to delete stale `.Generated.cs` before re-running codegen after a rename
2. `frb-skills/gum-integration/SKILL.md` — `ToGraphicalUiElement()`, mode 3 codegen consumption (instantiate `XxxRuntime` directly)
3. `frb-skills/multiplatform-conversion/SKILL.md` — already followed for csproj wiring
4. `frb-skills/entities-and-factories/SKILL.md` — Phase 4
5. `frb-skills/input-system/SKILL.md` — Phase 5
6. `.claude/skills/content-boundary/SKILL.md` — guides the AI/human split for authoring

`gumcli.exe` lives at `../Gum/Tools/Gum.Cli/bin/Debug/net8.0/gumcli.exe` relative to the FlatRedBall2 git root.

## Verification

1. `dotnet build samples/Solitaire/Solitaire.Desktop/` — ✅ clean
2. `dotnet build samples/Solitaire/Solitaire.BlazorGL/` — ✅ clean
3. `dotnet test tests/FlatRedBall2.Tests/` — runs after Phase 3
4. `dotnet run --project samples/Solitaire/Solitaire.Desktop/` — green felt window (Phase 1) → dealt layout + drag (Phase 4–5) → win overlay (Phase 6)
5. `dotnet run --project samples/Solitaire/Solitaire.BlazorGL/` — same in browser; aspect lock pillarboxes correctly on resize
6. Win-state smoke: hand-edit `Deal()` to put 51 cards in foundations and 1 in waste → confirm overlay shows and "New Game" reshuffles

## Open risks

- **State category composition in Gum runtime** — assumed independent categories compose because they touch disjoint child variables. Validate in Phase 2 with a single 3-state-applied test card before authoring all 52 states.
- **Touch on Blazor** — mouse/touch should "just work" but confirm in Phase 5 with browser touch emulation. Audio is gated by user gesture; not relevant for Solitaire unless we add SFX.
- **`ButtonStandard` for Win overlay** — `gumcli new --template empty` shipped the components, so available. Verify in Phase 6.

## Next action (user)

Open `samples/Solitaire/Solitaire.Common/Content/GumProject/GumProject.gumx` in the Gum editor, author **GameScreenGum** and **CardGum** per Phase 2 above, save, then return to Claude with "ready for codegen" (or similar). Claude resumes with `gumcli codegen-init` + `gumcli codegen`, swaps `GameScreen.cs` to instantiate the generated `GameScreenGumRuntime`, and starts Phase 3.

If you'd rather skip the editor round-trip and accept code-only Gum (no `.gumx` for screens/components), say so — Claude will pivot the remaining phases accordingly. **The user originally requested a Gum project specifically, so the editor path is the default.**
