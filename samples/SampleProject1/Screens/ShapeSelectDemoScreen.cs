using System;
using FlatRedBall2;
using FlatRedBall2.Collision;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

/// <summary>
/// Demonstrates WithFirstShape: two collision shapes on the same entity wired to different
/// relationships. The large cyan circle is visual only and overlaps walls. The small yellow
/// rect is the only shape that stops the player at walls — set via WithFirstShape.
///
/// Level has a narrow passage (64px wide). The cyan circle (radius 40 → 80px diameter) does
/// not fit; the yellow rect (20px) does. Walking through the passage makes the overlap visible.
/// </summary>
public class ShapeSelectDemoScreen : Screen
{
    private Factory<ShapeSelectPlayer> _playerFactory = null!;
    private TileShapeCollection _tiles = null!;
    private ShapeSelectPlayer _player = null!;
    private Label _statusLabel = null!;

    private const float GridSize = 32f;
    private const float OriginX = -480f;
    private const float OriginY = -288f;

    // Gap is at columns 14–15 (world X = –32 to +32, width = 64px).
    // Outer walls are 30 columns wide; inner wall divides the room at row 9.
    // Row 0 = bottom, rows increase upward — matches TileShapeCollection Y+ up convention.
    private static readonly string[] Level =
    {
        "##############################",  // row  0 — bottom outer wall
        "#............................#",  // row  1
        "#............................#",  // row  2
        "#............................#",  // row  3
        "#............................#",  // row  4
        "#............................#",  // row  5
        "#............................#",  // row  6
        "#............................#",  // row  7
        "#............................#",  // row  8
        "##############..##############",  // row  9 — inner wall; gap at cols 14–15 = 64px
        "#............................#",  // row 10
        "#............................#",  // row 11
        "#............................#",  // row 12
        "#............................#",  // row 13
        "#............................#",  // row 14
        "#............................#",  // row 15
        "#............................#",  // row 16
        "##############################",  // row 17 — top outer wall
    };

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(18, 22, 32);

        _playerFactory = new Factory<ShapeSelectPlayer>(this);

        BuildLevel();

        _player = _playerFactory.Create();
        // Start in the bottom room, centered horizontally
        _player.X = 0f;
        _player.Y = OriginY + 4.5f * GridSize;  // center of row 4

        SetupCollision();
        SetupHud();
    }

    public override void CustomActivity(FrameTime time)
    {
        // LastReposition accumulates all separation offsets for the frame; non-zero = hit a wall.
        bool hitWall = _player.LastReposition != System.Numerics.Vector2.Zero;

        // The gap (cols 14–15) spans world X = –32 to +32. The body circle extends ±40px from
        // center, so it overlaps the gap walls by 8px per side when the player is near X = 0.
        float gapLeft  = OriginX + 14 * GridSize;   // –32
        float gapRight = OriginX + 16 * GridSize;   //  +32
        float innerWallCenterY = OriginY + 9.5f * GridSize;

        bool nearPassage = MathF.Abs(_player.Y - innerWallCenterY) < 60f;
        bool circleOverlapsWalls =
            nearPassage &&
            (_player.X - _player.BodyCircle.Radius < gapLeft ||
             _player.X + _player.BodyCircle.Radius > gapRight);

        _statusLabel.Text =
            $"CollisionRect hit wall: {(hitWall ? "YES ←" : "no")}\n" +
            $"Body circle overlapping passage walls: {(circleOverlapsWalls ? "YES (visual only — does not block movement)" : "no")}";
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

        var outerColor = new Color(70, 90, 130);
        var innerColor = new Color(130, 90, 60);  // different color so inner wall stands out

        for (int row = 0; row < Level.Length; row++)
        {
            var line = Level[row];
            bool isInnerWall = row == 9;
            for (int col = 0; col < line.Length; col++)
            {
                if (line[col] != '#') continue;
                _tiles.AddTileAtCell(col, row);
                _tiles.GetTileAtCell(col, row)!.Color = isInnerWall ? innerColor : outerColor;
            }
        }

        _tiles.Visible = true;
    }

    private void SetupCollision()
    {
        // WithFirstShape wires only CollisionRect (20×20, yellow) to wall collision.
        // The BodyCircle (radius 40, cyan) is ignored by this relationship — it overlaps walls freely.
        AddCollisionRelationship(_playerFactory, _tiles)
            .WithFirstShape(p => p.CollisionRect)
            .MoveFirstOnCollision();
    }

    private void SetupHud()
    {
        var panel = new Panel();
        panel.Dock(Dock.Fill);

        var concept = new Label
        {
            Text =
                "WithFirstShape Demo\n" +
                "──────────────────────────────────────────────\n" +
                "Cyan circle  = BodyCircle  (radius 40, 80px wide)  — NOT wired to wall collision\n" +
                "Yellow rect  = CollisionRect (20×20)               — wired via WithFirstShape\n" +
                "\n" +
                "The passage in the orange wall is 64px wide.\n" +
                "  • BodyCircle (80px) would NOT fit — but it is ignored by the collision relationship.\n" +
                "  • CollisionRect (20px) stops the player at solid walls and fits through the gap.\n" +
                "\n" +
                "Walk up through the narrow passage: the cyan circle visually overlaps the wall\n" +
                "on both sides while the player passes through — because only the yellow rect stops you.",
        };
        concept.Anchor(Anchor.TopLeft);
        concept.X = 10;
        concept.Y = 10;
        panel.AddChild(concept);

        var controls = new Label { Text = "Arrow Keys / WASD: move" };
        controls.Anchor(Anchor.TopRight);
        controls.X = -10;
        controls.Y = 10;
        panel.AddChild(controls);

        _statusLabel = new Label { Text = "" };
        _statusLabel.Anchor(Anchor.BottomLeft);
        _statusLabel.X = 10;
        _statusLabel.Y = -10;
        panel.AddChild(_statusLabel);

        Add(panel);
    }
}
