namespace SampleProject1;

/// <summary>
/// Handcrafted brick grid layouts. Each character encodes a brick's hit count:
///   '.' = empty cell
///   '1'-'4' = brick requiring that many hits to destroy
/// All rows must be 14 characters wide.
/// Row 0 is the top row; rows increase downward (world-space Y decreases).
/// </summary>
public static class LevelData
{

    // Level 1: Classic descending-toughness rows — learn the system.
    private static readonly string[] Level1 =
    {
        "11111111111111",
        "22222222222222",
        "22222222222222",
        "33333333333333",
        "33333333333333",
        "11111111111111",
        "..............",
        "..............",
    };

    // Level 2: Two channels the player can thread the ball through for big runs.
    private static readonly string[] Level2 =
    {
        "111..1111..111",
        "111..1111..111",
        "22222222222222",
        "22222222222222",
        "33333333333333",
        "33333333333333",
        "1.1.1.1.1.1.1.",
        "..............",
    };

    // Level 3: Nested fortress — outer 4-hit wall, inner ring, hollow center.
    // Ball must eat through the outer ring, then the inner ring, to score the center bricks.
    private static readonly string[] Level3 =
    {
        "44444444444444",
        "44..........44",
        "44.44444444.44",
        "44.4......4.44",
        "44.4......4.44",
        "44.44444444.44",
        "44..........44",
        "44444444444444",
    };
    public static readonly string[][] Levels = { Level1, Level2, Level3 };
}
