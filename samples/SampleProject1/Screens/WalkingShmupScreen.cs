using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class WalkingShmupScreen : Screen
{
    private enum GamePhase { Start, Playing, GameOver }

    // Factories
    private Factory<ShmupPlayer> _playerFactory = null!;
    private Factory<ShmupBullet> _bulletFactory = null!;
    private Factory<ShmupEnemy> _enemyFactory = null!;
    private Factory<ShmupObstacle> _obstacleFactory = null!;

    private ShmupPlayer _player = null!;

    // Game state
    private GamePhase _phase = GamePhase.Start;
    private int _score;

    // Scrolling
    private const float ScrollSpeed = 60f;
    private float _worldY; // tracks how far the camera has scrolled

    // Spawning
    private float _nextSpawnY;
    private const float SpawnInterval = 300f; // world units between spawn rows
    private float _halfWidth;
    private float _halfHeight;

    // Cleanup threshold — destroy entities this far below the camera
    private const float CleanupMargin = 200f;

    // UI
    private Label _scoreLabel = null!;
    private Label _hpLabel = null!;
    private Label _announcementLabel = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 20, 30);
        _halfWidth = Camera.TargetWidth / 2f;
        _halfHeight = Camera.TargetHeight / 2f;

        // Factories
        _playerFactory = new Factory<ShmupPlayer>(this);
        _bulletFactory = new Factory<ShmupBullet>(this);
        _enemyFactory = new Factory<ShmupEnemy>(this);
        _obstacleFactory = new Factory<ShmupObstacle>(this);

        // Player
        _player = _playerFactory.Create();
        _player.X = 0f;
        _player.Y = -_halfHeight * 0.5f;

        // Collision relationships
        // Player vs obstacles — obstacles are immovable walls
        AddCollisionRelationship<ShmupPlayer, ShmupObstacle>(_playerFactory, _obstacleFactory)
            .MoveFirstOnCollision();

        // Player vs enemies — contact damage (trigger only, no physics push)
        AddCollisionRelationship<ShmupPlayer, ShmupEnemy>(_playerFactory, _enemyFactory)
            .CollisionOccurred += (player, _) =>
            {
                player.TakeDamage();
            };

        // Bullets vs enemies — destroy both
        AddCollisionRelationship<ShmupBullet, ShmupEnemy>(_bulletFactory, _enemyFactory)
            .CollisionOccurred += (bullet, enemy) =>
            {
                _score += enemy.PointValue;
                bullet.Destroy();
                enemy.Destroy();
            };

        // Bullets vs obstacles — bullets blocked by walls
        AddCollisionRelationship<ShmupBullet, ShmupObstacle>(_bulletFactory, _obstacleFactory)
            .CollisionOccurred += (bullet, _) =>
            {
                bullet.Destroy();
            };

        // Initial spawn zone starts above the visible area
        _nextSpawnY = _halfHeight + 100f;

        // Place some initial obstacles and enemies
        SpawnRow(_nextSpawnY);
        _nextSpawnY += SpawnInterval;
        SpawnRow(_nextSpawnY);
        _nextSpawnY += SpawnInterval;

        // UI
        SetupUI();
    }

    private void SetupUI()
    {
        _scoreLabel = new Label();
        _scoreLabel.Text = "Score: 0";
        _scoreLabel.Anchor(Anchor.TopRight);
        _scoreLabel.X = -20;
        _scoreLabel.Y = 10;
        Add(_scoreLabel);

        _hpLabel = new Label();
        _hpLabel.Text = "HP: 3";
        _hpLabel.Anchor(Anchor.TopLeft);
        _hpLabel.X = 20;
        _hpLabel.Y = 10;
        Add(_hpLabel);

        _announcementLabel = new Label();
        _announcementLabel.Anchor(Anchor.Center);
        _announcementLabel.Text = "Press SPACE to Start!";
        Add(_announcementLabel);
    }

    public override void CustomActivity(FrameTime time)
    {
        switch (_phase)
        {
            case GamePhase.Start:
                UpdateStartPhase();
                break;
            case GamePhase.Playing:
                UpdatePlayingPhase(time);
                break;
            case GamePhase.GameOver:
                UpdateGameOverPhase();
                break;
        }

        // Always update UI
        _scoreLabel.Text = $"Score: {_score}";
        _hpLabel.Text = $"HP: {(_player.IsDead ? 0 : _player.Hp)}";
    }

    private void UpdateStartPhase()
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Space))
        {
            _phase = GamePhase.Playing;
            _announcementLabel.IsVisible = false;
        }
    }

    private void UpdatePlayingPhase(FrameTime time)
    {
        // Scroll camera upward
        Camera.VelocityY = ScrollSpeed;
        _worldY = Camera.Y;

        // Bottom screen edge push — if player falls behind camera bottom, push them up
        float cameraBottom = _worldY - _halfHeight;
        float playerBottom = _player.Y - _player.Rectangle.Height / 2f;
        if (playerBottom < cameraBottom)
        {
            _player.Y = cameraBottom + _player.Rectangle.Height / 2f;
        }

        // Clamp player to screen bounds (left, right, top)
        float cameraLeft = -_halfWidth;
        float cameraRight = _halfWidth;
        float cameraTop = _worldY + _halfHeight;
        float playerHalfW = _player.Rectangle.Width / 2f;
        float playerHalfH = _player.Rectangle.Height / 2f;

        if (_player.X - playerHalfW < cameraLeft)
            _player.X = cameraLeft + playerHalfW;
        if (_player.X + playerHalfW > cameraRight)
            _player.X = cameraRight - playerHalfW;
        if (_player.Y + playerHalfH > cameraTop)
            _player.Y = cameraTop - playerHalfH;

        // Spawn new rows as the camera scrolls up
        while (_nextSpawnY < _worldY + _halfHeight + 400f)
        {
            SpawnRow(_nextSpawnY);
            _nextSpawnY += SpawnInterval;
        }

        // Cleanup entities that have scrolled far below the camera
        float cleanupY = cameraBottom - CleanupMargin;
        CleanupBelow(_enemyFactory, cleanupY);
        CleanupBelow(_obstacleFactory, cleanupY);
        CleanupBelow(_bulletFactory, cleanupY);

        // Check death
        if (_player.IsDead)
        {
            _phase = GamePhase.GameOver;
            _announcementLabel.Text = $"Game Over!\nScore: {_score}\n\nPress SPACE to restart";
            _announcementLabel.IsVisible = true;
            Camera.VelocityY = 0f;
        }
    }

    private void UpdateGameOverPhase()
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Space))
        {
            MoveToScreen<WalkingShmupScreen>();
        }
    }

    private void SpawnRow(float worldY)
    {
        var random = Engine.Random;

        // Spawn 1-3 obstacles in this row
        int obstacleCount = random.Between(1, 3);
        for (int i = 0; i < obstacleCount; i++)
        {
            var obstacle = _obstacleFactory.Create();
            obstacle.X = random.Between(-_halfWidth + 40f, _halfWidth - 40f);
            obstacle.Y = worldY + random.Between(-40f, 40f);

            // Vary sizes
            obstacle.Rectangle.Width = random.Between(40f, 120f);
            obstacle.Rectangle.Height = random.Between(30f, 80f);
        }

        // Spawn 1-2 enemies in the gaps
        int enemyCount = random.Between(1, 2);
        for (int i = 0; i < enemyCount; i++)
        {
            var enemy = _enemyFactory.Create();
            enemy.X = random.Between(-_halfWidth + 30f, _halfWidth - 30f);
            enemy.Y = worldY + random.Between(60f, 140f);
        }
    }

    private static void CleanupBelow<T>(Factory<T> factory, float yThreshold) where T : Entity, new()
    {
        // Iterate backwards to safely remove during iteration
        var instances = factory.Instances;
        for (int i = instances.Count - 1; i >= 0; i--)
        {
            if (instances[i].Y < yThreshold)
                instances[i].Destroy();
        }
    }
}
