using FlatRedBall2;
using Vector2 = System.Numerics.Vector2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using ShmupSample.Entities;

namespace ShmupSample.Screens;

public class GameplayScreen : Screen
{
    // Factories
    private Factory<PlayerShip> _playerFactory = null!;
    private Factory<PlayerBullet> _playerBulletFactory = null!;
    private Factory<FodderEnemy> _fodderFactory = null!;
    private Factory<ShooterEnemy> _shooterFactory = null!;
    private Factory<HeavyEnemy> _heavyFactory = null!;
    private Factory<EnemyBullet> _enemyBulletFactory = null!;
    private Factory<DeathParticle> _particleFactory = null!;

    private PlayerShip _player = null!;

    // HUD elements
    private Label _scoreLabel = null!;
    private Label _multiplierLabel = null!;
    private Label _healthLabel = null!;

    // Score / multiplier state
    private int _score;
    private int _multiplier = 1;

    // Wave spawning
    private WaveSpawner _waveSpawner = null!;
    private bool _gameOver;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(8, 8, 18);

        // Create factories (order matters — enemies need EnemyBullet factory before they shoot)
        _playerBulletFactory = new Factory<PlayerBullet>(this);
        _enemyBulletFactory = new Factory<EnemyBullet>(this);
        _fodderFactory = new Factory<FodderEnemy>(this);
        _shooterFactory = new Factory<ShooterEnemy>(this);
        _heavyFactory = new Factory<HeavyEnemy>(this);
        _particleFactory = new Factory<DeathParticle>(this);
        _playerFactory = new Factory<PlayerShip>(this);

        // Spawn player at bottom center
        _player = _playerFactory.Create();
        _player.X = 0f;
        _player.Y = -Camera.TargetHeight / 2f + 80f;
        _player.DamageTaken += OnPlayerDamageTaken;

        // Collision relationships
        SetupCollisions();

        // HUD
        SetupHud();

        // Wave spawner
        _waveSpawner = new WaveSpawner(this);
    }

    private void SetupCollisions()
    {
        // Player bullets vs fodder
        AddCollisionRelationship<PlayerBullet, FodderEnemy>(_playerBulletFactory, _fodderFactory)
            .CollisionOccurred += (bullet, enemy) =>
            {
                float ex = enemy.X;
                float ey = enemy.Y;
                bullet.Destroy();
                enemy.Destroy();
                SpawnDeathParticles(ex, ey, new Color(120, 220, 120, 220), 6);
                AddScore(10);
            };

        // Player bullets vs shooters
        AddCollisionRelationship<PlayerBullet, ShooterEnemy>(_playerBulletFactory, _shooterFactory)
            .CollisionOccurred += (bullet, enemy) =>
            {
                float ex = enemy.X;
                float ey = enemy.Y;
                bullet.Destroy();
                enemy.TakeDamage(1);
                if (!enemy.IsAlive)
                {
                    SpawnDeathParticles(ex, ey, new Color(255, 140, 0, 220), 8);
                    AddScore(30);
                }
            };

        // Player bullets vs heavies
        AddCollisionRelationship<PlayerBullet, HeavyEnemy>(_playerBulletFactory, _heavyFactory)
            .CollisionOccurred += (bullet, enemy) =>
            {
                float ex = enemy.X;
                float ey = enemy.Y;
                bullet.Destroy();
                enemy.TakeDamage(1);
                if (!enemy.IsAlive)
                {
                    SpawnDeathParticles(ex, ey, new Color(200, 60, 220, 220), 12);
                    AddScore(80);
                }
            };

        // Enemy bullets vs player
        AddCollisionRelationship<EnemyBullet, PlayerShip>(_enemyBulletFactory, _playerFactory)
            .CollisionOccurred += (bullet, player) =>
            {
                bullet.Destroy();
                player.TakeDamage(1);
            };

        // Fodder vs player — fodder dies, player takes damage
        AddCollisionRelationship<FodderEnemy, PlayerShip>(_fodderFactory, _playerFactory)
            .CollisionOccurred += (enemy, player) =>
            {
                float ex = enemy.X, ey = enemy.Y;
                enemy.Destroy();
                SpawnDeathParticles(ex, ey, new Color(120, 220, 120, 220), 6);
                player.TakeDamage(1);
            };

        // Shooters vs player — both take 1 damage
        AddCollisionRelationship<ShooterEnemy, PlayerShip>(_shooterFactory, _playerFactory)
            .CollisionOccurred += (enemy, player) =>
            {
                float ex = enemy.X, ey = enemy.Y;
                enemy.TakeDamage(1);
                player.TakeDamage(1);
                if (!enemy.IsAlive)
                    SpawnDeathParticles(ex, ey, new Color(255, 140, 0, 220), 8);
            };

        // Heavies vs player — both take 1 damage
        AddCollisionRelationship<HeavyEnemy, PlayerShip>(_heavyFactory, _playerFactory)
            .CollisionOccurred += (enemy, player) =>
            {
                float ex = enemy.X, ey = enemy.Y;
                enemy.TakeDamage(1);
                player.TakeDamage(1);
                if (!enemy.IsAlive)
                    SpawnDeathParticles(ex, ey, new Color(200, 60, 220, 220), 12);
            };
    }

    private void SetupHud()
    {
        // Score label — top right, understated
        _scoreLabel = new Label();
        _scoreLabel.Text = "0";
        _scoreLabel.Anchor(Anchor.TopRight);
        _scoreLabel.X = -16;
        _scoreLabel.Y = 16;
        AddGum(_scoreLabel);

        // Multiplier label — just below score
        _multiplierLabel = new Label();
        _multiplierLabel.Text = "x1";
        _multiplierLabel.Anchor(Anchor.TopRight);
        _multiplierLabel.X = -16;
        _multiplierLabel.Y = 44;
        AddGum(_multiplierLabel);

        // Health label — top left
        _healthLabel = new Label();
        _healthLabel.Text = BuildHealthText();
        _healthLabel.Anchor(Anchor.TopLeft);
        _healthLabel.X = 16;
        _healthLabel.Y = 16;
        AddGum(_healthLabel);
    }

    private string BuildHealthText() => $"HP: {_player.Health}/{_player.MaxHealth}";

    public override void CustomActivity(FrameTime time)
    {
        if (_gameOver) return;

        // Check win/lose
        if (!_player.IsAlive)
        {
            _gameOver = true;
            MoveToScreen<GameOverScreen>(s =>
            {
                s.FinalScore = _score;
                s.Won = false;
            });
            return;
        }

        // Update wave spawner
        _waveSpawner.Update(time);

        // Update HUD
        _healthLabel.Text = BuildHealthText();
        _scoreLabel.Text = _score.ToString();
        _multiplierLabel.Text = $"x{_multiplier}";
    }

    private void OnPlayerDamageTaken()
    {
        BreakMultiplier();
    }

    private void BreakMultiplier()
    {
        if (_multiplier > 1)
            _multiplier = Math.Max(1, _multiplier - 1);
    }

    private void AddScore(int basePoints)
    {
        _score += basePoints * _multiplier;
        // Killing enemies increases multiplier (capped at 8)
        if (_multiplier < 8)
            _multiplier++;
    }

    private void SpawnDeathParticles(float x, float y, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var p = _particleFactory.Create();
            p.X = x;
            p.Y = y;
            var vel = Engine.Random.RadialVector2(40f, 140f);
            p.VelocityX = vel.X;
            p.VelocityY = vel.Y;
            p.Launch(color, Engine.Random.Between(0.3f, 0.7f));
        }
    }

    // Called by WaveSpawner to spawn a fodder enemy on a waypoint path
    public void SpawnFodderOnPath(Vector2[] waypoints, float speed)
    {
        var enemy = _fodderFactory.Create();
        enemy.Escaped += BreakMultiplier;
        enemy.Launch(waypoints, speed);
    }

    public void SpawnShooter(float x, float y, float velY, float holdY)
    {
        var enemy = _shooterFactory.Create();
        enemy.X = x;
        enemy.Y = y;
        enemy.Escaped += BreakMultiplier;
        enemy.Launch(velY, holdY);
    }

    public void SpawnHeavy(float x, float y, float velY)
    {
        var enemy = _heavyFactory.Create();
        enemy.X = x;
        enemy.Y = y;
        enemy.VelocityY = velY;
    }

    public override void CustomDestroy() { }
}
