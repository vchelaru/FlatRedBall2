using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class CoinCollectorScreen : Screen
{
    private Factory<PlatformerPlayer> _playerFactory = null!;
    private Factory<PlatformCoin> _coinFactory = null!;

    private int _score;
    private float _timeRemaining = 60f;
    private bool _gameOver;

    private Label _scoreLabel = null!;
    private Label _timerLabel = null!;
    private Label _messageLabel = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(30, 30, 50);

        _playerFactory = new Factory<PlatformerPlayer>(this);
        _coinFactory = new Factory<PlatformCoin>(this);

        // Create level geometry using TileShapeCollection
        var tiles = new TileShapeCollection { GridSize = 32f };

        float halfW = Camera.TargetWidth / 2f;
        float halfH = Camera.TargetHeight / 2f;

        // Floor
        for (float x = -halfW; x < halfW; x += 32f)
            tiles.AddTileAtWorld(x, -halfH + 16f);

        // Left wall
        for (float y = -halfH; y < halfH; y += 32f)
            tiles.AddTileAtWorld(-halfW + 16f, y);

        // Right wall
        for (float y = -halfH; y < halfH; y += 32f)
            tiles.AddTileAtWorld(halfW - 16f, y);

        // Platforms — three tiers
        // Bottom-left platform
        for (float x = -halfW + 96f; x < -halfW + 288f; x += 32f)
            tiles.AddTileAtWorld(x, -halfH + 160f);

        // Bottom-right platform
        for (float x = halfW - 288f; x < halfW - 96f; x += 32f)
            tiles.AddTileAtWorld(x, -halfH + 160f);

        // Middle platform (centered)
        for (float x = -128f; x <= 128f; x += 32f)
            tiles.AddTileAtWorld(x, -halfH + 300f);

        // Upper-left platform
        for (float x = -halfW + 64f; x < -halfW + 224f; x += 32f)
            tiles.AddTileAtWorld(x, -halfH + 440f);

        // Upper-right platform
        for (float x = halfW - 224f; x < halfW - 64f; x += 32f)
            tiles.AddTileAtWorld(x, -halfH + 440f);

        tiles.IsVisible = true;
        Add(tiles);

        // Player — spawn on floor center
        var player = _playerFactory.Create();
        player.X = 0;
        player.Y = -halfH + 64f;

        // Coins — scatter across platforms
        SpawnCoin(-halfW + 192f, -halfH + 192f);
        SpawnCoin(halfW - 192f, -halfH + 192f);
        SpawnCoin(-64f, -halfH + 332f);
        SpawnCoin(64f, -halfH + 332f);
        SpawnCoin(0f, -halfH + 332f);
        SpawnCoin(-halfW + 128f, -halfH + 472f);
        SpawnCoin(halfW - 128f, -halfH + 472f);
        // A couple on the floor to get the player started
        SpawnCoin(-200f, -halfH + 64f);
        SpawnCoin(200f, -halfH + 64f);

        // Collision: player vs tiles (platformer bounce)
        AddCollisionRelationship(_playerFactory, tiles)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);

        // Collision: player vs coins (trigger — no physics)
        AddCollisionRelationship<PlatformerPlayer, PlatformCoin>(_playerFactory, _coinFactory)
            .CollisionOccurred += (_, coin) =>
            {
                coin.Destroy();
                _score++;
                _scoreLabel.Text = $"Coins: {_score}";

                if (_coinFactory.Instances.Count == 0)
                    WinGame();
            };

        // HUD
        _scoreLabel = new Label { Text = "Coins: 0" };
        _scoreLabel.Anchor(Anchor.TopLeft);
        _scoreLabel.X = 16;
        _scoreLabel.Y = 16;
        Add(_scoreLabel);

        _timerLabel = new Label { Text = "Time: 60" };
        _timerLabel.Anchor(Anchor.TopRight);
        _timerLabel.X = -16;
        _timerLabel.Y = 16;
        Add(_timerLabel);

        _messageLabel = new Label { Text = "" };
        _messageLabel.Anchor(Anchor.Center);
        _messageLabel.IsVisible = false;
        Add(_messageLabel);
    }

    private void SpawnCoin(float x, float y)
    {
        var coin = _coinFactory.Create();
        coin.X = x;
        coin.Y = y;
        coin.SetBaseY();
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_gameOver)
        {
            if (Engine.Input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.R))
                MoveToScreen<CoinCollectorScreen>();
            return;
        }

        _timeRemaining -= time.DeltaSeconds;
        if (_timeRemaining <= 0f)
        {
            _timeRemaining = 0f;
            LoseGame();
        }

        _timerLabel.Text = $"Time: {(int)MathF.Ceiling(_timeRemaining)}";
    }

    private void WinGame()
    {
        _gameOver = true;
        PauseThisScreen();
        _messageLabel.Text = $"You Win! All coins collected!\nPress R to restart";
        _messageLabel.IsVisible = true;
    }

    private void LoseGame()
    {
        _gameOver = true;
        PauseThisScreen();
        _messageLabel.Text = $"Time's Up! Collected {_score} coins.\nPress R to restart";
        _messageLabel.IsVisible = true;
    }
}
