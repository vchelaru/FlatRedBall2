---
name: gum-integration
description: "Gum Integration in FlatRedBall2. Use when working with UI, HUD, menus, buttons, labels, text display, StackPanel, Panel, layout, Gum Forms controls, Add, screen-space vs world-space UI, or any Gum-related question. Also trigger when user asks about displaying text on screen."
---

# Gum Integration in FlatRedBall2

Gum is FlatRedBall2's UI system, backed by the `Gum.MonoGame` NuGet package. It is **automatically initialized** by `FlatRedBallService.Initialize` — no setup required.

## Three Gum Usage Modes

Choose the mode that matches the project setup (see `gumcli` skill for how to create the project):

| Mode | Description | When to use |
|------|-------------|-------------|
| **Code-only** | No `.gumx` file. All controls created in C# with `new Button()` etc. | Prototypes, no Gum editor needed |
| **Project + dynamic access** | `.gumx` loaded; retrieve named elements at runtime via `GetFrameworkElementByName<T>()` | Editor integration, no codegen |
| **Project + codegen** | `.gumx` loaded + gumcli generates strongly-typed C# classes; instantiate and use typed properties directly | Best DX; recommended for new projects |

## Two Levels of Gum API

### Forms Controls — interactive UI (`Gum.Forms.Controls`)

High-level controls with built-in click, hover, focus, and keyboard navigation:

| Control | Use for |
|---------|---------|
| `Button` | Clickable buttons |
| `Label` | Display-only text |
| `TextBox` | Text input |
| `CheckBox` | Toggle on/off |
| `StackPanel` | Vertical/horizontal layout container |
| `Panel` | Free-layout container |

Use Forms controls for menus, buttons, and any interactive element. `Label` is also the right choice for simple score/status text.

### Visual Types — non-interactive rendering (`MonoGameGum.GueDeriving`)

Raw visual objects with no built-in input handling. Use these for non-interactive HUD elements — health bars, icons, solid-color shapes — when no Forms control fits:

| Type | Use for |
|------|---------|
| `TextRuntime` | Text with custom `FontSize`/`FontScale` not exposed by `Label` |
| `ColoredRectangleRuntime` | Solid-color rectangle (health bars, UI frames, heart indicators) |
| `SpriteRuntime` | Textured image |

```csharp
using MonoGameGum.GueDeriving;

// Health bar fill
var fill = new ColoredRectangleRuntime { Width = 100, Height = 12, Color = Color.Red };
fill.Anchor(Anchor.TopLeft);
fill.X = 20; fill.Y = 20;
Add(fill);

// Shrink it as health decreases:
fill.Width = _health / _maxHealth * 100f;
```

**Rule: use Forms controls for interactive elements. Use visual types directly for non-interactive HUD (health indicators, icons, status bars).**

## Quick Start

```csharp
// Always import BOTH — Forms controls and Wireframe (Anchor/Dock) live in different namespaces:
using Gum.Forms.Controls;  // Label, Panel, StackPanel, Button, etc.
using Gum.Wireframe;        // Anchor, Dock

// In Screen.CustomInitialize():
var button = new Button();
button.Text = "Click Me";
button.Click += (_, _) => Debug.WriteLine("clicked");

Add(button);   // pass FrameworkElement directly
```

## Add / Remove (Gum elements)

`Add` registers the element for rendering **and** per-frame input updates (cursor, clicks, hover, keyboard). All Forms controls work out of the box — no additional input wiring needed.

Always use `Add` instead of adding directly to `RenderList`:

```csharp
Add(button);          // FrameworkElement
Add(textRuntime);     // GraphicalUiElement (low-level visual)
Remove(button);
Remove(textRuntime);
```

**Do not call `button.AddToRoot()`** — this bypasses the FRB2 render-order system.

## Displaying Text (HUD, Score, Labels)

**Option A — `Label` (preferred)**:

```csharp
var scoreLabel = new Label();
scoreLabel.Text = "0";
scoreLabel.Anchor(Anchor.TopRight);
scoreLabel.X = -20;
scoreLabel.Y = 20;
Add(scoreLabel);

// Update at runtime:
scoreLabel.Text = _score.ToString();
```

**Option B — `TextRuntime` (low-level)**: Use only when you need `FontSize`, `FontScale`, etc. not exposed by `Label`.

```csharp
using MonoGameGum.GueDeriving;   // TextRuntime lives here, NOT Gum.Wireframe

var scoreText = new TextRuntime { Text = "0", FontSize = 48 };
Add(scoreText);
```

## Render Ordering (Layer / Z)

| Setting | Effect |
|---|---|
| `Layer = null` (default) | Drawn last — on top of all world objects. Right for HUDs and menus. |
| `z: 50f` parameter | Control ordering among multiple Gum elements or between Gum and sprites |

```csharp
Add(bgPanel, z: 0f);
Add(hudPanel, z: 100f);
```

## Screen-Space vs World-Space

**Screen-space (default)**: `screen.Add(element)` places elements in Gum's native coordinate system (pixels, Y-down, origin top-left). Use for HUDs, menus.

**World-space**: `entity.Add(element)` places a Gum element at the entity's world position. It follows the entity and shifts when the camera pans. The visual is automatically removed when the entity is destroyed.

## Loading Gum Screens from a .gumx Project File

When a `.gumx` project is loaded (via `EngineInitSettings.GumProjectFile`), you can instantiate a Gum screen defined in the project and add it to the FRB2 screen (e.g., as a background visual):

```csharp
using Gum.Forms;     // GetFrameworkElementByName extension method
using Gum.Managers;  // ObjectFinder
using MonoGameGum;   // ToGraphicalUiElement — easy to miss, different namespace from Gum.DataTypes

var gumScreenSave = ObjectFinder.Self.GumProjectSave.Screens
    .Find(s => s.Name == "MainMenuScreen");
Add(gumScreenSave!.ToGraphicalUiElement());
```

- `using MonoGameGum;` is required even though `ScreenSave` lives in `Gum.DataTypes` — the extension method is in `MonoGameGum`.
- **`GumProjectFile` path must NOT include `Content/`** — Gum's `FileManager` is already rooted at the MonoGame `Content/` directory. Use `"GumProject/GumProject.gumx"`, not `"Content/GumProject/GumProject.gumx"`. The double-`Content` causes a runtime load failure.

### If the project was created with gumcli

`gumcli new` scaffolds the project with **all standard Forms controls already included** as component and standard files (Button, TextBox, CheckBox, ListBox, etc.). These are immediately available — no additional setup required.

**Prefer defining Forms control instances in Gum XML files** (screen `.gusx` or component `.gucx`) rather than creating them purely in C# code. XML-defined instances are visible in the Gum editor so designers can adjust layout and visuals without touching code. Only instantiate controls in C# when they are fully dynamic (e.g., a variable-length list driven by runtime data).

### Mode 2 — Dynamic access via Get calls

When the Gum screen XML is loaded with `ToGraphicalUiElement()`, any Forms control instances declared in it are constructed and wired automatically — just hook up events in C#:

```csharp
// using Gum.Forms;            // GetFrameworkElementByName extension method
// using Gum.Forms.Controls;   // Button, Label, etc.
// using MonoGameGum;          // ToGraphicalUiElement

var root = gumScreenSave!.ToGraphicalUiElement();
Add(root);

// Retrieve a named instance and hook up events:
var startButton = root.GetFrameworkElementByName<Button>("StartButton");
startButton.Click += (_, _) => MoveToScreen<GameScreen>();
```

- `GetFrameworkElementByName<T>` is an extension method in `Gum.Forms` — add `using Gum.Forms;`.
- `Button` and other Forms types are in `Gum.Forms.Controls` — do **not** use `MonoGameGum.Forms.Controls`; those wrappers are `[Obsolete(error: true)]`.
- Forms controls (`new Button()`, etc.) created in C# still use built-in default visuals unless the project includes matching component files. Mix freely.

### Mode 3 — Codegen (strongly-typed)

After running `gumcli codegen`, each Gum screen and component gets a generated C# class. Instantiate the class directly — no `ToGraphicalUiElement()` or string-based lookup needed:

```csharp
// Generated class for "MainMenuScreen" Gum screen:
var mainMenu = new MainMenuScreenRuntime();
Add(mainMenu);

// Properties match the instance names defined in the Gum XML:
mainMenu.StartButton.Click += (_, _) => MoveToScreen<GameScreen>();
mainMenu.QuitButton.Click += (_, _) => Exit();
```

- Generated classes are named `<ElementName>Runtime` by convention (e.g., `MainMenuScreenRuntime`, `PauseMenuRuntime`).
- Accessing a property that doesn't exist is a compile error — much safer than string-based `GetFrameworkElementByName`.
- After any edit to the Gum XML, re-run `gumcli codegen` before referencing new/renamed instances in C#.
- `Add(mainMenu)` works the same as other modes — pass the generated runtime object directly.

## Showing / Hiding a Control

The API differs by type:

- **`FrameworkElement`** (Label, Button, etc.) — use `IsVisible`:
  ```csharp
  label.IsVisible = false;
  label.IsVisible = true;
  ```
- **Visual types** (`ColoredRectangleRuntime`, `TextRuntime`, `SpriteRuntime`) — use `Visible`:
  ```csharp
  rect.Visible = false;
  rect.Visible = true;
  ```

## Gotchas

- **Namespace**: `TextRuntime` is in `MonoGameGum.GueDeriving`. Forms controls (`Button`, `Label`, etc.) are in `Gum.Forms.Controls`. `Anchor`/`Dock` enums are in `Gum.Wireframe`. `GetFrameworkElementByName` extension is in `Gum.Forms`. Do **not** use `MonoGameGum.Forms.Controls` — all types there are `[Obsolete(error: true)]`.
- **Visibility by type** — `FrameworkElement` uses `.IsVisible`; visual types (`ColoredRectangleRuntime`, etc.) use `.Visible`. Do not use `element.Visual.Visible` directly on FrameworkElement.
- **Gum coordinates are screen pixels, Y-down** — opposite of the game world (Y-up, centered). Use `Anchor`/`Dock` to avoid hard-coding pixel positions.
- **Initialize order**: Do not create Gum elements before `FlatRedBallService.Initialize`.
- **`AddToRoot()` is NOT the FRB2 pattern**. Use `screen.Add(element)` instead.
- **No persistence across screen transitions** — Gum elements are fully cleaned up. Add them fresh in each screen's `CustomInitialize`.
- **World-space Gum**: Do not manually set `Visual.X/Y` on an entity-attached Gum element — it will be overwritten each frame.

## Pattern: Transient World-Space Text (e.g., Floating Score)

Create a short-lived entity that owns a Gum visual at its world position:

```csharp
class ScoreFloater : Entity
{
    private float _lifetime;
    private TextRuntime _label = new TextRuntime { FontSize = 32 };

    public int Score { set => _label.Text = $"+{value}"; }

    public override void CustomInitialize()
    {
        VelocityY = 80f;
        Add(_label);   // world-space — floats up with the entity
    }

    public override void CustomActivity(FrameTime time)
    {
        _lifetime += time.DeltaSeconds;
        if (_lifetime > 1.2f) Destroy();
    }
}

// Spawn: var floater = Engine.GetFactory<ScoreFloater>().Create();
```

Physics moves the entity; the Gum visual follows automatically.

## Layout Essentials

### Anchor and Dock

`Anchor` and `Dock` (in `Gum.Wireframe`) are the primary layout tools:

```csharp
label.Anchor(Anchor.BottomRight);   // pin to corner; X/Y offset inward from there
panel.Dock(Dock.Fill);              // fill parent entirely
panel.Dock(Dock.SizeToChildren);    // shrink-wrap content (default for Panel)
```

Valid `Anchor` values: `TopLeft`, `Top` (centered horizontally), `TopRight`, `BottomLeft`, `BottomRight`, `Center`.
**`Anchor.TopCenter` does not exist — use `Anchor.Top` for centered-top placement.**

### StackPanel

Stacks children vertically (default) or horizontally with optional spacing:

```csharp
var menu = new StackPanel();
menu.Spacing = 8;
menu.Anchor(Anchor.Center);

menu.AddChild(new Button { Text = "Start" });
menu.AddChild(new Button { Text = "Quit" });
Add(menu);
```

For horizontal: `new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 }`.

## Reference Files

For detailed positioning/size units (XUnits, YUnits, WidthUnits, HeightUnits) and Panel documentation, see:
- `references/layout.md` — Full unit tables and additional layout patterns
