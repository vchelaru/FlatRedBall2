using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class RiftTearPuzzle
{
    public Element[,] Grid { get; private set; } = new Element[3, 3];
    public Element[,] Target { get; private set; } = new Element[3, 3];
    public bool IsSolved => GridMatchesTarget();

    /// <summary>Shifts all 3 elements in the given row by one position.</summary>
    public void RotateRow(int row, bool right)
    {
        if (row < 0 || row > 2) return;

        if (right)
        {
            var temp = Grid[row, 2];
            Grid[row, 2] = Grid[row, 1];
            Grid[row, 1] = Grid[row, 0];
            Grid[row, 0] = temp;
        }
        else
        {
            var temp = Grid[row, 0];
            Grid[row, 0] = Grid[row, 1];
            Grid[row, 1] = Grid[row, 2];
            Grid[row, 2] = temp;
        }
    }

    /// <summary>Shifts all 3 elements in the given column by one position.</summary>
    public void RotateColumn(int col, bool down)
    {
        if (col < 0 || col > 2) return;

        if (down)
        {
            var temp = Grid[2, col];
            Grid[2, col] = Grid[1, col];
            Grid[1, col] = Grid[0, col];
            Grid[0, col] = temp;
        }
        else
        {
            var temp = Grid[0, col];
            Grid[0, col] = Grid[1, col];
            Grid[1, col] = Grid[2, col];
            Grid[2, col] = temp;
        }
    }

    /// <summary>
    /// Generates a puzzle by starting from a solved state and applying random moves.
    /// Difficulty (1-3) determines the number of scramble moves.
    /// </summary>
    public static RiftTearPuzzle Generate(int difficulty, Random? random = null)
    {
        random ??= Random.Shared;
        var puzzle = new RiftTearPuzzle();

        // Pick random elements for the target
        var elements = new[] { Element.Steam, Element.Fire, Element.Ice, Element.Lightning, Element.Aether, Element.Glitch };
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                var el = elements[random.Next(elements.Length)];
                puzzle.Target[r, c] = el;
                puzzle.Grid[r, c] = el;
            }

        // Scramble by applying random moves (inverse of what the player must do)
        int moves = Math.Clamp(difficulty, 1, 3);
        for (int i = 0; i < moves; i++)
        {
            bool isRow = random.Next(2) == 0;
            int index = random.Next(3);
            bool direction = random.Next(2) == 0;

            if (isRow)
                puzzle.RotateRow(index, direction);
            else
                puzzle.RotateColumn(index, direction);
        }

        // Ensure the puzzle isn't already solved after scrambling
        if (puzzle.IsSolved)
            puzzle.RotateRow(0, true);

        return puzzle;
    }

    private bool GridMatchesTarget()
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                if (Grid[r, c] != Target[r, c])
                    return false;
        return true;
    }
}
