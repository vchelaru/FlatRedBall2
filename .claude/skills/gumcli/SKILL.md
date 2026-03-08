---
name: gumcli
description: "Gum CLI tool for FlatRedBall2. Trigger at game/sample START — before any code is written — to ask the user whether the project will use Gum and which mode (code-only, project+dynamic, or project+codegen). Covers locating gumcli.exe, running gumcli new, .csproj content includes, and codegen."
---

# Gum CLI Setup

**Ask the Gum mode question at game-start time** — before any code is written, not when Gum UI is about to be implemented. This affects project structure and cannot be changed easily mid-build.

When starting any new game or sample, ask:

> "Before we start: will this project use Gum for UI (menus, HUD, labels)? If so, which mode?
> 1. **Code-only** — UI defined entirely in C#, no .gumx file, no Gum editor
> 2. **Project + dynamic access** — .gumx project file (editable in the Gum editor), access elements at runtime via `GetFrameworkElementByName<T>()` (returns a cast, no compile-time safety)
> 3. **Project + codegen** — .gumx project file + gumcli generates strongly-typed C# classes; access screens/components directly as typed properties with full IntelliSense"

If the user chooses **code-only (mode 1)**, skip this skill and proceed with the `gum-integration` skill.

If the user chooses **mode 2 or 3**, follow the steps below to create the project. For mode 3, also run codegen after any edits to the Gum XML files.

---

## Step 1 — Locate gumcli.exe

gumcli is not yet published as a dotnet tool. <!-- TODO: add `dotnet tool install` instructions once published -->

Find it via a relative path from the FlatRedBall2 git root:

```bash
# Get the git root of the current repo
GIT_ROOT=$(git rev-parse --show-toplevel)

# gumcli lives in the sibling Gum repo
GUMCLI="$GIT_ROOT/../Gum/Tools/Gum.Cli/bin/Debug/net8.0/gumcli.exe"
```

Verify it exists before proceeding:
```bash
if [ ! -f "$GUMCLI" ]; then
  echo "gumcli.exe not found at $GUMCLI"
  echo "Clone https://github.com/vchelaru/Gum next to FlatRedBall2 and build Gum.Cli."
  exit 1
fi
```

---

## Step 2 — Create the Gum Project

Run from the game/sample project directory:

```bash
"$GUMCLI" new Content/GumProject/GumProject.gumx
```

This creates:
```
Content/GumProject/
  GumProject.gumx
  Screens/
  Components/
  Standards/
  Behaviors/
  ExampleSpriteFrame.png
```

Use `GumProject.gumx` as the default name unless the user requests otherwise.

**The generated project includes ready-to-use Forms controls** (Button, TextBox, CheckBox, ListBox, etc.) as component and standard files under `Components/` and `Standards/`. These controls are fully functional out of the box — no extra setup needed.

**Important**: When adding Forms controls to a screen, define them as instances in the Gum screen `.gusx` XML file (or a component `.gucx` file), not just in C# code. Defining them in the XML lets the user open the project in the Gum editor and tweak visuals after the fact. Only fall back to pure-code instantiation for controls that are fully dynamic (e.g., a list whose count is data-driven at runtime).

---

## Step 3 — Add Content Includes to .csproj

Add a wildcard include so all Gum project files are copied to output:

```xml
<ItemGroup>
  <Content Include="Content\GumProject\**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

---

## Naming Convention (Mode 3 — Codegen)

When using codegen, Gum screen/component names collide with FRB2 `Screen` subclass names if both use the same identifier in the same namespace. **Use a `Gum` suffix on all Gum XML elements** to avoid this:

- Gum XML screen: `MainMenuScreenGum` → generates class `MainMenuScreenGum : FrameworkElement`
- FRB2 screen: `MainMenuScreen : Screen` → instantiates `new MainMenuScreenGum()`

This convention applies to screens and any components that would otherwise conflict with C# class names in the project. Standard controls (Button, Label, etc.) already have unique names and don't need the suffix.

---

## Step 4 — Codegen (Required for Mode 3, Optional for Mode 2)

**Do not manually create `ProjectCodeSettings.codsj`** — run `codegen-init` once to auto-detect the `.csproj` and generate it:

```bash
"$GUMCLI" codegen-init Content/GumProject/GumProject.gumx
```

This sets `CodeProjectRoot`, `RootNamespace`, and `OutputLibrary` automatically. Only needed once per project.

Then run codegen after any edits to Gum XML files:

```bash
"$GUMCLI" codegen Content/GumProject/GumProject.gumx
```

Or generate a specific element only:

```bash
"$GUMCLI" codegen Content/GumProject/GumProject.gumx --element MainMenuScreenGum
```

Exit codes: `0` = success, `1` = elements blocked by errors, `2` = load/config failure.

**Codegen generates two files per element**: `ElementName.Generated.cs` (never edit) and `ElementName.cs` (partial stub with `CustomInitialize()` for UI-specific setup). The FRB2 `Screen` class is separate — do not confuse the Gum `CustomInitialize` partial with the FRB2 screen's `CustomInitialize`.

**Old generated files are NOT deleted automatically** when you rename a Gum element. Always manually delete stale `.Generated.cs` and `.cs` stub files before re-running codegen after a rename.

**Workflow rule (mode 3)**: After any edit to a `.gusx`, `.gucx`, or `.gutx` file — including adding instances, renaming, or changing types — always run `gumcli check` then `gumcli codegen` before writing C# code that references those elements. The generated classes live in the project as regular `.cs` files — check them in alongside the Gum XML.

---

## Step 5 — Verify the Project

After creation, check for errors:

```bash
"$GUMCLI" check Content/GumProject/GumProject.gumx
```

Exit code `0` = no errors. Fix any reported errors before continuing.

---

## Loading the Gum Project at Runtime

See the `gum-integration` skill for how to pass the project path to `EngineInitSettings` and instantiate Gum screens from the project in C#.
