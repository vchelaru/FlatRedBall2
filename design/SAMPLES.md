# Samples Catalog

Every sample under `samples/`, grouped by the `frb-skills/` skill it exercises. A sample appears under each skill it uses. A skill with no samples is marked **gap**: that is where the next sample should go.

`engine-overview` is the cross-cutting start-here skill and is not catalogued.

## Samples

| Sample | Genre | Pitch | Controls |
|---|---|---|---|
| `samples/AnimationChainCommonSample` | tech demo | Plays a `.achj` with only `FlatRedBall.AnimationChain.Common` and a raw `SpriteBatch`, no engine. | Space: toggle Walk/Idle. Escape: quit. |
| `samples/AnimationChainSample` | tech demo | `.achx` playback through `AnimationPlayer`, including Multiply/Add color operations. | Space: cycle chain. R: reload `hero.achx`. Escape: quit. |
| `samples/GlueLoaderScratch` | tech demo | Boots an FRB1 Glue project (`.gluj`) directly, with no Glue codegen. | Escape: quit. No other bindings in code. |
| `samples/GumFormsPopupTest` | test bed | Gum Forms ComboBox, Menu, and TextBox popups/hover added through FRB2 screens. | Mouse: open dropdown/menu, click outside to close, hover text box. Escape: quit. |
| `samples/PlatformKing` | platformer | Two linked Tiled levels with double jump, cloud platforms, ladders, water, enemies, and breakable boxes. | A/D or Left/Right: move. Space: jump (hold higher, double jump). Up/Down: climb ladder, swim. Down: drop through cloud. Gamepad: left stick or D-pad, A to jump. Escape: quit. |
| `samples/ShmupSpace` | vertical shmup | Portrait shooter with hot-reloadable JSON tuning and a code-only Gum HUD. | Arrows/WASD or mouse: move. Space or mouse button: fire (hold to auto-fire). Title: Space/Enter/click to start. Escape: quit. |
| `samples/Solitaire` | card game | Klondike draw 3 built on a Gum project with tweened card moves, desktop and web. | Click stock: draw 3. Drag: move cards. Double-click: send to foundation. Ctrl+W (debug builds): show win screen. Escape: quit. |

## By skill

| Skill | Samples |
|---|---|
| `animation` | AnimationChainCommonSample, AnimationChainSample, PlatformKing, ShmupSpace |
| `audio` | **gap** |
| `automation-mode` | ShmupSpace, GlueLoaderScratch |
| `camera` | PlatformKing (`CameraControllingEntity`) |
| `collision-relationships` | PlatformKing, ShmupSpace |
| `content-and-assets` | AnimationChainSample (`TitleContainer` streams), ShmupSpace (JSON config) |
| `content-hot-reload` | ShmupSpace (per-extension dispatch), PlatformKing, Solitaire (screen restart) |
| `desktop-distribution` | **gap** |
| `entities-and-factories` | PlatformKing, ShmupSpace, Solitaire |
| `glue-project-loading` | GlueLoaderScratch |
| `grid-movement` | **gap** |
| `gum-integration` | GumFormsPopupTest, ShmupSpace (code-only), Solitaire (Gum project) |
| `gum-packaging` | Solitaire |
| `gumcli` | Solitaire |
| `hex-grid` | **gap** |
| `input-system` | PlatformKing (keyboard, gamepad), ShmupSpace (keyboard, cursor), Solitaire (cursor drag, double-click) |
| `levels` | PlatformKing |
| `multiplatform-conversion` | PlatformKing, ShmupSpace, Solitaire |
| `path-and-pathfollower` | **gap** |
| `performance` | **gap** |
| `physics-and-movement` | ShmupSpace (velocity) |
| `platformer-movement` | PlatformKing |
| `screens` | PlatformKing (level-to-level), ShmupSpace (title to game) |
| `shaders` | **gap** |
| `shadowdusk` | **gap** |
| `shapes` | ShmupSpace (`AARect`), PlatformKing (`TileShapes`) |
| `tile-grid` | **gap** |
| `tile-node-network` | **gap** |
| `timing` | ShmupSpace (fire cooldown) |
| `tmx` | PlatformKing |
| `top-down-movement` | ShmupSpace |
| `tweening` | Solitaire |

## Adding a sample

Add a row to **Samples**, then add its name to every skill row it exercises. Planned samples go in the skill table too, marked `(planned)`, so they have a home before they are built.
