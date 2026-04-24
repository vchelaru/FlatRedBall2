using System;
using System.IO;
using FlatRedBall2;
using FlatRedBall2.Animation;
using FlatRedBall2.Animation.Content;
using FlatRedBall2.Movement;
using FlatRedBall2.Rendering;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using ShmupSpace.Entities;

namespace ShmupSpace.Screens;

public class GameScreen : Screen
{
    private const string AnimationsPath = "Content/Animations/ShmupSpace.achx";
    private const string PlayerTopDownPath = "Content/player.topdown.json";
    private const string GameConfigPath = "Content/shmupspace.game.json";

    // Exposed to entities — all entities share one AnimationChainList and read config through this screen.
    public AnimationChainList Animations { get; private set; } = null!;
    public GameConfig Config { get; } = new();
    public TopDownConfig PlayerTopDownConfig { get; private set; } = new();

    private Factory<PlayerShip> _playerFactory = null!;
    private Factory<PlayerBullet> _bulletFactory = null!;
    private Factory<Enemy> _enemyFactory = null!;
    private Factory<Explosion> _explosionFactory = null!;

    private Label _scoreLabel = null!;
    private Label _livesLabel = null!;

    private int _score;
    private int _lives = 3;

    private float _spawnTimer;
    private float _respawnTimer;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(6, 6, 20);

        // Load content + configs before spawning — entities read from these in their CustomInitialize.
        Animations = AnimationChainListSave.FromFile(AnimationsPath).ToAnimationChainList(Engine.Content);
        Config.CopyFrom(GameConfig.FromJson(GameConfigPath));
        PlayerTopDownConfig = TopDownConfig.FromJson(PlayerTopDownPath);

        _playerFactory = new Factory<PlayerShip>(this);
        _bulletFactory = new Factory<PlayerBullet>(this);
        _enemyFactory = new Factory<Enemy>(this);
        _explosionFactory = new Factory<Explosion>(this);

        SpawnPlayer();

        // Bullet kills enemy; both are destroyed and an explosion plays.
        AddCollisionRelationship<PlayerBullet, Enemy>(_bulletFactory, _enemyFactory)
            .CollisionOccurred += (bullet, enemy) =>
            {
                SpawnExplosion(enemy.X, enemy.Y);
                bullet.Destroy();
                enemy.Destroy();
                _score += Config.Scoring.PerEnemyKill;
            };

        // Enemy hitting the player: player dies, enemy survives.
        AddCollisionRelationship<PlayerShip, Enemy>(_playerFactory, _enemyFactory)
            .CollisionOccurred += (player, _) =>
            {
                SpawnExplosion(player.X, player.Y);
                player.Destroy();
                _lives--;
                _respawnTimer = Config.Spawn.PlayerRespawnDelay;
            };

        BuildHud();

        // Hot-reload: JSON tweaks apply in place; asset changes (.achx, .png) restart the screen.
        WatchContentDirectory("Content", HandleContentChanged);
    }

    public override void CustomActivity(FrameTime time)
    {
        _spawnTimer -= time.DeltaSeconds;
        if (_spawnTimer <= 0f)
        {
            SpawnEnemy();
            _spawnTimer = Config.Spawn.EnemyInterval;
        }

        _scoreLabel.Text = $"Score: {_score}";
        _livesLabel.Text = $"Lives: {_lives}";

        if (_playerFactory.Instances.Count == 0)
        {
            if (_lives <= 0)
            {
                MoveToScreen<TitleScreen>();
                return;
            }

            _respawnTimer -= time.DeltaSeconds;
            if (_respawnTimer <= 0f)
                SpawnPlayer();
        }
    }

    private void HandleContentChanged(string relativePath)
    {
        var ext = Path.GetExtension(relativePath);
        if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            ReloadJson(relativePath);
        }
        else if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
        {
            // Engine auto-reloaded the texture in place — no restart needed.
        }
        else
        {
            RestartScreen(RestartMode.HotReload);
        }
    }

    private void ReloadJson(string relativePath)
    {
        // relativePath is the Content-relative path (e.g. "player.topdown.json").
        var full = Path.Combine("Content", relativePath);
        if (full.EndsWith("shmupspace.game.json", StringComparison.OrdinalIgnoreCase))
        {
            Config.CopyFrom(GameConfig.FromJson(full));
        }
        else if (full.EndsWith("player.topdown.json", StringComparison.OrdinalIgnoreCase))
        {
            PlayerTopDownConfig = TopDownConfig.FromJson(full);
            foreach (var player in _playerFactory.Instances)
                PlayerTopDownConfig.ApplyTo(player.TopDown);
        }
    }

    private void SpawnPlayer()
    {
        var player = _playerFactory.Create();
        player.X = 0f;
        player.Y = Camera.Bottom + 40f;
    }

    private void SpawnEnemy()
    {
        var enemy = _enemyFactory.Create();
        float margin = Config.Spawn.EnemyEdgeMargin;
        enemy.X = Engine.Random.Between(Camera.Left + margin, Camera.Right - margin);
        enemy.Y = Camera.Top + 12f;
    }

    private void SpawnExplosion(float x, float y)
    {
        var boom = _explosionFactory.Create();
        boom.X = x;
        boom.Y = y;
    }

    private void BuildHud()
    {
        var hudLayer = new Layer("HUD");
        Layers.Add(hudLayer);

        _scoreLabel = new Label { Text = "Score: 0" };
        _scoreLabel.Anchor(Anchor.TopLeft);
        _scoreLabel.X = 8;
        _scoreLabel.Y = 8;
        Add(_scoreLabel, layer: hudLayer);

        _livesLabel = new Label { Text = "Lives: 3" };
        _livesLabel.Anchor(Anchor.TopRight);
        _livesLabel.X = -8;
        _livesLabel.Y = 8;
        Add(_livesLabel, layer: hudLayer);
    }
}
