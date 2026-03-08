using FlatRedBall2.Collision;

namespace ZombieSample.Screens;

/// <summary>
/// Level 2 — "Two Walls".
/// Two horizontal walls create three chambers (lower, middle, upper).
/// Each wall has a gap on opposite sides, requiring the player to zigzag through the level.
/// 8 zombies spread across all three chambers.
/// </summary>
public sealed class Level2Screen : GameScreen
{
    // Lower wall: row 5, cols 1–8 and 13–30. Gap at cols 9–12 (right-of-center).
    // Upper wall: row 12, cols 1–12 and 18–30. Gap at cols 13–17 (center).

    protected override (int Col, int Row) GetPlayerCell() => (2, 2);
    protected override (int Col, int Row) GetGoalCell()   => (29, 15);

    protected override IEnumerable<(int Col, int Row)> GetZombieCells() =>
        new (int, int)[]
        {
            // Lower chamber (rows 1–4)
            (15, 3),
            (25, 3),
            // Middle section (rows 6–11)
            ( 5,  8),
            (14,  7),
            (22,  9),
            (28,  6),
            // Upper chamber (rows 13–16)
            ( 8, 14),
            (24, 14),
        };

    protected override void PopulateTiles(TileShapeCollection tiles)
    {
        // Lower wall — gap at cols 9–12
        AddWallRect(tiles, col:  1, row: 5, width:  8, height: 1);
        AddWallRect(tiles, col: 13, row: 5, width: 18, height: 1);

        // Upper wall — gap at cols 13–17
        AddWallRect(tiles, col:  1, row: 12, width: 12, height: 1);
        AddWallRect(tiles, col: 18, row: 12, width: 13, height: 1);
    }

    protected override void OnWin() => MoveToScreen<Level3Screen>();
}
