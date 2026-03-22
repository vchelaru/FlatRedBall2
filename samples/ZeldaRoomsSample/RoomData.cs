namespace ZeldaRoomsSample;

// Character key:
//   # = wall tile (64×64 units)
//   . = open floor
//   E = enemy spawn
//   X = exit gap on right edge (no wall, player walks through)
//
// Grid is 20 cols × 11 rows × 64 units = 1280 × 704 (fits within 1280×720 camera).
// The exit is always on the right edge (col 19), rows 4–6 marked 'X'.

public static class RoomData
{
    public static readonly RoomDefinition[] Rooms =
    {
        // Room 1: Open room, 1 enemy. Teaches movement and combat.
        new RoomDefinition(new[]
        {
            "####################",
            "#..................#",
            "#..................#",
            "#..................#",
            "#..................X",
            "#......E...........X",
            "#..................X",
            "#..................#",
            "#..................#",
            "#..................#",
            "####################",
        }),

        // Room 2: Wall segments create simple corridors. 2 enemies.
        new RoomDefinition(new[]
        {
            "####################",
            "#..................#",
            "#....####..........#",
            "#..................#",
            "#..................X",
            "#......E....E......X",
            "#..................X",
            "#..................#",
            "#....####..........#",
            "#..................#",
            "####################",
        }),

        // Room 3: L-shaped walls and a central pillar. 3 enemies.
        new RoomDefinition(new[]
        {
            "####################",
            "#..................#",
            "#..###.............#",
            "#..#...............#",
            "#..................X",
            "#.....E.###.E......X",
            "#..................X",
            "#...........#......#",
            "#...........###....#",
            "#......E...........#",
            "####################",
        }),

        // Room 4: Tight corridors. 4 enemies.
        new RoomDefinition(new[]
        {
            "####################",
            "#..................#",
            "#.######...........#",
            "#..................#",
            "#########..........X",
            "#......E....E......X",
            "#########..........X",
            "#..................#",
            "#.######...........#",
            "#......E....E......#",
            "####################",
        }),

        // Room 5: Open arena with pillars. 5 enemies.
        new RoomDefinition(new[]
        {
            "####################",
            "#..................#",
            "#...##......E..##..#",
            "#..................#",
            "#......E...........X",
            "#.....E.....E......X",
            "#..................X",
            "#..................#",
            "#...##.....E...##..#",
            "#..................#",
            "####################",
        }),
    };
}

public record RoomDefinition(string[] Layout);
