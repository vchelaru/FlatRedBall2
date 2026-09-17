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

import { mapAchjToTiledAnimations, parseAchj, parseAchx, collectAnimationChainFiles } from "./achj-mapper.mjs";

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

// Applies mapAchjToTiledAnimations's results to `tileset` (must already be inside a
// tileset.macro callback) and returns how many chains were applied.
function applyResults(tileset, results, warnings, sourceLabel) {
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
    entryTile.setProperty("achjSourceFile", sourceLabel);
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
  const warnings = results.flatMap((result) => result.warnings);
  let appliedCount = 0;

  tileset.macro(`Import AnimationChain frames from ${path}`, () => {
    appliedCount = applyResults(tileset, results, warnings, path);
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
  const warnings = [];
  let appliedCount = 0;
  let chainCount = 0;

  tileset.macro(`Import AnimationChain project from ${rootPath}`, () => {
    for (const path of filePaths) {
      let achj;
      try {
        achj = parseAnimationChainFile(path, readTextFile(path));
      } catch (error) {
        warnings.push(`"${path}": couldn't read - ${error.message}`);
        continue;
      }
      const results = mapAchjToTiledAnimations(achj, tilesetInfo, { silentTextureMismatch: true });
      chainCount += results.length;
      warnings.push(...results.flatMap((result) => result.warnings));
      appliedCount += applyResults(tileset, results, warnings, path);
    }
  });

  const summary = `AnimationChain project import: applied ${appliedCount} animation(s) (of ${chainCount} chains seen) from ${filePaths.length} file(s) under "${rootPath}".`;
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

tiled.registerAction("SetAchjProjectFolder", () => {
  const tileset = requireOpenTileset("Set AnimationChain Project Folder");
  if (!tileset) return;

  const rootPath = tiled.promptDirectory(null, "Set AnimationChain Project Folder");
  if (!rootPath) return;

  tileset.setProperty(PROJECT_ROOT_PROPERTY, rootPath);
  importProjectFolder(tileset, rootPath, true);
}).text = "Set AnimationChain Project Folder...";

tiled.registerAction("ReimportAchjProject", () => {
  const tileset = requireOpenTileset("Re-import AnimationChain Project");
  if (!tileset) return;

  const rootPath = tileset.property(PROJECT_ROOT_PROPERTY);
  if (!rootPath) {
    tiled.alert(`This tileset has no AnimationChain project folder set - run "Set AnimationChain Project Folder..." first.`);
    return;
  }
  importProjectFolder(tileset, rootPath, true);
}).text = "Re-import AnimationChain Project";

// Auto-reimport whenever a tileset with a remembered project folder is (re)opened, so
// edits made in AnimationEditor since the last session show up without a manual step.
// This only fires on open, not live while both apps are open side by side - Tiled's
// scripting engine has no confirmed timer API to poll the source files on an interval.
tiled.assetOpened.connect((asset) => {
  if (!asset.isTileset) return;
  const rootPath = asset.property(PROJECT_ROOT_PROPERTY);
  if (!rootPath) return;
  importProjectFolder(asset, rootPath, false);
});

tiled.extendMenu("Edit", [
  { action: "ImportAchjAnimation", before: "Preferences" },
  { action: "SetAchjProjectFolder", before: "Preferences" },
  { action: "ReimportAchjProject", before: "Preferences" },
]);
