using System;
using System.Collections.Generic;
using System.Numerics;
using MonoGame.Extended.Tilemaps;
using FlatRedBall2.Collision;
using XnaVec2 = Microsoft.Xna.Framework.Vector2;

namespace FlatRedBall2.Tiled;

/// <summary>
/// Generates a <see cref="TileShapes"/> from a <see cref="TilemapTileLayer"/>
/// by matching tiles on their <see cref="TilemapTileData.Class"/> attribute or a custom property.
/// The all-layers overloads (<see cref="GenerateFromClass(Tilemap, string, float, float)"/>,
/// <see cref="GenerateFromProperty(Tilemap, string, float, float)"/>) also match rectangle and
/// polygon objects on any <see cref="TilemapObjectLayer"/> — the single-layer overloads are
/// tile-layer only.
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
/// <para>
/// Object matching checks <see cref="TilemapRectangleObject"/> and <see cref="TilemapPolygonObject"/>
/// entries directly — tile objects (Tiled's "Insert Tile" tool) are not matched; use
/// <see cref="TileMap.CreateEntities"/> for those.
/// </para>
/// <para>
/// Rectangle objects support any position, size, and rotation that's an exact multiple of 90
/// degrees — the rectangle stays axis-aligned (just reoriented) and is clipped against the grid,
/// so it can span any number of cells and doesn't need to be grid-aligned. Rectangles at an
/// arbitrary (non-90-degree-multiple) angle, and all polygon objects, are converted to a
/// <see cref="Collision.Polygon"/>: one that fits within a single cell is positioned relative to
/// whichever grid cell contains its center (the same convention used for per-tile
/// <see cref="TilemapTileData.CollisionObjects"/>); one whose bounds span multiple cells is
/// registered as a free-floating shape via <see cref="Collision.TileShapes.AddSpanningPolygon"/>
/// instead, so it's found by broad-phase collision queries regardless of which cell the querying
/// shape occupies.
/// </para>
/// </remarks>
public static class TileMapCollisions
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
    /// <returns>A <see cref="TileShapes"/> containing one rectangle per matching tile.</returns>
    public static TileShapes GenerateFromClass(
        Tilemap tilemap,
        TilemapTileLayer layer,
        string className,
        float mapX = 0f,
        float mapY = 0f)
    {
        RequireOrthogonal(tilemap);
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
    /// <returns>A <see cref="TileShapes"/> containing one rectangle per matching tile.</returns>
    public static TileShapes GenerateFromProperty(
        Tilemap tilemap,
        TilemapTileLayer layer,
        string propertyName,
        float mapX = 0f,
        float mapY = 0f)
    {
        RequireOrthogonal(tilemap);
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
    /// <returns>A <see cref="TileShapes"/> containing one rectangle per matching tile across all layers.</returns>
    public static TileShapes GenerateFromClass(
        Tilemap tilemap,
        string className,
        float mapX = 0f,
        float mapY = 0f)
    {
        RequireOrthogonal(tilemap);
        return GenerateFromAllLayers(tilemap, mapX, mapY,
            tileData => string.Equals(tileData.Class, className, StringComparison.OrdinalIgnoreCase),
            obj => string.Equals(obj.Class, className, StringComparison.OrdinalIgnoreCase));
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
    /// <returns>A <see cref="TileShapes"/> containing one rectangle per matching tile across all layers.</returns>
    public static TileShapes GenerateFromProperty(
        Tilemap tilemap,
        string propertyName,
        float mapX = 0f,
        float mapY = 0f)
    {
        RequireOrthogonal(tilemap);
        return GenerateFromAllLayers(tilemap, mapX, mapY,
            tileData => tileData.Properties.TryGetValue(propertyName, out _),
            obj => obj.Properties.TryGetValue(propertyName, out _));
    }

    // TileShapes is a fixed-size rectangular broad-phase grid — it has no representation for the
    // diamond/staggered cell footprints that isometric, staggered, and hexagonal maps use. Rather
    // than emit silently-wrong axis-aligned rectangles for those orientations, refuse up front.
    // Use TileMap.GetCellAt for point-in-tile queries, or place collision objects on an object
    // layer in Tiled (object-layer scanning is unaffected — it doesn't use the tile grid).
    private static void RequireOrthogonal(Tilemap tilemap)
    {
        if (tilemap.Orientation != TilemapOrientation.Orthogonal)
            throw new NotSupportedException(
                $"Collision shape generation is not supported for {tilemap.Orientation} tilemaps " +
                "(isometric, staggered, hexagonal). Use TileMap.GetCellAt for point-in-tile " +
                "queries instead, or place collision objects on an object layer in Tiled.");
    }

    /// <summary>
    /// Repopulates an existing <see cref="TileShapes"/> from the given layer using the
    /// supplied predicate. Caller is responsible for clearing <paramref name="target"/> first if
    /// stale cells should be removed. Used by <see cref="TileMap.TryReload"/>.
    /// </summary>
    internal static void RegenerateInto(
        Tilemap tilemap,
        TilemapTileLayer layer,
        Func<TilemapTileData, bool> predicate,
        TileShapes target)
    {
        AddMatchingTiles(tilemap, layer, predicate, target);
    }

    /// <summary>
    /// All-layers variant of <see cref="RegenerateInto(Tilemap, TilemapTileLayer, Func{TilemapTileData, bool}, TileShapes)"/>.
    /// Also re-scans object layers using <paramref name="objectPredicate"/>.
    /// </summary>
    internal static void RegenerateInto(
        Tilemap tilemap,
        Func<TilemapTileData, bool> predicate,
        Func<TilemapObject, bool> objectPredicate,
        TileShapes target)
    {
        // target.X/Y were set from mapX/mapY by the original Generate call; mapY (the map's top
        // edge) is recovered from target.Y (the bottom edge) since object conversion needs the
        // top-edge convention. See GenerateFromAllLayers.
        float mapX = target.X;
        float mapY = target.Y + tilemap.Height * tilemap.TileHeight;

        foreach (var layer in tilemap.Layers)
        {
            switch (layer)
            {
                case TilemapTileLayer tileLayer:
                    AddMatchingTiles(tilemap, tileLayer, predicate, target);
                    break;
                case TilemapObjectLayer objectLayer:
                    AddMatchingObjects(objectLayer, mapX, mapY, objectPredicate, target);
                    break;
            }
        }
    }

    /// <summary>
    /// Core generator. Iterates every cell in the layer, resolves tileset metadata for non-empty
    /// tiles, and adds a collision rectangle for each tile that satisfies <paramref name="predicate"/>.
    /// Tiled rows (Y-down) are flipped to engine rows (Y-up).
    /// </summary>
    private static TileShapes Generate(
        Tilemap tilemap,
        TilemapTileLayer layer,
        float mapX,
        float mapY,
        Func<TilemapTileData, bool> predicate)
    {
        // mapY is the top edge (Tiled convention). TileShapes.Y is the bottom edge
        // (Y-up convention). Convert: bottom = top - totalHeight.
        var collection = new TileShapes
        {
            X = mapX,
            Y = mapY - layer.Height * tilemap.TileHeight,
            GridSize = tilemap.TileWidth
        };

        AddMatchingTiles(tilemap, layer, predicate, collection);
        return collection;
    }

    private static TileShapes GenerateFromAllLayers(
        Tilemap tilemap,
        float mapX,
        float mapY,
        Func<TilemapTileData, bool> tilePredicate,
        Func<TilemapObject, bool> objectPredicate)
    {
        var collection = new TileShapes
        {
            X = mapX,
            // Map-level height, not a per-layer height — must be set before any object layer is
            // processed (object layers may appear before tile layers in Tiled's layer order).
            Y = mapY - tilemap.Height * tilemap.TileHeight,
            GridSize = tilemap.TileWidth
        };

        foreach (var layer in tilemap.Layers)
        {
            switch (layer)
            {
                case TilemapTileLayer tileLayer:
                    AddMatchingTiles(tilemap, tileLayer, tilePredicate, collection);
                    break;
                case TilemapObjectLayer objectLayer:
                    AddMatchingObjects(objectLayer, mapX, mapY, objectPredicate, collection);
                    break;
            }
        }

        return collection;
    }

    /// <summary>
    /// Scans rectangle and polygon objects on <paramref name="layer"/> and adds a collision shape
    /// for each whose <see cref="TilemapObject.Class"/> or properties satisfy
    /// <paramref name="predicate"/>. A shape that fits within a single cell is positioned relative
    /// to whichever grid cell contains its center — the same convention used for per-tile
    /// <see cref="TilemapTileData.CollisionObjects"/>. A shape spanning multiple cells is clipped
    /// per cell (axis-aligned rectangles) or registered as a free-floating shape (polygons) so it's
    /// still found regardless of which cell the querying shape occupies.
    /// Tile objects (placed with Tiled's "Insert Tile" tool) are not matched here — use
    /// <see cref="TileMap.CreateEntities"/> for those.
    /// </summary>
    private static void AddMatchingObjects(
        TilemapObjectLayer layer,
        float mapX,
        float mapY,
        Func<TilemapObject, bool> predicate,
        TileShapes collection)
    {
        foreach (var obj in layer.Objects)
        {
            if (!predicate(obj)) continue;

            switch (obj)
            {
                case TilemapRectangleObject rectObj:
                    AddRectangleObject(rectObj, mapX, mapY, collection);
                    break;
                case TilemapPolygonObject polyObj:
                    AddPolygonObject(polyObj, mapX, mapY, collection);
                    break;
            }
        }
    }

    // Tiled rotates an object clockwise around its own (x,y) position (verified against
    // MonoGame.Extended's OrientedBoundingBox2D.GetCorners() — NOT the object's bounding-box
    // center). A rotation that's an exact multiple of 90 degrees keeps a rectangle axis-aligned
    // (just reoriented/swapped), so it's still emitted as a plain rect — any other angle can't be
    // an AARect and becomes a polygon instead.
    private static void AddRectangleObject(TilemapRectangleObject rectObj, float mapX, float mapY, TileShapes collection)
    {
        float w = rectObj.Size.X;
        float h = rectObj.Size.Y;
        XnaVec2[] local = { new(0, 0), new(w, 0), new(w, h), new(0, h) };
        var worldCorners = new Vector2[4];
        for (int i = 0; i < 4; i++)
        {
            var (rx, ry) = RotateAroundOrigin(local[i].X, local[i].Y, rectObj.Rotation);
            worldCorners[i] = new Vector2(
                mapX + rectObj.Position.X + rx,
                mapY - (rectObj.Position.Y + ry));
        }

        if (TryGetAxisAlignedBounds(worldCorners, out float left, out float right, out float bottom, out float top))
            AddClippedRectangleAcrossCells(left, right, bottom, top, collection);
        else
            AddPolygonFromWorldPoints(worldCorners, collection);
    }

    private static void AddPolygonObject(TilemapPolygonObject polyObj, float mapX, float mapY, TileShapes collection)
    {
        if (polyObj.Points == null || polyObj.Points.Length < 3) return;

        var worldPoints = new Vector2[polyObj.Points.Length];
        for (int i = 0; i < polyObj.Points.Length; i++)
        {
            var p = polyObj.Points[i];
            var (rx, ry) = RotateAroundOrigin(p.X, p.Y, polyObj.Rotation);
            worldPoints[i] = new Vector2(
                mapX + polyObj.Position.X + rx,
                mapY - (polyObj.Position.Y + ry));
        }

        AddPolygonFromWorldPoints(worldPoints, collection);
    }

    // Rotates a Tiled-local point (Y-down) by 'rotationRadians' clockwise around the origin.
    // Rotations within a small tolerance of an exact 90-degree multiple snap to integer
    // transforms instead of using sin/cos — this keeps axis-alignment detection
    // (TryGetAxisAlignedBounds) from being defeated by trig floating-point noise at exactly the
    // angles (0/90/180/270) that matter most for staying a plain AARect.
    private static (float x, float y) RotateAroundOrigin(float x, float y, float rotationRadians)
    {
        const float snapEps = 1e-4f;
        float quarterTurns = rotationRadians / (MathF.PI / 2f);
        int rounded = (int)MathF.Round(quarterTurns);
        if (MathF.Abs(quarterTurns - rounded) < snapEps)
        {
            return ((rounded % 4 + 4) % 4) switch
            {
                1 => (-y, x),
                2 => (-x, -y),
                3 => (y, -x),
                _ => (x, y),
            };
        }

        float cos = MathF.Cos(rotationRadians);
        float sin = MathF.Sin(rotationRadians);
        return (x * cos - y * sin, x * sin + y * cos);
    }

    // True if the four corners form an axis-aligned rectangle — every corner's X matches either
    // the min or max X (and likewise for Y). Any rotation that's a multiple of 90 degrees
    // satisfies this; arbitrary angles don't.
    private static bool TryGetAxisAlignedBounds(Vector2[] corners,
        out float left, out float right, out float bottom, out float top)
    {
        const float eps = 1e-3f;
        left = right = corners[0].X;
        bottom = top = corners[0].Y;
        foreach (var c in corners)
        {
            if (c.X < left) left = c.X;
            if (c.X > right) right = c.X;
            if (c.Y < bottom) bottom = c.Y;
            if (c.Y > top) top = c.Y;
        }

        foreach (var c in corners)
        {
            bool xOk = MathF.Abs(c.X - left) < eps || MathF.Abs(c.X - right) < eps;
            bool yOk = MathF.Abs(c.Y - bottom) < eps || MathF.Abs(c.Y - top) < eps;
            if (!xOk || !yOk) return false;
        }
        return true;
    }

    // Clips an axis-aligned world-space rectangle against the grid, emitting one sub-cell rect
    // per overlapped cell via the existing multi-rect-per-cell AddRectangleTileAtCell. Handles any
    // size/position — including spanning many cells and not being grid-aligned — because existing
    // SolidSides seam suppression already merges adjacent clipped pieces into one continuous
    // surface.
    private static void AddClippedRectangleAcrossCells(
        float left, float right, float bottom, float top, TileShapes collection)
    {
        const float eps = 1e-4f;
        int colMin = (int)MathF.Floor((left - collection.X) / collection.GridSize);
        int colMax = (int)MathF.Floor((right - collection.X) / collection.GridSize - eps);
        int rowMin = (int)MathF.Floor((bottom - collection.Y) / collection.GridSize);
        int rowMax = (int)MathF.Floor((top - collection.Y) / collection.GridSize - eps);

        for (int col = colMin; col <= colMax; col++)
        {
            for (int row = rowMin; row <= rowMax; row++)
            {
                float cellLeft = collection.X + col * collection.GridSize;
                float cellBottom = collection.Y + row * collection.GridSize;
                float clippedLeft = MathF.Max(left, cellLeft);
                float clippedRight = MathF.Min(right, cellLeft + collection.GridSize);
                float clippedBottom = MathF.Max(bottom, cellBottom);
                float clippedTop = MathF.Min(top, cellBottom + collection.GridSize);

                float w = clippedRight - clippedLeft;
                float h = clippedTop - clippedBottom;
                if (w <= 0f || h <= 0f) continue;

                var cellCenter = collection.GetCellWorldPosition(col, row);
                float worldCenterX = (clippedLeft + clippedRight) / 2f;
                float worldCenterY = (clippedBottom + clippedTop) / 2f;
                collection.AddRectangleTileAtCell(col, row,
                    worldCenterX - cellCenter.X, worldCenterY - cellCenter.Y, w, h);
            }
        }
    }

    // A polygon whose bounds fit within a single cell is placed at the cell containing its
    // centroid (participates in per-cell SolidSides bookkeeping, matching per-tile
    // CollisionObjects). A polygon spanning multiple cells goes through AddSpanningPolygon
    // instead — registering it at only its centroid cell would make broad-phase queries miss it
    // whenever the querying shape occupies a different cell within the polygon's bounds.
    private static void AddPolygonFromWorldPoints(Vector2[] worldPoints, TileShapes collection)
    {
        const float eps = 1e-4f;
        float minX = worldPoints[0].X, maxX = worldPoints[0].X;
        float minY = worldPoints[0].Y, maxY = worldPoints[0].Y;
        foreach (var p in worldPoints)
        {
            if (p.X < minX) minX = p.X;
            if (p.X > maxX) maxX = p.X;
            if (p.Y < minY) minY = p.Y;
            if (p.Y > maxY) maxY = p.Y;
        }

        int colMin = (int)MathF.Floor((minX - collection.X) / collection.GridSize);
        int colMax = (int)MathF.Floor((maxX - collection.X) / collection.GridSize - eps);
        int rowMin = (int)MathF.Floor((minY - collection.Y) / collection.GridSize);
        int rowMax = (int)MathF.Floor((maxY - collection.Y) / collection.GridSize - eps);

        if (colMin != colMax || rowMin != rowMax)
        {
            collection.AddSpanningPolygon(worldPoints);
            return;
        }

        float sumX = 0f, sumY = 0f;
        foreach (var p in worldPoints) { sumX += p.X; sumY += p.Y; }
        var centroid = new Vector2(sumX / worldPoints.Length, sumY / worldPoints.Length);

        var (col, row) = collection.GetCellAt(centroid);

        // AddPolygonTileAtCell supports only one polygon per cell. Object-layer authoring has no
        // such restriction — a rotated rect (converted to a polygon above) and a separate polygon
        // object can legitimately centroid into the same cell. Fall back to the same
        // AddSpanningPolygon mechanism used for multi-cell shapes rather than throwing.
        if (collection.GetPolygonTileAtCell(col, row) != null)
        {
            collection.AddSpanningPolygon(worldPoints);
            return;
        }

        var cellCenter = collection.GetCellWorldPosition(col, row);
        var localPoints = new List<Vector2>(worldPoints.Length);
        foreach (var p in worldPoints)
            localPoints.Add(p - cellCenter);

        collection.AddPolygonTileAtCell(col, row, Polygon.FromPoints(localPoints));
    }

    private static void AddMatchingTiles(
        Tilemap tilemap,
        TilemapTileLayer layer,
        Func<TilemapTileData, bool> predicate,
        TileShapes collection)
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

                // Tiled is Y-down; TileShapes is Y-up. Flip the row.
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
