using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class SpeedRunScreen : Screen
{
    private const float GridSize = 32f;
    private const float OriginX = -640f;
    private const float OriginY = -360f;

    private Factory<Player> _playerFactory = null!;
    private Factory<Collectable> _collectableFactory = null!;
    private TileShapeCollection _tiles = null!;

    private Label _timerLabel = null!;
    private Label _messageLabel = null!;

    private float _elapsed;
    private bool _timerStopped;

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(15, 20, 35);

        _playerFactory = new Factory<Player>(this);
        _collectableFactory = new Factory<Collectable>(this);

        BuildLevel();
        SetupCollision();
        SetupHud();
    }

    private void BuildLevel()
    {
        _tiles = new TileShapeCollection
        {
            X = OriginX,
            Y = OriginY,
            GridSize = GridSize,
        };
        Add(_tiles);

        var layout = SpeedRunLevelData.Layout;

        for (int row = 0; row < layout.Length; row++)
        {
            var line = layout[row];
            for (int col = 0; col < line.Length; col++)
            {
                float worldX = OriginX + col * GridSize + GridSize / 2f;
                float worldY = OriginY + row * GridSize + GridSize / 2f;

                switch (line[col])
                {
                    case '#':
                        _tiles.AddTileAtCell(col, row);
                        _tiles.GetTileAtCell(col, row)!.Color = new Color(75, 110, 65);
                        break;

                    case 'P':
                        var player = _playerFactory.Create();
                        player.X = worldX;
                        player.Y = worldY;
                        break;

                    case 'C':
                        var collectable = _collectableFactory.Create();
                        collectable.X = worldX;
                        collectable.Y = worldY;
                        break;
                }
            }
        }

        _tiles.IsVisible = true;
    }

    private void SetupCollision()
    {
        AddCollisionRelationship(_playerFactory, _tiles)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);

        AddCollisionRelationship<Player, Collectable>(_playerFactory, _collectableFactory)
            .CollisionOccurred += (_, collectable) =>
            {
                collectable.Destroy();
                _timerStopped = true;
                _messageLabel.Text = $"Collected! Time: {_elapsed:F2}s\nPress R to restart";
            };
    }

    private void SetupHud()
    {
        var panel = new Panel();
        panel.Dock(Dock.Fill);

        _timerLabel = new Label { Text = "0.00" };
        _timerLabel.Anchor(Anchor.Top);
        _timerLabel.Y = 10;
        panel.AddChild(_timerLabel);

        _messageLabel = new Label { Text = "" };
        _messageLabel.Anchor(Anchor.Center);
        panel.AddChild(_messageLabel);

        Add(panel);
    }

    public override void CustomActivity(FrameTime time)
    {
        if (!_timerStopped)
        {
            _elapsed += time.DeltaSeconds;
            _timerLabel.Text = $"{_elapsed:F2}";
        }

        if (_timerStopped && Engine.Input.Keyboard.WasKeyPressed(Keys.R))
        {
            MoveToScreen<SpeedRunScreen>();
        }
    }
}
