using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class DefaultCollisionDemoScreen : Screen
{
    private Factory<DefaultCollisionPlayer> _playerFactory = null!;
    private TileShapeCollection _tiles = null!;
    private Label _statusLabel = null!;
    private DefaultCollisionPlayer _player = null!;

    private const float GridSize = 32f;
    private const float OriginX = -480f;
    private const float OriginY = -288f;

    // Row 0 = bottom, rows increase upward — matches TileShapeCollection Y+ up convention.
    private static readonly string[] Level =
    {
        "##############################",  // row  0 — bottom wall
        "#............................#",
        "#............................#",
        "#....####............####....#",
        "#....#..#............#..#....#",
        "#....####............####....#",
        "#............................#",
        "#............................#",
        "#....####............####....#",
        "#....#..#............#..#....#",
        "#....####............####....#",
        "#............................#",
        "#............................#",
        "#............................#",
        "#............................#",
        "#............................#",
        "#............................#",
        "##############################",  // row 17 — top wall
    };

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 20, 30);

        _playerFactory = new Factory<DefaultCollisionPlayer>(this);

        BuildLevel();

        _player = _playerFactory.Create();
        _player.X = 0f;
        _player.Y = 0f;

        SetupCollision();
        SetupHud();
    }

    public override void CustomActivity(FrameTime time)
    {
        _statusLabel.Text = _player.IsCollisionEnabled
            ? "Body collision: ON  (blue filled circle stops at walls)"
            : "Body collision: OFF  (ghost — passes through walls)";
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

        var wallColor = new Color(80, 100, 140);
        for (int row = 0; row < Level.Length; row++)
        {
            var line = Level[row];
            for (int col = 0; col < line.Length; col++)
            {
                if (line[col] != '#') continue;
                _tiles.AddTileAtCell(col, row);
                _tiles.GetTileAtCell(col, row)!.Color = wallColor;
            }
        }

        _tiles.IsVisible = true;
    }

    private void SetupCollision()
    {
        AddCollisionRelationship(_playerFactory, _tiles)
            .MoveFirstOnCollision();
    }

    private void SetupHud()
    {
        var panel = new Panel();
        panel.Dock(Dock.Fill);

        var hint = new Label
        {
            Text = "Arrow Keys / WASD: move  |  Space: toggle body collision\n" +
                   "Filled circle = default collision  |  Outlined circle = NOT default collision"
        };
        hint.Anchor(Anchor.TopLeft);
        hint.X = 10;
        hint.Y = 10;
        panel.AddChild(hint);

        _statusLabel = new Label { Text = "Body collision: ON  (blue filled circle stops at walls)" };
        _statusLabel.Anchor(Anchor.BottomLeft);
        _statusLabel.X = 10;
        _statusLabel.Y = -10;
        panel.AddChild(_statusLabel);

        Add(panel);
    }
}
