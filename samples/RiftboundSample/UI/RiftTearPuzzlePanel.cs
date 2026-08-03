using FlatRedBall2;
using Gum.DataTypes;
using Gum.Forms.Controls;
using Gum.Wireframe;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using MonoGameGum.GueDeriving;
using RiftboundSample.Models;
using RiftboundSample.Systems;

namespace RiftboundSample.UI;

/// <summary>
/// Full-screen panel showing a 3x3 rift tear puzzle grid and a target pattern.
/// Arrow keys select row/column, Enter rotates row right, R rotates column down,
/// Q rotates row left, E rotates column up.
/// </summary>
public class RiftTearPuzzlePanel
{
    private const float CellSize = 24f;
    private const float CellSpacing = 4f;

    private Screen _screen = null!;
    private Panel _root = null!;
    private Label _titleLabel = null!;
    private Label _hintLabel = null!;
    private Label _statusLabel = null!;

    private ColoredRectangleRuntime[,] _gridCells = new ColoredRectangleRuntime[3, 3];
    private ColoredRectangleRuntime[,] _targetCells = new ColoredRectangleRuntime[3, 3];

    private RiftTearPuzzle? _puzzle;
    private RiftTearData? _tearData;

    // Selection: 0-2 = rows, 3-5 = columns
    private int _selection;

    public bool IsVisible => _root?.Visual.Visible ?? false;
    public event Action<RiftTearData>? PuzzleSolved;
    public event Action? Closed;

    public void Initialize(Screen screen)
    {
        _screen = screen;

        _root = new Panel();
        _root.Dock(Dock.Fill);
        _root.Visual.Visible = false;

        var bg = new ColoredRectangleRuntime
        {
            Width = 0, Height = 0,
            WidthUnits = DimensionUnitType.RelativeToParent,
            HeightUnits = DimensionUnitType.RelativeToParent,
            Red = 10, Green = 10, Blue = 25, Alpha = 240,
        };
        _root.Visual.Children.Add(bg);

        _titleLabel = new Label { Text = "-- Rift Tear --" };
        _titleLabel.Anchor(Anchor.Top);
        _titleLabel.Y = 8;
        _root.AddChild(_titleLabel);

        _hintLabel = new Label { Text = "Arrows: select  Enter: row right  Q: row left  R: col down  E: col up  Esc: close" };
        _hintLabel.Anchor(Anchor.BottomLeft);
        _hintLabel.X = 8;
        _hintLabel.Y = -8;
        _root.AddChild(_hintLabel);

        _statusLabel = new Label { Text = "" };
        _statusLabel.Anchor(Anchor.Top);
        _statusLabel.Y = 24;
        _root.AddChild(_statusLabel);

        // Build grid cells (left side)
        BuildCellGrid(_gridCells, 40, 50);

        // Build target cells (right side)
        BuildCellGrid(_targetCells, 180, 50);

        var gridLabel = new Label { Text = "Current" };
        gridLabel.Anchor(Anchor.TopLeft);
        gridLabel.X = 40;
        gridLabel.Y = 38;
        _root.AddChild(gridLabel);

        var targetLabel = new Label { Text = "Target" };
        targetLabel.Anchor(Anchor.TopLeft);
        targetLabel.X = 180;
        targetLabel.Y = 38;
        _root.AddChild(targetLabel);

        _screen.Add(_root);
    }

    public void Show(RiftTearPuzzle puzzle, RiftTearData tearData)
    {
        _puzzle = puzzle;
        _tearData = tearData;
        _selection = 0;
        _root.Visual.Visible = true;
        _statusLabel.Text = $"Difficulty: {tearData.Difficulty}";
        UpdateDisplay();
    }

    public void Hide()
    {
        _root.Visual.Visible = false;
    }

    public void Update(FlatRedBallService engine)
    {
        if (!IsVisible || _puzzle == null) return;

        var kb = engine.InputManager.Keyboard;

        if (kb.WasKeyPressed(Keys.Escape))
        {
            Hide();
            Closed?.Invoke();
            return;
        }

        if (_puzzle.IsSolved) return;

        if (kb.WasKeyPressed(Keys.Up))
            _selection = (_selection - 1 + 6) % 6;
        else if (kb.WasKeyPressed(Keys.Down))
            _selection = (_selection + 1) % 6;

        bool changed = false;
        if (kb.WasKeyPressed(Keys.Enter))
        {
            if (_selection < 3)
                _puzzle.RotateRow(_selection, right: true);
            else
                _puzzle.RotateColumn(_selection - 3, down: true);
            changed = true;
        }
        else if (kb.WasKeyPressed(Keys.Q))
        {
            if (_selection < 3)
                _puzzle.RotateRow(_selection, right: false);
            else
                _puzzle.RotateColumn(_selection - 3, down: false);
            changed = true;
        }
        else if (kb.WasKeyPressed(Keys.R))
        {
            if (_selection >= 3)
                _puzzle.RotateColumn(_selection - 3, down: true);
            else
                _puzzle.RotateRow(_selection, right: true);
            changed = true;
        }
        else if (kb.WasKeyPressed(Keys.E))
        {
            if (_selection >= 3)
                _puzzle.RotateColumn(_selection - 3, down: false);
            else
                _puzzle.RotateRow(_selection, right: false);
            changed = true;
        }

        if (changed)
        {
            UpdateDisplay();
            if (_puzzle.IsSolved)
            {
                _statusLabel.Text = "Solved! Press Esc to collect reward.";
                PuzzleSolved?.Invoke(_tearData!);
            }
        }
    }

    private void UpdateDisplay()
    {
        if (_puzzle == null) return;

        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                SetCellColor(_gridCells[r, c], _puzzle.Grid[r, c]);
                SetCellColor(_targetCells[r, c], _puzzle.Target[r, c]);
            }
    }

    private void BuildCellGrid(ColoredRectangleRuntime[,] cells, float startX, float startY)
    {
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
            {
                var cell = new ColoredRectangleRuntime
                {
                    Width = CellSize,
                    Height = CellSize,
                    X = startX + c * (CellSize + CellSpacing),
                    Y = startY + r * (CellSize + CellSpacing),
                    XOrigin = RenderingLibrary.Graphics.HorizontalAlignment.Left,
                    YOrigin = RenderingLibrary.Graphics.VerticalAlignment.Top,
                };
                _root.Visual.Children.Add(cell);
                cells[r, c] = cell;
            }
    }

    private static void SetCellColor(ColoredRectangleRuntime cell, Element element)
    {
        var (r, g, b) = element switch
        {
            Element.Steam => (180, 180, 200),
            Element.Fire => (220, 80, 40),
            Element.Ice => (80, 180, 220),
            Element.Lightning => (220, 220, 60),
            Element.Aether => (160, 80, 220),
            Element.Glitch => (60, 220, 120),
            _ => (100, 100, 100),
        };
        cell.Red = r;
        cell.Green = g;
        cell.Blue = b;
    }
}
