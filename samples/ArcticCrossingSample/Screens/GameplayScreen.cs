using ArcticCrossingSample.Data;
using ArcticCrossingSample.Entities;
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Entities;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using XnaColor = Microsoft.Xna.Framework.Color;

namespace ArcticCrossingSample.Screens;

public class GameplayScreen : Screen
{
    // Set by caller via MoveToScreen configure callback
    public int PhaseIndex { get; set; } = 1;
    public GameState State { get; set; } = new();

    // Factories
    private Factory<Player> _playerFactory = null!;
    private Factory<IcePlatform> _platformFactory = null!;
    private Factory<Checkpoint> _checkpointFactory = null!;
    private Factory<DeathZone> _deathZoneFactory = null!;
    private Factory<Collectible> _collectibleFactory = null!;
    private Factory<PolarBear> _polarBearFactory = null!;
    private Factory<Penguin> _penguinFactory = null!;
    private Factory<Seal> _sealFactory = null!;
    private Factory<Unicorn> _unicornFactory = null!;
    private Factory<CameraControllingEntity> _cameraFactory = null!;

    // Core references
    private Player _player = null!;
    private LevelData _level = null!;
    private CameraControllingEntity _cameraController = null!;

    // Game state
    private int _lives = 3;
    private int _score;
    private int _lastCheckpointIndex = -1;
    private float _lastCheckpointX;
    private float _lastCheckpointY;
    private bool _isDead;
    private float _respawnTimer;
    private bool _phaseComplete;
    private float _phaseCompleteTimer;

    // HUD
    private Label _livesLabel = null!;
    private Label _scoreLabel = null!;
    private Label _phaseLabel = null!;
    private float _phaseLabelTimer;
    private Label _hintLabel = null!;
    private float _hintTimer;

    // Pause
    private Panel _pauseOverlay = null!;

    // Mountain background visual
    private AxisAlignedRectangle _waterBg = null!;
    private AxisAlignedRectangle _mountainBg = null!;

    // Snowflake particles
    private readonly List<AxisAlignedRectangle> _snowflakes = new();

    public override void CustomInitialize()
    {
        _level = PhaseDefinitions.GetPhase(PhaseIndex);
        Camera.BackgroundColor = _level.BackgroundColor;

        _lastCheckpointX = _level.PlayerStartX;
        _lastCheckpointY = _level.PlayerStartY;

        InitFactories();
        SpawnBackground();
        SpawnLevel();
        SpawnPlayer();
        SpawnSnowflakes();
        SetupCollision();
        InitHud();
        InitPauseMenu();
    }

    private void InitFactories()
    {
        _playerFactory = new Factory<Player>(this);
        _platformFactory = new Factory<IcePlatform>(this);
        _checkpointFactory = new Factory<Checkpoint>(this);
        _deathZoneFactory = new Factory<DeathZone>(this);
        _collectibleFactory = new Factory<Collectible>(this);
        _polarBearFactory = new Factory<PolarBear>(this);
        _penguinFactory = new Factory<Penguin>(this);
        _sealFactory = new Factory<Seal>(this);
        _unicornFactory = new Factory<Unicorn>(this);
        _cameraFactory = new Factory<CameraControllingEntity>(this);
    }

    private void SpawnBackground()
    {
        // Water surface — wide blue band below death zone
        _waterBg = new AxisAlignedRectangle
        {
            Width = 60000f,
            Height = 600f,
            IsVisible = true,
            IsFilled = true,
            Color = new XnaColor(20, 55, 120, 200),
            X = 0f,
            Y = _level.DeathZoneY - 300f,
        };
        Add(_waterBg);

        // Mountain silhouette in background (grows larger per phase)
        float mountainScale = PhaseIndex / 5f;
        _mountainBg = new AxisAlignedRectangle
        {
            Width = 300f * (0.5f + mountainScale),
            Height = 400f * (0.5f + mountainScale),
            IsVisible = true,
            IsFilled = true,
            Color = new XnaColor(60, 70, 95, (int)(80 + 100 * mountainScale)),
            X = _level.LevelRightBound + 200f,
            Y = _level.DeathZoneY + 200f * (0.5f + mountainScale),
        };
        Add(_mountainBg);
    }

    private void SpawnLevel()
    {
        // Platforms
        foreach (var p in _level.Platforms)
        {
            var plat = _platformFactory.Create();
            plat.X = p.X;
            plat.Y = p.Y;
            plat.Rectangle.Width = p.Width;
            plat.Rectangle.Height = p.Height;
            plat.Type = p.Type;
            plat.MoveRangeX = p.MoveRangeX;
            plat.MoveRangeY = p.MoveRangeY;
            plat.MoveSpeed = p.MoveSpeed;
            plat.CrumbleDelay = p.CrumbleDelay;
            plat.OneShotDelay = p.OneShotDelay;
            plat.Initialize();
        }

        // Checkpoints
        foreach (var c in _level.Checkpoints)
        {
            var cp = _checkpointFactory.Create();
            cp.X = c.X;
            cp.Y = c.Y;
            cp.Index = c.Index;
        }

        // Death zone
        var dz = _deathZoneFactory.Create();
        dz.X = 0f;
        dz.Y = _level.DeathZoneY;

        // Collectibles
        foreach (var c in _level.Collectibles)
        {
            var col = _collectibleFactory.Create();
            col.X = c.X;
            col.Y = c.Y;
            col.PointValue = c.PointValue;
            col.InitPosition();
        }

        // NPCs
        foreach (var n in _level.Npcs)
        {
            switch (n.Kind)
            {
                case NpcKind.PolarBear:
                    var bear = _polarBearFactory.Create();
                    bear.X = n.X;
                    bear.Y = n.Y;
                    bear.HintText = n.HintText;
                    bear.InitPosition();
                    break;

                case NpcKind.Penguin:
                    var penguin = _penguinFactory.Create();
                    penguin.X = n.X;
                    penguin.Y = n.Y;
                    penguin.WaddleRange = n.WaddleRange;
                    penguin.CanBellySlide = n.CanBellySlide;
                    penguin.InitPosition();
                    break;

                case NpcKind.Seal:
                    var seal = _sealFactory.Create();
                    seal.X = n.X;
                    seal.Y = n.Y;
                    seal.InitPosition();
                    break;

                case NpcKind.Unicorn:
                    var unicorn = _unicornFactory.Create();
                    unicorn.X = n.X;
                    unicorn.Y = n.Y;
                    unicorn.InitPosition();
                    break;
            }
        }
    }

    private void SpawnPlayer()
    {
        _player = _playerFactory.Create();
        _player.X = _lastCheckpointX;
        _player.Y = _lastCheckpointY;
        _player.SetAppearance(State.IsFemale);

        // Camera follows player
        var mapBounds = new AxisAlignedRectangle
        {
            Width = _level.LevelRightBound - _level.LevelLeftBound + 400f,
            Height = _level.LevelTopBound - _level.DeathZoneY + 400f,
            X = (_level.LevelLeftBound + _level.LevelRightBound) / 2f,
            Y = (_level.DeathZoneY + _level.LevelTopBound) / 2f,
        };

        _cameraController = _cameraFactory.Create();
        _cameraController.Target = _player;
        _cameraController.Map = mapBounds;
        _cameraController.TargetApproachStyle = TargetApproachStyle.Smooth;
        _cameraController.TargetApproachCoefficient = 6f;
    }

    private void SpawnSnowflakes()
    {
        int count = PhaseIndex >= 4 ? 40 : 20;
        for (int i = 0; i < count; i++)
        {
            var flake = new AxisAlignedRectangle
            {
                Width = Engine.Random.Between(2f, 5f),
                Height = Engine.Random.Between(2f, 5f),
                IsVisible = true,
                IsFilled = true,
                Color = new XnaColor(255, 255, 255, Engine.Random.Between(80, 180)),
            };
            flake.X = Camera.X + Engine.Random.Between(-800f, 800f);
            flake.Y = Camera.Y + Engine.Random.Between(-400f, 400f);
            Add(flake);
            _snowflakes.Add(flake);
        }
    }

    private void SetupCollision()
    {
        // Player vs platforms
        AddCollisionRelationship<Player, IcePlatform>(_playerFactory, _platformFactory)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f)
            .CollisionOccurred += (player, platform) =>
            {
                if (player.LastReposition.Y > 0)
                    platform.NotifyPlayerOn();
            };

        // Player vs checkpoints
        AddCollisionRelationship<Player, Checkpoint>(_playerFactory, _checkpointFactory)
            .CollisionOccurred += (_, checkpoint) =>
            {
                if (!checkpoint.IsActivated)
                {
                    checkpoint.Activate();
                    _lastCheckpointIndex = checkpoint.Index;
                    _lastCheckpointX = checkpoint.X;
                    _lastCheckpointY = checkpoint.Y + 30f;
                }
            };

        // Player vs death zone
        AddCollisionRelationship<Player, DeathZone>(_playerFactory, _deathZoneFactory)
            .CollisionOccurred += (_, _) =>
            {
                if (!_isDead)
                    OnPlayerDeath();
            };

        // Player vs collectibles
        AddCollisionRelationship<Player, Collectible>(_playerFactory, _collectibleFactory)
            .CollisionOccurred += (_, collectible) =>
            {
                _score += collectible.PointValue;
                collectible.Collect();
                UpdateScoreDisplay();
            };

        // Player vs unicorn
        AddCollisionRelationship<Player, Unicorn>(_playerFactory, _unicornFactory)
            .CollisionOccurred += (_, unicorn) =>
            {
                if (!unicorn.IsCollected)
                {
                    unicorn.Collect();
                    _lives = Math.Min(_lives + 1, 5);
                    _score += 500;
                    UpdateLivesDisplay();
                    UpdateScoreDisplay();
                    ShowHint("Extra life! The unicorn smiles.");
                }
            };

        // Player vs penguins — bump
        AddCollisionRelationship<Player, Penguin>(_playerFactory, _penguinFactory)
            .BounceOnCollision(firstMass: 0.3f, secondMass: 1f, elasticity: 0.3f);

        // Player vs polar bears — trigger hint
        AddCollisionRelationship<Player, PolarBear>(_playerFactory, _polarBearFactory)
            .CollisionOccurred += (_, bear) =>
            {
                if (!string.IsNullOrEmpty(bear.HintText))
                    ShowHint(bear.HintText);
            };

        // Player vs seals — push player
        AddCollisionRelationship<Player, Seal>(_playerFactory, _sealFactory)
            .BounceOnCollision(firstMass: 0.2f, secondMass: 1f, elasticity: 0.2f);
    }

    private void InitHud()
    {
        _livesLabel = new Label();
        _livesLabel.Anchor(Anchor.TopLeft);
        _livesLabel.X = 20;
        _livesLabel.Y = 20;
        Add(_livesLabel);
        UpdateLivesDisplay();

        _scoreLabel = new Label();
        _scoreLabel.Anchor(Anchor.TopRight);
        _scoreLabel.X = -20;
        _scoreLabel.Y = 20;
        Add(_scoreLabel);
        UpdateScoreDisplay();

        _phaseLabel = new Label();
        _phaseLabel.Anchor(Anchor.Top);
        _phaseLabel.Y = 20;
        _phaseLabel.Text = $"Phase {_level.PhaseIndex}: {_level.PhaseName}";
        Add(_phaseLabel);
        _phaseLabelTimer = 3f;

        _hintLabel = new Label();
        _hintLabel.Anchor(Anchor.BottomLeft);
        _hintLabel.X = 20;
        _hintLabel.Y = -40;
        _hintLabel.IsVisible = false;
        Add(_hintLabel);
    }

    private void InitPauseMenu()
    {
        _pauseOverlay = new Panel();
        _pauseOverlay.Dock(Dock.Fill);
        _pauseOverlay.IsVisible = false;

        var menu = new StackPanel();
        menu.Spacing = 12;
        menu.Anchor(Anchor.Center);

        var titleLabel = new Label();
        titleLabel.Text = "PAUSED";
        menu.AddChild(titleLabel);

        var resumeBtn = new Button();
        resumeBtn.Text = "Resume";
        resumeBtn.Click += (_, _) => TogglePause();
        menu.AddChild(resumeBtn);

        var restartBtn = new Button();
        restartBtn.Text = "Restart Phase";
        restartBtn.Click += (_, _) =>
        {
            MoveToScreen<GameplayScreen>(s =>
            {
                s.PhaseIndex = PhaseIndex;
                s.State = State;
            });
        };
        menu.AddChild(restartBtn);

        var quitBtn = new Button();
        quitBtn.Text = "Quit to Menu";
        quitBtn.Click += (_, _) =>
        {
            MoveToScreen<TitleScreen>(s => s.State = State);
        };
        menu.AddChild(quitBtn);

        _pauseOverlay.AddChild(menu);
        Add(_pauseOverlay);
    }

    public override void CustomActivity(FrameTime time)
    {
        // Pause toggle
        if (Engine.Input.Keyboard.WasKeyPressed(Microsoft.Xna.Framework.Input.Keys.Escape))
            TogglePause();

        if (IsPaused) return;

        // Death/respawn
        if (_isDead)
        {
            _respawnTimer -= time.DeltaSeconds;
            if (_respawnTimer <= 0f)
                Respawn();
            return;
        }

        // Phase complete
        if (_phaseComplete)
        {
            _phaseCompleteTimer -= time.DeltaSeconds;
            if (_phaseCompleteTimer <= 0f)
            {
                State.UpdateHighScore(PhaseIndex, _score);
                State.UnlockNextPhase(PhaseIndex);

                if (PhaseIndex >= PhaseDefinitions.TotalPhases)
                    MoveToScreen<VictoryScreen>(s => s.State = State);
                else
                    MoveToScreen<PhaseSelectScreen>(s => s.State = State);
            }
            return;
        }

        // Check if player reached end of level
        if (_player.X > _level.LevelRightBound - 100f && !_phaseComplete)
        {
            _phaseComplete = true;
            _phaseCompleteTimer = 2f;
            ShowHint(PhaseIndex >= PhaseDefinitions.TotalPhases
                ? "You reached the summit!"
                : "Phase complete!");
        }

        // Phase label fade
        if (_phaseLabelTimer > 0f)
        {
            _phaseLabelTimer -= time.DeltaSeconds;
            if (_phaseLabelTimer <= 0f)
                _phaseLabel.IsVisible = false;
        }

        // Hint label fade
        if (_hintTimer > 0f)
        {
            _hintTimer -= time.DeltaSeconds;
            if (_hintTimer <= 0f)
                _hintLabel.IsVisible = false;
        }

        // Update snowflakes
        UpdateSnowflakes(time);
    }

    private void OnPlayerDeath()
    {
        _isDead = true;
        _lives--;
        UpdateLivesDisplay();

        _player.IsVisible = false;
        _player.VelocityX = 0f;
        _player.VelocityY = 0f;

        if (_lives <= 0)
        {
            ShowHint("Out of lives! Restarting phase...");
            _respawnTimer = 2f;
        }
        else
        {
            ShowHint($"Splash! {_lives} lives left.");
            _respawnTimer = 1f;
        }
    }

    private void Respawn()
    {
        _isDead = false;

        if (_lives <= 0)
        {
            MoveToScreen<GameplayScreen>(s =>
            {
                s.PhaseIndex = PhaseIndex;
                s.State = State;
            });
            return;
        }

        _player.X = _lastCheckpointX;
        _player.Y = _lastCheckpointY;
        _player.VelocityX = 0f;
        _player.VelocityY = 0f;
        _player.IsVisible = true;
    }

    private void TogglePause()
    {
        if (IsPaused)
        {
            UnpauseThisScreen();
            _pauseOverlay.IsVisible = false;
        }
        else
        {
            PauseThisScreen();
            _pauseOverlay.IsVisible = true;
        }
    }

    private void UpdateLivesDisplay()
    {
        string hearts = new string('\u2665', Math.Max(_lives, 0));
        _livesLabel.Text = hearts;
    }

    private void UpdateScoreDisplay()
    {
        _scoreLabel.Text = _score.ToString();
    }

    private void ShowHint(string text)
    {
        _hintLabel.Text = text;
        _hintLabel.IsVisible = true;
        _hintTimer = 3f;
    }

    private void UpdateSnowflakes(FrameTime time)
    {
        float windSpeed = PhaseIndex >= 4 ? 60f : 20f;
        float fallSpeed = PhaseIndex >= 4 ? 80f : 40f;

        foreach (var flake in _snowflakes)
        {
            flake.X -= windSpeed * time.DeltaSeconds;
            flake.Y -= fallSpeed * time.DeltaSeconds;

            float relX = flake.X - Camera.X;
            float relY = flake.Y - Camera.Y;

            if (relX < -700f) flake.X = Camera.X + 700f;
            if (relX > 700f) flake.X = Camera.X - 700f;
            if (relY < -400f) flake.Y = Camera.Y + 400f;
            if (relY > 400f) flake.Y = Camera.Y - 400f;
        }
    }
}
