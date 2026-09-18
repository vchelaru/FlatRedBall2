// Tiled extension: imports FlatRedBall2 AnimationEditor .achj (JSON) / .achx (FRB1
// XML) animation chains into the currently open tileset, as Tiled tile animations.
//
// Two ways to use it:
//  - "Import AnimationChain Frames..." - pick one .achj/.achx file.
//  - "Set AnimationChain Project Folder..." - point at a project's root folder once;
//    every .achx/.achj under it is scanned and any frame matching this tileset's
//    texture is applied. The folder is remembered as a tileset custom property
//    (achjProjectRoot), and reapplied automatically every time this tileset is opened
//    (tiled.assetOpened) - "Re-import AnimationChain Project" reruns it on demand
//    without waiting for a reopen.
//
// This is a thin wiring layer over achj-mapper.mjs's pure logic - it only talks to the
// live `tiled`/Tileset/TextFile/Image/File scripting globals, which don't exist outside
// a running Tiled process, so it isn't unit tested (see achj-mapper.test.mjs for the
// tested core).
//
// Install: copy this whole folder into Tiled's extensions directory
// (Edit > Preferences > Plugins and Extensions, or ~/.local/share/Tiled/extensions on
// Linux / %LOCALAPPDATA%/Tiled/extensions on Windows). Tiled loads extensions on
// startup and reloads them automatically when a file here changes.

import { mapAchjToTiledAnimations, parseAchj, parseAchx, collectAnimationChainFiles, relativePath, resolvePath, dirname } from "./achj-mapper.mjs";

const PROJECT_ROOT_PROPERTY = "achjProjectRoot";

function parseAnimationChainFile(path, text) {
  return path.toLowerCase().endsWith(".achx") ? parseAchx(text) : parseAchj(text);
}

function readTextFile(path) {
  const file = new TextFile(path, TextFile.ReadOnly);
  try {
    return file.readAll();
  } finally {
    file.close();
  }
}

// Tileset.imageFileName gives no pixel-size property directly; loading the same file
// as an Image is the only way to get it, which "UV" coordinateType frames need to
// convert to pixels. Best-effort: a tileset with "Pixel" coordinateType frames works
// fine even if this fails.
function tryGetTextureSize(imageFileName) {
  try {
    const image = new Image(imageFileName);
    return image.width > 0 && image.height > 0 ? { width: image.width, height: image.height } : null;
  } catch (error) {
    return null;
  }
}

function buildTilesetInfo(tileset) {
  const textureSize = tryGetTextureSize(tileset.imageFileName);
  return {
    tileWidth: tileset.tileWidth,
    tileHeight: tileset.tileHeight,
    columnCount: tileset.columnCount,
    margin: tileset.margin,
    tileSpacing: tileset.tileSpacing,
    imageFileName: tileset.imageFileName,
    textureWidth: textureSize?.width,
    textureHeight: textureSize?.height,
  };
}

// Array.prototype.flatMap isn't available in Tiled's embedded JS engine (throws
// "Property 'flatMap' ... is not a function" at runtime, despite being valid syntax) -
// this is the flatMap-free equivalent of `results.flatMap(r => r.warnings)`.
function collectWarnings(results) {
  const warnings = [];
  for (const result of results) warnings.push(...result.warnings);
  return warnings;
}

const SKIP_COUNT_LABELS = [
  ["textureMismatch", "different texture"],
  ["sizeMismatch", "wrong size"],
  ["notGridAligned", "not grid-aligned"],
  ["uvMissingPixelSize", "UV without pixel size"],
];

function addSkipCounts(total, counts) {
  for (const [key] of SKIP_COUNT_LABELS) total[key] += counts[key];
}

function describeSkipCounts(counts) {
  const parts = [];
  for (const [key, label] of SKIP_COUNT_LABELS) {
    if (counts[key] > 0) parts.push(`${counts[key]} ${label}`);
  }
  return parts.join(", ");
}

// Applies mapAchjToTiledAnimations's results to `tileset` (must already be inside a
// tileset.macro callback) and returns how many chains were applied. `sourceLabel` is
// the absolute .achx/.achj path (used as-is in warnings, for debugging); it's stored on
// each tile's achjSourceFile property relative to `baseDir` (the tileset's own
// directory) instead, for the same portability reason as achjProjectRoot - `baseDir`
// null (tileset never saved) falls back to storing the absolute path.
function applyResults(tileset, results, warnings, sourceLabel, baseDir) {
  const storedSourceLabel = baseDir ? relativePath(baseDir, sourceLabel) : sourceLabel;
  let appliedCount = 0;
  for (const result of results) {
    if (result.entryTileId === null) continue;
    const entryTile = tileset.findTile(result.entryTileId);
    if (!entryTile) {
      warnings.push(`chain "${result.chainName}" (${sourceLabel}): tile id ${result.entryTileId} doesn't exist in this tileset - skipped.`);
      continue;
    }
    entryTile.frames = result.frames;
    entryTile.setProperty("achjAnimationName", result.chainName);
    entryTile.setProperty("achjSourceFile", storedSourceLabel);
    appliedCount++;
  }
  return appliedCount;
}

function importSingleFile(tileset, path) {
  if (!File.exists(path)) {
    tiled.alert(`"${path}" doesn't exist (or isn't reachable from here).`);
    return;
  }

  let achj;
  try {
    achj = parseAnimationChainFile(path, readTextFile(path));
  } catch (error) {
    tiled.alert(`Couldn't read "${path}":\n${error.message}`);
    return;
  }

  const tilesetInfo = buildTilesetInfo(tileset);
  const results = mapAchjToTiledAnimations(achj, tilesetInfo);
  const warnings = collectWarnings(results);
  const baseDir = projectRootDir(tileset);
  let appliedCount = 0;

  tileset.macro(`Import AnimationChain frames from ${path}`, () => {
    appliedCount = applyResults(tileset, results, warnings, path, baseDir);
  });

  const summary = `Applied ${appliedCount} of ${results.length} animation chain(s) from "${path}".`;
  tiled.log(summary);
  if (warnings.length > 0) {
    tiled.log(warnings.join("\n"));
    tiled.alert(`${summary}\n\n${warnings.length} warning(s) - see the Console view for details.`);
  } else {
    tiled.alert(summary);
  }
}

function listDirTiled(path) {
  const join = (name) => `${path}/${name}`;
  return {
    dirs: File.directoryEntries(path, File.Dirs | File.NoDotAndDotDot).map(join),
    files: File.directoryEntries(path, File.Files).map(join),
  };
}

// interactive=true shows a summary dialog (manual run); interactive=false only logs to
// the Console (the automatic tiled.assetOpened run - a modal on every tileset open
// would be obnoxious).
function importProjectFolder(tileset, rootPath, interactive) {
  const filePaths = collectAnimationChainFiles(rootPath, listDirTiled);
  const tilesetInfo = buildTilesetInfo(tileset);
  const baseDir = projectRootDir(tileset);
  const warnings = [];
  const totalSkipCounts = { textureMismatch: 0, uvMissingPixelSize: 0, sizeMismatch: 0, notGridAligned: 0 };
  let appliedCount = 0;
  let chainCount = 0;

  // tallySkips: true - in a project with many .achx/.achj files, most frames scanned
  // were never meant to become tile animations at all (wrong texture, wrong size,
  // off-grid). Itemizing each one as a warning buries the warnings that are actually
  // actionable, so these get tallied into totalSkipCounts and reported as one line
  // instead (see describeSkipCounts below).
  tileset.macro(`Import AnimationChain project from ${rootPath}`, () => {
    for (const path of filePaths) {
      let achj;
      try {
        achj = parseAnimationChainFile(path, readTextFile(path));
      } catch (error) {
        warnings.push(`"${path}": couldn't read - ${error.message}`);
        continue;
      }
      const results = mapAchjToTiledAnimations(achj, tilesetInfo, { tallySkips: true });
      chainCount += results.length;
      warnings.push(...collectWarnings(results));
      for (const result of results) addSkipCounts(totalSkipCounts, result.skipCounts);
      appliedCount += applyResults(tileset, results, warnings, path, baseDir);
    }
  });

  const skipSummary = describeSkipCounts(totalSkipCounts);
  const summary =
    `AnimationChain project import: applied ${appliedCount} animation(s) (of ${chainCount} chains seen) from ${filePaths.length} file(s) under "${rootPath}".` +
    (skipSummary ? ` Skipped frames not meant for this tileset: ${skipSummary}.` : "");
  tiled.log(summary);
  if (warnings.length > 0) tiled.log(warnings.join("\n"));
  if (interactive) {
    tiled.alert(warnings.length > 0 ? `${summary}\n\n${warnings.length} warning(s) - see the Console view for details.` : summary);
  }
}

function requireOpenTileset(actionLabel) {
  const activeAsset = tiled.activeAsset;
  if (!activeAsset || !activeAsset.isTileset) {
    tiled.alert(`Open a tileset before running ${actionLabel}.`);
    return null;
  }
  return activeAsset;
}

tiled.registerAction("ImportAchjAnimation", () => {
  const tileset = requireOpenTileset("Import AnimationChain Frames");
  if (!tileset) return;

  const path = tiled.promptOpenFile(null, "AnimationChain files (*.achj *.achx);;All files (*)", "Import AnimationChain Frames");
  if (!path) return;

  importSingleFile(tileset, path);
}).text = "Import AnimationChain Frames...";

// The stored property is relative to the tileset's own .tsx location (see
// relativePath/resolvePath in achj-mapper.mjs), so the .tsx stays portable across
// machines/checkouts instead of baking in one absolute path. A tileset with no
// fileName yet (never saved) has nothing to be relative to, so it falls back to
// storing the absolute path outright.
function projectRootDir(tileset) {
  return tileset.fileName ? dirname(tileset.fileName) : null;
}

tiled.registerAction("SetAchjProjectFolder", () => {
  const tileset = requireOpenTileset("Set AnimationChain Project Folder");
  if (!tileset) return;

  const rootPath = tiled.promptDirectory(null, "Set AnimationChain Project Folder");
  if (!rootPath) return;

  const baseDir = projectRootDir(tileset);
  tileset.setProperty(PROJECT_ROOT_PROPERTY, baseDir ? relativePath(baseDir, rootPath) : rootPath);
  importProjectFolder(tileset, rootPath, true);
}).text = "Set AnimationChain Project Folder...";

tiled.registerAction("ReimportAchjProject", () => {
  const tileset = requireOpenTileset("Re-import AnimationChain Project");
  if (!tileset) return;

  const storedPath = tileset.property(PROJECT_ROOT_PROPERTY);
  if (!storedPath) {
    tiled.alert(`This tileset has no AnimationChain project folder set - run "Set AnimationChain Project Folder..." first.`);
    return;
  }
  const baseDir = projectRootDir(tileset);
  importProjectFolder(tileset, baseDir ? resolvePath(baseDir, storedPath) : storedPath, true);
}).text = "Re-import AnimationChain Project";

// Auto-reimport whenever a tileset with a remembered project folder is (re)opened, so
// edits made in AnimationEditor since the last session show up without a manual step.
// This only fires on open, not live while both apps are open side by side - Tiled's
// scripting engine has no confirmed timer API to poll the source files on an interval.
tiled.assetOpened.connect((asset) => {
  if (!asset.isTileset) return;
  const storedPath = asset.property(PROJECT_ROOT_PROPERTY);
  if (!storedPath) return;
  const baseDir = projectRootDir(asset);
  importProjectFolder(asset, baseDir ? resolvePath(baseDir, storedPath) : storedPath, false);
});

tiled.extendMenu("Edit", [
  { action: "ImportAchjAnimation", before: "Preferences" },
  { action: "SetAchjProjectFolder", before: "Preferences" },
  { action: "ReimportAchjProject", before: "Preferences" },
]);
