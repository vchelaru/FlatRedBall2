using FlatRedBall2.Collision;

namespace ZombieSample.Screens;

/// <summary>
/// Level 3 — "The Zigzag".
/// Two vertical walls divide the map into three lanes.
/// Left wall has a gap at the top; right wall has a gap at the bottom.
/// Player must zigzag through all three lanes to reach the goal.
/// 10 zombies across all lanes.
/// </summary>
public sealed class Level3Screen : GameScreen
{
    // Left wall:  col 25, rows 1–29.  Gap at rows 30–43 (near top).
    // Right wall: col 54, rows 8–43.  Gap at rows 1–7  (near bottom).

    protected override (int Col, int Row) GetPlayerCell() => (7, 5);
    protected override (int Col, int Row) GetGoalCell()   => (72, 39);

    protected override IEnumerable<(int Col, int Row)> GetZombieCells() =>
        new (int, int)[]
        {
            // Left lane (cols 1–24)
            (10, 10),
            (15, 22),
            ( 8, 35),
            // Center lane (cols 26–53)
            (39,  7),
            (39, 22),
            (39, 37),
            // Right lane (cols 55–78)
            (65,  7),
            (65, 20),
            (65, 32),
            (70, 39),
        };

    protected override void PopulateTiles(TileShapeCollection tiles)
    {
        // Left vertical wall — gap at rows 30–43 (top)
        AddWallRect(tiles, col: 25, row:  1, width: 1, height: 29);

        // Right vertical wall — gap at rows 1–7 (bottom)
        AddWallRect(tiles, col: 54, row:  8, width: 1, height: 36);
    }

    // Level 3 is the last level — OnWin goes to WinScreen (default).
}
