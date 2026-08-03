namespace ArcticCrossingSample.Data;

/// Shared game state that persists across screens.
/// Passed via MoveToScreen configure callbacks.
public class GameState
{
    public bool IsFemale { get; set; }
    public int HighestUnlockedPhase { get; set; } = 1;
    public int[] HighScores { get; } = new int[6]; // index 1-5 for phases

    public void UnlockNextPhase(int justCompleted)
    {
        if (justCompleted + 1 > HighestUnlockedPhase && justCompleted < PhaseDefinitions.TotalPhases)
            HighestUnlockedPhase = justCompleted + 1;
    }

    public void UpdateHighScore(int phase, int score)
    {
        if (score > HighScores[phase])
            HighScores[phase] = score;
    }
}
