using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;

namespace RiftboundSample.Levels;

public enum MapTheme
{
    Overworld,
    Ethereal,
    Nexus,
    Fade
}

/// <summary>
/// Result of parsing a <see cref="MapData"/> grid. Contains collision geometry,
/// spawn positions, and marker positions for the screen to consume.
/// </summary>
public class LoadedMap
{
    public TileShapeCollection Walls { get; init; } = null!;
    public (float X, float Y) PlayerStart { get; set; }
    public List<(float X, float Y)> EnemySpawns { get; init; } = [];
    public List<(float X, float Y)> NpcPositions { get; init; } = [];
    public List<(float X, float Y)> DoorPositions { get; init; } = [];
    public List<(float X, float Y)> ShopPositions { get; init; } = [];
    public List<(float X, float Y)> InnPositions { get; init; } = [];
    public List<(float X, float Y)> BossDoorPositions { get; init; } = [];
    public List<(float X, float Y)> RiftTearPositions { get; init; } = [];
    public List<(float X, float Y)> ColosseumPositions { get; init; } = [];

    /// <summary>
    /// Maps door world positions to target map IDs, derived from MapData.DoorTargets.
    /// </summary>
    public Dictionary<(float X, float Y), string> DoorTargetLookup { get; init; } = [];

    /// <summary>Width of the full map in world units.</summary>
    public float MapWidth { get; init; }

    /// <summary>Height of the full map in world units.</summary>
    public float MapHeight { get; init; }

    /// <summary>Number of columns in the grid.</summary>
    public int Cols { get; init; }

    /// <summary>Number of rows in the grid.</summary>
    public int Rows { get; init; }

    /// <summary>World X of the map's left edge.</summary>
    public float OriginX { get; init; }

    /// <summary>World Y of the map's bottom edge.</summary>
    public float OriginY { get; init; }
}

public static class MapLoader
{
    /// <summary>
    /// Parses a <see cref="MapData"/> grid into collision geometry and spawn positions.
    /// The grid is centered at the world origin. Row 0 = top of screen = highest Y value.
    /// </summary>
    public static LoadedMap Load(MapData map, Screen screen, MapTheme theme = MapTheme.Overworld)
    {
        int rows = map.Grid.Length;
        int cols = map.Grid[0].Length;
        float tileSize = map.TileSize;

        float mapWidth = cols * tileSize;
        float mapHeight = rows * tileSize;

        // Center the map at the origin.
        // originX/originY = bottom-left corner of the grid in world space.
        float originX = -mapWidth / 2f;
        float originY = -mapHeight / 2f;

        // TileShapeCollection X/Y = bottom-left corner of cell (0,0).
        // Cell (0,0) is the bottom-left tile. Grid row 0 is the top, so
        // grid row (rows-1) maps to tile row 0.
        var walls = new TileShapeCollection
        {
            X = originX,
            Y = originY,
            GridSize = tileSize,
        };

        var result = new LoadedMap
        {
            Walls = walls,
            MapWidth = mapWidth,
            MapHeight = mapHeight,
            Cols = cols,
            Rows = rows,
            OriginX = originX,
            OriginY = originY,
        };

        // Floor tiles for visual rendering
        var floors = new TileShapeCollection
        {
            X = originX,
            Y = originY,
            GridSize = tileSize,
        };

        for (int gridRow = 0; gridRow < rows; gridRow++)
        {
            // Grid row 0 = top = highest Y. Tile row = (rows - 1 - gridRow).
            int tileRow = rows - 1 - gridRow;

            for (int col = 0; col < map.Grid[gridRow].Length; col++)
            {
                char c = map.Grid[gridRow][col];
                float worldX = originX + col * tileSize + tileSize / 2f;
                float worldY = originY + tileRow * tileSize + tileSize / 2f;

                switch (c)
                {
                    case '#':
                        walls.AddTileAtCell(col, tileRow);
                        break;

                    case 'P':
                        result.PlayerStart = (worldX, worldY);
                        floors.AddTileAtCell(col, tileRow);
                        break;

                    case 'E':
                        result.EnemySpawns.Add((worldX, worldY));
                        floors.AddTileAtCell(col, tileRow);
                        break;

                    case 'N':
                        result.NpcPositions.Add((worldX, worldY));
                        floors.AddTileAtCell(col, tileRow);
                        break;

                    case 'D':
                        result.DoorPositions.Add((worldX, worldY));
                        floors.AddTileAtCell(col, tileRow);
                        // Build door target lookup from grid coordinates
                        if (map.DoorTargets.TryGetValue((col, gridRow), out var target))
                            result.DoorTargetLookup[(worldX, worldY)] = target;
                        break;

                    case 'B':
                        result.BossDoorPositions.Add((worldX, worldY));
                        floors.AddTileAtCell(col, tileRow);
                        // Boss doors can also have targets
                        if (map.DoorTargets.TryGetValue((col, gridRow), out var bossTarget))
                            result.DoorTargetLookup[(worldX, worldY)] = bossTarget;
                        break;

                    case 'S':
                        result.ShopPositions.Add((worldX, worldY));
                        floors.AddTileAtCell(col, tileRow);
                        break;

                    case 'I':
                        result.InnPositions.Add((worldX, worldY));
                        floors.AddTileAtCell(col, tileRow);
                        break;

                    case 'R':
                        result.RiftTearPositions.Add((worldX, worldY));
                        floors.AddTileAtCell(col, tileRow);
                        break;

                    case 'C':
                        result.ColosseumPositions.Add((worldX, worldY));
                        floors.AddTileAtCell(col, tileRow);
                        break;

                    case '.':
                        floors.AddTileAtCell(col, tileRow);
                        break;
                }
            }
        }

        // Theme-dependent colors
        var (wallColor, floorColor) = theme switch
        {
            MapTheme.Ethereal => (new Color(80, 40, 120), new Color(140, 110, 180)),
            MapTheme.Nexus => (new Color(30, 120, 120), new Color(60, 180, 180)),
            MapTheme.Fade => (new Color(100, 30, 30), new Color(160, 155, 155)),
            _ => (new Color(60, 60, 70), new Color(100, 100, 90)),
        };

        // Register wall tiles for rendering
        walls.Color = wallColor;
        walls.IsFilled = true;
        walls.OutlineThickness = 0f;
        walls.IsVisible = true;
        screen.Add(walls);

        // Register floor tiles for rendering
        floors.Color = floorColor;
        floors.IsFilled = true;
        floors.OutlineThickness = 0f;
        floors.IsVisible = true;
        screen.Add(floors);

        return result;
    }
}
