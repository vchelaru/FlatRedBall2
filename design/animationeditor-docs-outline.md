# AnimationEditor Docs: Proposed Structure

Reviewed against Diátaxis (tutorial / how-to / reference / explanation). Each page should be one type.

## Findings on the previous outline

1. **Every page was a how-to except Quick Start and Overview.** There was no reference section, so facts like property meanings and shortcuts had nowhere to live and would leak into how-to pages.
2. **Overview mixed two types.** "What `.achx` is" is explanation; "the window" is reference. Split them.
3. **Topic pages were named by feature, not by goal.** How-to titles should name what the user wants to do ("Align animations"), not what the tool has ("Frame Offsets").
4. **Guides and Frame Offsets are one task.** You look at guides to see misalignment, then shift offsets to fix it. The old docs teach them together. Merge.
5. **Timing is too thin for a page.** Two sections. Fold it into "Build an animation".
6. **The API pages are a different audience** (programmers, not artists). They get their own Code section, nested under AnimationEditor like the others.
7. **Everything sits under one AnimationEditor section.** GitBook groups can't nest, so How-To Guides, Reference and Code are parent pages (each with its own README listing its children) inside a single AnimationEditor group.
8. **How-to pages cover common operations only, grouped by what you're editing.** Edit Animations is whole-animation actions; Edit Frames is frame actions. Rare options (Add Multiple Frames, Invert Frame Order) are left out. Properties beyond frame length, Loop, and Locked live on Reference ▸ Inspector Properties.
9. **Use the editor's own labels in headings** (Placed Tile, Relative X / Relative Y, Add AxisAlignedRectangle), not internal names.
10. **Tint Frames is cut as a page.** Tinting is a section of Edit Frames instead; the full color field list lives on Reference ▸ Inspector Properties.
11. **Save/undo and single-file work get coverage.** Every user needs them, and they were only mentioned in Quick Start.
12. **No single-child sections.** Concepts would hold one page, so How Animations Work sits directly under AnimationEditor instead.

## Proposed nav

```
AnimationEditor                          (SUMMARY.md group)
│
├── AnimationEditor                      (landing page: animationeditor/readme.md)
│   ## What Is the AnimationEditor
│   ## Supported Files                   (.achx, .achj, Tiled .tsx; feature .tsx here)
│   ## Where to Go Next                  (links to Quick Start and each section)
│
├── Quick Start                          (tutorial)
│   ## Introduction
│   ## Open a Project Folder
│   ## Add an Animation
│   ## Edit Frames
│   ## Add More Frames
│   ## Watch It Play
│   ## Save
│   ## Next Steps
│
├── How-To Guides                        (parent page: how-to/README.md, lists its guides; ordered by how often a new user needs each; file management last since Quick Start covers the basics)
│   ├── Edit animations
│   │   ## Add, rename, and delete an animation
│   │   ## Reorder animations (drag in the tree)
│   │   ## Duplicate an animation (copy and paste)
│   │   ## Flip an animation (e.g. WalkRight from WalkLeft)
│   │   ## Change an animation's speed (Adjust Frame Time)
│   │   ## Turn looping on or off (Loop)
│   │   ## Lock an animation (why the + button disappears)
│   ├── Edit frames
│   │   ## Add frames (+ button, drop a PNG, Ctrl+click, copy/paste, duplicate)
│   │   ## Delete frames
│   │   ## Change what part of the image a frame shows (drag, grid snap, double-click a cell, pixel coordinates, move all frames)
│   │   ## Reorder frames (drag in the tree)
│   │   ## Edit several frames at once (multi-select; mixed values)
│   │   ## Change a frame's image (TEXTURE field, … browse, drop a PNG)
│   │   ## Flip a frame (Horizontal, Vertical, Diagonal)
│   │   ## Change how long a frame shows
│   │   ## Tint a frame (Color Mode + R/G/B/A)
│   │   ## Offset a frame (Relative X / Y, drag in the preview)
│   ├── Preview an animation             (right after editing: used after every change)
│   │   ## Playback and speed
│   │   ## Show the previous frame (onion skin)
│   │   ## Preview several animations together (multi-select stacks them)
│   ├── Align animations
│   │   ## Use guides to spot misalignment
│   │   ## Shift a whole animation (Adjust Offsets)
│   ├── Animate a Tiled tileset (.tsx)
│   │   ## Open a .tsx file
│   │   ## Animate a tile
│   │   ## Name animations (auto "ID:{tileId}" names)
│   │   ## Choose the placed tile (Placed Tile)
│   │   ## Fix animations Tiled can't represent (warning icon)
│   │   ## What isn't available in .tsx projects (flipping, offsets/Adjust Offsets, color, collision shapes; Grid always on)
│   ├── Add collision shapes
│   │   ## Add a rectangle, circle, or polygon (Add AxisAlignedRectangle / Add Circle / Add Polygon)
│   ├── Work with project folders
│   │   ## Introduction
│   │   ## Open and Close a Project Folder
│   │   ## Find Files (Images and Animations Tabs)
│   │   ## Open, Switch, and Close Files
│   │   ## Add Animation Files to a Project
│   │   ## Find Which Animations Use an Image
│   │   ## Work without a project folder      (File ▸ New Animation / Load… / Load Recent)
│   └── Save and undo changes
│       ## Save and Save As
│       ## Auto-save
│       ## Undo, redo, and the History tab
│       ## Reload from disk
│
├── How Animations Work                  (explanation: concepts/how-animations-work.md; before Reference)
│   ## Animations, frames, and textures
│   ## Why frames point at regions instead of holding images
│   ## The preview is a reference rendering
│   ## How Tiled tilesets map to animations (tiles, tile IDs, multi-tile groups)
│
├── Reference                            (parent page: reference/README.md, lists its pages)
│   ├── The window                       (labeled screenshot, one line per panel)
│   ├── Inspector properties             (every animation and frame field, incl. Loop, Locked, Placed Tile, color: meaning, units, range)
│   ├── Keyboard shortcuts               (generate from HotkeyRegistry so it can't drift)
│   └── File formats                     (.achx, .achj, and .tsx: what's stored, which runtimes read each)
│
└── Code                                 (parent page: api/README.md, lists its pages)
    ├── Animations in MonoGame           (how-to)
    └── Reading Raw Animation Data       (how-to)
```

## Notes

- **Quick Start stays thin.** Bare steps with a screenshot each; every step links to its how-to page. No explanation. It replaces Your First Animation.
- **Reference pages should be generated where possible.** Keyboard shortcuts can come straight from `AnimationEditor.Core/Hotkeys/HotkeyRegistry.cs`; screenshots come from the headless harness (`animation-editor-screenshots` skill). Hand-written reference goes stale fastest.
- **Parent pages** (`how-to/README.md`, `reference/README.md`, `api/README.md`) are real pages in GitBook: a sentence on what the section is for, then links to its children.
- **The landing page's How-To Guides link** points at `how-to/README.md` once it exists.
- **Promote Tiled support up front.** The docs landing page and the first line of Quick Start should say the editor works with both `.achx` and Tiled `.tsx` tilesets, and link to "Animate a Tiled tileset". A feature buried in the how-to list isn't promoted.
- **PixiJS export is not documented.** It's experimental; add a page when someone asks.

- **Choose What a Frame Shows was merged into Edit Frames.** Region editing is a section there. The magic wand is intentionally undocumented for now (rarely used).
