# .achx / .achj Authoring Reference

`.achx` (XML) and `.achj` (JSON) are two on-disk dialects of the same `AnimationChainListSave` data model — same fields, same semantics, just different serialization. AnimationEditor writes `.achx` by default. This reference covers the XML schema for creating or editing `.achx` files directly; the JSON dialect uses the same field set with camelCase names (see `.achj` below).

## Which dialect gets loaded

`ContentLoader.LoadAnimationChainList(path)` and `AchxLoader.Load(path)` both pick the dialect **by file extension** — `.achj` parses as JSON, anything else (including `.achx`) parses as XML. No caller branching needed; just use the right extension.

For code paths with no file path to inspect (clipboard paste, an in-memory stream), `AnimationChainListSave.FromString`/`FromStream` (and `AchxLoader.Load(Stream)`) instead **content-sniff**: a leading `{` after whitespace/BOM means JSON, anything else parses as XML.

## .achj (JSON) shape

Same data, camelCase keys, no XML wrapper element:

```json
{
  "fileRelativeTextures": true,
  "timeMeasurementUnit": "Undefined",
  "coordinateType": "Pixel",
  "animationChains": [
    {
      "name": "CharacterWalkRight",
      "frames": [
        { "textureName": "AnimatedSpritesheet.png", "frameLength": 0.1,
          "leftCoordinate": 0, "rightCoordinate": 16, "topCoordinate": 0, "bottomCoordinate": 32 }
      ]
    }
  ]
}
```

Optional/default-valued fields (`flipHorizontal`, `relativeX`, shape colors, etc.) are omitted when unset, same as the XML writer omits its equivalent elements.

## Document Structure

```xml
<?xml version="1.0" encoding="utf-8"?>
<AnimationChainArraySave
    xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance"
    xmlns:xsd="http://www.w3.org/2001/XMLSchema">
  <FileRelativeTextures>true</FileRelativeTextures>
  <TimeMeasurementUnit>Undefined</TimeMeasurementUnit>
  <CoordinateType>Pixel</CoordinateType>

  <AnimationChain>
    <!-- chains here -->
  </AnimationChain>
</AnimationChainArraySave>
```

### Root-Level Elements

| Element | Value | Notes |
|---|---|---|
| `FileRelativeTextures` | `true` | Texture paths are relative to the `.achx` file location |
| `TimeMeasurementUnit` | `Undefined` | Always use `Undefined` — `FrameLength` is in seconds regardless |
| `CoordinateType` | `Pixel` | Frame coordinates are in pixels (not UV) |

## AnimationChain

Each `<AnimationChain>` is a named sequence of frames.

```xml
<AnimationChain>
  <Name>CharacterWalkRight</Name>
  <Frame><!-- frame 1 --></Frame>
  <Frame><!-- frame 2 --></Frame>
</AnimationChain>
```

`<Name>` is the string used in `sprite.PlayAnimation("CharacterWalkRight")`.

## Frame Elements

```xml
<Frame>
  <TextureName>AnimatedSpritesheet.png</TextureName>
  <FrameLength>0.1</FrameLength>
  <LeftCoordinate>0</LeftCoordinate>
  <RightCoordinate>16</RightCoordinate>
  <TopCoordinate>0</TopCoordinate>
  <BottomCoordinate>32</BottomCoordinate>
  <FlipHorizontal>true</FlipHorizontal>
  <RelativeY>16</RelativeY>
  <ShapesSave>
    <AARectSaves />
    <AxisAlignedCubeSaves />
    <PolygonSaves />
    <CircleSaves />
    <SphereSaves />
  </ShapesSave>
</Frame>
```

### Frame Element Reference

| Element | Required | Default | Notes |
|---|---|---|---|
| `TextureName` | Yes | — | Relative path to the texture file (e.g., `AnimatedSpritesheet.png`) |
| `FrameLength` | Yes | — | Duration in seconds (e.g., `0.1` = 100ms) |
| `LeftCoordinate` | Yes | — | Left edge of source rectangle in pixels |
| `RightCoordinate` | Yes | — | Right edge of source rectangle in pixels |
| `TopCoordinate` | Yes | — | Top edge of source rectangle in pixels |
| `BottomCoordinate` | Yes | — | Bottom edge of source rectangle in pixels |
| `FlipHorizontal` | No | `false` | Mirror the frame horizontally — used for left-facing variants |
| `RelativeX` | No | `0` | Horizontal offset from entity origin — sprites already draw X-centered, so this is usually `0`. See Ground-Contact Point below |
| `RelativeY` | No | `0` | Marks the frame's ground-contact point above the entity origin — set by eye to match the art, not computed from sprite height. See Ground-Contact Point below |
| `Red`/`Green`/`Blue`/`Alpha`/`ColorOperation` | No | unset | Per-frame color; `ColorOperation` is `Multiply` or `Add`. Unset values inherit from the previous frame in the chain. See the color gotchas in `SKILL.md` |
| `ShapesSave` | No | — | Per-frame collision shapes (`AARectSaves`/`CircleSaves`/`PolygonSaves`) — see `per-frame-shapes.md` |
| `Events` | No | — | `<Event><Name>Footstep</Name><Data>optional</Data></Event>` entries, last in the frame (`.achj`: `"events": [{"name", "data"}]`). FRB2-only: Gum/FRB1 drop them if they re-save the file |

### Ground-Contact Point

`RelativeY` marks where a frame's visual ground contact sits relative to the entity origin — it is not derived from sprite height. Flush-bottom (half-height, e.g. `16` on a 32px-tall frame) is only correct for a strictly side-on camera (Mario-style platformers), where the sprite's bounding-box bottom *is* the ground-contact point. Any camera with vertical tilt — a top-down RPG, isometric, or a platformer with a downward-tilted camera (Donkey Kong Country-style) — shows a sliver of the ground plane, so the contact point sits somewhere inside the bounding box and has to be set by eye per frame. `RelativeX` stays `0` in almost all cases since sprites already draw X-centered on the entity; only an asymmetric frame (a lunging attack, an off-center pivot) needs a nonzero value.

For a top-down game specifically: author every frame's origin (AnimationEditor's origin crosshair, or the Adjust Offsets tool) at the center of the character's feet at ground level — where the feet meet the floor, horizontally centered. Once every frame agrees on that point, the entity's `X`/`Y` *is* that ground position, with no offset math at the call site — moving a character to a new level's spawn point is just `entity.X = doorwayX; entity.Y = doorwayY`.

### Coordinate System

Coordinates are **pixel positions** on the source texture, measured from the **top-left corner** (standard image coordinates):
- `Left`/`Right` = horizontal range (width = Right - Left)
- `Top`/`Bottom` = vertical range (height = Bottom - Top)

Example: a 16x32 frame at column 3, row 1 on a 16px grid:
- `LeftCoordinate` = 48, `RightCoordinate` = 64
- `TopCoordinate` = 32, `BottomCoordinate` = 64

## Common Patterns

### Left/Right Pairs via FlipHorizontal

Define the right-facing animation with normal coordinates, then duplicate for left-facing with `<FlipHorizontal>true</FlipHorizontal>` added to each frame. The source coordinates stay the same — only the render is mirrored.

### Single-Frame Animations

Objects like coins, blocks, or projectiles that have a static pose still use an `AnimationChain` with one `Frame`. This keeps the API uniform — everything goes through `PlayAnimation`.

### Multi-Frame Walk Cycles

Use 2+ frames with equal `FrameLength`. The template uses `0.1` (10 FPS). Increase for slower, decrease for faster.
