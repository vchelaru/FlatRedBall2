using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class PlatformerDemoScreen : Screen
{
    private Factory<Player> _playerFactory = null!;
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
        "#......................................#",  // row  5
        "#......................................#",  // row  6
        "#.......######......######.............#",  // row  7 — low platforms
        "#......................................#",  // row  8
        "#......................................#",  // row  9
        "#......................................#",  // row 10
        "#....####.........####.........####....#",  // row 11 — mid platforms
        "#......................................#",  // row 12
        "#......................................#",  // row 13
        "#......................................#",  // row 14
        "#.........#####.......#####............#",  // row 15 — upper-mid platforms
        "#......................................#",  // row 16
        "#......................................#",  // row 17
        "#......................................#",  // row 18
        "#.......########.......########........#",  // row 19 — high platforms
        "#......................................#",  // row 20
        "#......................................#",  // row 21
    };

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(15, 20, 35);

        _playerFactory = new Factory<Player>(this);

        BuildLevel();

        var player = _playerFactory.Create();
        player.X = 0f;
        // Floor top = OriginY + 3 rows * GridSize = -264. Player half-height = 24.
        player.Y = OriginY + 3 * GridSize + 24f;

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

        for (int row = 0; row < Level.Length; row++)
        {
            var line = Level[row];
            for (int col = 0; col < line.Length; col++)
            {
                if (line[col] != '#') continue;
                _tiles.AddTileAtCell(col, row);
                _tiles.GetTileAtCell(col, row)!.Color = new Color(75, 110, 65);
            }
        }

        _tiles.IsVisible = true;
    }

    private void SetupCollision()
    {
        AddCollisionRelationship(_playerFactory, _tiles)
            .BounceOnCollision(firstMass: 0f, secondMass: 1f, elasticity: 0f);
    }

    private void SetupHud()
    {
        var panel = new Panel();
        panel.Dock(Dock.Fill);

        var hint = new Label { Text = "Arrow Keys / WASD to move  |  Space to jump" };
        hint.Anchor(Anchor.TopLeft);
        hint.X = 10;
        hint.Y = 10;
        panel.AddChild(hint);

        Add(panel);
    }
}
