using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class PlatformerBallScreen : Screen
{
    private Factory<Player> _playerFactory = null!;
    private Factory<BouncingBall> _ballFactory = null!;
    private TileShapeCollection _tiles = null!;

    private const float GridSize = 32f;
    private const float OriginX = -640f;
    private const float OriginY = -360f;

    // Row 0 = bottom of level, rows increase upward — matches TileShapeCollection's Y+ up convention.
    private static readonly string[] Level =
    {
        "########################################",  // row  0 — floor
        "########################################",  // row  1 — floor
        "########################################",  // row  2 — floor
        "#......................................#",  // row  3
        "#......................................#",  // row  4
        "#..####................................#",  // row  5 — low-left ledge
        "#......................................#",  // row  6
        "#............######....................#",  // row  7 — low-center platform
        "#......................................#",  // row  8
        "#..............................####....#",  // row  9 — low-right ledge
        "#......................................#",  // row 10
        "#....########.........................#",  // row 11 — mid-left platform
        "#......................................#",  // row 12
        "#....................########..........#",  // row 13 — mid-right platform
        "#......................................#",  // row 14
        "#..........#####.......................#",  // row 15 — upper-mid
        "#......................................#",  // row 16
        "#........................#####.........#",  // row 17 — upper-right
        "#......................................#",  // row 18
        "#.......########.......########........#",  // row 19 — high platforms
        "#......................................#",  // row 20
        "########################################",  // row 21 — ceiling
    };

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(12, 15, 28);

        _playerFactory = new Factory<Player>(this);
        _ballFactory = new Factory<BouncingBall>(this);

        BuildLevel();

        // Player on the mid-left platform (row 11)
        var player = _playerFactory.Create();
        player.X = OriginX + 8 * GridSize;
        player.Y = OriginY + 12 * GridSize + 24f; // half player height above platform top

        // Ball spawned high, above the right platform area
        var ball = _ballFactory.Create();
        ball.X = OriginX + 16 * GridSize;
        ball.Y = OriginY + 18 * GridSize;

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

        var tileColor = new Color(75, 110, 65);

        for (int row = 0; row < Level.Length; row++)
        {
            var line = Level[row];
            for (int col = 0; col < line.Length; col++)
            {
                if (line[col] != '#') continue;
                _tiles.AddTileAtCell(col, row);
                _tiles.GetTileAtCell(col, row)!.Color = tileColor;
            }
        }

        _tiles.IsVisible = true;
    }

    private void SetupCollision()
    {
        AddCollisionRelationship(_playerFactory, _tiles)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);

        AddCollisionRelationship(_ballFactory, _tiles)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0.8f);

        AddCollisionRelationship(_playerFactory, _ballFactory)
            .BounceOnCollision(firstMass: 1f, secondMass: 1f, elasticity: 0.5f);
    }

    private void SetupHud()
    {
        var panel = new Panel();
        panel.Dock(Dock.Fill);

        var hint = new Label { Text = "WASD to move  |  Space to jump  |  Watch the bouncing ball!" };
        hint.Anchor(Anchor.TopLeft);
        hint.X = 10;
        hint.Y = 10;
        panel.AddChild(hint);

        Add(panel);
    }
}
