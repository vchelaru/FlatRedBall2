# AnimationEditor Docs: Proposed Structure

Reviewed against Diátaxis (tutorial / how-to / reference / explanation). Each page should be one type.

## Findings on the previous outline

1. **Every page was a how-to except Quick Start and Overview.** There was no reference section, so facts like property meanings and shortcuts had nowhere to live and would leak into how-to pages.
2. **Overview mixed two types.** "What `.achx` is" is explanation; "the window" is reference. Split them.
3. **Topic pages were named by feature, not by goal.** How-to titles should name what the user wants to do ("Align animations"), not what the tool has ("Frame Offsets").
4. **Guides and Frame Offsets are one task.** You look at guides to see misalignment, then shift offsets to fix it. The old docs teach them together. Merge.
5. **Timing is too thin for a page.** Two sections. Fold it into "Build an animation".
6. **The API pages are a different audience** (programmers, not artists). They should be their own nav group, not under the editor.

## Proposed nav

```
AnimationEditor                         (landing page: replaces animationeditor/readme.md)
│   ## What the AnimationEditor is      (one paragraph)
│   ## Supported files                   (.achx, .achj, Tiled .tsx; feature .tsx here)
│   ## Where to go next                  (links to Quick Start and each section)
│
├── Quick Start                          (tutorial)
│   ## Open a folder
│   ## Add an animation
│   ## Drop in a sprite sheet
│   ## Grid + Add Multiple Frames
│   ## Watch it play
│   ## Save
│
├── How-To Guides                        (how-to)
│   ├── Work with project folders
│   │   ## Open and close a project folder
│   │   ## Find files (Images and Animations tabs)
│   │   ## Open, switch, and close files
│   │   ## Save as .achx or .achj
│   │   ## Find which animations use an image
│   ├── Build an animation
│   │   ## Add animations and frames
│   │   ## Add many frames at once
│   │   ## Set frame length
│   │   ## Retime a whole animation
│   ├── Define frame regions
│   │   ## Snap regions to a grid
│   │   ## Select a region with the magic wand
│   │   ## Enter exact pixel coordinates
│   ├── Align animations
│   │   ## Use guides to spot misalignment
│   │   ## Shift a frame with RelativeX / RelativeY
│   ├── Flip and tint frames
│   ├── Organize animations
│   │   ## Rename and reorder
│   │   ## Duplicate and mirror an animation
│   ├── Animate a Tiled tileset (.tsx)
│   │   ## Open a .tsx file
│   │   ## Animate a tile
│   │   ## Name animations (auto "ID:{tileId}" names)
│   │   ## Choose the placed tile (Owner tile)
│   │   ## Fix animations Tiled can't represent (warning icon)
│   ├── Add collision shapes
│   └── Preview an animation
│       ## Playback and speed
│       ## Onion skin
│
├── Reference                            (reference)
│   ├── The window                       (labeled screenshot, one line per panel)
│   ├── Frame properties                 (every inspector field: meaning, units, range)
│   ├── Keyboard shortcuts               (generate from HotkeyRegistry so it can't drift)
│   └── File formats                     (.achx, .achj, and .tsx: what's stored, which runtimes read each)
│
└── Concepts                             (explanation)
    └── How animations work
        ## Animations, frames, and textures
        ## Why frames point at regions instead of holding images
        ## The preview is a reference rendering
        ## How Tiled tilesets map to animations (tiles, tile IDs, multi-tile groups)

Code
│
├── Animations in MonoGame               (how-to)
└── Reading Raw Animation Data           (how-to)
```

## Notes

- **Quick Start stays thin.** Bare steps with a screenshot each; every step links to its how-to page. No explanation. It replaces Your First Animation.
- **Reference pages should be generated where possible.** Keyboard shortcuts can come straight from `AnimationEditor.Core/Hotkeys/HotkeyRegistry.cs`; screenshots come from the headless harness (`animation-editor-screenshots` skill). Hand-written reference goes stale fastest.
- **Delete** `animationeditor/page-1.md` and the empty `animationeditor/api/README.md`.
- **Promote Tiled support up front.** The docs landing page and the first line of Quick Start should say the editor works with both `.achx` and Tiled `.tsx` tilesets, and link to "Animate a Tiled tileset". A feature buried in the how-to list isn't promoted.
- **PixiJS export is not documented.** It's experimental; add a page when someone asks.
