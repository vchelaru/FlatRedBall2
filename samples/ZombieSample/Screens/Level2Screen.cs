using FlatRedBall2.Collision;

namespace ZombieSample.Screens;

/// <summary>
/// Level 2 — "Two Walls".
/// Two horizontal walls create three chambers; gaps on opposite sides force a zigzag path.
/// 8 zombies spread across all three chambers.
/// </summary>
public sealed class Level2Screen : GameScreen
{
    // Lower wall: row 11, cols 1–20 and 31–78.  Gap at cols 21–30.
    // Upper wall: row 30, cols 1–30 and 43–78.  Gap at cols 31–42.

    protected override (int Col, int Row) GetPlayerCell() => (3, 3);
    protected override (int Col, int Row) GetGoalCell()   => (74, 39);

    protected override IEnumerable<(int Col, int Row)> GetZombieCells() =>
        new (int, int)[]
        {
            // Lower chamber (rows 1–10)
            (37,  5),
            (62,  5),
            // Middle section (rows 12–29)
            (12, 20),
            (35, 17),
            (55, 22),
            (70, 14),
            // Upper chamber (rows 31–43)
            (20, 37),
            (60, 37),
        };

    protected override void PopulateTiles(TileShapeCollection tiles)
    {
        // Lower wall — gap at cols 21–30
        AddWallRect(tiles, col:  1, row: 11, width: 20, height: 1);
        AddWallRect(tiles, col: 31, row: 11, width: 48, height: 1);

        // Upper wall — gap at cols 31–42
        AddWallRect(tiles, col:  1, row: 30, width: 30, height: 1);
        AddWallRect(tiles, col: 43, row: 30, width: 36, height: 1);
    }

    protected override void OnWin() => MoveToScreen<Level3Screen>();
}
