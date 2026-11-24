---
name: animation-editor-tiled
description: "AnimationEditor Tiled .tsx support — opening a tileset as a native project. Triggers: .tsx, TsxWriter, NativeTsxAnimationSync, IsNativeTsxProject, ParentId, multi-tile animation."
---

# AnimationEditor — Tiled Tilesets

The editor opens a Tiled `.tsx` as a native project (`ProjectManager.LoadTsxProject`,
`IsNativeTsxProject`). Tile animations load as chains (`TiledAnimationToAchjMapper`) and save back
into the `.tsx` (`NativeTsxAnimationSync`, `TsxWriter`). Code lives in `AnimationEditor.Core/Tiled/`.
General editor layout is in the `animation-editor` skill.

The older achx→tsx push sync (`.tiledsync` files, `TilesetAnimationSync`) is not recommended.
Don't build on it or document it.

## Landmines

- **A tile animation stores only rects and durations.** Flips, offsets, color, collision shapes, events, keyframe data and Loop=off are dropped on save. `TsxLossyDataCheck` reports what was dropped. Don't rely on the inspector hiding those controls, because other entry points (paste, context menu) can still create them.
- **Multi-tile animations need one shared footprint.** A chain wider than one tile is saved as an anchor tile plus satellite tiles, each tagged with a `ParentId` property. If any frame's size differs from the others, the whole chain's animation is silently dropped. `FrameFootprintSync` keeps frames in lockstep when one is resized.
- **`TsxWriter` writes every tileset construct, but only by a recovery step where DotTiled loses data.** The `<transformations>` element (all four flags) is written as Tiled does. Custom class properties (with nested members) and enum properties are written; DotTiled flattens an enum to a plain string/int and drops its `propertytype`, so `TsxLoader` swaps it for `EnumReferenceProperty`. Wangsets and per-tile object layers are written (a layer is compared by its rendered XML, so an object edit always saves); DotTiled drops a wangset's `type`, so the writer re-reads it from the existing file by wangset name. `TsxCompatibilityChecker` checks this before open. The writer patches the file in place, so unchanged tiles keep their original bytes.
- **Invalid groups are kept, not dropped.** A dangling, chained or backward `ParentId` becomes its own anchor, and `TsxAnimationValidator` flags it. That flag is the warning icon in the tree.
