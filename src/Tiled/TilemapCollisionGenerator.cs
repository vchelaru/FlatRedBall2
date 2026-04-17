using System;
using System.Collections.Generic;
using System.Numerics;
using MonoGame.Extended.Tilemaps;
using FlatRedBall2.Collision;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tiled;

/// <summary>
/// Generates a <see cref="TileShapeCollection"/> from a <see cref="TilemapTileLayer"/>
/// by matching tiles on their <see cref="TilemapTileData.Class"/> attribute or a custom property.
/// </summary>
/// <remarks>
/// <para>
/// Both public methods take <c>mapX</c> and <c>mapY</c> parameters that position the map in
/// world space. <c>mapX</c> is the <b>left edge</b> of the map; <c>mapY</c> is the <b>top edge</b>
/// (because Tiled's origin is top-left). The generator converts to Y-up internally — callers do
/// not need to flip anything.
/// </para>
/// <para>
/// Tile matching uses the tileset metadata from <see cref="Tilemap.Tilesets"/>. Only tiles with a
/// non-zero global ID that pass the predicate produce collision rectangles.
/// </para>
/// </remarks>
public static class TileMapCollisionGenerator
{
    /// <summary>
    /// Scans every tile in <paramref name="layer"/> and adds a collision rectangle for each tile
    /// whose tileset <see cref="TilemapTileData.Class"/> equals <paramref name="className"/>
    /// (case-insensitive).
    /// </summary>
    /// <param name="tilemap">The parsed tilemap — provides tile dimensions and tileset metadata.</param>
    /// <param name="layer">The tile layer to scan.</param>
    /// <param name="className">
    /// The <see cref="TilemapTileData.Class"/> value to match (case-insensitive).
    /// In Tiled, this is the "Class" field on a tile in the tileset editor.
    /// </param>
    /// <param name="mapX">Left edge of the map in world space.</param>
    /// <param name="mapY">
    /// Top edge of the map in world space (Tiled convention). The generator converts to Y-up
    /// internally — pass the top edge, not the bottom.
    /// </param>
    /// <returns>A <see cref="TileShapeCollection"/> containing one rectangle per matching tile.</returns>
    public static TileShapeCollection GenerateFromClass(
        Tilemap tilemap,
        TilemapTileLayer layer,
        string className,
        float mapX = 0f,
        float mapY = 0f)
    {
        return Generate(tilemap, layer, mapX, mapY,
            tileData => string.Equals(tileData.Class, className, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Scans every tile in <paramref name="layer"/> and adds a collision rectangle for each tile
    /// whose tileset definition contains a custom property named <paramref name="propertyName"/>.
    /// </summary>
    /// <param name="tilemap">The parsed tilemap — provides tile dimensions and tileset metadata.</param>
    /// <param name="layer">The tile layer to scan.</param>
    /// <param name="propertyName">
    /// The custom property name to look for on each tile's tileset data.
    /// The property value is ignored — only its presence matters.
    /// </param>
    /// <param name="mapX">Left edge of the map in world space.</param>
    /// <param name="mapY">
    /// Top edge of the map in world space (Tiled convention). The generator converts to Y-up
    /// internally — pass the top edge, not the bottom.
    /// </param>
    /// <returns>A <see cref="TileShapeCollection"/> containing one rectangle per matching tile.</returns>
    public static TileShapeCollection GenerateFromProperty(
        Tilemap tilemap,
        TilemapTileLayer layer,
        string propertyName,
        float mapX = 0f,
        float mapY = 0f)
    {
        return Generate(tilemap, layer, mapX, mapY,
            tileData => tileData.Properties.TryGetValue(propertyName, out _));
    }

    /// <summary>
    /// Scans every tile in all tile layers of <paramref name="tilemap"/> and adds a collision
    /// rectangle for each tile whose tileset <see cref="TilemapTileData.Class"/> equals
    /// <paramref name="className"/> (case-insensitive).
    /// </summary>
    /// <param name="tilemap">The parsed tilemap — provides tile dimensions and tileset metadata.</param>
    /// <param name="className">
    /// The <see cref="TilemapTileData.Class"/> value to match (case-insensitive).
    /// </param>
    /// <param name="mapX">Left edge of the map in world space.</param>
    /// <param name="mapY">
    /// Top edge of the map in world space (Tiled convention). The generator converts to Y-up
    /// internally — pass the top edge, not the bottom.
    /// </param>
    /// <returns>A <see cref="TileShapeCollection"/> containing one rectangle per matching tile across all layers.</returns>
    public static TileShapeCollection GenerateFromClass(
        Tilemap tilemap,
        string className,
        float mapX = 0f,
        float mapY = 0f)
    {
        return GenerateFromAllLayers(tilemap, mapX, mapY,
            tileData => string.Equals(tileData.Class, className, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Scans every tile in all tile layers of <paramref name="tilemap"/> and adds a collision
    /// rectangle for each tile whose tileset definition contains a custom property named
    /// <paramref name="propertyName"/>.
    /// </summary>
    /// <param name="tilemap">The parsed tilemap — provides tile dimensions and tileset metadata.</param>
    /// <param name="propertyName">
    /// The custom property name to look for on each tile's tileset data.
    /// The property value is ignored — only its presence matters.
    /// </param>
    /// <param name="mapX">Left edge of the map in world space.</param>
    /// <param name="mapY">
    /// Top edge of the map in world space (Tiled convention). The generator converts to Y-up
    /// internally — pass the top edge, not the bottom.
    /// </param>
    /// <returns>A <see cref="TileShapeCollection"/> containing one rectangle per matching tile across all layers.</returns>
    public static TileShapeCollection GenerateFromProperty(
        Tilemap tilemap,
        string propertyName,
        float mapX = 0f,
        float mapY = 0f)
    {
        return GenerateFromAllLayers(tilemap, mapX, mapY,
            tileData => tileData.Properties.TryGetValue(propertyName, out _));
    }

    /// <summary>
    /// Repopulates an existing <see cref="TileShapeCollection"/> from the given layer using the
    /// supplied predicate. Caller is responsible for clearing <paramref name="target"/> first if
    /// stale cells should be removed. Used by <see cref="TileMap.TryReload"/>.
    /// </summary>
    internal static void RegenerateInto(
        Tilemap tilemap,
        TilemapTileLayer layer,
        Func<TilemapTileData, bool> predicate,
        TileShapeCollection target)
    {
        AddMatchingTiles(tilemap, layer, predicate, target);
    }

    /// <summary>
    /// All-layers variant of <see cref="RegenerateInto(Tilemap, TilemapTileLayer, Func{TilemapTileData, bool}, TileShapeCollection)"/>.
    /// </summary>
    internal static void RegenerateInto(
        Tilemap tilemap,
        Func<TilemapTileData, bool> predicate,
        TileShapeCollection target)
    {
        foreach (var layer in tilemap.Layers)
        {
            if (layer is TilemapTileLayer tileLayer)
                AddMatchingTiles(tilemap, tileLayer, predicate, target);
        }
    }

    /// <summary>
    /// Core generator. Iterates every cell in the layer, resolves tileset metadata for non-empty
    /// tiles, and adds a collision rectangle for each tile that satisfies <paramref name="predicate"/>.
    /// Tiled rows (Y-down) are flipped to engine rows (Y-up).
    /// </summary>
    private static TileShapeCollection Generate(
        Tilemap tilemap,
        TilemapTileLayer layer,
        float mapX,
        float mapY,
        Func<TilemapTileData, bool> predicate)
    {
        // mapY is the top edge (Tiled convention). TileShapeCollection.Y is the bottom edge
        // (Y-up convention). Convert: bottom = top - totalHeight.
        var collection = new TileShapeCollection
        {
            X = mapX,
            Y = mapY - layer.Height * tilemap.TileHeight,
            GridSize = tilemap.TileWidth
        };

        AddMatchingTiles(tilemap, layer, predicate, collection);
        return collection;
    }

    private static TileShapeCollection GenerateFromAllLayers(
        Tilemap tilemap,
        float mapX,
        float mapY,
        Func<TilemapTileData, bool> predicate)
    {
        // Use the first tile layer's dimensions for the collection grid.
        // All tile layers in a Tiled map share the same tile dimensions.
        var collection = new TileShapeCollection
        {
            X = mapX,
            GridSize = tilemap.TileWidth
        };

        foreach (var layer in tilemap.Layers)
        {
            if (layer is not TilemapTileLayer tileLayer)
                continue;

            // Set Y based on this layer's height (should be consistent across layers).
            collection.Y = mapY - tileLayer.Height * tilemap.TileHeight;
            AddMatchingTiles(tilemap, tileLayer, predicate, collection);
        }

        return collection;
    }

    private static void AddMatchingTiles(
        Tilemap tilemap,
        TilemapTileLayer layer,
        Func<TilemapTileData, bool> predicate,
        TileShapeCollection collection)
    {
        for (int row = 0; row < layer.Height; row++)
        {
            for (int col = 0; col < layer.Width; col++)
            {
                TilemapTile? tileNullable = layer.GetTile(col, row);
                if (!tileNullable.HasValue || tileNullable.Value.GlobalId == 0)
                    continue;

                TilemapTile tile = tileNullable.Value;

                TilemapTileData? tileData = tile.GetTileData(tilemap.Tilesets);
                if (tileData == null || !predicate(tileData))
                    continue;

                // Tiled is Y-down; TileShapeCollection is Y-up. Flip the row.
                int flippedRow = layer.Height - 1 - row;

                BuildCollisionShapes(tileData, collection.GridSize, tile.FlipFlags,
                    out var polygons, out var rects);

                if (polygons == null && rects == null)
                {
                    collection.AddTileAtCell(col, flippedRow);
                    continue;
                }

                if (polygons != null)
                    foreach (var proto in polygons)
                        collection.AddPolygonTileAtCell(col, flippedRow, proto);

                if (rects != null)
                    foreach (var r in rects)
                        collection.AddRectangleTileAtCell(col, flippedRow, r.cx, r.cy, r.w, r.h);
            }
        }
    }

    // Converts polygon and rectangle collision objects on the tile into local-space shapes
    // centered on (0, 0) with Y-up. Applies Tiled flip flags (diagonal, then horizontal, then
    // vertical) per Tiled's rendering semantics. A tile with any collision object emits those
    // custom shapes instead of the default full-cell rect. Ellipse and polyline collision
    // objects are ignored — see TODOS.md.
    private static void BuildCollisionShapes(
        TilemapTileData tileData,
        float gridSize,
        TilemapTileFlipFlags flipFlags,
        out List<Polygon>? polygons,
        out List<(float cx, float cy, float w, float h)>? rects)
    {
        polygons = null;
        rects = null;
        if (tileData.CollisionObjects == null || tileData.CollisionObjects.Count == 0)
            return;

        float half = gridSize / 2f;
        bool flipD = (flipFlags & TilemapTileFlipFlags.FlipDiagonally) != 0;
        bool flipH = (flipFlags & TilemapTileFlipFlags.FlipHorizontally) != 0;
        bool flipV = (flipFlags & TilemapTileFlipFlags.FlipVertically) != 0;

        foreach (var obj in tileData.CollisionObjects)
        {
            if (obj is TilemapPolygonObject polyObj && polyObj.Points != null && polyObj.Points.Length >= 3)
            {
                var localPoints = new List<Vector2>(polyObj.Points.Length);
                foreach (var p in polyObj.Points)
                {
                    // Tiled pixel (Y-down, origin at tile top-left) → FRB2 local (Y-up, centered).
                    XnaVec2 tiled = polyObj.Position + p;
                    float x = tiled.X - half;
                    float y = half - tiled.Y;
                    ApplyFlips(ref x, ref y, flipD, flipH, flipV);
                    localPoints.Add(new Vector2(x, y));
                }

                polygons ??= new List<Polygon>();
                polygons.Add(Polygon.FromPoints(localPoints));
            }
            else if (obj is TilemapRectangleObject rectObj)
            {
                // Tiled rect: top-left (Position.X, Position.Y), size (Size.X, Size.Y), Y-down.
                // Convert center to FRB2 local (Y-up, centered on cell).
                float w = rectObj.Size.X;
                float h = rectObj.Size.Y;
                float cx = rectObj.Position.X + w / 2f - half;
                float cy = half - (rectObj.Position.Y + h / 2f);

                // Diagonal flip transposes across the tile's main diagonal — swap center and size.
                if (flipD)
                {
                    (cx, cy) = (-cy, -cx);
                    (w, h) = (h, w);
                }
                if (flipH) cx = -cx;
                if (flipV) cy = -cy;

                rects ??= new List<(float, float, float, float)>();
                rects.Add((cx, cy, w, h));
            }
        }
    }

    // Applies Tiled flip flags in declared D → H → V order in centered-Y-up local space.
    // Winding may reverse under odd flip counts; callers that produce polygons rely on
    // Polygon.FromPoints / SAT to normalize winding internally.
    private static void ApplyFlips(ref float x, ref float y, bool flipD, bool flipH, bool flipV)
    {
        if (flipD) (x, y) = (-y, -x);
        if (flipH) x = -x;
        if (flipV) y = -y;
    }
}
