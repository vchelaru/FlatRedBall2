using FlatRedBall2;
using FlatRedBall2.Collision;
using FlatRedBall2.Entities;
using FlatRedBall2.Math;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using SampleProject1.Entities;

namespace SampleProject1.Screens;

public class CameraFollowDemoScreen : Screen
{
    private Factory<TopDownPlayer> _playerFactory = null!;
    private Factory<CameraControllingEntity> _cameraFactory = null!;
    private TileShapeCollection _tiles = null!;

    private const float GridSize = 32f;
    private const int Cols = 80;
    private const int Rows = 45;
    private const float OriginX = -(Cols * GridSize / 2f);
    private const float OriginY = -(Rows * GridSize / 2f);

    public override void CustomInitialize()
    {
        Camera.BackgroundColor = new Color(15, 20, 28);

        _playerFactory = new Factory<TopDownPlayer>(this);
        _cameraFactory = new Factory<CameraControllingEntity>(this);

        BuildLevel();

        var player = _playerFactory.Create();
        // Spawn near center of map, clear of obstacles
        player.X = 0f;
        player.Y = 0f;

        SetupCollision();
        SetupCamera(player);
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

        var wallColor = new Color(70, 90, 130);

        // Outer border
        for (int col = 0; col < Cols; col++)
        {
            SetWall(col, 0, wallColor);
            SetWall(col, Rows - 1, wallColor);
        }
        for (int row = 1; row < Rows - 1; row++)
        {
            SetWall(0, row, wallColor);
            SetWall(Cols - 1, row, wallColor);
        }

        // Scattered solid blocks for navigation interest
        var accentColor = new Color(100, 70, 120);
        AddBlock( 8,  5,  5,  5, accentColor); // bottom-left
        AddBlock(33,  3,  8,  4, accentColor); // bottom-center
        AddBlock(62,  5,  5,  5, accentColor); // bottom-right
        AddBlock( 5, 18,  4, 10, accentColor); // left-center column
        AddBlock(18, 14, 12,  3, accentColor); // center-left horizontal bar
        AddBlock(48, 14, 12,  3, accentColor); // center-right horizontal bar
        AddBlock(71, 18,  4, 10, accentColor); // right-center column
        AddBlock(34, 24, 12,  5, accentColor); // center island — player spawns above this
        AddBlock( 8, 33,  5,  5, accentColor); // top-left
        AddBlock(33, 37,  8,  4, accentColor); // top-center
        AddBlock(62, 33,  5,  5, accentColor); // top-right

        _tiles.IsVisible = true;
    }

    private void SetWall(int col, int row, Color color)
    {
        _tiles.AddTileAtCell(col, row);
        _tiles.GetTileAtCell(col, row)!.Color = color;
    }

    private void AddBlock(int col, int row, int width, int height, Color color)
    {
        for (int r = row; r < row + height; r++)
            for (int c = col; c < col + width; c++)
                SetWall(c, r, color);
    }

    private void SetupCollision()
    {
        AddCollisionRelationship(_playerFactory, _tiles)
            .MoveFirstOnCollision();
    }

    private void SetupCamera(TopDownPlayer player)
    {
        // Map bounds centered at world origin — same center as the tile collection
        var mapBounds = new BoundsRectangle(Cols * GridSize, Rows * GridSize);

        var cam = _cameraFactory.Create();
        cam.Target = player;
        cam.Map = mapBounds;
        cam.TargetApproachStyle = TargetApproachStyle.Smooth;
        cam.TargetApproachCoefficient = 8f;
    }

    private void SetupHud()
    {
        var panel = new Panel();
        panel.Dock(Dock.Fill);

        var hint = new Label { Text = "Arrow Keys / WASD — camera follows player, clamped to map edges" };
        hint.Anchor(Anchor.TopLeft);
        hint.X = 10;
        hint.Y = 10;
        panel.AddChild(hint);

        Add(panel);
    }
}
