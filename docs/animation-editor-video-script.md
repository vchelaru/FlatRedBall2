# Animation Editor Video Script

This is a recording plan, not a finished video. The actual video production is not done in this repo.

## Goal

Show how to open an `.achx`, edit chains and frames, add shapes, preview animation, and use the Avalonia-port workflows that speed up day-to-day work.

## Target length

6–8 minutes

## Demo assets to gather

- FRB Guy sprite
- `samples/AnimationChainSample/Content/hero.achx`
- `samples/PlatformKing/PlatformKing.Common/Content/Animations/PlayerAnimations.achx`
- `samples/PlatformKing/PlatformKing.Common/Content/Animations/EnemyAnimations.achx`
- `samples/ShmupSpace/ShmupSpace.Common/Content/Animations/ShmupSpace.achx`
- Sample spritesheet PNG
- Example project folder with relative textures
- Optional hitbox-heavy sample animation

## Chapter outline

### 0:00–0:20 — Intro

- State what the Animation Editor does.
- Mention the Avalonia rewrite.
- Show the two-panel layout.

### 0:20–1:00 — Open a file

- Open an `.achx`.
- Point out recent files.
- Show the tree view selecting the current chain.

### 1:00–2:10 — Edit a chain and frames

- Add or duplicate a chain.
- Rename it.
- Reorder frames.
- Change frame length.
- Show chain flip and frame flip controls.

### 2:10–3:20 — Add collision shapes

- Add a rectangle hitbox.
- Add a circle if useful.
- Move and resize shapes in the wireframe.
- Show the shape in the tree and property panel.

### 3:20–4:20 — Preview playback

- Play the animation.
- Pause and stop.
- Toggle loop and onion skin.
- Adjust preview zoom and sprite alignment.

### 4:20–5:20 — Texture workflow

- Open the texture viewer.
- Select a UV region.
- Create a frame from a region.
- Demonstrate drag-and-drop of a PNG onto a chain or frame.

### 5:20–6:20 — Avalonia-port highlights

- Show wireframe texture selection.
- Show grid snap and guide lines.
- Show copy/paste or recent files.
- Mention GIF export and resize texture if time allows.

### 6:20–7:00 — Wrap-up

- Summarize the core workflow.
- Point viewers to the full guide.

## Suggested demo sequence

1. Open a sample `.achx`.
2. Select `WalkLeft`.
3. Duplicate to `WalkRight`.
4. Add or adjust frame timing.
5. Add per-frame hitboxes.
6. Preview with onion skin.
7. Load a spritesheet and create a frame from a region.
8. Drag a PNG onto a frame.
9. Save and show recent files.

## Recording notes

- Keep the cursor movement slow and deliberate.
- Zoom in on the wireframe when editing shapes.
- Use a simple project with clean textures.
- Cut out dead time when dialogs are open.
- Capture the tree view whenever selection changes matter.

## On-screen callouts

- Two-panel layout: wireframe on top, preview on bottom
- Animation Chain
- Frame
- ShapeCollectionSave
- Recent Files
- Onion skin
- Snap to grid
