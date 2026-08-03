namespace RiftboundSample.Levels;

/// <summary>
/// Defines a tile-based map layout using a character grid.
/// '#' = wall, '.' = floor, 'P' = player start, 'E' = enemy spawn,
/// 'D' = door/transition, 'S' = shop, 'I' = inn, 'N' = NPC, 'B' = boss door,
/// 'R' = rift tear, 'C' = colosseum.
/// </summary>
public class MapData
{
    public string Name { get; set; } = "";
    public int TileSize { get; set; } = 16;
    public MapTheme Theme { get; set; } = MapTheme.Overworld;
    public string[] Grid { get; set; } = [];

    /// <summary>
    /// Maps door grid positions (col, row) to target map IDs.
    /// </summary>
    public Dictionary<(int Col, int Row), string> DoorTargets { get; set; } = [];
}
