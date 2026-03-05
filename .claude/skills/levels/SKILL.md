---
name: levels
description: "Level Data in FlatRedBall2. Use when working with level layouts, tile grids, level progression, loading level data, parsing maps, or transitioning between levels. Covers grid-based string layouts, record-based placement, and level advancement patterns."
---

# Level Data in FlatRedBall2

Level layouts are defined in code, in a dedicated file separate from screen logic. This keeps data easy to scan and edit without touching game logic.

> **Future:** TMX and JSON level sources are planned. Keeping layouts in a separate file means the swap only touches one place.

## Separate Data from Logic

Create a dedicated file (e.g., `LevelData.cs`) containing only static data:

```csharp
public static class LevelData
{
    public static readonly string[][] Levels = { Level1, Level2, Level3 };

    private static readonly string[] Level1 =
    {
        "##########",
        "#........#",
        "#.@@....@#",
        "#........#",
        "##########",
    };

    private static readonly string[] Level2 = { /* ... */ };
    private static readonly string[] Level3 = { /* ... */ };
}
```

The character encoding is entirely up to the game. Define what each character means in a comment at the top of the file, or in the parsing method.

## Parsing in a Screen

The screen reads the data and spawns entities. Define the mapping between characters and entity types in a `LoadLevel` method:

```csharp
public class GameScreen : Screen
{
    public int LevelIndex { get; set; } = 0;

    public override void CustomInitialize()
    {
        _wallFactory  = new Factory<Wall>(this);
        _playerFactory = new Factory<Player>(this);
        // ... other factories ...

        LoadLevel(LevelData.Levels[LevelIndex]);
    }

    private void LoadLevel(string[] layout)
    {
        float cellSize = 64f;
        int cols = layout[0].Length;
        int rows = layout.Length;

        float startX = -(cols / 2f) * cellSize + cellSize / 2f;
        float startY =  (rows / 2f) * cellSize - cellSize / 2f;

        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < layout[row].Length; col++)
            {
                float x = startX + col * cellSize;
                float y = startY - row * cellSize;  // row 0 is top; Y decreases downward

                switch (layout[row][col])
                {
                    case '#':
                        var wall = _wallFactory.Create();
                        wall.X = x; wall.Y = y;
                        break;
                    case '@':
                        var enemy = _enemyFactory.Create();
                        enemy.X = x; enemy.Y = y;
                        break;
                    case 'P':
                        var player = _playerFactory.Create();
                        player.X = x; player.Y = y;
                        break;
                }
            }
        }
    }
}
```

Row 0 is the top of the grid. Each successive row subtracts `cellSize` from Y (world space is Y+ up).

## Level Advancement

Pass the next level index when transitioning screens:

```csharp
if (levelComplete)
{
    int next = LevelIndex + 1;
    if (next < LevelData.Levels.Length)
        MoveToScreen<GameScreen>(s => s.LevelIndex = next);
    else
        MoveToScreen<GameOverScreen>(s => s.Win = true);
}
```

## Non-Grid Layouts

For games where entities aren't on a fixed grid, use a list of placement records instead:

```csharp
public static class LevelData
{
    public record EnemyPlacement(float X, float Y, string Type);

    public static readonly EnemyPlacement[][] EnemyLevels =
    {
        // Level 1
        new[]
        {
            new EnemyPlacement(100f, 200f, "Grunt"),
            new EnemyPlacement(-80f, 350f, "Shooter"),
        },
    };
}
```
