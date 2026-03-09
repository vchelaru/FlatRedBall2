using FlatRedBall2.Collision;

namespace ZombieSample.Screens;

/// <summary>
/// Level 1 — "Wide Open".
/// A single horizontal wall at mid-screen with an off-center gap.
/// Player starts bottom-left; goal is top-right.
/// 6 zombies spread across both halves.
/// </summary>
public sealed class Level1Screen : GameScreen
{
    protected override (int Col, int Row) GetPlayerCell() => (3, 3);
    protected override (int Col, int Row) GetGoalCell()   => (74, 39);

    protected override IEnumerable<(int Col, int Row)> GetZombieCells() =>
        new (int, int)[]
        {
            (20, 10),   // lower-left
            (55, 10),   // lower-right
            (15, 30),   // upper-left
            (40, 30),   // near gap, upper
            (55, 30),   // upper-right
            (70, 33),   // near goal
        };

    protected override void PopulateTiles(TileShapeCollection tiles)
    {
        // Left segment — gap starts at col 29
        AddWallRect(tiles, col: 1,  row: 22, width: 28, height: 1);
        // Right segment — gap ends at col 37
        AddWallRect(tiles, col: 38, row: 22, width: 41, height: 1);
    }

    protected override void OnWin() => MoveToScreen<Level2Screen>();
}
