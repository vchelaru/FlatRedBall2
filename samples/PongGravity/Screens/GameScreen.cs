using System.Collections.Generic;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Microsoft.Xna.Framework;
using PongGravity.Entities;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace PongGravity.Screens;

public class GameScreen : Screen
{
    // Factories
    private Factory<Ball> _ballFactory = null!;
    private Factory<Paddle> _paddleFactory = null!;
    private Factory<GravityWell> _wellFactory = null!;

    // Game objects
    private Ball _ball = null!;
    private Paddle _p1 = null!;
    private Paddle _p2 = null!;

    // Scores
    private int _scoreP1;
    private int _scoreP2;
    private const int WinScore = 7;

    // Boundaries
    private const float FieldHalfWidth = 620f;
    private const float FieldHalfHeight = 320f;
    private const float PaddleX = 580f;

    // Gravity well spawning
    private float _wellSpawnTimer = WellSpawnInterval;
    private const float WellSpawnInterval = 6f;
    private const float GravityConstant = 60000f;
    private const float MinDist = 20f; // clamp at close range to cap max force
    // MaxGravityDist lives in GravityWell.InfluenceRadius so the visual circle matches exactly

    // Teleport cooldown
    private float _teleportCooldown = 0f;

    // UI
    private GameScreenGum _hud = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new XnaColor(8, 8, 20);

        _ballFactory = new Factory<Ball>(this);
        _paddleFactory = new Factory<Paddle>(this);
        _wellFactory = new Factory<GravityWell>(this);

        SetupPaddles();
        SetupBall();
        SetupCollision();
        SetupUI();
    }

    private void SetupPaddles()
    {
        _p1 = _paddleFactory.Create();
        _p1.X = -PaddleX;
        _p1.Y = 0f;
        _p1.SetKeys(Microsoft.Xna.Framework.Input.Keys.W, Microsoft.Xna.Framework.Input.Keys.S);
        _p1.Rectangle.Color = new XnaColor(80, 180, 255, 255);

        _p2 = _paddleFactory.Create();
        _p2.X = PaddleX;
        _p2.Y = 0f;
        _p2.SetKeys(Microsoft.Xna.Framework.Input.Keys.Up, Microsoft.Xna.Framework.Input.Keys.Down);
        _p2.Rectangle.Color = new XnaColor(255, 120, 80, 255);
    }

    private void SetupBall()
    {
        _ball = _ballFactory.Create();
        _ball.X = 0f;
        _ball.Y = 0f;
        _ball.Launch();
    }

    private void SetupCollision()
    {
        // Trigger-only: no movement response — teleport is handled in the callback
        AddCollisionRelationship<Ball, GravityWell>(_ballFactory, _wellFactory)
            .CollisionOccurred += (ball, well) =>
            {
                if (_teleportCooldown > 0f || well.Partner == null) return;
                ball.X = well.Partner.X;
                ball.Y = well.Partner.Y;
                _teleportCooldown = 0.5f;
            };

        AddCollisionRelationship<Ball, Paddle>(_ballFactory, _paddleFactory)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 1f)
            .CollisionOccurred += (ball, paddle) =>
            {
                // Where on the paddle did it hit? -1 = bottom edge, +1 = top edge
                float hitFactor = Math.Clamp(
                    (ball.Y - paddle.Y) / (paddle.Rectangle.Height / 2f), -1f, 1f);

                // Speed up slightly on each paddle hit, capped at 700
                float speed = MathF.Sqrt(ball.VelocityX * ball.VelocityX + ball.VelocityY * ball.VelocityY);
                speed = MathF.Min(speed * 1.05f, 700f);

                // Launch angle: hit position drives Y influence (±45°), plus a small random jitter
                float angle = hitFactor * (MathF.PI / 4f)
                    + Engine.Random.Between(-0.12f, 0.12f);

                // VelocityX direction is already correct after BounceOnCollision
                float dir = MathF.Sign(ball.VelocityX);
                ball.VelocityX = dir * MathF.Cos(angle) * speed;
                ball.VelocityY = MathF.Sin(angle) * speed;
            };
    }

    private void SetupUI()
    {
        _hud = new GameScreenGum();
        Add(_hud);
    }

    public override void CustomActivity(FrameTime time)
    {
        HandleTopBottomBounce();
        ApplyGravityWells(time.DeltaSeconds);
        HandleScoring();
        UpdateWellSpawnTimer(time);

        if (_teleportCooldown > 0f)
            _teleportCooldown -= time.DeltaSeconds;
    }

    private void HandleTopBottomBounce()
    {
        const float BallRadius = 12f;
        if (_ball.Y + BallRadius > FieldHalfHeight)
        {
            _ball.Y = FieldHalfHeight - BallRadius;
            if (_ball.VelocityY > 0f) _ball.VelocityY = -_ball.VelocityY;
        }
        else if (_ball.Y - BallRadius < -FieldHalfHeight)
        {
            _ball.Y = -FieldHalfHeight + BallRadius;
            if (_ball.VelocityY < 0f) _ball.VelocityY = -_ball.VelocityY;
        }
    }

    private void ApplyGravityWells(float deltaSeconds)
    {
        // Reset ball acceleration each frame before accumulating gravity
        _ball.AccelerationX = 0f;
        _ball.AccelerationY = 0f;

        foreach (var well in _wellFactory.Instances)
        {
            float dx = well.X - _ball.X;
            float dy = well.Y - _ball.Y;
            float dist = MathF.Sqrt(dx * dx + dy * dy);

            bool inRange = dist > 0.001f && dist <= GravityWell.InfluenceRadius;

            well.SetInfluencing(inRange, deltaSeconds);

            if (!inRange) continue;

            float clampedDist = MathF.Max(dist, MinDist);
            float force = GravityConstant / clampedDist; // linear falloff
            float sign = well.IsBlackHole ? 1f : -1f;   // pull vs push
            _ball.AccelerationX += sign * (dx / dist) * force;
            _ball.AccelerationY += sign * (dy / dist) * force;
        }
    }

    private void HandleScoring()
    {
        if (_ball.X > FieldHalfWidth)
        {
            _scoreP1++;
            _hud.P1ScoreLabel.Text = _scoreP1.ToString();
            if (CheckWin(1)) return;
            ResetRound();
        }
        else if (_ball.X < -FieldHalfWidth)
        {
            _scoreP2++;
            _hud.P2ScoreLabel.Text = _scoreP2.ToString();
            if (CheckWin(2)) return;
            ResetRound();
        }
    }

    // Returns true if the game is over (screen transition requested).
    private bool CheckWin(int player)
    {
        if (_scoreP1 >= WinScore || _scoreP2 >= WinScore)
        {
            WinScreen.Winner = player;
            MoveToScreen<WinScreen>();
            return true;
        }
        return false;
    }

    private void ResetRound()
    {
        // Destroy all gravity wells
        foreach (var well in _wellFactory)
            well.Destroy();

        // Reset ball
        _ball.X = 0f;
        _ball.Y = 0f;
        _ball.AccelerationX = 0f;
        _ball.AccelerationY = 0f;
        _ball.Launch();

        // Reset spawn timer and teleport cooldown
        _wellSpawnTimer = WellSpawnInterval;
        _teleportCooldown = 0f;
    }

    private void UpdateWellSpawnTimer(FrameTime time)
    {
        _wellSpawnTimer -= time.DeltaSeconds;
        if (_wellSpawnTimer <= 0f)
        {
            SpawnWellPair();
            _wellSpawnTimer = WellSpawnInterval;
        }
    }

    private void SpawnWellPair()
    {
        var blackHole = _wellFactory.Create();
        blackHole.X = Engine.Random.Between(-450f, 450f);
        blackHole.Y = Engine.Random.Between(-260f, 260f);
        blackHole.SetupAsBlackHole();

        var whiteHole = _wellFactory.Create();
        whiteHole.X = Engine.Random.Between(-450f, 450f);
        whiteHole.Y = Engine.Random.Between(-260f, 260f);
        whiteHole.SetupAsWhiteHole();

        blackHole.Partner = whiteHole;
        whiteHole.Partner = blackHole;
    }
}
