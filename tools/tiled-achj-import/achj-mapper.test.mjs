import { test } from "node:test";
import assert from "node:assert/strict";
import { parseAchj, frameDurationMs, mapAchjToTiledAnimations } from "./achj-mapper.mjs";

const tilesetInfo = {
  tileWidth: 16,
  tileHeight: 32,
  columnCount: 4,
  margin: 0,
  tileSpacing: 0,
  imageFileName: "AnimatedSpritesheet.png",
};

function achjText(overrides = {}) {
  return JSON.stringify({
    fileRelativeTextures: true,
    timeMeasurementUnit: "Second",
    coordinateType: "Pixel",
    animationChains: [
      {
        name: "Walk",
        frames: [
          { textureName: "AnimatedSpritesheet.png", frameLength: 0.1, leftCoordinate: 0, rightCoordinate: 16, topCoordinate: 0, bottomCoordinate: 32 },
          { textureName: "AnimatedSpritesheet.png", frameLength: 0.1, leftCoordinate: 16, rightCoordinate: 32, topCoordinate: 0, bottomCoordinate: 32 },
        ],
      },
    ],
    ...overrides,
  });
}

test("parseAchj parses a well-formed .achj file", () => {
  const achj = parseAchj(achjText());
  assert.equal(achj.coordinateType, "Pixel");
  assert.equal(achj.animationChains.length, 1);
  assert.equal(achj.animationChains[0].name, "Walk");
});

test("parseAchj rejects a file with no animationChains array", () => {
  assert.throws(() => parseAchj(JSON.stringify({ coordinateType: "Pixel" })), /animationChains/);
});

test("frameDurationMs converts seconds to milliseconds", () => {
  assert.equal(frameDurationMs(0.1, "Second"), 100);
});

test("frameDurationMs passes milliseconds through unchanged", () => {
  assert.equal(frameDurationMs(150, "Millisecond"), 150);
});

test("mapAchjToTiledAnimations maps grid-aligned pixel frames to tile ids", () => {
  const achj = parseAchj(achjText());
  const [result] = mapAchjToTiledAnimations(achj, tilesetInfo);
  assert.equal(result.chainName, "Walk");
  assert.equal(result.warnings.length, 0);
  assert.deepEqual(result.frames, [
    { tileId: 0, duration: 100 },
    { tileId: 1, duration: 100 },
  ]);
  assert.equal(result.entryTileId, 0);
});

test("mapAchjToTiledAnimations computes tile id from row and column", () => {
  const achj = parseAchj(
    achjText({
      animationChains: [
        {
          name: "SecondRow",
          frames: [
            { textureName: "AnimatedSpritesheet.png", frameLength: 0.2, leftCoordinate: 32, rightCoordinate: 48, topCoordinate: 32, bottomCoordinate: 64 },
          ],
        },
      ],
    })
  );
  const [result] = mapAchjToTiledAnimations(achj, tilesetInfo);
  // row 1 (y=32/32), column 2 (x=32/16), columnCount=4 -> tileId = 1*4 + 2 = 6
  assert.deepEqual(result.frames, [{ tileId: 6, duration: 200 }]);
});

test("mapAchjToTiledAnimations skips a frame whose rect doesn't match the tileset's tile size", () => {
  const achj = parseAchj(
    achjText({
      animationChains: [
        {
          name: "WrongSize",
          frames: [
            { textureName: "AnimatedSpritesheet.png", frameLength: 0.1, leftCoordinate: 0, rightCoordinate: 20, topCoordinate: 0, bottomCoordinate: 32 },
          ],
        },
      ],
    })
  );
  const [result] = mapAchjToTiledAnimations(achj, tilesetInfo);
  assert.equal(result.frames.length, 0);
  assert.match(result.warnings[0], /doesn't match tile size/);
});

test("mapAchjToTiledAnimations skips a frame not aligned to the tile grid", () => {
  const achj = parseAchj(
    achjText({
      animationChains: [
        {
          name: "Unaligned",
          frames: [
            { textureName: "AnimatedSpritesheet.png", frameLength: 0.1, leftCoordinate: 4, rightCoordinate: 20, topCoordinate: 0, bottomCoordinate: 32 },
          ],
        },
      ],
    })
  );
  const [result] = mapAchjToTiledAnimations(achj, tilesetInfo);
  assert.equal(result.frames.length, 0);
  assert.match(result.warnings[0], /not aligned to the tile grid/);
});

test("mapAchjToTiledAnimations skips a frame that references a different texture", () => {
  const achj = parseAchj(
    achjText({
      animationChains: [
        {
          name: "OtherTexture",
          frames: [
            { textureName: "OtherSheet.png", frameLength: 0.1, leftCoordinate: 0, rightCoordinate: 16, topCoordinate: 0, bottomCoordinate: 32 },
          ],
        },
      ],
    })
  );
  const [result] = mapAchjToTiledAnimations(achj, tilesetInfo);
  assert.equal(result.frames.length, 0);
  assert.match(result.warnings[0], /different texture/);
});

test("mapAchjToTiledAnimations keeps a flipped frame but warns the flip is dropped", () => {
  const achj = parseAchj(
    achjText({
      animationChains: [
        {
          name: "Flipped",
          frames: [
            { textureName: "AnimatedSpritesheet.png", frameLength: 0.1, leftCoordinate: 0, rightCoordinate: 16, topCoordinate: 0, bottomCoordinate: 32, flipHorizontal: true },
          ],
        },
      ],
    })
  );
  const [result] = mapAchjToTiledAnimations(achj, tilesetInfo);
  assert.equal(result.frames.length, 1);
  assert.match(result.warnings[0], /flip.*dropped/i);
});

test("mapAchjToTiledAnimations refuses to map a tileset with margin or spacing", () => {
  const achj = parseAchj(achjText());
  const marginedTileset = { ...tilesetInfo, margin: 2 };
  const [result] = mapAchjToTiledAnimations(achj, marginedTileset);
  assert.equal(result.frames.length, 0);
  assert.match(result.warnings[0], /margin or spacing/);
});
