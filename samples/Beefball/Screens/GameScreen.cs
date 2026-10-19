using Beefball.Entities;
using FlatRedBall.Glue.StateInterpolation;
using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Tweening;
using Gum.GueDeriving;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using RenderingLibrary.Graphics;
using Layer = FlatRedBall2.Rendering.Layer;
using Vector2 = System.Numerics.Vector2;

namespace Beefball.Screens;

public partial class GameScreen : Screen
{
    private enum MatchState { Playing, GoalPause, Won }

    private const float P1StartX = -180f;
    private const float P2StartX = 180f;

    private BeefballConfig _config = null!;
    private ArenaGlow _arenaGlow = null!;

    private Factory<Wall> _walls = null!;
    private Factory<GoalBlocker> _blockers = null!;
    private Factory<Player> _players = null!;
    private Factory<Puck> _pucks = null!;
    private Factory<TrailDot> _trail = null!;
    private Factory<Spark> _sparks = null!;
    private Factory<HitRing> _rings = null!;
    private Factory<GoalWash> _washFactory = null!;
    private readonly GoalWash[] _washes = new GoalWash[2];

    private Player _player1 = null!;
    private Player _player2 = null!;
    private Puck _puck = null!;

    private readonly int[] _scores = new int[2];
    private MatchState _state = MatchState.Playing;
    private float _stateTimer;
    private int _lastScorer;

    private TextRuntime _scoreText1 = null!;
    private TextRuntime _scoreText2 = null!;
    private TextRuntime _bannerMain = null!;
    private TextRuntime _bannerSub = null!;
    private AARect _winVeil = null!;

    public override void CustomInitialize()
    {
        _config = BeefballConfig.Load();
        Camera.BackgroundColor = _config.Palette.Background;

        new ArenaVisuals(this, _config);
        // Dims the arena behind the win banner so the text stays readable over the players.
        _winVeil = new AARect
        {
            Width = 2f * (Arena.HalfWidth + Arena.PocketDepth), Height = 2f * Arena.HalfHeight,
            IsFilled = true, IsVisible = false, OutlineThickness = 0f, Z = 50f,
            Color = Visuals.WithAlpha(_config.Palette.Background, 0.6f),
        };
        Add(_winVeil);

        _walls = new Factory<Wall>(this);
        _blockers = new Factory<GoalBlocker>(this);
        _players = new Factory<Player>(this);
        _pucks = new Factory<Puck>(this);
        _trail = new Factory<TrailDot>(this).EnablePooling().Prewarm(40);
        _sparks = new Factory<Spark>(this).EnablePooling().Prewarm(60);
        _rings = new Factory<HitRing>(this).EnablePooling().Prewarm(4);
        _arenaGlow = new Factory<ArenaGlow>(this).Create(g => g.Config = _config);
        _washFactory = new Factory<GoalWash>(this);
        for (int i = 0; i < 2; i++)
        {
            float side = i == 0 ? -1f : 1f;
            var wash = _washFactory.Create();
            wash.X = side * (Arena.HalfWidth + Arena.PocketDepth / 2f);
            _washes[i] = wash;
        }

        foreach (var (x, y, w, h) in Arena.SolidWalls())
        {
            var wall = _walls.Create(e => { e.Width = w; e.Height = h; });
            wall.X = x; wall.Y = y;
        }
        foreach (var (x, y, w, h) in Arena.GoalBlockers())
        {
            var blocker = _blockers.Create(e => { e.Width = w; e.Height = h; });
            blocker.X = x; blocker.Y = y;
        }

        _player1 = _players.Create(p => { p.Team = 1; p.Config = _config; });
        _player2 = _players.Create(p => { p.Team = 2; p.Config = _config; });
        _puck = _pucks.Create(p => p.Config = _config);
        ResetPositions();

        WireCollisions();
        BuildHud();

        WatchContentDirectory("Content", relativePath =>
        {
            if (relativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                RestartScreen(RestartMode.HotReload);
        });
    }

    private void WireCollisions()
    {
        var c = _config.Collision;
        float playerMass = _config.Player.Mass;

        AddCollisionRelationship<Player, Wall>(_players, _walls)
            .BounceFirstOnCollision(c.WallElasticity)
            .CollisionStarted += (player, _) =>
            {
                float speed = player.Velocity.Length();
                if (speed > 180f)
                    SpawnSparks(new Vector2(player.X, player.Y), player.TeamColor, 5, 40f, 130f);
            };

        AddCollisionRelationship<Player, GoalBlocker>(_players, _blockers)
            .BounceFirstOnCollision(c.WallElasticity);

        AddCollisionRelationship<Puck, Wall>(_pucks, _walls)
            .BounceFirstOnCollision(c.WallElasticity)
            .CollisionStarted += (puck, _) =>
            {
                float speed = puck.Velocity.Length();
                if (speed > 40f)
                    SpawnSparks(new Vector2(puck.X, puck.Y), _config.Palette.Puck, 4 + (int)Math.Min(8f, speed / 80f), 40f, 150f);
            };

        AddSelfCollisionRelationship<Player>(_players)
            .BounceBothOnCollision(playerMass, playerMass, c.PlayerPlayerElasticity);

        AddCollisionRelationship<Player, Puck>(_players, _pucks)
            .BounceBothOnCollision(playerMass, _config.Puck.Mass, c.PlayerPuckElasticity)
            .CollisionStarted += (player, puck) =>
            {
                float speed = puck.Velocity.Length();
                SpawnSparks(new Vector2(puck.X, puck.Y), player.TeamColor, 6 + (int)Math.Min(10f, speed / 60f), 60f, 220f);
                var ring = _rings.Create();
                ring.Launch(player, player.TeamColor, _config.Player.Radius, _config.Player.Radius * 2.4f);
            };
    }

    private void SpawnSparks(Vector2 position, Color color, int count, float minSpeed, float maxSpeed)
    {
        for (int i = 0; i < count; i++)
        {
            var spark = _sparks.Create();
            spark.X = position.X;
            spark.Y = position.Y;
            spark.Launch(Engine.Random.RadialVector2(minSpeed, maxSpeed), Color.Lerp(color, Color.White, 0.4f), Engine.Random.Between(0.4f, 0.9f));
        }
    }

    // ---- HUD (Gum, code-only) ---------------------------------------------------------------

    private void BuildHud()
    {
        var hud = new Layer("HUD");
        Layers.Add(hud);

        var palette = _config.Palette;
        float rowY = 34f;
        // Arena x of 200 and 600 (out of 800) sit at canvas x 300 and 700 once the arena is centered.
        float cx1 = Arena.ResolutionWidth / 2f - 200f;
        float cx2 = Arena.ResolutionWidth / 2f + 200f;

        Add(UiText.Make(UiText.Spaced("TEAM") + "  1", 16, Visuals.WithAlpha(palette.Team1, 0.85f), cx1 - 10f, rowY + 4f, HorizontalAlignment.Right), hud);
        _scoreText1 = UiText.Make("0", 46, palette.Team1, cx1 + 10f, rowY, HorizontalAlignment.Left, bold: true);
        Add(_scoreText1, hud);

        Add(UiText.Make(UiText.Spaced("TEAM") + "  2", 16, Visuals.WithAlpha(palette.Team2, 0.85f), cx2 - 10f, rowY + 4f, HorizontalAlignment.Right), hud);
        _scoreText2 = UiText.Make("0", 46, palette.Team2, cx2 + 10f, rowY, HorizontalAlignment.Left, bold: true);
        Add(_scoreText2, hud);

        float centerX = Arena.ResolutionWidth / 2f;
        float centerY = Arena.ResolutionHeight / 2f;
        _bannerMain = UiText.Make("", 110, palette.Text, centerX, centerY - 20f, bold: true);
        _bannerSub = UiText.Make("", 30, palette.Text, centerX, centerY + 70f);
        _bannerMain.Visible = false;
        _bannerSub.Visible = false;
        Add(_bannerMain, hud);
        Add(_bannerSub, hud);
    }

    private void RefreshScores()
    {
        _scoreText1.Text = _scores[0].ToString();
        _scoreText2.Text = _scores[1].ToString();
    }

    private void ShowBanner(string main, string sub, Color color, bool pop)
    {
        _bannerMain.Text = main;
        _bannerSub.Text = sub;
        _bannerMain.Color = color;
        _bannerSub.Color = Color.Lerp(color, Color.White, 0.35f);
        _bannerMain.Visible = true;
        _bannerSub.Visible = !string.IsNullOrEmpty(sub);

        if (!pop)
        {
            _bannerMain.Alpha = 255;
            _bannerSub.Alpha = 255;
            _bannerMain.FontScale = 1f;
            return;
        }

        // Scale pops in with a little overshoot while alpha rises; the sub line follows shortly after.
        _bannerMain.Alpha = 0;
        _bannerSub.Alpha = 0;
        this.Tween(v =>
        {
            _bannerMain.FontScale = 0.55f + 0.45f * v;
            _bannerMain.Alpha = (int)(255f * Math.Clamp(v * 1.6f, 0f, 1f));
        }, 0f, 1f, TimeSpan.FromSeconds(0.38), InterpolationType.Back, Easing.Out);
        this.Tween(v => _bannerSub.Alpha = (int)(255f * v),
            0f, 1f, TimeSpan.FromSeconds(0.3), InterpolationType.Quadratic, Easing.Out);
    }

    private void HideBanner()
    {
        _bannerMain.Visible = false;
        _bannerSub.Visible = false;
    }

    // ---- Match flow -------------------------------------------------------------------------

    public override void CustomActivity(FrameTime time)
    {
        var keyboard = Engine.Input.Keyboard;
        if (keyboard.WasKeyPressed(Keys.Escape))
        {
            MoveToScreen<TitleScreen>();
            return;
        }

        switch (_state)
        {
            case MatchState.Playing:
                CheckForGoal();
                break;

            case MatchState.GoalPause:
                _stateTimer -= time.DeltaSeconds;
                if (_stateTimer <= 0f)
                {
                    if (_scores[_lastScorer - 1] >= _config.Match.ScoreToWin)
                        BeginWon();
                    else
                        BeginNextRound();
                }
                break;

            case MatchState.Won:
                if (keyboard.WasKeyPressed(Keys.Enter))
                    MoveToScreen<TitleScreen>();
                break;
        }
    }

    private void CheckForGoal()
    {
        float edge = Arena.HalfWidth + _config.Puck.Radius;
        if (Math.Abs(_puck.Y) >= Arena.GoalHalfHeight) return;

        // Team 1 starts left and scores in the right goal; team 2 the reverse.
        if (_puck.X > edge) BeginGoal(1);
        else if (_puck.X < -edge) BeginGoal(2);
    }

    private void BeginGoal(int scorer)
    {
        _state = MatchState.GoalPause;
        _stateTimer = _config.Match.GoalPauseSeconds;
        _lastScorer = scorer;
        _scores[scorer - 1]++;
        RefreshScores();

        foreach (var player in _players.Instances)
        {
            player.InputEnabled = false;
            player.Velocity = Vector2.Zero;
            player.Acceleration = Vector2.Zero;
        }

        // The puck settles into the middle of the pocket it scored in.
        int pocket = scorer == 1 ? 1 : 0;
        float side = pocket == 0 ? -1f : 1f;
        float restX = side * (Arena.HalfWidth + Arena.PocketDepth * 0.55f);
        _puck.Velocity = Vector2.Zero;
        _puck.Acceleration = Vector2.Zero;
        _puck.Tween(v => _puck.X = v, _puck.X, restX, TimeSpan.FromSeconds(0.3), InterpolationType.Cubic, Easing.Out);
        _puck.Tween(v => _puck.Y = v, _puck.Y, 0f, TimeSpan.FromSeconds(0.3), InterpolationType.Cubic, Easing.Out);

        var scorerColor = _config.Palette.TeamColor(scorer);
        this.Tween(v => _arenaGlow.SetPocketPulse(pocket, v), 1f, 0f, TimeSpan.FromSeconds(1.3),
            InterpolationType.Cubic, Easing.Out);
        this.Tween(v => _washes[pocket].Set(scorerColor, 0.32f * v * _config.Glow.Strength), 1f, 0f, TimeSpan.FromSeconds(1.4),
            InterpolationType.Quadratic, Easing.Out);

        SpawnSparks(new Vector2(restX, 0f), scorerColor, 24, 80f, 300f);

        ShowBanner("GOAL!", $"TEAM {scorer} SCORES", scorerColor, pop: true);
    }

    private void BeginNextRound()
    {
        HideBanner();
        ResetPositions();
        _state = MatchState.Playing;
    }

    private void BeginWon()
    {
        _state = MatchState.Won;
        _winVeil.IsVisible = true;
        var color = _config.Palette.TeamColor(_lastScorer);
        if (_bannerMain.FontSize != 84) _bannerMain.FontSize = 84;
        ShowBanner($"PLAYER {_lastScorer} WINS", "Press Enter", color, pop: true);
        _bannerMain.FontScale = 1f;
    }

    private void ResetPositions()
    {
        _player1.StopAllTweens();
        _player2.StopAllTweens();
        _puck.StopAllTweens();

        foreach (var (player, x) in new[] { (_player1, P1StartX), (_player2, P2StartX) })
        {
            player.X = x;
            player.Y = 0f;
            player.Velocity = Vector2.Zero;
            player.Acceleration = Vector2.Zero;
            player.CooldownRemaining = 0f;
            player.InputEnabled = true;
        }

        _puck.X = 0f;
        _puck.Y = 0f;
        _puck.Velocity = Vector2.Zero;
        _puck.Acceleration = Vector2.Zero;
    }

    public override void SaveHotReloadState(HotReloadState state)
    {
        state.Preserve(_player1);
        state.Preserve(_player2);
        state.Preserve(_puck);
        state.Set("score1", _scores[0]);
        state.Set("score2", _scores[1]);
        state.Set("matchState", (int)_state);
        state.Set("stateTimer", _stateTimer);
        state.Set("lastScorer", _lastScorer);
    }

    public override void RestoreHotReloadState(HotReloadState state)
    {
        state.Restore(_player1);
        state.Restore(_player2);
        state.Restore(_puck);
        _scores[0] = state.Get<int>("score1");
        _scores[1] = state.Get<int>("score2");
        RefreshScores();

        // A reload during a goal moment or the win screen resumes it. Without this the preserved
        // puck would still be inside a pocket while the match thinks play is live, and score again.
        _state = (MatchState)state.Get<int>("matchState");
        _stateTimer = state.Get<float>("stateTimer");
        _lastScorer = state.Get<int>("lastScorer");
        if (_state == MatchState.Playing) return;

        foreach (var player in _players.Instances)
            player.InputEnabled = false;
        var color = _config.Palette.TeamColor(_lastScorer);
        if (_state == MatchState.GoalPause)
        {
            ShowBanner("GOAL!", $"TEAM {_lastScorer} SCORES", color, pop: false);
        }
        else
        {
            _winVeil.IsVisible = true;
            _bannerMain.FontSize = 84;
            ShowBanner($"PLAYER {_lastScorer} WINS", "Press Enter", color, pop: false);
        }
    }
}
