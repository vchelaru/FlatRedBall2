using BouncingBallsSample.Entities;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace BouncingBallsSample.Screens;

public class GameScreen : Screen
{
    private Factory<Ball> _ballFactory = null!;
    private TileShapeCollection _tiles = null!;
    private Label _pauseLabel = null!;
    private Label _timeScaleLabel = null!;
    private SoundEffect _hitSound = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 25, 35);

        _ballFactory = new Factory<Ball>(this);

        SetupArena();

        var song = Engine.Content.Load<Song>("IGB3Song");
        _hitSound = Engine.Content.Load<SoundEffect>("SoundEffectInstanceFile");
        Engine.Audio.PlaySong(song);

        SetupCollision();
        SetupUI();
    }

    private void SetupArena()
    {
        _tiles = new TileShapeCollection
        {
            GridSize = 32f,
            X = -640f,
            Y = -360f,
        };

        // Floor: row 0, columns 0–39
        for (int col = 0; col <= 39; col++)
            _tiles.AddTileAtCell(col, 0);

        // Ceiling: row 21, columns 0–39
        for (int col = 0; col <= 39; col++)
            _tiles.AddTileAtCell(col, 21);

        // Left wall: col 0, rows 1–20
        for (int row = 1; row <= 20; row++)
            _tiles.AddTileAtCell(0, row);

        // Right wall: col 39, rows 1–20
        for (int row = 1; row <= 20; row++)
            _tiles.AddTileAtCell(39, row);

        _tiles.Color = new Color(80, 100, 120);
        _tiles.IsFilled = true;
        _tiles.IsVisible = true;
        Add(_tiles);
    }

    const float MinHitSpeed = 100f;
    const float MaxHitSpeed = 1000f;

    private void SetupCollision()
    {
        // Balls bounce off the tile walls — manual physics so we can measure delta-v for volume
        var ballVsTiles = AddCollisionRelationship(_ballFactory, _tiles);
        ballVsTiles.CollisionOccurred += (ball, tiles) =>
        {
            var preVelocity = ball.Velocity;
            var sep = ball.GetSeparationVector(tiles);
            ball.ApplySeparationOffset(sep);
            ball.AdjustVelocityFromSeparation(sep, tiles, thisMass: 0f, otherMass: 1f, elasticity: 0.8f);
            float impact = (ball.Velocity - preVelocity).Length();
            if (impact >= MinHitSpeed)
                Engine.Audio.Play(_hitSound, volume: Math.Clamp(impact / MaxHitSpeed, 0f, 1f));
        };

        // Balls bounce off each other — manual physics so we can measure delta-v for volume
        var ballVsBall = AddCollisionRelationship<Ball>(_ballFactory);
        ballVsBall.CollisionOccurred += (a, b) =>
        {
            var preVelocity = a.Velocity;
            var sep = a.GetSeparationVector(b);
            a.ApplySeparationOffset(sep * 0.5f);
            b.ApplySeparationOffset(sep * -0.5f);
            a.AdjustVelocityFromSeparation(sep, b, thisMass: 1f, otherMass: 1f, elasticity: 0.9f);
            float impact = (a.Velocity - preVelocity).Length();
            if (impact >= MinHitSpeed)
                Engine.Audio.Play(_hitSound, volume: Math.Clamp(impact / MaxHitSpeed, 0f, 1f));
        };
    }

    public override void CustomDestroy()
    {
        Engine.Audio.StopSong();
    }

    private void SetupUI()
    {
        _pauseLabel = new Label();
        _pauseLabel.Text = "Paused — press ESC to resume";
        _pauseLabel.Anchor(Anchor.Center);
        _pauseLabel.IsVisible = false;
        Add(_pauseLabel);

        _timeScaleLabel = new Label();
        _timeScaleLabel.Anchor(Anchor.TopLeft);
        _timeScaleLabel.X = 8;
        _timeScaleLabel.Y = 8;
        UpdateTimeScaleLabel();
        Add(_timeScaleLabel);
    }

    private void UpdateTimeScaleLabel() =>
        _timeScaleLabel.Text = $"Time Scale: {Engine.Time.TimeScale:F1}  (↑/↓ to adjust)";

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.Input.Keyboard.WasKeyPressed(Keys.Escape))
        {
            if (IsPaused)
            {
                UnpauseThisScreen();
                _pauseLabel.IsVisible = false;
            }
            else
            {
                PauseThisScreen();
                _pauseLabel.IsVisible = true;
            }
        }

        var keyboard = Engine.Input.Keyboard;
        if (keyboard.WasKeyPressed(Keys.Up))
        {
            Engine.Time.TimeScale = MathF.Round(Engine.Time.TimeScale + 0.1f, 1);
            UpdateTimeScaleLabel();
        }
        else if (keyboard.WasKeyPressed(Keys.Down))
        {
            Engine.Time.TimeScale = MathF.Max(0f, MathF.Round(Engine.Time.TimeScale - 0.1f, 1));
            UpdateTimeScaleLabel();
        }

        if (!IsPaused && Engine.Input.Cursor.PrimaryPressed)
        {
            var pos = Engine.Input.Cursor.WorldPosition;
            var ball = _ballFactory.Create();
            ball.X = pos.X;
            ball.Y = pos.Y;
            ball.VelocityX = Engine.Random.Between(-300f, 300f);
            ball.VelocityY = Engine.Random.Between(-100f, 200f);
            ball.Circle.Color = new Color(
                Engine.Random.Next(100, 255),
                Engine.Random.Next(100, 255),
                Engine.Random.Next(100, 255));
        }
    }
}
