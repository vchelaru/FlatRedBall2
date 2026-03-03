using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using SpaceInvadersSample.Entities;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace SpaceInvadersSample.Screens;

public class SpaceInvadersScreen : Screen
{
    private const int Cols = 11;
    private const int Rows = 5;
    private const int TotalEnemies = Cols * Rows;

    private Factory<PlayerBullet> _playerBulletFactory = null!;
    private Factory<EnemyBullet> _enemyBulletFactory = null!;
    private Factory<Player> _playerFactory = null!;
    private Factory<Enemy> _enemyFactory = null!;

    private bool _playerAlive = true;
    private int _score;

    private float _formationOffsetX;
    private float _formationOffsetY;
    private int _formationDirX = 1;
    private float _formationSpeedX = 60f;

    private float _enemyShootTimer = 2f;

    private Label _scoreLabel = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(5, 5, 15);

        // PlayerBullet factory must be registered before Player is created
        // because Player.CustomActivity calls Engine.GetFactory<PlayerBullet>().
        _playerBulletFactory = new Factory<PlayerBullet>(this);
        _enemyBulletFactory = new Factory<EnemyBullet>(this);
        _playerFactory = new Factory<Player>(this);
        _enemyFactory = new Factory<Enemy>(this);

        SpawnPlayer();
        SpawnEnemyGrid();
        SetupCollision();
        SetupHud();
    }

    private void SpawnPlayer()
    {
        var player = _playerFactory.Create();
        player.X = 0f;
        player.Y = -295f;
    }

    private void SpawnEnemyGrid()
    {
        XnaColor[] rowColors =
        {
            new XnaColor(220, 120, 255, 255), // row 0 — purple
            new XnaColor(255,  80,  80, 255), // row 1 — red
            new XnaColor(255, 180,  60, 255), // row 2 — orange
            new XnaColor(255, 255,  80, 255), // row 3 — yellow
            new XnaColor( 80, 220, 255, 255), // row 4 — cyan
        };

        for (int row = 0; row < Rows; row++)
        {
            for (int col = 0; col < Cols; col++)
            {
                var enemy = _enemyFactory.Create();
                enemy.BaseX = (col - 5) * 52f;
                enemy.BaseY = 180f - row * 40f;
                enemy.Rectangle.Color = rowColors[row];
                enemy.X = enemy.BaseX;
                enemy.Y = enemy.BaseY;
            }
        }
    }

    private void SetupCollision()
    {
        AddCollisionRelationship<PlayerBullet, Enemy>(_playerBulletFactory, _enemyFactory)
            .CollisionOccurred += (bullet, enemy) =>
            {
                bullet.Destroy();
                enemy.Destroy();
                _score += 10;
                UpdateHud();
            };

        AddCollisionRelationship<EnemyBullet, Player>(_enemyBulletFactory, _playerFactory)
            .CollisionOccurred += (bullet, _) =>
            {
                bullet.Destroy();
                _playerAlive = false;
            };
    }

    private void SetupHud()
    {
        var hud = new Panel();
        hud.Dock(Dock.Fill);

        _scoreLabel = new Label { Text = "Score: 0" };
        _scoreLabel.Anchor(Anchor.TopLeft);
        _scoreLabel.X = 10;
        _scoreLabel.Y = 10;
        hud.AddChild(_scoreLabel);

        AddGum(hud);
    }

    private void UpdateHud()
    {
        _scoreLabel.Text = $"Score: {_score}";
    }

    private void UpdateFormation(FrameTime time)
    {
        _formationOffsetX += _formationSpeedX * _formationDirX * time.DeltaSeconds;

        float minX = float.MaxValue;
        float maxX = float.MinValue;

        foreach (var enemy in _enemyFactory.Instances)
        {
            float wx = enemy.BaseX + _formationOffsetX;
            if (wx < minX) minX = wx;
            if (wx > maxX) maxX = wx;
        }

        if (_formationDirX > 0 && maxX + 12f > 560f)
        {
            _formationOffsetX -= (maxX + 12f - 560f);
            _formationDirX = -1;
            _formationOffsetY -= 30f;
            _formationSpeedX = MathF.Min(_formationSpeedX + 8f, 220f);
        }
        else if (_formationDirX < 0 && minX - 12f < -560f)
        {
            _formationOffsetX += (-560f - (minX - 12f));
            _formationDirX = 1;
            _formationOffsetY -= 30f;
            _formationSpeedX = MathF.Min(_formationSpeedX + 8f, 220f);
        }

        foreach (var enemy in _enemyFactory.Instances)
        {
            enemy.X = enemy.BaseX + _formationOffsetX;
            enemy.Y = enemy.BaseY + _formationOffsetY;
        }
    }

    private void UpdateEnemyShooting(FrameTime time)
    {
        _enemyShootTimer -= time.DeltaSeconds;
        if (_enemyShootTimer > 0f || _enemyFactory.Instances.Count == 0)
            return;

        float ratio = (float)_enemyFactory.Instances.Count / TotalEnemies;
        _enemyShootTimer = Engine.Random.Between(0.5f + ratio * 1.5f, 1.5f + ratio * 1.5f);

        int index = Engine.Random.Next(_enemyFactory.Instances.Count);
        var shooter = _enemyFactory.Instances[index];

        var bullet = _enemyBulletFactory.Create();
        bullet.X = shooter.X;
        bullet.Y = shooter.Y - 15f;
    }

    public override void CustomActivity(FrameTime time)
    {
        if (!_playerAlive)
        {
            MoveToScreen<GameOverScreen>(s => { s.FinalScore = _score; s.Won = false; });
            return;
        }

        UpdateFormation(time);
        UpdateEnemyShooting(time);

        if (_enemyFactory.Instances.Count == 0)
            MoveToScreen<GameOverScreen>(s => { s.FinalScore = _score; s.Won = true; });
    }
}
