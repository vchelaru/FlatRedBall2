using System.Collections.Generic;
using System.Linq;

namespace FlatRedBall2.Tests.Collision;

// Named tile layouts used by the TileResolution* tests. '#' is a tile. Row 0 of the array is the top.
// Every layout is padded with empty cells so an actor can approach from any side.
internal static class TileResolutionMaps
{
    private static string[] Pad(int cells, params string[] rows)
    {
        int width = rows.Max(r => r.Length);
        string blank = new string('.', width + cells * 2);
        var result = new List<string>();
        for (int i = 0; i < cells; i++) result.Add(blank);
        foreach (var r in rows)
            result.Add(new string('.', cells) + r.PadRight(width, '.') + new string('.', cells));
        for (int i = 0; i < cells; i++) result.Add(blank);
        return result.ToArray();
    }

    public static readonly IReadOnlyDictionary<string, string[]> All = new Dictionary<string, string[]>
    {
        ["single"] = Pad(3, "#"),
        ["block2x2"] = Pad(3, "##", "##"),
        ["block3x3"] = Pad(3, "###", "###", "###"),
        ["block7x7"] = Pad(2, "#######", "#######", "#######", "#######", "#######", "#######", "#######"),
        ["floor"] = Pad(3, "##########"),
        ["wall"] = Pad(3, "#", "#", "#", "#", "#", "#"),
        ["thickfloor"] = Pad(3, "##########", "##########", "##########"),
        ["lBottomLeft"] = Pad(3, "#.", "##"),
        ["lBottomRight"] = Pad(3, ".#", "##"),
        ["lTopLeft"] = Pad(3, "##", "#."),
        ["lTopRight"] = Pad(3, "##", ".#"),
        ["lBig"] = Pad(3, "#..", "#..", "###"),
        ["tJunction"] = Pad(3, "###", ".#.", ".#."),
        ["tInverted"] = Pad(3, ".#.", ".#.", "###"),
        ["plus"] = Pad(3, ".#.", "###", ".#."),
        ["ledge"] = Pad(3, "...##", "...##", "######"),
        ["stairs"] = Pad(3, "....#", "...##", "..###", ".####", "#####"),
        ["pit1"] = Pad(2, "####.####", "####.####", "####.####"),
        ["pit2"] = Pad(2, "####..####", "####..####", "####..####"),
        ["pillars"] = Pad(2, "#...#", "#...#", "#####"),
        ["pillarGap1"] = Pad(2, "#.#", "#.#", "###"),
        ["diagonalPair"] = Pad(3, ".#", "#."),
        ["checker"] = Pad(2, "#.#.", ".#.#", "#.#.", ".#.#"),
        ["room"] = Pad(2, "#######", "#.....#", "#.....#", "#.....#", "#######"),
        ["roomWithDoor"] = Pad(2, "#######", "#.....#", "#......", "#.....#", "#######"),
        ["thinCeiling"] = Pad(3, "##########"),
        ["overhang"] = Pad(2, "#######", "#......", "#......", "#######"),
        ["cave"] = Pad(1, "##########", "#...#....#", "#..###...#", "#........#", "###..#####", "#........#", "##########"),
    };

    public static string[] Get(string name) => All[name];

    /// <summary>Cell width and height of a named layout.</summary>
    public static (int width, int height) Size(string name) => (All[name].Max(r => r.Length), All[name].Length);

    public static IEnumerable<object[]> Names() => All.Keys.Select(k => new object[] { k });
}
