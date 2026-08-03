using FlatRedBall2;
using Vector2 = System.Numerics.Vector2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Strikers1945Sample.Entities;

namespace Strikers1945Sample.Screens;

public class GameplayScreen : Screen
{
    public static int LastScore { get; private set; }
    public static int EnemiesDefeated { get; private set; }
    public static int HighestChain { get; private set; }

    private Factory<PlayerShip> _playerFactory = null!;
    private Factory<PlayerBullet> _playerBulletFactory = null!;
    private Factory<FodderEnemy> _fodderFactory = null!;
    private Factory<ShooterEnemy> _shooterFactory = null!;
    private Factory<HeavyEnemy> _heavyFactory = null!;
    private Factory<DiveBomberEnemy> _diveBomberFactory = null!;
    private Factory<TurretEnemy> _turretFactory = null!;
    private Factory<EnemyBullet> _enemyBulletFactory = null!;
    private Factory<DeathParticle> _particleFactory = null!;
    private Factory<Pickup> _pickupFactory = null!;
    private Factory<ChargeProjectile> _chargeFactory = null!;
    private Factory<Boss> _bossFactory = null!;
    private PlayerShip _player = null!;
    private Boss? _boss;

    private Label _scoreLabel = null!;
    private Label _livesLabel = null!;
    private Label _bombLabel = null!;
    private Label _weaponLabel = null!;
    private Label _levelCardLabel = null!;
    private Label _bossHpLabel = null!;
    private Label _chainLabel = null!;
    private Label _warningLabel = null!;
    private float _levelCardTimer;
    private float _warningTimer;
    private int _enemiesDefeated;
    private int _highestChain;

    public static float EnemyHpMultiplier = 1f;

    private int _score;
    private int _lives = 3;
    private bool _gameOver;
    private WaveSpawner _waveSpawner = null!;
    private ScrollingBackground _background = null!;
    private int _continues = 3;
    private bool _showingContinue;
    private float _continueTimer;
    private Label? _continueLabel;
    private bool _levelClearPause;
    private float _levelClearTimer;
    private Label? _levelClearLabel;
    private int _pendingNextLevel;
    private float _chainTimer;
    private int _chainCount;
    private int _chainMultiplier = 1;
    private int _grazeScore;
    private bool _waveNoDamage = true;
    private int _lastWaveLaunched;
    private int _currentLevel;
    private LevelDefinition _levelDef = null!;
    private bool _bossActive;
    private float _shakeTimer;
    private float _shakeIntensity;

    public override void CustomInitialize()
    {
        _playerBulletFactory = new Factory<PlayerBullet>(this);
        _enemyBulletFactory = new Factory<EnemyBullet>(this);
        _fodderFactory = new Factory<FodderEnemy>(this);
        _shooterFactory = new Factory<ShooterEnemy>(this);
        _heavyFactory = new Factory<HeavyEnemy>(this);
        _diveBomberFactory = new Factory<DiveBomberEnemy>(this);
        _turretFactory = new Factory<TurretEnemy>(this);
        _particleFactory = new Factory<DeathParticle>(this);
        _pickupFactory = new Factory<Pickup>(this);
        _chargeFactory = new Factory<ChargeProjectile>(this);
        _bossFactory = new Factory<Boss>(this);
        _playerFactory = new Factory<PlayerShip>(this);

        EnemiesDefeated = 0;
        HighestChain = 0;
        _currentLevel = 0;
        _levelDef = LevelDefinition.AllLevels[0];
        LoadBackground();

        _player = _playerFactory.Create();
        _player.X = 0f;
        _player.Y = -Camera.TargetHeight / 2f + 80f;
        _player.SuperFired += OnSuperFired;
        _waveSpawner = new WaveSpawner(this, _levelDef);
        SetupCollisions();
        SetupHud();
    }

    private void LoadBackground()
    {
        Camera.BackgroundColor = _levelDef.BackgroundColor;
        var tiles = new Texture2D[_levelDef.BackgroundTiles.Length];
        for (int i = 0; i < tiles.Length; i++)
            tiles[i] = Engine.ContentManager.Load<Texture2D>(_levelDef.BackgroundTiles[i]);
        _background = new ScrollingBackground(this, tiles, scrollSpeed: 50f, density: _levelDef.TileDensity);
        ApplyDifficultyScaling();
    }

    private void ApplyDifficultyScaling()
    {
        EnemyBullet.SpeedMultiplier = _levelDef.LevelNumber switch { 1 => 1.0f, 2 => 1.1f, 3 => 1.25f, 4 => 1.4f, _ => 1.6f };
        EnemyHpMultiplier = _levelDef.LevelNumber switch { 1 => 1.0f, 2 => 1.2f, 3 => 1.5f, 4 => 1.8f, _ => 2.0f };
    }

    private void SetupCollisions()
    {
        AddCollisionRelationship<PlayerBullet, FodderEnemy>(_playerBulletFactory, _fodderFactory).CollisionOccurred += (bullet, enemy) => { float ex = enemy.X, ey = enemy.Y; bullet.Destroy(); enemy.Destroy(); SpawnDeathParticles(ex, ey, new Color(255, 180, 60), 6); AddScore(150); TryDropPickup(ex, ey, 0.15f); };
        AddCollisionRelationship<PlayerBullet, ShooterEnemy>(_playerBulletFactory, _shooterFactory).CollisionOccurred += (bullet, enemy) => { float ex = enemy.X, ey = enemy.Y; bullet.Destroy(); enemy.TakeDamage(1); if (!enemy.IsAlive) { SpawnDeathParticles(ex, ey, new Color(255, 100, 40), 10); AddScore(400); TryDropPickup(ex, ey, 0.35f); } };
        AddCollisionRelationship<PlayerBullet, HeavyEnemy>(_playerBulletFactory, _heavyFactory).CollisionOccurred += (bullet, enemy) => { float ex = enemy.X, ey = enemy.Y; bullet.Destroy(); enemy.TakeDamage(1); if (!enemy.IsAlive) { SpawnDeathParticles(ex, ey, new Color(200, 80, 255), 12); AddScore(1000); SpawnPickup(ex, ey, Engine.Random.NextSingle() < 0.7f ? PickupType.Power : PickupType.Bomb); } };
        AddCollisionRelationship<PlayerBullet, DiveBomberEnemy>(_playerBulletFactory, _diveBomberFactory).CollisionOccurred += (bullet, enemy) => { float ex = enemy.X, ey = enemy.Y; bullet.Destroy(); enemy.TakeDamage(1); if (!enemy.IsAlive) { SpawnDeathParticles(ex, ey, new Color(255, 160, 80), 10); AddScore(500); TryDropPickup(ex, ey, 0.25f); } };
        AddCollisionRelationship<PlayerBullet, TurretEnemy>(_playerBulletFactory, _turretFactory).CollisionOccurred += (bullet, enemy) => { float ex = enemy.X, ey = enemy.Y; bullet.Destroy(); enemy.TakeDamage(1); if (!enemy.IsAlive) { SpawnDeathParticles(ex, ey, new Color(180, 255, 100), 10); AddScore(600); TryDropPickup(ex, ey, 0.30f); } };
        AddCollisionRelationship<PlayerBullet, Boss>(_playerBulletFactory, _bossFactory).CollisionOccurred += (bullet, boss) => { bullet.Destroy(); boss.TakeDamage(1); AddScore(boss.IsPhase2 ? 20 : 10); };
        AddCollisionRelationship<ChargeProjectile, FodderEnemy>(_chargeFactory, _fodderFactory).CollisionOccurred += (_, enemy) => { enemy.Destroy(); SpawnDeathParticles(enemy.X, enemy.Y, new Color(100, 200, 255), 8); AddScore(150); };
        AddCollisionRelationship<ChargeProjectile, ShooterEnemy>(_chargeFactory, _shooterFactory).CollisionOccurred += (proj, enemy) => { enemy.TakeDamage(proj.Damage); if (!enemy.IsAlive) { SpawnDeathParticles(enemy.X, enemy.Y, new Color(100, 200, 255), 10); AddScore(400); } };
        AddCollisionRelationship<ChargeProjectile, HeavyEnemy>(_chargeFactory, _heavyFactory).CollisionOccurred += (proj, enemy) => { enemy.TakeDamage(proj.Damage); if (!enemy.IsAlive) { SpawnDeathParticles(enemy.X, enemy.Y, new Color(100, 200, 255), 12); AddScore(1000); SpawnPickup(enemy.X, enemy.Y, PickupType.Power); } };
        AddCollisionRelationship<ChargeProjectile, DiveBomberEnemy>(_chargeFactory, _diveBomberFactory).CollisionOccurred += (proj, enemy) => { enemy.TakeDamage(proj.Damage); if (!enemy.IsAlive) { SpawnDeathParticles(enemy.X, enemy.Y, new Color(100, 200, 255), 10); AddScore(500); } };
        AddCollisionRelationship<ChargeProjectile, TurretEnemy>(_chargeFactory, _turretFactory).CollisionOccurred += (proj, enemy) => { enemy.TakeDamage(proj.Damage); if (!enemy.IsAlive) { SpawnDeathParticles(enemy.X, enemy.Y, new Color(100, 200, 255), 10); AddScore(600); } };
        AddCollisionRelationship<ChargeProjectile, Boss>(_chargeFactory, _bossFactory).CollisionOccurred += (proj, boss) => { boss.TakeDamage(proj.Damage); AddScore(boss.IsPhase2 ? 60 : 30); };
        AddCollisionRelationship<EnemyBullet, PlayerShip>(_enemyBulletFactory, _playerFactory).CollisionOccurred += (bullet, player) => { if (player.IsInvincible) { bullet.Destroy(); return; } bullet.Destroy(); OnPlayerHit(); };
        AddCollisionRelationship<FodderEnemy, PlayerShip>(_fodderFactory, _playerFactory).CollisionOccurred += (enemy, player) => { if (player.IsInvincible) return; enemy.Destroy(); SpawnDeathParticles(enemy.X, enemy.Y, new Color(255, 180, 60), 6); OnPlayerHit(); };
        AddCollisionRelationship<ShooterEnemy, PlayerShip>(_shooterFactory, _playerFactory).CollisionOccurred += (enemy, player) => { if (player.IsInvincible) return; enemy.TakeDamage(1); OnPlayerHit(); };
        AddCollisionRelationship<HeavyEnemy, PlayerShip>(_heavyFactory, _playerFactory).CollisionOccurred += (enemy, player) => { if (player.IsInvincible) return; enemy.TakeDamage(1); OnPlayerHit(); };
        AddCollisionRelationship<DiveBomberEnemy, PlayerShip>(_diveBomberFactory, _playerFactory).CollisionOccurred += (enemy, player) => { if (player.IsInvincible) return; enemy.TakeDamage(1); OnPlayerHit(); };
        AddCollisionRelationship<TurretEnemy, PlayerShip>(_turretFactory, _playerFactory).CollisionOccurred += (enemy, player) => { if (player.IsInvincible) return; enemy.TakeDamage(1); OnPlayerHit(); };
        AddCollisionRelationship<Boss, PlayerShip>(_bossFactory, _playerFactory).CollisionOccurred += (boss, player) => { if (player.IsInvincible) return; OnPlayerHit(); };
        AddCollisionRelationship<Pickup, PlayerShip>(_pickupFactory, _playerFactory).CollisionOccurred += (pickup, player) =>
        {
            switch (pickup.Type)
            {
                case PickupType.Power: if (_player.WeaponLevel >= 4) AddScore(1000); else _player.PowerUp(); break;
                case PickupType.Bomb: _player.AddBomb(); break;
                case PickupType.Medal: AddScore(pickup.GetMedalScore()); break;
            }
            pickup.Destroy();
        };
    }

    private void SetupHud()
    {
        _scoreLabel = new Label(); _scoreLabel.Text = "0"; _scoreLabel.Anchor(Anchor.TopRight); _scoreLabel.X = -16; _scoreLabel.Y = 8; Add(_scoreLabel);
        _livesLabel = new Label(); _livesLabel.Text = "LIVES: x3"; _livesLabel.Anchor(Anchor.TopLeft); _livesLabel.X = 16; _livesLabel.Y = 8; Add(_livesLabel);
        _bombLabel = new Label(); _bombLabel.Text = "BOMB: x2"; _bombLabel.Anchor(Anchor.TopLeft); _bombLabel.X = 16; _bombLabel.Y = 28; Add(_bombLabel);
        _weaponLabel = new Label(); _weaponLabel.Text = "POW: 1"; _weaponLabel.Anchor(Anchor.TopLeft); _weaponLabel.X = 16; _weaponLabel.Y = 48; Add(_weaponLabel);
        _bossHpLabel = new Label(); _bossHpLabel.Text = ""; _bossHpLabel.Anchor(Anchor.TopRight); _bossHpLabel.X = -16; _bossHpLabel.Y = 28; Add(_bossHpLabel);
        _chainLabel = new Label(); _chainLabel.Text = ""; _chainLabel.Anchor(Anchor.TopRight); _chainLabel.X = -16; _chainLabel.Y = 48; Add(_chainLabel);
        _levelCardLabel = new Label(); _levelCardLabel.Text = ""; _levelCardLabel.Anchor(Anchor.TopLeft); _levelCardLabel.X = 100; _levelCardLabel.Y = 340; Add(_levelCardLabel);
        _warningLabel = new Label(); _warningLabel.Text = ""; _warningLabel.Anchor(Anchor.TopLeft); _warningLabel.X = 150; _warningLabel.Y = 320; Add(_warningLabel);
        ShowLevelCard();
    }

    private void ShowLevelCard()
    {
        _levelCardTimer = 2.5f;
        _levelCardLabel.Text = $"Level {_levelDef.LevelNumber}: {_levelDef.LevelName}";
    }

    public override void CustomActivity(FrameTime time)
    {
        if (_gameOver) return;

        if (_showingContinue)
        {
            _continueTimer -= time.DeltaSeconds;
            var kb = Engine.InputManager.Keyboard;
            if (kb.WasKeyPressed(Keys.Z) || kb.WasKeyPressed(Keys.Space))
            {
                _continues--;
                _lives = 3;
                _showingContinue = false;
                if (_continueLabel != null) { Remove(_continueLabel); _continueLabel = null; }
                _player.X = 0f;
                _player.Y = -Camera.TargetHeight / 2f + 80f;
                _player.VelocityX = 0f;
                _player.VelocityY = 0f;
                _player.MakeInvincible();
            }
            else if (_continueTimer <= 0f)
            {
                _gameOver = true;
                LastScore = _score;
                MoveToScreen<GameOverScreen>(s => { s.FinalScore = _score; s.LevelReached = _currentLevel + 1; });
            }
            else if (_continueLabel != null)
            {
                _continueLabel.Text = $"CONTINUE? x{_continues}  ({(int)_continueTimer + 1})";
            }
            return;
        }

        if (_levelClearPause)
        {
            _levelClearTimer -= time.DeltaSeconds;
            _background.Update(time.DeltaSeconds);
            if (_levelClearTimer <= 0f)
            {
                _levelClearPause = false;
                if (_levelClearLabel != null) { Remove(_levelClearLabel); _levelClearLabel = null; }
                AdvanceToLevel(_pendingNextLevel);
            }
            return;
        }

        Pickup.PlayerPosition = new Vector2(_player.X, _player.Y);
        _background.Update(time.DeltaSeconds);

        if (_levelCardTimer > 0f)
        {
            _levelCardTimer -= time.DeltaSeconds;
            if (_levelCardTimer <= 0f) _levelCardLabel.Text = "";
        }

        if (!_bossActive)
        {
            _waveSpawner.Update(time);
            if (_waveSpawner.WavesLaunched >= _levelDef.WaveCount && !_bossActive) { SpawnBoss(); _bossActive = true; }
        }

        if (_bossActive && (_boss == null || !_boss.IsAlive)) OnBossDefeated();

        if (_shakeTimer > 0f)
        {
            _shakeTimer -= time.DeltaSeconds;
            float intensity = _shakeIntensity * (_shakeTimer > 0f ? 1f : 0f);
            Camera.X = Engine.Random.Between(-intensity, intensity);
            Camera.Y = Engine.Random.Between(-intensity, intensity);
        }
        else { Camera.X = 0f; Camera.Y = 0f; }

        UpdateHud();
    }

    private void UpdateHud()
    {
        _scoreLabel.Text = _score.ToString();
        _livesLabel.Text = $"LIVES: {_lives}";
        _bombLabel.Text = $"BOMB: {_player.BombStock}";
        _weaponLabel.Text = $"LV: {_player.WeaponLevel}";
        _bossHpLabel.Text = _boss != null && _boss.IsAlive ? $"BOSS: {(int)(_boss.HealthPercent * 100)}%" : "";
    }

    private void SpawnBoss()
    {
        _boss = _bossFactory.Create();
        _boss.Configure(_levelDef.BossSprite, _levelDef.BossPhase2Sprite, _levelDef.BossHealth);
        _boss.Destroyed += () =>
        {
            if (_boss != null) SpawnDeathParticles(_boss.X, _boss.Y, new Color(255, 200, 60), 20);
            AddScore(20000);
            TriggerShake(8f, 0.5f);
        };
    }

    private void OnBossDefeated()
    {
        _boss = null;
        _bossActive = false;
        int nextLevel = _currentLevel + 1;

        if (nextLevel >= LevelDefinition.AllLevels.Length)
        {
            _gameOver = true;
            LastScore = _score;
            MoveToScreen<VictoryScreen>();
            return;
        }

        _levelClearPause = true;
        _levelClearTimer = 2f;
        _pendingNextLevel = nextLevel;
        _levelClearLabel = new Label();
        _levelClearLabel.Text = "LEVEL CLEAR!";
        _levelClearLabel.Anchor(Anchor.TopLeft);
        _levelClearLabel.X = 130;
        _levelClearLabel.Y = 340;
        Add(_levelClearLabel);
    }

    private void AdvanceToLevel(int nextLevel)
    {
        _currentLevel = nextLevel;
        _levelDef = LevelDefinition.AllLevels[nextLevel];
        LoadBackground();
        _waveSpawner = new WaveSpawner(this, _levelDef);
        ShowLevelCard();
    }

    private void OnPlayerHit()
    {
        SpawnDeathParticles(_player.X, _player.Y, new Color(80, 180, 255), 12);
        TriggerShake(4f, 0.25f);
        _lives--;
        _player.ResetPower();

        if (_lives <= 0)
        {
            if (_continues > 0)
            {
                _showingContinue = true;
                _continueTimer = 10f;
                _continueLabel = new Label();
                _continueLabel.Text = $"CONTINUE? x{_continues}  (10)";
                _continueLabel.Anchor(Anchor.TopLeft);
                _continueLabel.X = 120;
                _continueLabel.Y = 340;
                Add(_continueLabel);
            }
            else
            {
                _gameOver = true;
                LastScore = _score;
                MoveToScreen<GameOverScreen>(s => { s.FinalScore = _score; s.LevelReached = _currentLevel + 1; });
            }
            return;
        }

        _player.X = 0f;
        _player.Y = -Camera.TargetHeight / 2f + 80f;
        _player.VelocityX = 0f;
        _player.VelocityY = 0f;
        _player.MakeInvincible();
    }

    private void OnSuperFired()
    {
        TriggerShake(6f, 0.4f);
        foreach (var bullet in _enemyBulletFactory.Instances.ToArray()) { SpawnDeathParticles(bullet.X, bullet.Y, new Color(255, 255, 200), 2); bullet.Destroy(); }
        foreach (var enemy in _fodderFactory.Instances.ToArray()) { SpawnDeathParticles(enemy.X, enemy.Y, new Color(255, 200, 100), 6); enemy.Destroy(); AddScore(150); }
        foreach (var enemy in _shooterFactory.Instances.ToArray()) { float ex = enemy.X, ey = enemy.Y; enemy.TakeDamage(5); if (!enemy.IsAlive) { SpawnDeathParticles(ex, ey, new Color(255, 200, 100), 8); AddScore(400); } }
        foreach (var enemy in _heavyFactory.Instances.ToArray()) { float ex = enemy.X, ey = enemy.Y; enemy.TakeDamage(5); if (!enemy.IsAlive) { SpawnDeathParticles(ex, ey, new Color(255, 200, 100), 10); AddScore(1000); } }
        foreach (var enemy in _diveBomberFactory.Instances.ToArray()) { float ex = enemy.X, ey = enemy.Y; enemy.TakeDamage(5); if (!enemy.IsAlive) { SpawnDeathParticles(ex, ey, new Color(255, 200, 100), 8); AddScore(500); } }
        foreach (var enemy in _turretFactory.Instances.ToArray()) { float ex = enemy.X, ey = enemy.Y; enemy.TakeDamage(5); if (!enemy.IsAlive) { SpawnDeathParticles(ex, ey, new Color(255, 200, 100), 10); AddScore(600); } }
        if (_boss != null && _boss.IsAlive) _boss.TakeDamage(5);
    }

    private void TryDropPickup(float x, float y, float chance)
    {
        if (Engine.Random.NextSingle() > chance) return;
        float roll = Engine.Random.NextSingle();
        SpawnPickup(x, y, roll < 0.50f ? PickupType.Medal : roll < 0.85f ? PickupType.Power : PickupType.Bomb);
    }

    private void SpawnPickup(float x, float y, PickupType type)
    {
        var pickup = _pickupFactory.Create(); pickup.X = x; pickup.Y = y; pickup.Configure(type);
    }

    private void AddScore(int points, bool isKill = false)
    {
        if (isKill)
        {
            _chainCount++;
            _chainTimer = 2.0f;
            _chainMultiplier = Math.Min(_chainCount / 3 + 1, 5);
            _score += points * _chainMultiplier;
            EnemiesDefeated++;
            if (_chainCount > HighestChain) HighestChain = _chainCount;
        }
        else { _score += points; }
    }

    private void TriggerShake(float intensity, float duration) { _shakeIntensity = intensity; _shakeTimer = duration; }

    private void SpawnDeathParticles(float x, float y, Color color, int count)
    {
        for (int i = 0; i < count; i++)
        {
            var p = _particleFactory.Create();
            p.X = x + Engine.Random.Between(-6f, 6f); p.Y = y + Engine.Random.Between(-6f, 6f);
            var vel = Engine.Random.RadialVector2(40f, 250f);
            p.VelocityX = vel.X; p.VelocityY = vel.Y;
            p.Launch(color, Engine.Random.Between(0.3f, 0.8f));
        }
    }

    public void SpawnFodderOnPath(Vector2[] waypoints, float speed) { _fodderFactory.Create().Launch(waypoints, speed); }
    public void SpawnFodderFormation(Vector2[] waypoints, float speed, float offsetX) { var e = _fodderFactory.Create(); e.IsFormation = true; e.FormationOffsetX = offsetX; e.Launch(waypoints, speed); }
    public void SpawnShooter(float x, float y, float velY, float holdY) { var e = _shooterFactory.Create(); e.X = x; e.Y = y; e.Launch(velY, holdY); }
    public void SpawnHeavy(float x, float y) { if (!_levelDef.HasHeavyEnemies) return; _heavyFactory.Create().X = x; }
    public void SpawnDiveBomber(float x, float y) { if (!_levelDef.HasDiveBombers) return; _diveBomberFactory.Create().Launch(x, y); }
    public void SpawnTurret(float x, float y) { var e = _turretFactory.Create(); e.X = x; e.Y = y; }

    public override void CustomDestroy() { }
}
