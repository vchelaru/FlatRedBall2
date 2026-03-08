using FlatRedBall2.Collision;

namespace ZombieSample.Screens;

/// <summary>
/// Level 3 — "The Zigzag".
/// Two tall vertical walls divide the map into three lanes (left, center, right).
/// The left wall has a gap at the top; the right wall has a gap at the bottom.
/// The player must zigzag: left lane → cross top gap → center → cross bottom gap → right lane → goal.
/// 10 zombies are distributed across all three lanes.
/// </summary>
public sealed class Level3Screen : GameScreen
{
    // Left wall:  col 10, rows 1–12. Gap at rows 13–16 (near top).
    // Right wall: col 21, rows 4–16. Gap at rows 1–3 (near bottom).

    protected override (int Col, int Row) GetPlayerCell() => (3, 2);
    protected override (int Col, int Row) GetGoalCell()   => (28, 15);

    protected override IEnumerable<(int Col, int Row)> GetZombieCells() =>
        new (int, int)[]
        {
            // Left lane (cols 1–9)
            ( 5,  5),
            ( 7, 10),
            ( 4, 14),
            // Center lane (cols 11–20)
            (15,  3),
            (15, 10),
            (15, 15),
            // Right lane (cols 22–30) — heavy guard near goal
            (25,  3),
            (25,  8),
            (25, 12),
            (27, 15),
        };

    protected override void PopulateTiles(TileShapeCollection tiles)
    {
        // Left vertical wall — gap at rows 13–16 (top portion)
        AddWallRect(tiles, col: 10, row:  1, width: 1, height: 12);

        // Right vertical wall — gap at rows 1–3 (bottom portion)
        AddWallRect(tiles, col: 21, row:  4, width: 1, height: 13);
    }

    // Level 3 is the last level — winning goes to the WinScreen (default behavior).
}
