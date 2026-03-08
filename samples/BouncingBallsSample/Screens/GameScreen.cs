using BouncingBallsSample.Entities;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace BouncingBallsSample.Screens;

public class GameScreen : Screen
{
    private Factory<Ball> _ballFactory = null!;
    private TileShapeCollection _tiles = null!;
    private Label _pauseLabel = null!;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 25, 35);

        _ballFactory = new Factory<Ball>(this);

        SetupArena();
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

    private void SetupCollision()
    {
        // Balls bounce off the tile walls
        AddCollisionRelationship(_ballFactory, _tiles)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0.8f);

        // Balls bounce off each other
        AddCollisionRelationship<Ball>(_ballFactory)
            .BounceOnCollision(firstMass: 1f, secondMass: 1f, elasticity: 0.9f);
    }

    private void SetupUI()
    {
        _pauseLabel = new Label();
        _pauseLabel.Text = "Paused — press ESC to resume";
        _pauseLabel.Anchor(Anchor.Center);
        _pauseLabel.IsVisible = false;
        Add(_pauseLabel);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (Engine.InputManager.Keyboard.WasKeyPressed(Keys.Escape))
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

        if (!IsPaused && Engine.InputManager.Cursor.PrimaryPressed)
        {
            var pos = Engine.InputManager.Cursor.WorldPosition;
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
