// Tiled extension: imports a FlatRedBall2 AnimationEditor .achj (JSON) or .achx (FRB1
// XML) file and applies its animation chains to the currently open tileset as Tiled
// tile animations.
//
// This is a thin wiring layer over achj-mapper.mjs's pure logic - it only talks to the
// live `tiled`/Tileset/TextFile/Image scripting globals, which don't exist outside a
// running Tiled process, so it isn't unit tested (see achj-mapper.test.mjs for the
// tested core).
//
// Install: copy this whole folder into Tiled's extensions directory
// (Edit > Preferences > Plugins and Extensions, or ~/.local/share/Tiled/extensions on
// Linux / %LOCALAPPDATA%/Tiled/extensions on Windows). Tiled loads extensions on
// startup; use Edit > Reload Extensions if your version has one, otherwise restart.

import { mapAchjToTiledAnimations, parseAchj, parseAchx } from "./achj-mapper.mjs";

function parseAnimationChainFile(path, text) {
  return path.toLowerCase().endsWith(".achx") ? parseAchx(text) : parseAchj(text);
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

tiled.registerAction("ImportAchjAnimation", (action) => {
  const activeAsset = tiled.activeAsset;
  if (!activeAsset || !activeAsset.isTileset) {
    tiled.alert("Open a tileset before running Import AnimationChain Frames.");
    return;
  }
  const tileset = activeAsset;

  const path = tiled.promptOpenFile(null, "AnimationChain files (*.achj *.achx)", "Import AnimationChain Frames");
  if (!path) return;

  const file = new TextFile(path, TextFile.ReadOnly);
  const text = file.readAll();
  file.close();

  let achj;
  try {
    achj = parseAnimationChainFile(path, text);
  } catch (error) {
    tiled.alert(`Couldn't read "${path}":\n${error.message}`);
    return;
  }

  const textureSize = tryGetTextureSize(tileset.imageFileName);
  const tilesetInfo = {
    tileWidth: tileset.tileWidth,
    tileHeight: tileset.tileHeight,
    columnCount: tileset.columnCount,
    margin: tileset.margin,
    tileSpacing: tileset.tileSpacing,
    imageFileName: tileset.imageFileName,
    textureWidth: textureSize?.width,
    textureHeight: textureSize?.height,
  };

  const results = mapAchjToTiledAnimations(achj, tilesetInfo);
  const warnings = results.flatMap((result) => result.warnings);
  let appliedCount = 0;

  tileset.macro(`Import AnimationChain frames from ${path}`, () => {
    for (const result of results) {
      if (result.entryTileId === null) continue;
      const entryTile = tileset.findTile(result.entryTileId);
      if (!entryTile) {
        warnings.push(`chain "${result.chainName}": tile id ${result.entryTileId} doesn't exist in this tileset - skipped.`);
        continue;
      }
      entryTile.frames = result.frames;
      entryTile.setProperty("achjAnimationName", result.chainName);
      appliedCount++;
    }
  });

  const summary = `Applied ${appliedCount} of ${results.length} animation chain(s) from "${path}".`;
  tiled.log(summary);
  if (warnings.length > 0) {
    tiled.log(warnings.join("\n"));
    tiled.alert(`${summary}\n\n${warnings.length} warning(s) - see the Console view for details.`);
  } else {
    tiled.alert(summary);
  }
}).text = "Import AnimationChain Frames...";

tiled.extendMenu("Edit", [
  { action: "ImportAchjAnimation", before: "Preferences" },
]);
