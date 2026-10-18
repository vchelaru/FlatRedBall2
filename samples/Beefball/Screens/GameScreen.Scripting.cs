using Beefball.Entities;
using Vector2 = System.Numerics.Vector2;

namespace Beefball.Screens;

/// <summary>Hooks the screenshot mode and headless checks use to stage states. Not used by normal play.</summary>
public partial class GameScreen
{
    public Player Player1 => _player1;
    public Player Player2 => _player2;
    public Puck Puck => _puck;
    public int Score1 => _scores[0];
    public int Score2 => _scores[1];
    public bool IsWon => _state == MatchState.Won;
    public bool IsGoalPause => _state == MatchState.GoalPause;

    /// <summary>Puck flying across the table toward player 2, so the trail and a hit burst show up.</summary>
    public void StageRally()
    {
        _player1.X = -150f; _player1.Y = 60f;
        _player1.CooldownRemaining = _config.Player.DashCooldownSeconds * 0.45f;
        _player2.X = 210f; _player2.Y = -20f;
        _puck.X = -40f; _puck.Y = -20f;
        _puck.Velocity = new Vector2(430f, 0f);
    }

    /// <summary>Fires the puck at the given team's opponent goal so it scores within a few frames.</summary>
    public void StageGoal(int scorer)
    {
        float dir = scorer == 1 ? 1f : -1f;
        _puck.X = dir * (Arena.HalfWidth - 20f);
        _puck.Y = 10f;
        _puck.Velocity = new Vector2(dir * 500f, 0f);
    }

    public void StageScores(int team1, int team2)
    {
        _scores[0] = team1;
        _scores[1] = team2;
        RefreshScores();
    }
}
