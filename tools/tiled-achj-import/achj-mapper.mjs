// Pure logic for converting a FlatRedBall2 AnimationEditor .achj file into Tiled's
// per-tile animation format ({ tileId, duration }[] assigned to Tile.frames). No
// dependency on the `tiled` scripting global, so this can run and be tested under
// plain Node. See achj-import.mjs for the Tiled-facing wiring that calls this.

const EPSILON = 0.001;

export function parseAchj(jsonText) {
  const achj = JSON.parse(jsonText);
  if (!Array.isArray(achj.animationChains)) {
    throw new Error("Not a valid .achj file: missing 'animationChains' array.");
  }
  return achj;
}

export function frameDurationMs(frameLength, timeMeasurementUnit) {
  if (timeMeasurementUnit === "Millisecond") return frameLength;
  // "Second" and "Undefined" both use seconds (AnimationChainListSaveExtensions treats
  // "Undefined" as seconds at runtime).
  return frameLength * 1000;
}

function frameRectPixels(frame, achj, tilesetInfo) {
  if (achj.coordinateType === "UV") {
    return null; // UV coordinateType needs the texture's pixel size, which the Tiled
    // scripting API doesn't expose for tileset images; re-export the .achj with
    // "coordinateType": "Pixel" instead.
  }
  return {
    left: frame.leftCoordinate,
    top: frame.topCoordinate,
    width: frame.rightCoordinate - frame.leftCoordinate,
    height: frame.bottomCoordinate - frame.topCoordinate,
  };
}

function mapFrame(frame, achj, tilesetInfo, warnings, frameIndex, chainName) {
  const label = `chain "${chainName}" frame ${frameIndex}`;

  const textureBaseName = frame.textureName.split(/[\\/]/).pop();
  const tilesetBaseName = tilesetInfo.imageFileName.split(/[\\/]/).pop();
  if (textureBaseName !== tilesetBaseName) {
    warnings.push(`${label}: references a different texture ("${frame.textureName}") than the open tileset ("${tilesetInfo.imageFileName}") - skipped.`);
    return null;
  }

  const rect = frameRectPixels(frame, achj, tilesetInfo);
  if (!rect) {
    warnings.push(`${label}: "UV" coordinateType is not supported - re-export the .achj as "Pixel" coordinateType - skipped.`);
    return null;
  }

  if (Math.abs(rect.width - tilesetInfo.tileWidth) > EPSILON || Math.abs(rect.height - tilesetInfo.tileHeight) > EPSILON) {
    warnings.push(`${label}: frame rect ${rect.width}x${rect.height} doesn't match tile size ${tilesetInfo.tileWidth}x${tilesetInfo.tileHeight} - skipped.`);
    return null;
  }

  if (Math.abs(rect.left % tilesetInfo.tileWidth) > EPSILON || Math.abs(rect.top % tilesetInfo.tileHeight) > EPSILON) {
    warnings.push(`${label}: frame rect origin (${rect.left}, ${rect.top}) is not aligned to the tile grid - skipped.`);
    return null;
  }

  if (frame.flipHorizontal || frame.flipVertical || frame.flipDiagonal) {
    warnings.push(`${label}: uses a flip flag; Tiled tile animation frames can't flip per-frame, so the flip is dropped.`);
  }

  const column = Math.round(rect.left / tilesetInfo.tileWidth);
  const row = Math.round(rect.top / tilesetInfo.tileHeight);
  const tileId = row * tilesetInfo.columnCount + column;

  return { tileId, duration: frameDurationMs(frame.frameLength, achj.timeMeasurementUnit) };
}

export function mapAchjToTiledAnimations(achj, tilesetInfo) {
  return achj.animationChains.map((chain) => {
    const warnings = [];

    if (tilesetInfo.margin || tilesetInfo.tileSpacing) {
      warnings.push(`chain "${chain.name}": tileset has non-zero margin or spacing, which this importer can't account for when computing tile ids - skipped.`);
      return { chainName: chain.name, frames: [], entryTileId: null, warnings };
    }

    const frames = chain.frames
      .map((frame, index) => mapFrame(frame, achj, tilesetInfo, warnings, index, chain.name))
      .filter((frame) => frame !== null);

    return {
      chainName: chain.name,
      frames,
      entryTileId: frames.length > 0 ? frames[0].tileId : null,
      warnings,
    };
  });
}
