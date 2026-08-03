using FlatRedBall2;
using Gum.Forms.Controls;
using Gum.Wireframe;
using MonoGameGum.GueDeriving;
using RenderingLibrary.Graphics;
using RiftboundSample.Entities;
using RiftboundSample.Levels;

namespace RiftboundSample.UI;

/// <summary>
/// A small minimap panel in the top-right corner showing dots for the player,
/// enemies, NPCs, and doors.
/// </summary>
public class MinimapPanel
{
    private const float PanelSize = 100f;
    private const float DotSize = 4f;

    private Screen _screen = null!;
    private LoadedMap _map = null!;
    private Panel _root = null!;

    // Dots
    private ColoredRectangleRuntime _playerDot = null!;
    private readonly List<ColoredRectangleRuntime> _enemyDots = [];
    private readonly List<ColoredRectangleRuntime> _markerDots = [];

    public void Initialize(Screen screen, LoadedMap map)
    {
        _screen = screen;
        _map = map;

        _root = new Panel();
        _root.Anchor(Anchor.TopRight);
        _root.Visual.WidthUnits = Gum.DataTypes.DimensionUnitType.Absolute;
        _root.Visual.HeightUnits = Gum.DataTypes.DimensionUnitType.Absolute;
        _root.Visual.Width = PanelSize + 8;
        _root.Visual.Height = PanelSize + 8;
        _root.X = -4;
        _root.Y = 4;

        // Background
        var bg = new ColoredRectangleRuntime
        {
            X = 0,
            Y = 0,
            Width = PanelSize + 8,
            Height = PanelSize + 8,
            Color = new Microsoft.Xna.Framework.Color(0, 0, 0, 160),
        };
        _root.Visual.Children.Add(bg);

        // Static wall dots
        for (int gridRow = 0; gridRow < _map.Rows; gridRow++)
        {
            int tileRow = _map.Rows - 1 - gridRow;
            var gridLine = Brasshollow.Map.Grid[gridRow];
            for (int col = 0; col < gridLine.Length; col++)
            {
                if (gridLine[col] == '#')
                {
                    float mx = MapToMinimapX(_map.OriginX + col * _map.Walls.GridSize + _map.Walls.GridSize / 2f);
                    float my = MapToMinimapY(_map.OriginY + tileRow * _map.Walls.GridSize + _map.Walls.GridSize / 2f);

                    var wallDot = new ColoredRectangleRuntime
                    {
                        X = mx + 4,
                        Y = my + 4,
                        Width = DotSize - 1,
                        Height = DotSize - 1,
                        Color = new Microsoft.Xna.Framework.Color(80, 80, 90),
                    };
                    _root.Visual.Children.Add(wallDot);
                }
            }
        }

        // Player dot (green)
        _playerDot = CreateDot(new Microsoft.Xna.Framework.Color(60, 220, 80));
        _root.Visual.Children.Add(_playerDot);

        // Static marker dots (NPCs = yellow, doors = blue)
        foreach (var (nx, ny) in _map.NpcPositions)
            AddMarkerDot(nx, ny, new Microsoft.Xna.Framework.Color(220, 200, 50));

        foreach (var (dx, dy) in _map.DoorPositions)
            AddMarkerDot(dx, dy, new Microsoft.Xna.Framework.Color(60, 100, 220));

        foreach (var (sx, sy) in _map.ShopPositions)
            AddMarkerDot(sx, sy, new Microsoft.Xna.Framework.Color(220, 160, 40));

        foreach (var (ix, iy) in _map.InnPositions)
            AddMarkerDot(ix, iy, new Microsoft.Xna.Framework.Color(160, 80, 200));

        screen.Add(_root);
    }

    public void Update(
        PlayerEntity player,
        Factory<OverworldEnemyEntity> enemies,
        Factory<MarkerEntity> markers)
    {
        // Update player dot
        _playerDot.X = MapToMinimapX(player.X) + 4;
        _playerDot.Y = MapToMinimapY(player.Y) + 4;

        // Update enemy dots (recreate if count changed)
        while (_enemyDots.Count < enemies.Instances.Count)
        {
            var dot = CreateDot(new Microsoft.Xna.Framework.Color(200, 50, 50));
            _root.Visual.Children.Add(dot);
            _enemyDots.Add(dot);
        }

        for (int i = 0; i < _enemyDots.Count; i++)
        {
            if (i < enemies.Instances.Count)
            {
                var enemy = enemies.Instances[i];
                _enemyDots[i].X = MapToMinimapX(enemy.X) + 4;
                _enemyDots[i].Y = MapToMinimapY(enemy.Y) + 4;
                _enemyDots[i].Visible = true;
            }
            else
            {
                _enemyDots[i].Visible = false;
            }
        }
    }

    private float MapToMinimapX(float worldX)
    {
        float normalized = (worldX - _map.OriginX) / _map.MapWidth;
        return normalized * PanelSize;
    }

    private float MapToMinimapY(float worldY)
    {
        // Minimap Y is top-down (Gum Y-down), world Y is bottom-up
        float normalized = (worldY - _map.OriginY) / _map.MapHeight;
        return (1f - normalized) * PanelSize;
    }

    private void AddMarkerDot(float worldX, float worldY, Microsoft.Xna.Framework.Color color)
    {
        var dot = CreateDot(color);
        dot.X = MapToMinimapX(worldX) + 4;
        dot.Y = MapToMinimapY(worldY) + 4;
        _root.Visual.Children.Add(dot);
        _markerDots.Add(dot);
    }

    private static ColoredRectangleRuntime CreateDot(Microsoft.Xna.Framework.Color color)
    {
        return new ColoredRectangleRuntime
        {
            Width = DotSize,
            Height = DotSize,
            Color = color,
        };
    }
}
