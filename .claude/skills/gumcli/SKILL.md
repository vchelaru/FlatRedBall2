---
name: gumcli
description: "Gum CLI tool for FlatRedBall2. Use when creating a game or sample that will use Gum UI (buttons, menus, labels, text boxes, HUD). BEFORE writing any Gum-related code, ask the user whether to use the Gum tool (gumcli.exe) or Gum in code only. Covers locating gumcli.exe, running gumcli new, .csproj content includes, and optional codegen."
---

# Gum CLI Setup

Use this skill **before writing any Gum UI code**. First ask the user which Gum mode they want:

> "Should this project use **Gum tool** (gumcli generates a .gumx project file, editable in the Gum editor) or **Gum code-only** (UI defined entirely in C# with no .gumx file)?"

If the user chooses **Gum code-only**, skip this skill and proceed with the `gum-integration` skill.

If the user chooses **Gum tool**, follow the steps below.

---

## Step 1 — Locate gumcli.exe

gumcli is not yet published as a dotnet tool. <!-- TODO: add `dotnet tool install` instructions once published -->

Find it via a relative path from the FlatRedBall2 git root:

```bash
# Get the git root of the current repo
GIT_ROOT=$(git rev-parse --show-toplevel)

# gumcli lives in the sibling Gum repo
GUMCLI="$GIT_ROOT/../Gum/Gum.Cli/bin/Debug/net8.0/gumcli.exe"
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

---

## Step 3 — Add Content Includes to .csproj

Add a wildcard include so all Gum project files are copied to output:

```xml
<ItemGroup>
  <Content Include="Content\GumProject\**" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

---

## Step 4 — Ask About Codegen (Optional)

Ask the user:

> "Do you want to use **gumcli codegen** to generate C# classes from your Gum elements? This lets you reference Gum screens and components as strongly-typed C# classes. If you're not sure, you can skip this for now."

If the user says **yes**:

- `codegen` requires a `ProjectCodeSettings.codsj` file with `CodeProjectRoot` configured — this is set up via the Gum editor, not gumcli. Note this limitation to the user and suggest they configure it in the editor first.
- Once configured, generate all elements:
  ```bash
  "$GUMCLI" codegen Content/GumProject/GumProject.gumx
  ```
- Or generate specific elements:
  ```bash
  "$GUMCLI" codegen Content/GumProject/GumProject.gumx --element Button
  ```
- Exit code `0` = success, `1` = elements blocked by errors, `2` = load/config failure.

If the user says **no**, skip codegen.

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
