# .achx XML Authoring Reference

`.achx` files are XML serializations of `AnimationChainListSave`. This reference covers the XML schema for creating or editing `.achx` files directly.

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
| `RelativeY` | No | `0` | Vertical offset from entity origin in world units. For a 16x32 character with origin at the feet, use `16` (half height) to center the sprite |
| `ShapesSave` | No | — | Per-frame collision shapes (not yet implemented in FRB2 — present in the XML format for forward compatibility with FRB1 files) |

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
