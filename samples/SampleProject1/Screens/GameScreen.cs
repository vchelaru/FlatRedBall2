using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
// Alias: Gum.Forms.Controls also defines RepositionDirections (for UI layout) — disambiguate.
using RepositionDirections = FlatRedBall2.Collision.RepositionDirections;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class GameScreen : Screen
{
    // Set by MoveToScreen configure before CustomInitialize for level progression
    public int LevelIndex { get; set; } = 0;
    public int InitialScore { get; set; } = 0;
    public int InitialLives { get; set; } = 3;
    public int InitialPassiveMultiplier { get; set; } = 1;

    public static int HighScore { get; private set; } = 0;

    private Factory<TrailParticle> _trailParticleFactory = null!;
    private Factory<Wall> _wallFactory = null!;
    private Factory<Paddle> _paddleFactory = null!;
    private Factory<Ball> _ballFactory = null!;
    private Factory<Brick> _brickFactory = null!;
    private Factory<DeathZone> _deathZoneFactory = null!;
    private Factory<ScoreFloater> _scoreFloaterFactory = null!;

    // Brick grid for RepositionDirections management: tracks which grid cells are occupied
    // so interior sides can be suppressed (preventing ball snagging at brick seams).
    private readonly Dictionary<(int col, int row), Brick> _brickGrid = new();
    private readonly Dictionary<Brick, (int col, int row)> _brickToGrid = new();

    private Paddle _paddle = null!;
    private Ball? _ball;

    private bool _ballAttached;
    private float _ballSpeed = 350f;
    private const float BallSpeedIncreaseRate = 20f;
    private const float MaxBallSpeed = 700f;

    private int _lives;
    private int _score;
    private int _passiveMultiplier;
    private int _activeMultiplier;
    private int _initialBrickCount;
    private bool _levelComplete;

    private Label _scoreLabel = null!;
    private Label _livesLabel = null!;
    private Label _passiveLabel = null!;
    private Label _activeLabel = null!;
    private Label _launchHint = null!;

    public override void CustomInitialize()
    {
        _score = InitialScore;
        _lives = InitialLives;
        _passiveMultiplier = InitialPassiveMultiplier;
        _activeMultiplier = 1;

        Camera.BackgroundColor = new Color(8, 8, 25);

        // TrailParticle factory must be registered first — Ball.CustomActivity calls GetFactory<TrailParticle>()
        _trailParticleFactory = new Factory<TrailParticle>(this);
        _wallFactory = new Factory<Wall>(this);
        _paddleFactory = new Factory<Paddle>(this);
        _ballFactory = new Factory<Ball>(this);
        _brickFactory = new Factory<Brick>(this);
        _deathZoneFactory = new Factory<DeathZone>(this);
        _scoreFloaterFactory = new Factory<ScoreFloater>(this);

        SpawnBoundaryWalls();

        _paddle = _paddleFactory.Create();
        _paddle.X = 0f;
        _paddle.Y = -300f;

        var deathZone = _deathZoneFactory.Create();
        deathZone.X = 0f;
        deathZone.Y = -420f;

        LoadLevel(LevelData.Levels[LevelIndex]);
        SetupCollision(deathZone);
        SetupHud();
        SpawnBall();
    }

    private void SpawnBoundaryWalls()
    {
        var left = _wallFactory.Create();
        left.X = -680f;
        left.Rectangle.Width = 80f;
        left.Rectangle.Height = 1000f;

        var right = _wallFactory.Create();
        right.X = 680f;
        right.Rectangle.Width = 80f;
        right.Rectangle.Height = 1000f;

        var top = _wallFactory.Create();
        top.Y = 400f;
        top.Rectangle.Width = 1500f;
        top.Rectangle.Height = 80f;
    }

    private void SpawnBall()
    {
        _ball = _ballFactory.Create();
        _ball.X = _paddle.X;
        _ball.Y = _paddle.Y + Paddle.PaddleHeight / 2f + 9f + 2f;
        _ball.VelocityX = 0f;
        _ball.VelocityY = 0f;
        _ballAttached = true;
    }

    private void LoadLevel(string[] layout)
    {
        const float cellW = Brick.BrickWidth + 4f;
        const float cellH = Brick.BrickHeight + 4f;
        int cols = layout[0].Length;

        // Center the grid horizontally
        float startX = -(cols * cellW - 4f) / 2f + Brick.BrickWidth / 2f;
        float startY = 260f;

        for (int row = 0; row < layout.Length; row++)
        {
            for (int col = 0; col < layout[row].Length; col++)
            {
                char c = layout[row][col];
                if (c == '.') continue;

                var brick = _brickFactory.Create();
                brick.X = startX + col * cellW;
                brick.Y = startY - row * cellH;
                brick.HitsRemaining = c - '0';
                brick.UpdateColor();

                _brickGrid[(col, row)] = brick;
                _brickToGrid[brick] = (col, row);
            }
        }

        _initialBrickCount = _brickFactory.Instances.Count;

        // Suppress interior sides so the ball glides across brick surfaces without snagging.
        // In grid coords, row 0 is the top row; higher rows are lower in world space.
        foreach (var ((col, row), brick) in _brickGrid)
        {
            var dirs = RepositionDirections.All;
            if (_brickGrid.ContainsKey((col - 1, row))) dirs &= ~RepositionDirections.Left;
            if (_brickGrid.ContainsKey((col + 1, row))) dirs &= ~RepositionDirections.Right;
            if (_brickGrid.ContainsKey((col, row - 1))) dirs &= ~RepositionDirections.Up;   // row-1 = above in world
            if (_brickGrid.ContainsKey((col, row + 1))) dirs &= ~RepositionDirections.Down; // row+1 = below in world
            brick.Rectangle.RepositionDirections = dirs;
        }
    }

    private void SetupCollision(DeathZone deathZone)
    {
        // Ball vs walls — physically bounce and re-normalize to constant speed
        AddCollisionRelationship<Ball, Wall>(_ballFactory, _wallFactory)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f)
            .CollisionOccurred += (ball, _) => NormalizeBallVelocity(ball);

        // Ball vs paddle — override the built-in bounce with skill-based angle
        AddCollisionRelationship<Ball, Paddle>(_ballFactory, _paddleFactory)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f)
            .CollisionOccurred += (ball, paddle) =>
            {
                if (_ballAttached) return;
                ApplyPaddleBounce(ball, paddle);
                _passiveMultiplier++;
                _activeMultiplier = 1;
                UpdateHud();
            };

        // Ball vs brick — bounce, score, decrement hit count
        AddCollisionRelationship<Ball, Brick>(_ballFactory, _brickFactory)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f)
            .CollisionOccurred += (ball, brick) =>
            {
                int points = brick.HitValue * _passiveMultiplier * _activeMultiplier;
                _score += points;
                _activeMultiplier++;
                NormalizeBallVelocity(ball);

                var floater = _scoreFloaterFactory.Create();
                floater.X = brick.X;
                floater.Y = brick.Y;
                floater.Points = points;

                // If this hit will destroy the brick, restore its neighbors' RepositionDirections
                // before calling TakeHit so the grid is consistent.
                bool willDestroy = brick.HitsRemaining == 1;
                (int col, int row) brickPos = default;
                bool wasTracked = willDestroy && _brickToGrid.TryGetValue(brick, out brickPos);
                if (wasTracked)
                {
                    _brickGrid.Remove(brickPos);
                    _brickToGrid.Remove(brick);
                }

                brick.TakeHit();

                if (wasTracked)
                    RestoreNeighborDirections(brickPos.col, brickPos.row);

                UpdateHud();
            };

        // Ball falls past paddle — lose a life
        AddCollisionRelationship<Ball, DeathZone>(_ballFactory, _deathZoneFactory)
            .CollisionOccurred += (ball, _) =>
            {
                ball.Destroy();
                _ball = null;
                _lives--;
                _passiveMultiplier = 1;
                _activeMultiplier = 1;

                if (_lives <= 0)
                {
                    if (_score > HighScore) HighScore = _score;
                    MoveToScreen<GameOverScreen>(s => { s.FinalScore = _score; s.Won = false; });
                }
                else
                {
                    SpawnBall();
                    UpdateHud();
                }
            };
    }

    // When a brick is destroyed, its four neighbors' RepositionDirections gain back the side
    // that faced the destroyed brick — that side is now an exterior surface again.
    private void RestoreNeighborDirections(int col, int row)
    {
        if (_brickGrid.TryGetValue((col - 1, row), out var left))  left.Rectangle.RepositionDirections  |= RepositionDirections.Right;
        if (_brickGrid.TryGetValue((col + 1, row), out var right)) right.Rectangle.RepositionDirections |= RepositionDirections.Left;
        if (_brickGrid.TryGetValue((col, row - 1), out var above)) above.Rectangle.RepositionDirections |= RepositionDirections.Down;
        if (_brickGrid.TryGetValue((col, row + 1), out var below)) below.Rectangle.RepositionDirections |= RepositionDirections.Up;
    }

    /// <summary>
    /// Computes ball exit angle based on where the ball hits the paddle.
    /// Edges produce steep angles; center produces a straighter shot.
    /// Always sends the ball upward (positive VelocityY).
    /// </summary>
    private void ApplyPaddleBounce(Ball ball, Paddle paddle)
    {
        float offset = (ball.X - paddle.X) / (Paddle.PaddleWidth / 2f);
        offset = Math.Clamp(offset, -0.95f, 0.95f);

        // Guarantee minimum horizontal component to prevent the ball going perfectly vertical
        if (MathF.Abs(offset) < 0.15f)
            offset = offset >= 0f ? 0.15f : -0.15f;

        float maxAngle = 65f * MathF.PI / 180f;
        float angle = offset * maxAngle;

        ball.VelocityX = _ballSpeed * MathF.Sin(angle);
        ball.VelocityY = _ballSpeed * MathF.Cos(angle);  // Cos(angle) is always positive → always upward
    }

    private void NormalizeBallVelocity(Ball ball)
    {
        float speed = MathF.Sqrt(ball.VelocityX * ball.VelocityX + ball.VelocityY * ball.VelocityY);
        if (speed > 0.01f)
        {
            ball.VelocityX = ball.VelocityX / speed * _ballSpeed;
            ball.VelocityY = ball.VelocityY / speed * _ballSpeed;
        }
    }

    private void SetupHud()
    {
        var hud = new Panel();
        hud.Dock(Dock.Fill);

        _livesLabel = new Label { Text = "Lives: 3" };
        _livesLabel.Anchor(Anchor.TopLeft);
        _livesLabel.X = 10;
        _livesLabel.Y = 10;
        hud.AddChild(_livesLabel);

        _passiveLabel = new Label { Text = "Passive: x1" };
        _passiveLabel.Anchor(Anchor.TopLeft);
        _passiveLabel.X = 10;
        _passiveLabel.Y = 35;
        hud.AddChild(_passiveLabel);

        _activeLabel = new Label { Text = "Active: x1" };
        _activeLabel.Anchor(Anchor.TopLeft);
        _activeLabel.X = 10;
        _activeLabel.Y = 60;
        hud.AddChild(_activeLabel);

        _scoreLabel = new Label { Text = "Score: 0" };
        _scoreLabel.Anchor(Anchor.Top);
        _scoreLabel.Y = 10;
        hud.AddChild(_scoreLabel);

        var levelLabel = new Label { Text = $"Level {LevelIndex + 1}" };
        levelLabel.Anchor(Anchor.TopRight);
        levelLabel.X = -10;
        levelLabel.Y = 10;
        hud.AddChild(levelLabel);

        _launchHint = new Label { Text = "" };
        _launchHint.Anchor(Anchor.Top);
        _launchHint.Y = 680;
        hud.AddChild(_launchHint);

        Add(hud);
        UpdateHud();
    }

    private void UpdateHud()
    {
        _scoreLabel.Text = $"Score: {_score}";
        _livesLabel.Text = $"Lives: {_lives}";
        _passiveLabel.Text = $"Passive: x{_passiveMultiplier}";
        _activeLabel.Text = $"Active: x{_activeMultiplier}";
        _launchHint.Text = _ballAttached ? "Press SPACE to launch" : "";
    }

    public override void CustomActivity(FrameTime time)
    {
        _ballSpeed = Math.Min(MaxBallSpeed, _ballSpeed + BallSpeedIncreaseRate * time.DeltaSeconds);

        if (_ballAttached && _ball != null)
        {
            // Keep ball positioned above paddle center while it hasn't been launched
            _ball.X = _paddle.X;
            _ball.Y = _paddle.Y + Paddle.PaddleHeight / 2f + 9f + 2f;
            _ball.VelocityX = 0f;
            _ball.VelocityY = 0f;

            if (Engine.Input.Keyboard.WasKeyPressed(Keys.Space))
            {
                _ballAttached = false;
                _ball.VelocityY = _ballSpeed;
                UpdateHud();
            }
        }

        if (!_levelComplete && _initialBrickCount > 0 && _brickFactory.Instances.Count == 0)
        {
            _levelComplete = true;
            int nextLevel = LevelIndex + 1;

            if (nextLevel < LevelData.Levels.Length)
            {
                MoveToScreen<GameScreen>(s =>
                {
                    s.LevelIndex = nextLevel;
                    s.InitialScore = _score;
                    s.InitialLives = _lives;
                    s.InitialPassiveMultiplier = _passiveMultiplier;
                });
            }
            else
            {
                if (_score > HighScore) HighScore = _score;
                MoveToScreen<GameOverScreen>(s => { s.FinalScore = _score; s.Won = true; });
            }
        }
    }
}
