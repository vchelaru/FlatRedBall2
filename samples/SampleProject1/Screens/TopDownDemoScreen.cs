using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class TopDownDemoScreen : Screen
{
    private Factory<TopDownPlayer> _playerFactory = null!;
    private Factory<SightLine> _sightLineFactory = null!;
    private TileShapeCollection _tiles = null!;

    private const float GridSize = 32f;
    private const float OriginX = -640f;
    private const float OriginY = -360f;

    // Row 0 = bottom, rows increase upward — matches TileShapeCollection Y+ up convention.
    private static readonly string[] Level =
    {
        "########################################",  // row  0 — bottom wall
        "#......................................#",  // row  1
        "#......................................#",  // row  2
        "#......##......................##......#",  // row  3 — pillars
        "#......##......................##......#",  // row  4
        "#......................................#",  // row  5
        "#......................................#",  // row  6
        "#....############....############.....#",  // row  7 — horizontal walls
        "#......................................#",  // row  8
        "#......................................#",  // row  9
        "#.............######...............#..#",  // row 10 — mid obstacle
        "#.............#....#...............#..#",  // row 11
        "#.............######...............#..#",  // row 12
        "#......................................#",  // row 13
        "#......................................#",  // row 14
        "#....############....############.....#",  // row 15 — horizontal walls
        "#......................................#",  // row 16
        "#......................................#",  // row 17
        "#......##......................##......#",  // row 18 — pillars
        "#......##......................##......#",  // row 19
        "#......................................#",  // row 20
        "#......................................#",  // row 21 - intentionally empty (top wall is row 22)
        "########################################",  // row 22 — top wall
    };

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(20, 25, 30);

        _playerFactory = new Factory<TopDownPlayer>(this);
        _sightLineFactory = new Factory<SightLine>(this);

        BuildLevel();

        var player = _playerFactory.Create();
        // Spawn in the open center area
        player.X = 0f;
        player.Y = 0f;

        var sightLine = _sightLineFactory.Create();
        sightLine.Player = player;
        sightLine.Tiles = _tiles;

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

        _tiles.Visible = true;
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

        var hint = new Label { Text = "Arrow Keys / WASD to move" };
        hint.Anchor(Anchor.TopLeft);
        hint.X = 10;
        hint.Y = 10;
        panel.AddChild(hint);

        Add(panel);
    }
}
