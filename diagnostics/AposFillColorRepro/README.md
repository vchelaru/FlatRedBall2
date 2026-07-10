# AposFillColorRepro — issue #663, pure Apos.Shapes (no FlatRedBall2)

Isolates the macOS DesktopGL bug where a **black `FillRectangle` renders blue** (the blue channel is
forced to 1.0). No FlatRedBall2 — just MonoGame + Apos.Shapes — so it proves the bug lives in
Apos.Shapes × the macOS GL driver, and gives a clean bed for shader experiments and an upstream issue.

Uses FRB2's precompiled `apos-shapes.xnb` (via `AposShapesPrecompiled.props`) so no Wine is needed.

## Run

```
dotnet run
```

A black rectangle on light gray. On macOS it renders **blue**; on Windows it's correct (black).

## Find the healer (the key experiment)

Real projects "self-heal" — only the first filled shape is wrong. Press number keys to change what
is drawn **before** the black rectangle each frame and watch its color:

| Key | Drawn before the fill | Prediction |
|-----|------------------------|------------|
| `0` | nothing | **blue** (baseline) |
| `1` | a `SpriteBatch` sprite (plain, unit 0) | still blue? |
| `2` | an Apos **textured** `Draw` (Apos's textured shader branch) | **black**? |
| `3` | another Apos fill (`FillCircle`, same branch as the bug) | still blue? |

Whichever mode turns the rectangle **black** is the mechanism. Note which key(s) heal it.

## Going deeper (shader)

To hack the shader, swap the `Apos.Shapes` `PackageReference` in the `.csproj` for a `ProjectReference`
to your local Apos.Shapes source. (You'll then need the content pipeline to compile `apos-shapes.fx`,
which on macOS means Wine — or edit/compile the shader on Windows and copy the `.xnb`.)
