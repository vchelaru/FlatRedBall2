using FlatRedBall2.Collision;

namespace ZombieSample.Screens;

/// <summary>
/// Level 1 — "Wide Open".
/// A single horizontal center wall with a gap forces the player to pick a side.
/// 6 zombies: 2 start on the player's side, 4 guard the upper half near the goal.
/// </summary>
public sealed class Level1Screen : GameScreen
{
    // Center wall: row 9, cols 1–13 and 17–30. Gap at cols 14–16.
    // Player spawns bottom-left; goal is top-right.

    protected override (int Col, int Row) GetPlayerCell() => (2, 2);
    protected override (int Col, int Row) GetGoalCell()   => (29, 15);

    protected override IEnumerable<(int Col, int Row)> GetZombieCells() =>
        new (int, int)[]
        {
            ( 8,  5),  // lower-left, same side as player
            (22,  5),  // lower-right, cuts off the easy right path
            ( 6, 12),  // upper-left, guards above the wall
            (16, 12),  // near the gap — will chase through it
            (22, 12),  // upper-right
            (28, 13),  // near goal
        };

    protected override void PopulateTiles(TileShapeCollection tiles)
    {
        // Left segment of center wall (no gap on left border side)
        AddWallRect(tiles, col: 1,  row: 9, width: 13, height: 1);
        // Right segment — gap at cols 14-16
        AddWallRect(tiles, col: 17, row: 9, width: 14, height: 1);
    }

    protected override void OnWin() => MoveToScreen<Level2Screen>();
}
