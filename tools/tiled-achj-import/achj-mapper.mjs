// Pure logic for converting a FlatRedBall2 AnimationEditor .achj/.achx file into
// Tiled's per-tile animation format ({ tileId, duration }[] assigned to Tile.frames).
// No dependency on the `tiled` scripting global, so this can run and be tested under
// plain Node. See achj-import.mjs for the Tiled-facing wiring that calls this.

const EPSILON = 0.001;

export function parseAchj(jsonText) {
  const achj = JSON.parse(jsonText);
  if (!Array.isArray(achj.animationChains)) {
    throw new Error("Not a valid .achj file: missing 'animationChains' array.");
  }
  return achj;
}

// Minimal hand-rolled reader for FRB1's .achx XML dialect (root
// <AnimationChainArraySave>, see AnimationChainListSave.cs's ToXDocument/ParseXml).
// Not a general XML parser - it only extracts the small fixed set of elements this
// format uses, none of which nest inside a same-named element, so a non-greedy regex
// per tag is enough. Written this way (rather than DOMParser/XDocument) because it has
// to run both under plain Node (for tests) and inside Tiled's embedded JS engine,
// neither of which is guaranteed to expose a DOM.
function decodeXmlEntities(text) {
  return text
    .replace(/&lt;/g, "<")
    .replace(/&gt;/g, ">")
    .replace(/&quot;/g, '"')
    .replace(/&apos;/g, "'")
    .replace(/&amp;/g, "&");
}

function xmlChildText(xml, tag) {
  const match = new RegExp(`<${tag}>([\\s\\S]*?)</${tag}>`).exec(xml);
  return match ? decodeXmlEntities(match[1]) : null;
}

function xmlChildBlocks(xml, tag) {
  const re = new RegExp(`<${tag}\\b[^>]*>([\\s\\S]*?)</${tag}>`, "g");
  const blocks = [];
  let match;
  while ((match = re.exec(xml)) !== null) blocks.push(match[1]);
  return blocks;
}

function xmlFloatChild(xml, tag, defaultValue) {
  const text = xmlChildText(xml, tag);
  return text === null ? defaultValue : parseFloat(text);
}

function parseAchxFrame(xml) {
  return {
    textureName: xmlChildText(xml, "TextureName") ?? "",
    frameLength: xmlFloatChild(xml, "FrameLength", 0),
    leftCoordinate: xmlFloatChild(xml, "LeftCoordinate", 0),
    rightCoordinate: xmlFloatChild(xml, "RightCoordinate", 1),
    topCoordinate: xmlFloatChild(xml, "TopCoordinate", 0),
    bottomCoordinate: xmlFloatChild(xml, "BottomCoordinate", 1),
    flipHorizontal: xmlChildText(xml, "FlipHorizontal") === "true",
    flipVertical: xmlChildText(xml, "FlipVertical") === "true",
    flipDiagonal: xmlChildText(xml, "FlipDiagonal") === "true",
  };
}

export function parseAchx(xmlText) {
  const rootMatch = /<AnimationChainArraySave\b[^>]*>([\s\S]*)<\/AnimationChainArraySave>/.exec(xmlText);
  if (!rootMatch) {
    throw new Error("Not a valid .achx file: missing an <AnimationChainArraySave> root element.");
  }
  const root = rootMatch[1];

  return {
    fileRelativeTextures: xmlChildText(root, "FileRelativeTextures") === "true",
    timeMeasurementUnit: xmlChildText(root, "TimeMeasurementUnit") ?? "Second",
    coordinateType: xmlChildText(root, "CoordinateType") ?? "UV",
    animationChains: xmlChildBlocks(root, "AnimationChain").map((chainXml) => ({
      name: xmlChildText(chainXml, "Name") ?? "",
      frames: xmlChildBlocks(chainXml, "Frame").map(parseAchxFrame),
    })),
  };
}

export function frameDurationMs(frameLength, timeMeasurementUnit) {
  if (timeMeasurementUnit === "Millisecond") return frameLength;
  // "Second" and "Undefined" both use seconds (AnimationChainListSaveExtensions treats
  // "Undefined" as seconds at runtime).
  return frameLength * 1000;
}

function frameRectPixels(frame, achj, tilesetInfo) {
  if (achj.coordinateType === "UV") {
    return {
      left: frame.leftCoordinate * tilesetInfo.textureWidth,
      top: frame.topCoordinate * tilesetInfo.textureHeight,
      width: (frame.rightCoordinate - frame.leftCoordinate) * tilesetInfo.textureWidth,
      height: (frame.bottomCoordinate - frame.topCoordinate) * tilesetInfo.textureHeight,
    };
  }
  return {
    left: frame.leftCoordinate,
    top: frame.topCoordinate,
    width: frame.rightCoordinate - frame.leftCoordinate,
    height: frame.bottomCoordinate - frame.topCoordinate,
  };
}

function mapFrame(frame, achj, tilesetInfo, warnings, frameIndex, chainName, options) {
  const label = `chain "${chainName}" frame ${frameIndex}`;

  const textureBaseName = frame.textureName.split(/[\\/]/).pop();
  const tilesetBaseName = tilesetInfo.imageFileName.split(/[\\/]/).pop();
  if (textureBaseName !== tilesetBaseName) {
    // In project-wide bulk mode, most chains belong to some *other* tileset's texture -
    // that's the expected case, not a warning-worthy one.
    if (!options.silentTextureMismatch) {
      warnings.push(`${label}: references a different texture ("${frame.textureName}") than the open tileset ("${tilesetInfo.imageFileName}") - skipped.`);
    }
    return null;
  }

  if (achj.coordinateType === "UV" && !(tilesetInfo.textureWidth && tilesetInfo.textureHeight)) {
    warnings.push(`${label}: "UV" coordinateType needs the tileset image's pixel dimensions, which weren't available - skipped.`);
    return null;
  }

  const rect = frameRectPixels(frame, achj, tilesetInfo);

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

export function mapAchjToTiledAnimations(achj, tilesetInfo, options = {}) {
  return achj.animationChains.map((chain) => {
    const warnings = [];

    if (tilesetInfo.margin || tilesetInfo.tileSpacing) {
      warnings.push(`chain "${chain.name}": tileset has non-zero margin or spacing, which this importer can't account for when computing tile ids - skipped.`);
      return { chainName: chain.name, frames: [], entryTileId: null, warnings };
    }

    const frames = chain.frames
      .map((frame, index) => mapFrame(frame, achj, tilesetInfo, warnings, index, chain.name, options))
      .filter((frame) => frame !== null);

    return {
      chainName: chain.name,
      frames,
      entryTileId: frames.length > 0 ? frames[0].tileId : null,
      warnings,
    };
  });
}

// Recursively finds every .achx/.achj file under rootPath. `listDir(path)` is injected
// (`{ dirs: string[], files: string[] }`, both as full paths) rather than calling the
// `File` scripting global directly, so this can be exercised under plain Node with a
// fake in-memory tree - see achj-import.mjs for the real Tiled-backed listDir.
function toPosixPath(path) {
  return path.replace(/\\/g, "/");
}

function isAbsolutePath(path) {
  return /^[a-zA-Z]:\//.test(path) || path.startsWith("/");
}

export function dirname(path) {
  const posix = toPosixPath(path);
  const lastSlash = posix.lastIndexOf("/");
  return lastSlash === -1 ? "" : posix.slice(0, lastSlash);
}

// Expresses `toDir` relative to `fromDir` (both absolute), e.g. for storing a project
// folder on a tileset without baking in the current machine's absolute path. Path
// segments are compared case-insensitively since Windows paths are case-insensitive.
export function relativePath(fromDir, toDir) {
  const fromParts = toPosixPath(fromDir).split("/").filter(Boolean);
  const toParts = toPosixPath(toDir).split("/").filter(Boolean);

  let commonLength = 0;
  while (
    commonLength < fromParts.length &&
    commonLength < toParts.length &&
    fromParts[commonLength].toLowerCase() === toParts[commonLength].toLowerCase()
  ) {
    commonLength++;
  }

  const upSegments = new Array(fromParts.length - commonLength).fill("..");
  const downSegments = toParts.slice(commonLength);
  const result = [...upSegments, ...downSegments];
  return result.length === 0 ? "." : result.join("/");
}

// The inverse of relativePath: resolves `pathValue` against `baseDir`. If `pathValue`
// is already absolute, it's returned as-is - this keeps a tileset property saved by an
// older version of this tool (which stored an absolute path outright) working.
export function resolvePath(baseDir, pathValue) {
  if (isAbsolutePath(pathValue)) return toPosixPath(pathValue);

  const resultParts = toPosixPath(baseDir).split("/").filter(Boolean);
  for (const segment of toPosixPath(pathValue).split("/")) {
    if (segment === "" || segment === ".") continue;
    if (segment === "..") resultParts.pop();
    else resultParts.push(segment);
  }
  return resultParts.join("/");
}

export function collectAnimationChainFiles(rootPath, listDir) {
  const found = [];
  const stack = [rootPath];
  while (stack.length > 0) {
    const dir = stack.pop();
    const { dirs, files } = listDir(dir);
    for (const file of files) {
      if (/\.(achx|achj)$/i.test(file)) found.push(file);
    }
    stack.push(...dirs);
  }
  return found;
}
